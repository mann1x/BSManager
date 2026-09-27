using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace BSManager
{
    // Switches the system OpenXR runtime to SteamVR while the headset is on and restores the previous one when it goes off.
    //
    // The active runtime lives in HKLM, so changing it needs administrator rights. BSManager normally runs without them:
    // enabling the option asks for elevation once (UAC) and registers a scheduled task that runs "BSManager.exe --openxr-apply"
    // with the highest privileges of the current user. Each switch then writes the wanted runtime to the user's settings and
    // starts that task, with no further prompt. The elevated side only accepts runtimes registered under AvailableRuntimes
    // (HKLM, written by the runtimes' own installers), so the task can't be used to point OpenXR at an arbitrary file.
    internal static class OpenXRRuntime
    {
        private const string KhronosKey = @"SOFTWARE\Khronos\OpenXR\1";
        private const string SettingsKey = @"SOFTWARE\ManniX\BSManager";
        // Runtime that was active before BSManager switched to SteamVR; kept in the settings so a restart can still restore it
        private const string PreviousValue = "OpenXRPreviousRuntime";
        // Runtime the elevated task should make active
        private const string TargetValue = "OpenXRTarget";
        private const string TaskName = @"BSManager\OpenXR runtime";

        public const string ApplyArg = "--openxr-apply";
        public const string RegisterTaskArg = "--openxr-register-task";

        private static string ExePath => Environment.ProcessPath;

        // Form1 routes this to its log; the elevated helper runs without the tray app and only traces
        public static Action<string> Log = msg => Trace.WriteLine(msg);

        public static bool IsAdmin() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        public static string GetActive()
        {
            using RegistryKey key = Registry.LocalMachine.OpenSubKey(KhronosKey, false);
            return key?.GetValue("ActiveRuntime")?.ToString();
        }

        // Makes sure switches can happen without prompts: true when running as administrator or when the elevated task exists
        // for this executable, otherwise asks for elevation (UAC) to register it. False if the user declined.
        public static bool EnsureCanSwitch()
        {
            if (IsAdmin() || TaskRegisteredForThisExe()) return true;

            try
            {
                var psi = new ProcessStartInfo(ExePath, $"{RegisterTaskArg} {WindowsIdentity.GetCurrent().User.Value}")
                {
                    UseShellExecute = true,
                    Verb = "runas",
                };
                using Process p = Process.Start(psi);
                p.WaitForExit();
                Log($"[OpenXR] Task registration exited with {p.ExitCode}");
                return p.ExitCode == 0;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Log("[OpenXR] Elevation declined, the OpenXR switch stays off");
                return false;
            }
        }

        // steamxr_win64.json of the SteamVR install that OpenVR uses, falling back to the default Steam library
        public static string FindSteamVR()
        {
            try
            {
                string vrpath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "openvr", "openvrpaths.vrpath");
                if (File.Exists(vrpath))
                {
                    var runtimes = JObject.Parse(File.ReadAllText(vrpath))["runtime"]?.Values<string>() ?? Enumerable.Empty<string>();
                    foreach (string dir in runtimes)
                    {
                        string json = Path.Combine(dir, "steamxr_win64.json");
                        if (File.Exists(json)) return json;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[OpenXR] Can't read openvrpaths.vrpath: {ex.Message}");
            }

            using RegistryKey steam = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam", false);
            string steamDir = steam?.GetValue("InstallPath")?.ToString();
            if (!string.IsNullOrEmpty(steamDir))
            {
                string json = Path.Combine(steamDir, "steamapps", "common", "SteamVR", "steamxr_win64.json");
                if (File.Exists(json)) return json;
            }
            return null;
        }

        public static void SwitchToSteamVR()
        {
            string steamvr = FindSteamVR();
            if (steamvr == null)
            {
                Log("[OpenXR] SteamVR OpenXR runtime not found, not switching");
                return;
            }

            string active = GetActive();
            if (SamePath(active, steamvr))
            {
                Log("[OpenXR] SteamVR is already the active runtime");
                return;
            }

            using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
            // No previous runtime means there is nothing to restore later
            if (!string.IsNullOrEmpty(active)) settings.SetValue(PreviousValue, active);
            if (Apply(steamvr)) Log($"[OpenXR] Switching the active runtime from \"{active}\" to SteamVR \"{steamvr}\"");
        }

        public static void Restore()
        {
            using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
            string previous = settings.GetValue(PreviousValue)?.ToString();
            if (string.IsNullOrEmpty(previous)) return;
            settings.DeleteValue(PreviousValue, false);

            string active = GetActive();
            string steamvr = FindSteamVR();
            // Leave the runtime alone if something else changed it meanwhile
            if (steamvr != null && !SamePath(active, steamvr))
            {
                Log($"[OpenXR] Active runtime changed to \"{active}\" meanwhile, not restoring \"{previous}\"");
                return;
            }
            if (Apply(previous)) Log($"[OpenXR] Restoring the active runtime to \"{previous}\"");
        }

        // Sets the active runtime directly when running as administrator, otherwise through the elevated task
        private static bool Apply(string runtime)
        {
            if (IsAdmin()) return SetActive(runtime);

            using (RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey))
                settings.SetValue(TargetValue, runtime);

            int exit = RunHidden("schtasks.exe", $"/Run /TN \"{TaskName}\"", out string output);
            if (exit != 0)
            {
                Log($"[OpenXR] Can't start the elevated task (enable the OpenXR option again to re-create it): {output.Trim()}");
                return false;
            }
            return true;
        }

        // Entry point of the elevated task (BSManager.exe --openxr-apply)
        public static int ApplyFromTask()
        {
            using RegistryKey settings = Registry.CurrentUser.OpenSubKey(SettingsKey, true);
            string target = settings?.GetValue(TargetValue)?.ToString();
            if (string.IsNullOrEmpty(target)) return 1;
            settings.DeleteValue(TargetValue, false);
            return SetActive(target) ? 0 : 2;
        }

        private static bool SetActive(string runtime)
        {
            try
            {
                if (!IsAvailable(runtime))
                {
                    Log($"[OpenXR] \"{runtime}\" is not a registered OpenXR runtime (AvailableRuntimes), not switching");
                    return false;
                }
                using RegistryKey key = Registry.LocalMachine.CreateSubKey(KhronosKey, true);
                key.SetValue("ActiveRuntime", runtime);
                return true;
            }
            catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException)
            {
                Log("[OpenXR] Can't change the active runtime: no administrator rights");
                return false;
            }
        }

        private static bool IsAvailable(string runtime)
        {
            if (!File.Exists(runtime)) return false;
            using RegistryKey available = Registry.LocalMachine.OpenSubKey(KhronosKey + @"\AvailableRuntimes", false);
            return available != null && available.GetValueNames().Any(name => SamePath(name, runtime));
        }

        // Entry point of the elevation prompt (BSManager.exe --openxr-register-task <user SID>)
        public static int RegisterTask(string userSid)
        {
            if (!Regex.IsMatch(userSid ?? "", @"^S-1-[0-9-]+$")) return 1;

            string xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Lets BSManager switch the OpenXR runtime to SteamVR while the headset is on, without a UAC prompt each time.</Description>
  </RegistrationInfo>
  <Principals>
    <Principal id=""Author"">
      <UserId>{userSid}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>Queue</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT1M</ExecutionTimeLimit>
    <Hidden>true</Hidden>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>""{SecurityElement.Escape(ExePath)}""</Command>
      <Arguments>{ApplyArg}</Arguments>
    </Exec>
  </Actions>
</Task>";
            string file = Path.Combine(Path.GetTempPath(), $"bsmanager-openxr-{Guid.NewGuid():N}.xml");
            try
            {
                File.WriteAllText(file, xml, System.Text.Encoding.Unicode);
                return RunHidden("schtasks.exe", $"/Create /TN \"{TaskName}\" /XML \"{file}\" /F", out _);
            }
            finally
            {
                File.Delete(file);
            }
        }

        private static bool TaskRegisteredForThisExe()
        {
            if (RunHidden("schtasks.exe", $"/Query /TN \"{TaskName}\" /XML", out string xml) != 0) return false;
            Match m = Regex.Match(xml, @"<Command>""?(.*?)""?</Command>");
            return m.Success && SamePath(SecurityElementUnescape(m.Groups[1].Value), ExePath);
        }

        private static string SecurityElementUnescape(string s) =>
            s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");

        private static int RunHidden(string file, string args, out string output)
        {
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using Process p = Process.Start(psi);
            output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode;
        }

        private static bool SamePath(string a, string b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) &&
            string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    }
}
