using System;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace BSManager
{
    // Switches the system OpenXR runtime to SteamVR while the headset is on and restores the previous one when it goes off.
    // The active runtime lives in HKLM, so changing it needs BSManager to run as administrator.
    internal static class OpenXRRuntime
    {
        private const string KhronosKey = @"SOFTWARE\Khronos\OpenXR\1";
        private const string SettingsKey = @"SOFTWARE\ManniX\BSManager";
        // Runtime that was active before BSManager switched to SteamVR; kept in the settings so a restart can still restore it
        private const string PreviousValue = "OpenXRPreviousRuntime";

        public static string GetActive()
        {
            using RegistryKey key = Registry.LocalMachine.OpenSubKey(KhronosKey, false);
            return key?.GetValue("ActiveRuntime")?.ToString();
        }

        public static bool CanWrite()
        {
            try
            {
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(KhronosKey, true);
                return key != null;
            }
            catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException)
            {
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
                Form1.LogLine($"[OpenXR] Can't read openvrpaths.vrpath: {ex.Message}");
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
                Form1.LogLine("[OpenXR] SteamVR OpenXR runtime not found, not switching");
                return;
            }

            string active = GetActive();
            if (SamePath(active, steamvr))
            {
                Form1.LogLine("[OpenXR] SteamVR is already the active runtime");
                return;
            }

            using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
            if (!SetActive(steamvr)) return;
            // Remember what to restore only after the switch succeeded; no previous runtime means there is nothing to restore
            if (!string.IsNullOrEmpty(active)) settings.SetValue(PreviousValue, active);
            Form1.LogLine($"[OpenXR] Active runtime switched from \"{active}\" to SteamVR \"{steamvr}\"");
        }

        public static void Restore()
        {
            using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
            string previous = settings.GetValue(PreviousValue)?.ToString();
            if (string.IsNullOrEmpty(previous)) return;

            string active = GetActive();
            string steamvr = FindSteamVR();
            // Leave the runtime alone if something else changed it meanwhile
            if (steamvr != null && !SamePath(active, steamvr))
            {
                Form1.LogLine($"[OpenXR] Active runtime changed to \"{active}\" meanwhile, not restoring \"{previous}\"");
                settings.DeleteValue(PreviousValue, false);
                return;
            }
            if (!File.Exists(previous))
            {
                Form1.LogLine($"[OpenXR] Previous runtime \"{previous}\" no longer exists, not restoring");
                settings.DeleteValue(PreviousValue, false);
                return;
            }
            if (SetActive(previous))
            {
                settings.DeleteValue(PreviousValue, false);
                Form1.LogLine($"[OpenXR] Active runtime restored to \"{previous}\"");
            }
        }

        private static bool SetActive(string runtime)
        {
            try
            {
                using RegistryKey key = Registry.LocalMachine.CreateSubKey(KhronosKey, true);
                key.SetValue("ActiveRuntime", runtime);
                return true;
            }
            catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException)
            {
                Form1.LogLine("[OpenXR] Can't change the active runtime: BSManager is not running as administrator");
                return false;
            }
        }

        private static bool SamePath(string a, string b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) &&
            string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    }
}
