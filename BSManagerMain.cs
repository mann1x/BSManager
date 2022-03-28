using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using System.Diagnostics;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;
using System.Reflection;
using System.Threading;
using Microsoft.Win32;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IWshRuntimeLibrary;
using AutoUpdaterDotNET;
using System.Runtime.Serialization;
using System.Timers;
using System.ServiceProcess;
using File = System.IO.File;
using System.Text;
using Microsoft.Toolkit.Uwp.Notifications;
using System.IO.Packaging;
using NUnit.Framework;
using System.Globalization;

namespace BSManager
{
    public enum MsgSeverity
    {
        INFO,
        WARNING,
        ERROR
    }

    public partial class Form1 : Form

    {
        readonly ComponentResourceManager resources = new ComponentResourceManager(typeof(Form1));

        static int bsCount = 0;

        static List<string> bsSerials = new List<string>();
        static List<string> sbsSerials = new List<string>();
        static List<string> pbsSerials = new List<string>();

        static IEnumerable<JToken> bsTokens;

        // Current data format
        static DataFormat _dataFormat = DataFormat.Hex;

        static string _versionInfo;

        static TimeSpan _timeout = TimeSpan.FromSeconds(5);

        static string steamvr_lhjson;
        static string pimax_lhjson;

        static bool slhfound = false;
        static bool plhfound = false;

        private HashSet<Lighthouse> _lighthouses = new HashSet<Lighthouse>();
        private BluetoothLEAdvertisementWatcher watcher;
        private ManagementEventWatcher insertWatcher;
        private ManagementEventWatcher removeWatcher;

        private int _delayCmd = 500;

        private const string v2_ON = "01";
        private const string v2_OFF = "00";

        private readonly Guid v2_powerGuid = Guid.Parse("00001523-1212-efde-1523-785feabcd124");
        private readonly Guid v2_powerCharacteristic = Guid.Parse("00001525-1212-efde-1523-785feabcd124");

        private const string v1_ON = "12 00 00 28 FF FF FF FF 00 00 00 00 00 00 00 00 00 00 00 00";
        private const string v1_OFF = "12 01 00 28 FF FF FF FF 00 00 00 00 00 00 00 00 00 00 00 00";

        private readonly Guid v1_powerGuid = Guid.Parse("0000cb00-0000-1000-8000-00805f9b34fb");
        private readonly Guid v1_powerCharacteristic = Guid.Parse("0000cb01-0000-1000-8000-00805f9b34fb");

        private int _V2DoubleCheckMin = 5;
        private bool V2BaseStations = false;
        private bool V2BaseStationsVive = false;

        public bool HeadSetState = false;

        private static int processingCmdSync = 0;
        private static int processingLHSync = 0;

        private int ProcessLHtimerCycle = 1000;

        public Thread thrUSBDiscovery;
        public Thread thrProcessLH;

        private DateTime LastCmdStamp;
        private LastCmd LastCmdSent;

        System.Timers.Timer ProcessLHtimer = new System.Timers.Timer();

        private static TextWriterTraceListener traceEx = new TextWriterTraceListener("BSManager_exceptions.log", "BSManagerEx");
        private static TextWriterTraceListener traceDbg = new TextWriterTraceListener("BSManager.log", "BSManagerDbg");

        private readonly string fnKillList = "BSManager.kill.txt";
        private readonly string fnGraceList = "BSManager.grace.txt";

        private string[] kill_list = new string[] { };
        private string[] graceful_list = new string[] { "vrmonitor", "vrdashboard", "ReviveOverlay", "vrmonitor" };
        private string[] cleanup_pilist = new string[] { "pi_server", "piservice", "pitool" };

        private static bool debugLog = false;
        private static bool ManageRuntime = false;
        private static string RuntimePath = "";
        private static bool LastManage = false;
        private static bool ShowProgressToast = true;
        private static bool SetProgressToast = true;

        protected List<Windows.UI.Notifications.ToastNotification> ptoastNotificationList = new List<Windows.UI.Notifications.ToastNotification>();

        [System.Runtime.InteropServices.DllImportAttribute("user32.dll")]
        public static extern bool PostMessage(IntPtr handleWnd, UInt32 Msg, Int32 wParam, UInt32 lParam);

        const int WM_QUERYENDSESSION = 0x0011,
                  WM_ENDSESSION = 0x0016,
                  WM_TRUE = 0x1,
                  WM_FALSE = 0x0;


        [System.Runtime.InteropServices.DllImportAttribute("user32.dll", EntryPoint = "FindWindowEx")]
        public static extern int FindWindowEx(int hwndParent, int hwndEnfant, int lpClasse, string lpTitre);

        [System.Runtime.InteropServices.DllImportAttribute("user32.dll")]
        static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [System.Runtime.InteropServices.DllImportAttribute("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr parentWindow, IntPtr previousChildWindow, string windowClass, string windowTitle);

        public Form1()
        {
            LogLine($"[BSMANAGER] FORM INIT ");

            InitializeComponent();

            Application.ApplicationExit += delegate { notifyIcon1.Dispose(); };
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            try
            {
                Trace.AutoFlush = true;

                this.Hide();

                var name = Assembly.GetExecutingAssembly().GetName();
                _versionInfo = string.Format($"{name.Version.Major:0}.{name.Version.Minor:0}.{name.Version.Build:0}");

                LogLine($"[BSMANAGER] STARTED ");
                LogLine($"[BSMANAGER] Version: {_versionInfo}");

                FindRuntime();

                using (RegistryKey registrySettingsCheck = Registry.CurrentUser.OpenSubKey("SOFTWARE\\ManniX\\BSManager", true))
                {

                    RegistryKey registrySettings;

                    if (registrySettingsCheck == null)
                    {
                        registrySettings = Registry.CurrentUser.CreateSubKey
                            ("SOFTWARE\\ManniX\\BSManager");
                    }

                    registrySettings = Registry.CurrentUser.OpenSubKey("SOFTWARE\\ManniX\\BSManager", true);

                    if (registrySettings.GetValue("DebugLog") == null)
                    {
                        toolStripDebugLog.Checked = false;
                        debugLog = false;
                        LogLine($"[BSMANAGER] Debug Log disabled");
                    }
                    else
                    {
                        toolStripDebugLog.Checked = true;
                        debugLog = true;
                        LogLine($"[BSMANAGER] Debug Log enabled");
                    }

                    if (registrySettings.GetValue("ManageRuntime") == null)
                    {
                        RuntimeToolStripMenuItem.Checked = false;
                        ManageRuntime = false;
                        LogLine($"[BSMANAGER] Manage Runtime disabled");
                    }
                    else
                    {
                        RuntimeToolStripMenuItem.Checked = true;
                        ManageRuntime = true;
                        LogLine($"[BSMANAGER] Manage Runtime enabled");
                    }

                    if (registrySettings.GetValue("ShowProgressToast") == null)
                    {
                        disableProgressToastToolStripMenuItem.Checked = true;
                        SetProgressToast = false;
                        ShowProgressToast = false;
                        LogLine($"[BSMANAGER] Progress Toast disabled");
                    }
                    else
                    {
                        disableProgressToastToolStripMenuItem.Checked = false;
                        SetProgressToast = true;
                        ShowProgressToast = true;
                        LogLine($"[BSMANAGER] Progress Toast enabled");
                    }
                }

                AutoUpdater.ReportErrors = false;
                AutoUpdater.InstalledVersion = new Version(_versionInfo);
                AutoUpdater.DownloadPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                AutoUpdater.RunUpdateAsAdmin = false;
                AutoUpdater.Synchronous = true;
                AutoUpdater.ParseUpdateInfoEvent += AutoUpdaterOnParseUpdateInfoEvent;
                AutoUpdater.Start("https://raw.githubusercontent.com/mann1x/BSManager/master/BSManager/AutoUpdaterBSManager.json");

                bSManagerVersionToolStripMenuItem.Text = "BSManager Version " + _versionInfo;

                using (RegistryKey registryStart = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    string _curpath = registryStart.GetValue("BSManager").ToString();
                    if (_curpath == null)
                    {
                        toolStripRunAtStartup.Checked = false;
                    }
                    else
                    {
                        if (_curpath != MyExecutableWithPath) registryStart.SetValue("BSManager", MyExecutableWithPath);
                        toolStripRunAtStartup.Checked = true;
                    }
                }

                string [] _glist = null;
                string [] _klist = null;

                _glist = ProcListLoad(fnGraceList, "graceful");
                _klist = ProcListLoad(fnKillList, "immediate");

                if (_glist != null) graceful_list = _glist;
                if (_klist != null) kill_list = _klist;

                _glist = null; _klist = null;

                slhfound = Read_SteamVR_config();
                if (!slhfound)
                {
                    SteamVR_DB_ToolStripMenuItem.Text = "SteamVR DB not found in registry";
                }
                else
                {
                    slhfound = Load_LH_DB("SteamVR" );
                    if (!slhfound) { SteamVR_DB_ToolStripMenuItem.Text = "SteamVR DB file parse error"; }
                    else
                    {
                        SteamVR_DB_ToolStripMenuItem.Text = "Serials:";
                        foreach (string bs in sbsSerials)
                        {
                            SteamVR_LH_ToolStripMenuItem.DropDownItems.Add(bs);
                        }
                    }
                }

                plhfound = Read_Pimax_config();
                if (!plhfound)
                {
                    Pimax_DB_ToolStripMenuItem.Text = "Pimax DB not found";
                }
                else
                {
                    plhfound = Load_LH_DB("Pimax");
                    if (!plhfound) { Pimax_DB_ToolStripMenuItem.Text = "Pimax DB file parse error"; }
                    else
                    {
                        Pimax_DB_ToolStripMenuItem.Text = "Serials:";
                        foreach (string bs in pbsSerials)
                        {
                            Pimax_LH_ToolStripMenuItem.DropDownItems.Add(bs);
                        }
                    }
                }

                WqlEventQuery insertQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");

                insertWatcher = new ManagementEventWatcher(insertQuery);
                insertWatcher.EventArrived += new EventArrivedEventHandler(DeviceInsertedEvent);
                insertWatcher.Start();

                WqlEventQuery removeQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");
                removeWatcher = new ManagementEventWatcher(removeQuery);
                removeWatcher.EventArrived += new EventArrivedEventHandler(DeviceRemovedEvent);
                removeWatcher.Start();

                watcher = new BluetoothLEAdvertisementWatcher();
                watcher.Received += AdvertisementWatcher_Received;

                thrUSBDiscovery = new Thread(RunUSBDiscovery);
                thrUSBDiscovery.Start();

                thrProcessLH = new Thread(RunProcessLH);

                while (true)
                {
                    if (!thrUSBDiscovery.IsAlive)
                    {
                        LogLine("[LightHouse] Starting LightHouse Thread");
                        thrProcessLH.Start();
                        break;
                    }
                }

                const string scheme = "pack";
                if (!UriParser.IsKnownScheme(scheme))
                {
                    Assert.That(PackUriHelper.UriSchemePack, Is.EqualTo(scheme));
                }

                // Listen to notification activation
                ToastNotificationManagerCompat.OnActivated += toastArgs =>
                {
                    // Obtain the arguments from the notification
                    ToastArguments args = ToastArguments.Parse(toastArgs.Argument);
                    // Clear the Toast Progress List
                    if (args["conversationId"] == "9113") ptoastNotificationList.Clear();
                };

            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }

        }

        private void HandleEx(Exception ex)
        {
            try {
                string _msg = ex.Message;
                if (ex.Source != string.Empty && ex.Source != null) _msg = $"{_msg} Source: {ex.Source}";
                new ToastContentBuilder()
                    .AddHeader("6789", "Exception raised", "")
                    .AddText(_msg)
                    .AddText(ex.StackTrace)
                    .Show(toast =>
                    {
                        toast.ExpirationTime = DateTime.Now.AddSeconds(360);
                    });
                LogLine($"{ex}");
                traceEx.WriteLine($"[{DateTime.Now}] {ex}");
                traceEx.Flush();
            }
            catch (Exception e)
            {
                LogLine($"[HANDLEEX] Exception: {e}");
            }

        }
        public static void LogLine(string msg)
        {
            Trace.WriteLine($"{msg}");
            if (debugLog) {
                traceDbg.WriteLine($"[{DateTime.Now}] {msg}");
                traceDbg.Flush();
            }
        }

        public void BalloonMsg(string msg, string header = "BSManager")
        {
            try
            {
                new ToastContentBuilder()
                    .AddText(header)
                    .AddText(msg)
                    .Show(toast =>
                    {
                        toast.ExpirationTime = DateTime.Now.AddSeconds(120);
                    });
                Trace.WriteLine($"{msg}");
                if (debugLog)
                {
                    traceDbg.WriteLine($"[{DateTime.Now}] {msg}");
                    traceDbg.Flush();
                }
            }
            catch (Exception e)
            {
                LogLine($"[BALLOONMSG] Exception: {e}");
            }
        }

        private void timerManageRuntime()
        {
            Task.Delay(TimeSpan.FromMilliseconds(15000))
                .ContinueWith(task => doManageRuntime());
        }

        private IntPtr[] GetProcessWindows(int process)
        {
            IntPtr[] apRet = (new IntPtr[256]);
            int iCount = 0;
            IntPtr pLast = IntPtr.Zero;
            do
            {
                pLast = FindWindowEx(IntPtr.Zero, pLast, null, null);
                int iProcess_;
                GetWindowThreadProcessId(pLast, out iProcess_);
                if (iProcess_ == process) apRet[iCount++] = pLast;
            } while (pLast != IntPtr.Zero);
            System.Array.Resize(ref apRet, iCount);
            return apRet;
        }


        private void doManageRuntime()
        {
            try
            {

                void _pKill(Process _p2kill)
                {
                    try
                    {
                        _p2kill.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                        LogLine($"[Manage Runtime] {_p2kill.ProcessName} has probably already exited");
                    }
                    catch (AggregateException)
                    {
                        LogLine($"[Manage Runtime] {_p2kill.ProcessName} can't be killed: not all processes in the tree can be killed");
                    }
                    catch (NotSupportedException)
                    {
                        LogLine($"[Manage Runtime] {_p2kill.ProcessName} can't be killed: operation not supported");
                    }
                    catch (Win32Exception)
                    {
                        LogLine($"[Manage Runtime] {_p2kill.ProcessName} can't be killed: not enogh privileges or already exiting");
                    }
                }

                void _pClose(Process _p2close)
                {
                    try
                    {
                        _p2close.CloseMainWindow();
                    }
                    catch (InvalidOperationException)
                    {
                        string ProcessName = _p2close.ProcessName;
                        LogLine($"[Manage Runtime] {ProcessName} has probably already exited");
                    }
                }


                void loopKill(string[] procnames, bool graceful)
                {
                    foreach (string procname in procnames)
                    {
                        Process[] ProcsArray = Process.GetProcessesByName(procname);
                        if (ProcsArray.Count() > 0)
                        {
                            foreach (Process Proc2Kill in ProcsArray)
                            {
                                string ProcessName = Proc2Kill.ProcessName;
                                LogLine($"[Manage Runtime] Closing {ProcessName} with PID={Proc2Kill.Id}");
                                if (graceful)
                                {
                                    _pClose(Proc2Kill);
                                }
                                else
                                {
                                    _pKill(Proc2Kill);
                                }
                                for (int i = 0; i < 20; i++)
                                {
                                    if (!Proc2Kill.HasExited)
                                    {
                                        Thread.Sleep(250);
                                        Proc2Kill.Refresh();
                                        Thread.Sleep(250);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                if (!Proc2Kill.HasExited)
                                {
                                    _pKill(Proc2Kill);
                                    Thread.Sleep(250);
                                    Proc2Kill.Refresh();
                                    Thread.Sleep(250);
                                    if (!Proc2Kill.HasExited)
                                    {
                                        IntPtr[] wnd = GetProcessWindows(Int32.Parse((Proc2Kill.Id).ToString()));
                                        var wm_ret = PostMessage(wnd[0], WM_ENDSESSION, WM_TRUE, 0x80000000);
                                        Thread.Sleep(1000);
                                        if (!Proc2Kill.HasExited)
                                        {
                                            LogLine($"[Manage Runtime] {ProcessName} can't be killed, still running");
                                        }
                                    }
                                    else
                                    {
                                        LogLine($"[Manage Runtime] {ProcessName} killed");
                                        Proc2Kill.Close();
                                        Proc2Kill.Dispose();
                                    }
                                }
                                else
                                {
                                    LogLine($"[Manage Runtime] {ProcessName} killed");
                                    Proc2Kill.Close();
                                    Proc2Kill.Dispose();
                                }
                            }
                        }
                        else
                        {
                            LogLine($"[Manage Runtime] {procname} can't be killed: not found");
                        }
                        ProcsArray = null;
                    }
                }


                if (ManageRuntime) {
                    if (!HeadSetState && LastManage) {

#if DEBUG
                        
                        Process[] localAll = Process.GetProcesses();
                        foreach (Process processo in localAll)
                        {
                            LogLine($"[PROCESSES] Active: {processo.ProcessName} PID={processo.Id}");
                        }
                        
#endif
                        ServiceController sc = new ServiceController("PiServiceLauncher");
                        LogLine($"[Manage Runtime] PiService is currently: {sc.Status}");

                        if ((sc.Status.Equals(ServiceControllerStatus.Running)) ||
                             (sc.Status.Equals(ServiceControllerStatus.StartPending)))
                        {
                            LogLine($"[Manage Runtime] Stopping PiService");
                            sc.Stop();
                            sc.Refresh();
                            LogLine($"[Manage Runtime] PiService is now: {sc.Status}");
                        }

                        loopKill(cleanup_pilist, false);

                        if (graceful_list.Length > 0)
                            loopKill(graceful_list, true);

                        if (kill_list.Length > 0)
                            loopKill(kill_list, false);

                        LastManage = false;

                    }
                    else if (HeadSetState && !LastManage)
                    {
                        Process[] PiToolArray = Process.GetProcessesByName("Pitool");
                        LogLine($"[Manage Runtime] Found {PiToolArray.Count()} PiTool running");

                        if (PiToolArray.Count() == 0)
                        {
                            ProcessStartInfo startInfo = new ProcessStartInfo(RuntimePath + "\\Pitool.exe", "hide");
                            startInfo.WindowStyle = ProcessWindowStyle.Minimized;
                            Process PiTool = Process.Start(startInfo);
                            LogLine($"[Manage Runtime] Started PiTool ({RuntimePath + "\\Pitool.exe"}) with PID={PiTool.Id}");
                        }

                        LastManage = true;
                    }
                }
            }
            catch (Exception e) when (e is Win32Exception || e is FileNotFoundException)
            {
                LogLine($"[Manage Runtime] The following exception was raised: {e}");
            }
        }

        private void USBDiscovery()
        {
            try
            {
                ManagementObjectCollection collection;
                using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_USBHub"))
                    collection = searcher.Get();

                foreach (var device in collection)
                {
                    string did = (string)device.GetPropertyValue("DeviceID");

                    LogLine($"[USB Discovery] DID={did}");

                    CheckHMDOn(did);

                }

                collection.Dispose();

                return;
            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }
        }

        private void CheckHMDOn(string did)
        {
            try
            {
                string _hmd = "";
                string action = "ON";

                if (did.Contains("VID_0483&PID_0101")) _hmd = "PIMAX HMD";
                else if (did.Contains("VID_2996&PID_0309")) _hmd = "VIVE PRO HMD";
                else if (did.Contains("VID_17E9&PID_6101")) _hmd = "VIVE WIRELESS ADAPTER";

                if (_hmd.Length > 0)
                {
                    if (SetProgressToast) ShowProgressToast = true;
                    LogLine($"[HMD] ## {_hmd} {action} ");
                    ChangeHMDStrip($" {_hmd} {action} ", true);
                    this.notifyIcon1.Icon = BSManagerRes.bsmanager_on;
                    HeadSetState = true;
                    Task.Delay(TimeSpan.FromMilliseconds(5000))
                        .ContinueWith(task => checkLHState(lh => !lh.PoweredOn, true));
                    LogLine($"[HMD] Runtime {action}: ManageRuntime is {ManageRuntime}");
                    timerManageRuntime();
                }
            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }
        }

        private void checkLHState(Func<Lighthouse, bool> lighthousePredicate, bool hs_state)
        {
            if (HeadSetState == hs_state) {
                var results = _lighthouses.Where(lighthousePredicate);
                if (results.Any())
                {
                    foreach (Lighthouse lh in _lighthouses)
                    {
                        lh.ProcessDone = false;
                    }
                }
            }
        }


        private void CheckHMDOff(string did)
        {
            try
            {
                string _hmd = "";
                string action = "OFF";

                if (did.Contains("VID_0483&PID_0101")) _hmd = "PIMAX HMD";
                if (did.Contains("VID_2996&PID_0309")) _hmd = "VIVE PRO HMD";

                if (_hmd.Length > 0)
                {
                    if (SetProgressToast) ShowProgressToast = true;
                    LogLine($"[HMD] ## {_hmd} {action} ");
                    ChangeHMDStrip($" {_hmd} {action} ", false);
                    this.notifyIcon1.Icon = BSManagerRes.bsmanager_off;
                    HeadSetState = false;
                    Task.Delay(TimeSpan.FromMilliseconds(5000))
                        .ContinueWith(task => checkLHState(lh => lh.PoweredOn, false));
                    LogLine($"[HMD] Runtime {action}: ManageRuntime is {ManageRuntime}");
                    timerManageRuntime();
                }
            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }

        }


        private void DeviceInsertedEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];

                foreach (var property in instance.Properties)
                {
                    if (property.Name == "PNPDeviceID")
                    {
                        CheckHMDOn(property.Value.ToString());
                    }
                    //LogLine($" INSERTED " + property.Name + " = " + property.Value);
                }
                e.NewEvent.Dispose();
            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }
        }

        private void DeviceRemovedEvent(object sender, EventArrivedEventArgs e)
        {
            try {
                ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                foreach (var property in instance.Properties)
                {
                    if (property.Name == "PNPDeviceID")
                    {
                        CheckHMDOff(property.Value.ToString());
                    }
                    //LogLine($" REMOVED " + property.Name + " = " + property.Value);
                }
                e.NewEvent.Dispose();
            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }
        }

        private void ProcessLH_ElapsedEventHandler(object sender, ElapsedEventArgs e)
        {
            int sync = Interlocked.CompareExchange(ref processingLHSync, 1, 0);
            if (sync == 0)
            {
                OnProcessLH(sender, e);
                processingLHSync = 0;
            }
        }

        public void ProcessWatcher(bool start)
        {
            if (start)
            {
                if (watcher.Status == BluetoothLEAdvertisementWatcherStatus.Stopped || watcher.Status == BluetoothLEAdvertisementWatcherStatus.Created)
                {
                    ptoastNotificationList.Clear();
                    LogLine($"[LightHouse] Starting BLE Watcher Status: {watcher.Status}");
                    watcher.Start();
                    Thread.Sleep(250);
                    LogLine($"[LightHouse] Started BLE Watcher Status: {watcher.Status}");
                }
            }
            else
            {
                if (watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started && watcher.Status != BluetoothLEAdvertisementWatcherStatus.Stopping)
                {
                    LogLine($"[LightHouse] Stopping BLE Watcher Status: {watcher.Status}");
                    watcher.Stop();
                    Thread.Sleep(250);
                    LogLine($"[LightHouse] Stopped BLE Watcher Status: {watcher.Status}");
                    ptoastNotificationList.Clear();
                }
            }
        }

        public void OnProcessLH(object sender, ElapsedEventArgs args)
        {
            try
            {
                bool _done = true;

                if (V2BaseStationsVive  && LastCmdSent == LastCmd.SLEEP && !HeadSetState)
                {
                    TimeSpan _delta = DateTime.Now - LastCmdStamp;

                    //LogLine($"LastCmdSent {LastCmdSent} _delta {_delta}");

                    if (_delta.Minutes >= _V2DoubleCheckMin)
                    {
                        ShowProgressToast = false;
                        foreach (Lighthouse lh in _lighthouses)
                        {
                            lh.ProcessDone = false;
                        }
                    }
                }

                foreach (Lighthouse _lh in _lighthouses)
                {
                    if (_lh.ProcessDone == false) _done = false;
                }

                if (_lighthouses.Count == 0 || _lighthouses.Count < bsCount)
                {
                    ProcessWatcher(true);
                }
                else if (_done)
                {
                    ProcessWatcher(false);
                }
                else
                {
                    ProcessWatcher(true);
                }
                Thread.Sleep(ProcessLHtimerCycle);
            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }
        }


        void RunUSBDiscovery()
        {
            USBDiscovery();
        }

        void RunProcessLH()
        {
            ProcessLHtimer.Interval = ProcessLHtimerCycle;
            ProcessLHtimer.Elapsed += new ElapsedEventHandler(ProcessLH_ElapsedEventHandler);
            ProcessLHtimer.Start();
        }

        private void AutoUpdaterOnParseUpdateInfoEvent(ParseUpdateInfoEventArgs args)
        {
            dynamic json = JsonConvert.DeserializeObject(args.RemoteData);
            args.UpdateInfo = new UpdateInfoEventArgs
            {
                CurrentVersion = json.version,
                ChangelogURL = json.changelog,
                DownloadURL = json.url,
                Mandatory = new Mandatory
                {
                    Value = json.mandatory.value,
                    UpdateMode = json.mandatory.mode,
                    MinimumVersion = json.mandatory.minVersion
                },
                CheckSum = new CheckSum
                {
                    Value = json.checksum.value,
                    HashingAlgorithm = json.checksum.hashingAlgorithm
                }
            };
        }
        private void ChangeHMDStrip(string label, bool _checked)
        {
            try
            {
                BeginInvoke((MethodInvoker)delegate {
                    ToolStripMenuItemHmd.Text = label;
                    ToolStripMenuItemHmd.Checked = _checked;
                });
            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }
        }

        private void ChangeDiscoMsg(string count, string nameBS)
        {
            try
            {
                BeginInvoke((MethodInvoker)delegate {
                    ToolStripMenuItemDisco.Text = $"Discovered: {count}/{bsCount}";
                    toolStripMenuItemBS.DropDownItems.Add(nameBS);
                });



            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }
        }
        private void ChangeBSMsg(string _name, bool _poweredOn, LastCmd _lastCmd, Action _action)
        {
            try
            {
                string _cmdStatus = "";
                string _actionStatus = "";
                switch (_lastCmd)
                {
                    case LastCmd.ERROR:
                        _cmdStatus = "[ERROR] ";
                        break;
                    default:
                        _cmdStatus = "";
                        break;
                }
                switch (_action)
                {
                    case Action.WAKEUP:
                        _actionStatus = " - Going to Wakeup";
                        break;
                    case Action.SLEEP:
                        _actionStatus = " - Going to Standby";
                        break;
                    default:
                        _actionStatus = "";
                        break;
                }

                BeginInvoke((MethodInvoker)delegate {
                    foreach (ToolStripMenuItem item in toolStripMenuItemBS.DropDownItems)
                    {
                        if (item.Text.StartsWith(_name))
                        {
                            if (_poweredOn) item.Image = BSManagerRes.bsmanager_on.ToBitmap();
                            if (!_poweredOn) item.Image = null;
                            item.Text = $"{_name} {_cmdStatus}{_actionStatus}";
                        }
                    }

                });



            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }
        }

        private bool Read_SteamVR_config()
        {
            try
            {
                steamvr_lhjson = string.Empty;
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\WOW6432Node\\Valve\\Steam"))
                {
                    if (key != null)
                    {
                        Object o = key.GetValue("InstallPath");
                        if (o != null)
                        {

                            steamvr_lhjson = o.ToString() + "\\config\\lighthouse\\lighthousedb.json";
                            if (File.Exists(steamvr_lhjson))
                            {
                                LogLine($"[CONFIG] Found SteamVR LH DB at Path={steamvr_lhjson}");
                                return true;
                            }
                            else
                            {
                                LogLine($"[CONFIG] Not found SteamVR LH DB at Path={steamvr_lhjson}");
                                return false;
                            }
                        }
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                HandleEx(ex);
                return false;
            }
        }

        private bool Read_Pimax_config()
        {
            try
            {
                pimax_lhjson = string.Empty;
                string ProgramDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                pimax_lhjson = ProgramDataFolder + "\\pimax\\runtime\\config\\lighthouse\\lighthousedb.json";
                if (File.Exists(pimax_lhjson))
                {
                    LogLine($"[CONFIG] Found Pimax LH DB at Path={pimax_lhjson}");
                    return true;
                }
                else
                {
                    LogLine($"[CONFIG] Not found Pimax LH DB at Path={pimax_lhjson}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                HandleEx(ex);
                return false;
            }
        }

        private bool Load_LH_DB(string db_name)
        {
            try
            {
                string _lhjson = string.Empty;
                if (db_name == "Pimax")
                {
                    _lhjson = pimax_lhjson;
                }
                else
                {
                    _lhjson = steamvr_lhjson;
                }
                using (StreamReader r = new StreamReader(_lhjson))
                {
                    string json = r.ReadToEnd();
                    LogLine($"[CONFIG] SteamDB JSON Length={json.Length}");
                    JObject o = JObject.Parse(json);
                    LogLine($"[CONFIG] SteamDB JSON Parsed");

                    bsTokens = o.SelectTokens("$..base_serial_number");

                    int _maxbs = 6;
                    int _curbs = 1;
                    int _bsCount = 0;

                    foreach (JToken bsitem in bsTokens)
                    {
                        if (!bsSerials.Contains(bsitem.ToString())) bsSerials.Add(bsitem.ToString());
                        if (db_name == "Pimax")
                        {
                            if (!pbsSerials.Contains(bsitem.ToString())) pbsSerials.Add(bsitem.ToString());
                            _bsCount = pbsSerials.Count;
                        }
                        else
                        {
                            if (!sbsSerials.Contains(bsitem.ToString())) sbsSerials.Add(bsitem.ToString());
                            _bsCount = sbsSerials.Count;
                        }

                        LogLine($"[CONFIG] {db_name} DB Base Station Serial={bsitem}");
                        _curbs++;
                        if (_curbs > _maxbs) break;
                    }

                    LogLine($"[CONFIG] {db_name} DB Base Stations List=" + string.Join(", ", bsSerials));

                    bsCount = bsSerials.Count();

                    LogLine($"[CONFIG] {db_name} DB Base Stations count: {_bsCount}");

                    return true;

                }
            }
            catch (Exception ex)
            {
                HandleEx(ex);
                return false;
            }
        }

        private void AdvertisementWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            try {

                //Trace.WriteLine($"Advertisment: {args.Advertisement.LocalName}");

                if (!args.Advertisement.LocalName.StartsWith("LHB-") && !args.Advertisement.LocalName.StartsWith("HTC BS "))
                {
                    return;
                }

                //Trace.WriteLine($"Advertisment: {args.Advertisement.LocalName}");

                var existing = _lighthouses.SingleOrDefault(lh => lh.Address == args.BluetoothAddress);

                if (existing == null)
                {
                    LogLine($"[LightHouse] Found lighthouse {args.Advertisement.LocalName}");

                    existing = new Lighthouse(args.Advertisement.LocalName, args.BluetoothAddress);
                    _lighthouses.Add(existing);
                    ChangeDiscoMsg(_lighthouses.Count.ToString(), existing.Name);
                }

                int intpstate = 0;

                if (args.Advertisement.LocalName.StartsWith("LHB-"))
                {
                    var valveData = args.Advertisement.GetManufacturerDataByCompanyId(0x055D);
                    var htcData = args.Advertisement.GetManufacturerDataByCompanyId(0x02ED);

                    if (valveData.Count > 0) {

                        existing.Manufacturer = BSManufacturer.VIVE;

                        var valveDataSingle = valveData.Single();
                        var data = new byte[valveDataSingle.Data.Length];

                        using (var reader = DataReader.FromBuffer(valveDataSingle.Data))
                        {
                            reader.ReadBytes(data);
                        }

                        if (!string.IsNullOrEmpty(data[4].ToString()))
                        {
                            intpstate = Int32.Parse(data[4].ToString());
                            existing.V2PoweredOn = intpstate > 0;

                            //existing.PoweredOn = data[4] == 0x03;
                            //LogLine($"{existing.Name} power status {intpstate} last {existing.lastPowerState} PoweredOn={existing.PoweredOn}");
                        }

                        V2BaseStationsVive = true;
                    }
                    else if (htcData.Count > 0)
                    {
                        var htcDataSingle = htcData.Single();
                        var data = new byte[htcDataSingle.Data.Length];


                        using (var reader = DataReader.FromBuffer(htcDataSingle.Data))
                        {
                            reader.ReadBytes(data);
                        }


                        if (!string.IsNullOrEmpty(data[4].ToString()))
                        {

                            intpstate = Int32.Parse(data[4].ToString());
                            existing.V2PoweredOn = intpstate > 0;

                            //existing.PoweredOn = data[4] == 0x03;
                            //LogLine($"{existing.Name} power status {intpstate} last {existing.lastPowerState} PoweredOn={existing.PoweredOn}");
                        }
                    }

                    V2BaseStations = true;
                    existing.V2 = true;

                    if (existing.V2PoweredOn && existing.LastCmd == LastCmd.SLEEP && !HeadSetState && existing.Manufacturer == BSManufacturer.VIVE)
                    {
                        TimeSpan _delta = DateTime.Now - existing.LastCmdStamp;
                        if (_delta.Minutes >= _V2DoubleCheckMin)
                        {
                            if (0 == Interlocked.Exchange(ref processingCmdSync, 1))
                            {
                                ShowProgressToast = false;
                                LogLine($"[LightHouse] Processing SLEEP check {_V2DoubleCheckMin} minutes still ON for: {existing.Name}");
                                ProcessLighthouseAsync(existing, "SLEEP");
                                existing.ProcessDone = true;
                            }
                        }
                        else
                        {
                            existing.ProcessDone = true;
                        }
                    }

                }
                else
                {
                    existing.V2 = false;
                }


                if (HeadSetState)
                {
                    if (existing.V2 && (existing.LastCmd == LastCmd.NONE) && existing.PoweredOn)
                    {
                        ChangeBSMsg(existing.Name, true, LastCmd.WAKEUP, Action.NONE);
                        return;
                    }
                    if (existing.LastCmd != LastCmd.WAKEUP)
                    {
                        if (0 == Interlocked.Exchange(ref processingCmdSync, 1))
                        {
                            ProcessLighthouseAsync(existing, "WAKEUP");
                        }
                    }
                }
                else
                {
                    if (existing.V2 && (existing.LastCmd == LastCmd.NONE) && !existing.PoweredOn)
                    {
                        ChangeBSMsg(existing.Name, false, LastCmd.SLEEP, Action.NONE);
                        return;
                    }
                    if (existing.LastCmd != LastCmd.SLEEP)
                    {
                        if (0 == Interlocked.Exchange(ref processingCmdSync, 1))
                        {
                            ProcessLighthouseAsync(existing, "SLEEP");
                        }
                    }
                }

                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }

        }

        private void ProcessLighthouseAsync(Lighthouse lh, string command)
        {
            try
            {
                void exitProcess(string msg)
                {
                    throw new ProcessError($"{msg}");
                }

                var progressdec = CultureInfo.InvariantCulture.Clone() as CultureInfo;
                progressdec.NumberFormat.NumberDecimalSeparator = ".";
                string _toastAction = (command == "WAKEUP") ? "Waking up" : "Set to sleep";
                uint pidx = 1;
                string ptag = "LHProcess";
                string pgroup = "LHProcess";
                
                int _leftDone = 0;
                foreach (Lighthouse lhDone in _lighthouses) {
                    if (lhDone.ProcessDone) _leftDone++;
                }

                lh.OpsTotal++;

                int _doneCount=_leftDone+1;

                var pcontent = new ToastContentBuilder()
                    .AddArgument("action", "viewConversation")
                    .AddArgument("conversationId", 9113)
                    .AddText("Commandeering the Base Stations...")
                    .AddAudio(null,null,true)
                    .AddVisualChild(new AdaptiveProgressBar()
                    {
                        Title = new BindableString("title"),
                        Value = new BindableProgressBarValue("progressValue"),
                        ValueStringOverride = new BindableString("pregressCount"),
                        Status = new BindableString("progressStatus")
                    }).GetToastContent();

                int eRate = (int)Math.Round((double)(100 * lh.ErrorTotal) / lh.OpsTotal);
                bool showeRate = (eRate > 10) ? true : false;
                string bsmanuf = "";
                if (V2BaseStations)
                    bsmanuf = (lh.Manufacturer == BSManufacturer.HTC) ? " (by HTC)" : " (by VIVE)";

                var ptoast = new Windows.UI.Notifications.ToastNotification(pcontent.GetXml());

                if (ptoastNotificationList.Count == 0)
                {
                    ptoast.Tag = ptag;
                    ptoast.Group = pgroup;
                    ptoast.Data = new Windows.UI.Notifications.NotificationData();
                    ptoast.Data.Values["title"] = $"BS {lh.Name}{bsmanuf}";
                    ptoast.Data.Values["progressValue"] = "0";
                    ptoast.Data.Values["pregressCount"] = $"{_doneCount}/{bsCount}";
                    ptoast.Data.Values["progressStatus"] = $"{_toastAction}... (0%)";
                    ptoast.Data.SequenceNumber = pidx;
                    ptoast.ExpirationTime = DateTime.Now.AddSeconds(120);

                    ptoastNotificationList.Add(ptoast);
                    if (ShowProgressToast) ToastNotificationManagerCompat.CreateToastNotifier().Show(ptoast);
                } 
                else
                {
                    ptoast = ptoastNotificationList[0];
                    var initdata = new Windows.UI.Notifications.NotificationData
                    {
                        SequenceNumber = pidx++
                    };
                    initdata.Values["title"] = $"BS {lh.Name}{bsmanuf}";
                    initdata.Values["progressValue"] = $"0";
                    initdata.Values["pregressCount"] = $"{_doneCount}/{bsCount}";
                    initdata.Values["progressStatus"] = $"{_toastAction}... (0%)";
                    if (ShowProgressToast) ToastNotificationManagerCompat.CreateToastNotifier().Update(initdata, ptag, pgroup);
                }

                void updateProgress(double percentage, string _msg = "Commandeering")
                {
                    string _ptag = "LHProcess";
                    string _pgroup = "LHProcess";
                    var data = new Windows.UI.Notifications.NotificationData
                    {
                        SequenceNumber = pidx++
                    };
                    double _p = percentage / 100;
                    string _status = $"{_toastAction}... ({percentage}%)";
                    if (showeRate) _status += $" [Errors {eRate}%]";
                    data.Values["title"] = $"BS {lh.Name}{bsmanuf}";
                    data.Values["progressValue"] = $"{_p.ToString(progressdec)}";
                    data.Values["pregressCount"] = $"{_doneCount}/{bsCount}";
                    data.Values["progressStatus"] = _status;
                    if (ShowProgressToast) ToastNotificationManagerCompat.CreateToastNotifier().Update(data, _ptag, _pgroup);
                }


                LogLine($"[{lh.Name}] START Processing command: {command}");

                lh.Action = (command == "WAKEUP") ? Action.WAKEUP : Action.SLEEP;

                ChangeBSMsg(lh.Name, lh.PoweredOn, lh.LastCmd, lh.Action);

                Guid _powerServGuid = v1_powerGuid;
                Guid _powerCharGuid = v1_powerCharacteristic;

                if (lh.V2)
                {
                    _powerServGuid = v2_powerGuid;
                    _powerCharGuid = v2_powerCharacteristic;
                }
                //https://docs.microsoft.com/en-us/windows/uwp/devices-sensors/gatt-client
                var potentialLighthouseTask = BluetoothLEDevice.FromBluetoothAddressAsync(lh.Address).AsTask();
                potentialLighthouseTask.Wait();

                if (ShowProgressToast) updateProgress(10);

                Thread.Sleep(_delayCmd);

                if (!potentialLighthouseTask.IsCompletedSuccessfully || potentialLighthouseTask.Result == null) exitProcess($"Could not connect to lighthouse");

                using var btDevice = potentialLighthouseTask.Result;

                if (ShowProgressToast) updateProgress(20);

                Thread.Sleep(_delayCmd);

                var gattServicesTask = btDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask();
                gattServicesTask.Wait();

                if (ShowProgressToast) updateProgress(30);

                Thread.Sleep(_delayCmd);

                if (!gattServicesTask.IsCompletedSuccessfully || gattServicesTask.Result.Status != GattCommunicationStatus.Success) exitProcess($"Failed to get services");

                LogLine($"[{lh.Name}] Got services: {gattServicesTask.Result.Services.Count}");

                foreach (var _serv in gattServicesTask.Result.Services.ToArray())
                {
                    LogLine($"[{lh.Name}] Service Attr: {_serv.AttributeHandle} Uuid: {_serv.Uuid}");
                }

                using var service = gattServicesTask.Result.Services.SingleOrDefault(s => s.Uuid == _powerServGuid);

                if (ShowProgressToast) updateProgress(40);

                Thread.Sleep(_delayCmd);

                if (service == null) exitProcess($"Could not find power service");

                LogLine($"[{lh.Name}] Found power service");

                var powerCharacteristicsTask = service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask();
                powerCharacteristicsTask.Wait();

                if (ShowProgressToast) updateProgress(50);

                Thread.Sleep(_delayCmd);

                if (!powerCharacteristicsTask.IsCompletedSuccessfully || powerCharacteristicsTask.Result.Status != GattCommunicationStatus.Success)
                    exitProcess($"Could not get power service characteristics");

                var powerChar = powerCharacteristicsTask.Result.Characteristics.SingleOrDefault(c => c.Uuid == _powerCharGuid);

                if (ShowProgressToast) updateProgress(60);

                Thread.Sleep(_delayCmd);

                if (powerChar == null) exitProcess($"Could not get power characteristic");

                if (ShowProgressToast) updateProgress(70);

                Thread.Sleep(_delayCmd);

                LogLine($"[{lh.Name}] Found power characteristic");

                if (ShowProgressToast) updateProgress(80);

                string data = v1_OFF;
                if (command == "WAKEUP") data = v1_ON;

                if (lh.V2)
                {
                    data = v2_OFF;
                    if (command == "WAKEUP") data = v2_ON;
                }

                string[] values = data.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                byte[] bytes = new byte[values.Length];

                for (int i = 0; i < values.Length; i++)
                    bytes[i] = Convert.ToByte(values[i], (_dataFormat == DataFormat.Dec ? 10 : (_dataFormat == DataFormat.Hex ? 16 : 2)));

                var writer = new DataWriter();
                writer.ByteOrder = ByteOrder.LittleEndian;
                writer.WriteBytes(bytes);

                var buff = writer.DetachBuffer();

                LogLine($"[{lh.Name}] Sending {command} command to {lh.Name}");
                var writeResultTask = powerChar.WriteValueAsync(buff).AsTask();
                writeResultTask.Wait();

                if (ShowProgressToast) updateProgress(95);

                Thread.Sleep(_delayCmd);

                if (!writeResultTask.IsCompletedSuccessfully || writeResultTask.Result != GattCommunicationStatus.Success) exitProcess($"Failed to write {command} command");

                lh.LastCmd = (command == "WAKEUP") ? LastCmd.WAKEUP : LastCmd.SLEEP;
                lh.PoweredOn = (command == "WAKEUP") ? true : false;

                btDevice.Dispose();

                LogLine($"[{lh.Name}] SUCCESS command {command}");

                if (ShowProgressToast) updateProgress(100, "Command received!");

                Thread.Sleep(_delayCmd);

                LastCmdSent = lh.LastCmd;
                LastCmdStamp = DateTime.Now;

                lh.Action = Action.NONE;

                lh.ProcessDone = true;

                ChangeBSMsg(lh.Name, lh.PoweredOn, lh.LastCmd, lh.Action);

                lh.TooManyErrors = true;

                Interlocked.Exchange(ref processingCmdSync, 0);

                LogLine($"[{lh.Name}] END Processing");
            }
            catch (ProcessError ex)
            {
                LogLine($"[{lh.Name}] ERROR Processing ({lh.HowManyErrors}): {ex}");
                lh.ErrorStrings = lh.ErrorStrings.Insert(0, $"{ex.Message}\n");
                if (lh.TooManyErrors) BalloonMsg($"{lh.ErrorStrings}", $"[{lh.Name}] LAST ERRORS:");
                lh.LastCmd = LastCmd.ERROR;
                ChangeBSMsg(lh.Name, lh.PoweredOn, lh.LastCmd, lh.Action);
                Interlocked.Exchange(ref processingCmdSync, 0);
            }
            catch (Exception ex)
            {
                LogLine($"[{lh.Name}] ERROR Exception Processing ({lh.HowManyErrors}): {ex}");
                lh.LastCmd = LastCmd.ERROR;
                lh.ErrorStrings = lh.ErrorStrings.Insert(0, $"{ex.Message}\n");
                if (lh.TooManyErrors) BalloonMsg($"{lh.ErrorStrings}", $"[{lh.Name}] LAST ERRORS:");
                ChangeBSMsg(lh.Name, lh.PoweredOn, lh.LastCmd, lh.Action);
                Interlocked.Exchange(ref processingCmdSync, 0);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            while (true)
            {
                if (0 == Interlocked.Exchange(ref processingCmdSync, 1)) break;
                Thread.Sleep(100);
            }

        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            traceDbg.Close();
            traceEx.Close();
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void createDesktopShortcutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try {
                object shDesktop = (object)"Desktop";
                WshShell shell = new WshShell();
                string shortcutAddress = (string)shell.SpecialFolders.Item(ref shDesktop) + @"\BSManager.lnk";
                IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutAddress);
                shortcut.Description = "Open BSManager";
                shortcut.Hotkey = "";
                shortcut.TargetPath = MyExecutableWithPath;
                shortcut.Save();
            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }
        }

        private void licenseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openlink("https://github.com/mann1x/BSManager/LICENSE");
        }

        private void documentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openlink("https://github.com/mann1x/BSManager/");
        }

        private void toolStripRunAtStartup_Click(object sender, EventArgs e)
        {

            using (RegistryKey registryStart = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (!toolStripRunAtStartup.Checked)
                {
                    registryStart.SetValue("BSManager", MyExecutableWithPath);
                    toolStripRunAtStartup.Checked = true;
                }
                else
                {
                    registryStart.DeleteValue("BSManager", false);
                    toolStripRunAtStartup.Checked = false;
                }
            }

        }

        private void openlink(string uri)
        {
            var psi = new ProcessStartInfo();
            psi.UseShellExecute = true;
            psi.FileName = uri;
            Process.Start(psi);
        }

        private string MyExecutableWithPath
        {
            get
            {
                string filepath = Process.GetCurrentProcess().MainModule.FileName;
                string extension = Path.GetExtension(filepath).ToLower();
                if (String.Equals(extension, ".dll"))
                {
                    string folder = Path.GetDirectoryName(filepath);
                    string fileName = Path.GetFileNameWithoutExtension(filepath);
                    fileName = String.Concat(fileName, ".exe");
                    filepath = Path.Combine(folder, fileName);
                }
                return filepath;
            }
        }

        private string[] ProcListLoad(string _filename, string friendlyListName)
        {
            try
            {
                string[] _list = null;
                if (File.Exists(_filename))
                {
                    _list = File.ReadLines(_filename).ToArray();
                    LogLine($"[CONFIG] Loaded custom processes list for {friendlyListName} killing: {string.Join(", ", _list)}");
                }
                return _list;
            }
            catch (Exception ex)
            {
                HandleEx(ex);
                return null;
            }
        }


        private void FindRuntime()
        {
            try
            {
                using (RegistryKey registryManage = Registry.CurrentUser.OpenSubKey("SOFTWARE\\ManniX\\BSManager", true))
                {
                    ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Service");
                    var collection = searcher.Get().Cast<ManagementBaseObject>()
                            .Where(mbo => (string)mbo.GetPropertyValue("Name") == "PiServiceLauncher")
                            .Select(mbo => (string)mbo.GetPropertyValue("PathName"));

                    if (collection.Any())
                    {
                        RuntimePath = Path.GetDirectoryName(collection.First());

                        LogLine($"[CONFIG] Found Runtime={RuntimePath}");
                        registryManage.SetValue("ManageRuntimePath", RuntimePath);
                        
                    }
                    else
                    {
                        BalloonMsg($"[CONFIG] Pimax Runtime not found");
                        LogLine($"[CONFIG] Pimax Runtime not found");
                    }
                }
            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }

}

        private void toolStripDebugLog_Click(object sender, EventArgs e)
        {

            using (RegistryKey registryDebug = Registry.CurrentUser.OpenSubKey("SOFTWARE\\ManniX\\BSManager", true))
            {
                if (!toolStripDebugLog.Checked)
                {
                    registryDebug.SetValue("DebugLog", "1");
                    toolStripDebugLog.Checked = true;
                }
                else
                {
                    registryDebug.DeleteValue("DebugLog", false);
                    toolStripDebugLog.Checked = false;
                }

            }
        }

        private void disableProgressToastToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                using (RegistryKey registryManage = Registry.CurrentUser.OpenSubKey("SOFTWARE\\ManniX\\BSManager", true))
                {
                    if (!disableProgressToastToolStripMenuItem.Checked)
                    {
                        SetProgressToast = false;
                        ShowProgressToast = false;
                        registryManage.DeleteValue("ShowProgressToast", false);
                        disableProgressToastToolStripMenuItem.Checked = true;
                    }
                    else
                    {
                        SetProgressToast = true;
                        ShowProgressToast = true;
                        registryManage.SetValue("ShowProgressToast", "1");
                        disableProgressToastToolStripMenuItem.Checked = false;
                    }
                }

            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }
        }
       
        private void RuntimeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                using (RegistryKey registryManage = Registry.CurrentUser.OpenSubKey("SOFTWARE\\ManniX\\BSManager", true))
                {
                    if (!RuntimeToolStripMenuItem.Checked)
                    {
                            ManageRuntime = true;
                            registryManage.SetValue("ManageRuntime", "1");
                            RuntimeToolStripMenuItem.Checked = true;
                    }
                    else
                    {
                        ManageRuntime = false;
                        registryManage.DeleteValue("ManageRuntime", false);
                        RuntimeToolStripMenuItem.Checked = false;
                    }
                }

            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }

        }
        public new void Dispose()
        {
            ProcessLHtimer.Enabled = false;
            ProcessLHtimer.Stop();
            removeWatcher.Stop();
            insertWatcher.Stop();
            watcher.Stop();
            Dispose(true);
        }


    }
    public class ProcessError : Exception
    {
        public ProcessError()
        {
        }

        public ProcessError(string message) : base(message)
        {
        }

        public ProcessError(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ProcessError(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        ProcessError(int severity, string message) : base(message)
        {
        }
    }

}