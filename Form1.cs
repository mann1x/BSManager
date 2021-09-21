using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using System.Diagnostics;
using Windows.Devices.Enumeration;
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

namespace BSManager
{
    public partial class Form1 : Form

    {
        readonly ComponentResourceManager resources = new(typeof(Form1));

        static int bsCount = 0;

        static List<string> bsSerials = new List<string>();

        static IEnumerable<JToken> bsTokens;

        // Current data format
        static DataFormat _dataFormat = DataFormat.Hex;

        static string _versionInfo;

        static TimeSpan _timeout = TimeSpan.FromSeconds(5);

        static string steamvr_lhjson;

        static bool lhfound = false;

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

        public bool HeadSetState = false;

        private static int processingCmdSync = 0;
        private static int processingLHSync = 0;

        private int ProcessLHtimerCycle = 1000;

        public Thread thrUSBDiscovery;
        public Thread thrProcessLH;

        private DateTime LastCmdStamp;
        private LastCmd LastCmdSent;

        System.Timers.Timer ProcessLHtimer = new System.Timers.Timer();

        private TextWriterTraceListener traceEx = new TextWriterTraceListener("BSManager_exceptions.log", "BSManagerEx");
        private TextWriterTraceListener traceDbg = new TextWriterTraceListener("BSManager.log", "BSManagerDbg");

        private bool debugLog = false;

        public Form1()
        {
            LogLine($"FORM INIT ");

            InitializeComponent();

            Application.ApplicationExit += delegate { notifyIcon1.Dispose(); };
        }

        private void HandleEx(Exception ex)
        {
            try { 
                notifyIcon1.ShowBalloonTip(1000, null, ex.ToString(), ToolTipIcon.Error);
                LogLine($"{ex}");
                traceEx.WriteLine($"[{DateTime.Now}] {ex}");
                traceEx.Flush();
            }
            catch (Exception e)
            {
                LogLine($"{e}");
            }

        }
        private void LogLine(string msg)
        {
            Trace.WriteLine($"{msg}");
            if (debugLog) { 
                traceDbg.WriteLine($"[{DateTime.Now}] {msg}");
                traceDbg.Flush();
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

                    LogLine($"DID={did}");

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

                if (did.Contains("VID_0483&PID_0101")) _hmd = "PIMAX HMD";
                if (did.Contains("VID_2996&PID_0309")) _hmd = "VIVE PRO HMD";

                if (_hmd.Length > 0)
                {
                    LogLine($"## {_hmd} ON ");
                    ChangeHMDStrip($" {_hmd} ON ", true);
                    this.notifyIcon1.Icon = Resource1.bsmanager_on;
                    HeadSetState = true;
                    foreach (Lighthouse lh in _lighthouses)
                    {
                        lh.ProcessDone = false;
                    }
                }
            }
            catch (Exception ex)
            {
                HandleEx(ex);
            }
        }

        private void CheckHMDOff(string did)
        {
            try
            {
                string _hmd = "";

                if (did.Contains("VID_0483&PID_0101")) _hmd = "PIMAX HMD";
                if (did.Contains("VID_2996&PID_0309")) _hmd = "VIVE PRO HMD";

                if (_hmd.Length > 0)
                {
                    LogLine($"## {_hmd} OFF ");
                    ChangeHMDStrip($" {_hmd} OFF ", false);
                    this.notifyIcon1.Icon = (Icon)(resources.GetObject("notifyIcon1.Icon"));
                    HeadSetState = false;
                    foreach (Lighthouse lh in _lighthouses)
                    {
                        lh.ProcessDone = false;
                    }
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

        private void Form1_Load(object sender, EventArgs e)
        {

            try
            {
                Trace.AutoFlush = true;

                this.Hide();

                var name = Assembly.GetExecutingAssembly().GetName();
                _versionInfo = string.Format($"{name.Version.Major:0}.{name.Version.Minor:0}.{name.Version.Build:0}");

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
                    }
                    else
                    {
                        toolStripDebugLog.Checked = true;
                        debugLog = true;
                    }
                }

                LogLine($"STARTED ");
                LogLine($"Version: {_versionInfo}");
                
                AutoUpdater.ReportErrors = false;
                AutoUpdater.InstalledVersion = new Version(_versionInfo);
                AutoUpdater.DownloadPath = Application.StartupPath;
                AutoUpdater.RunUpdateAsAdmin = false;
                AutoUpdater.Synchronous = true;
                AutoUpdater.ParseUpdateInfoEvent += AutoUpdaterOnParseUpdateInfoEvent;
                AutoUpdater.Start("https://raw.githubusercontent.com/mann1x/BSManager/master/BSManager/AutoUpdaterBSManager.json");

                bSManagerVersionToolStripMenuItem.Text = "BSManager Version " + _versionInfo;

                using (RegistryKey registryStart = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (registryStart.GetValue("BSManager") == null)
                    {
                        toolStripRunAtStartup.Checked = false;
                    }
                    else
                    {
                        toolStripRunAtStartup.Checked = true;
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

                //Thread.Sleep(500);
                thrProcessLH = new Thread(RunProcessLH);

                while (true)
                {
                    if(!thrUSBDiscovery.IsAlive)
                    {
                        LogLine("Starting ProcessLH Thread");
                        thrProcessLH.Start();
                        break;
                    }
                }

                lhfound = Read_SteamVR_config();
                if (!lhfound)
                {
                    SteamDBToolStripMenuItem.Text = "SteamVR DB not found in registry";
                }
                else
                {
                    lhfound = Load_LH_DB();
                    if (!lhfound) { SteamDBToolStripMenuItem.Text = "SteamVR DB file parse error"; } 
                    else
                    {
                        SteamDBToolStripMenuItem.Text = "Serials:";
                        foreach (string bs in bsSerials)
                        {
                            steamVRLHDBToolStripMenuItem.DropDownItems.Add(bs);
                        }
                    }                
                }

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
                    LogLine($"Starting BLE Watcher Status: {watcher.Status}");
                    watcher.Start();
                    Thread.Sleep(250);
                    LogLine($"Started BLE Watcher Status: {watcher.Status}");
                }
            }
            else
            {
                if (watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started && watcher.Status != BluetoothLEAdvertisementWatcherStatus.Stopping)
                {
                    LogLine($"Stopping BLE Watcher Status: {watcher.Status}");
                    watcher.Stop();
                    Thread.Sleep(250);
                    LogLine($"Stopped BLE Watcher Status: {watcher.Status}");
                }
            }
        }

        public void OnProcessLH(object sender, ElapsedEventArgs args)
        {
            try
            {
                bool _done = true;

                if (V2BaseStations && LastCmdSent == LastCmd.SLEEP && !HeadSetState)
                    {
                    TimeSpan _delta = DateTime.Now - LastCmdStamp;

                    //LogLine($"LastCmdSent {LastCmdSent} _delta {_delta}");

                    if (_delta.Minutes >= _V2DoubleCheckMin)
                    {
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
                else if(_done)
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
                            if (_poweredOn) item.Image = Resource1.bsmanager_on.ToBitmap();
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
                            LogLine($"STEAMVRPATH={steamvr_lhjson}");
                            return true;
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

        private bool Load_LH_DB()
        {
            try
            {
                using (StreamReader r = new StreamReader(steamvr_lhjson))
                {
                    string json = r.ReadToEnd();
                    LogLine($"SteamDB JSON Length={json.Length}");
                    JObject o = JObject.Parse(json);
                    LogLine($"SteamDB JSON Parsed");

                    bsTokens = o.SelectTokens("$..base_serial_number");

                    int _maxbs = 6;
                    int _curbs = 1;
                    
                    foreach (JToken bsitem in bsTokens)
                    {
                        if (!bsSerials.Contains(bsitem.ToString())) bsSerials.Add(bsitem.ToString());
                        LogLine($"Base Station Serial={bsitem}");
                        _curbs++;
                        if (_curbs > _maxbs) break;
                    }

                    LogLine($"Base Stations List=" + string.Join(", ", bsSerials));

                    bsCount = bsSerials.Count();
                    LogLine($"Base Stations in SteamDB: {bsCount}");
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
                    LogLine($"Found lighthouse {args.Advertisement.LocalName}");

                    existing = new Lighthouse(args.Advertisement.LocalName, args.BluetoothAddress);
                    _lighthouses.Add(existing);
                    ChangeDiscoMsg(_lighthouses.Count.ToString(), existing.Name);
                }
                
                int intpstate = 0;

                if (args.Advertisement.LocalName.StartsWith("LHB-"))
                {
                    var valveData = args.Advertisement.GetManufacturerDataByCompanyId(0x055D).Single();
                    var data = new byte[valveData.Data.Length];

                    using (var reader = DataReader.FromBuffer(valveData.Data))
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
                    
                    V2BaseStations = true;
                    existing.V2 = true;

                    if (existing.V2PoweredOn && existing.LastCmd == LastCmd.SLEEP && !HeadSetState)
                    {
                        TimeSpan _delta = DateTime.Now - existing.LastCmdStamp;
                        if (_delta.Minutes >= _V2DoubleCheckMin)
                        {
                            if (0 == Interlocked.Exchange(ref processingCmdSync, 1))
                            {
                                LogLine($"Processing SLEEP check {_V2DoubleCheckMin} minutes still ON for: {existing.Name}");
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

        private void BalloonMsg(string msg, ToolTipIcon level = ToolTipIcon.Warning)
        {
            notifyIcon1.ShowBalloonTip(1000, null, msg, level);
            Trace.WriteLine(msg);
        }

        private void ProcessLighthouseAsync(Lighthouse lh, string command)
        {
            try
            {
                void exitProcess(string msg)
                {
                    throw new ProcessError($"{msg}");
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

                Thread.Sleep(_delayCmd);

                if (!potentialLighthouseTask.IsCompletedSuccessfully || potentialLighthouseTask.Result == null) exitProcess($"[{lh.Name}] Could not connect to lighthouse");

                using var btDevice = potentialLighthouseTask.Result;

                Thread.Sleep(_delayCmd);

                var gattServicesTask = btDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask();
                gattServicesTask.Wait();

                Thread.Sleep(_delayCmd);

                if (!gattServicesTask.IsCompletedSuccessfully || gattServicesTask.Result.Status != GattCommunicationStatus.Success) exitProcess($"[{lh.Name}] Failed to get services");

                LogLine($"[{lh.Name}] Got services: {gattServicesTask.Result.Services.Count}");

                foreach (var _serv in gattServicesTask.Result.Services.ToArray())
                {
                    LogLine($"[{lh.Name}] Service Attr: {_serv.AttributeHandle} Uuid: {_serv.Uuid}");
                }

                using var service = gattServicesTask.Result.Services.SingleOrDefault(s => s.Uuid == _powerServGuid);

                Thread.Sleep(_delayCmd);

                if (service == null) exitProcess($"[{lh.Name}] Could not find power service");

                LogLine($"[{lh.Name}] Found power service");

                var powerCharacteristicsTask = service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask();
                powerCharacteristicsTask.Wait();

                Thread.Sleep(_delayCmd);

                if (!powerCharacteristicsTask.IsCompletedSuccessfully || powerCharacteristicsTask.Result.Status != GattCommunicationStatus.Success)
                    exitProcess($"[{lh.Name}] Could not get power service characteristics");

                var powerChar = powerCharacteristicsTask.Result.Characteristics.SingleOrDefault(c => c.Uuid == _powerCharGuid);

                Thread.Sleep(_delayCmd);

                if (powerChar == null) exitProcess($"[{lh.Name}] Could not get power characteristic");

                Thread.Sleep(_delayCmd);

                LogLine($"[{lh.Name}] Found power characteristic");

                    
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

                Thread.Sleep(_delayCmd);

                if (!writeResultTask.IsCompletedSuccessfully || writeResultTask.Result != GattCommunicationStatus.Success) exitProcess($"[{lh.Name}] Failed to write {command} command");

                lh.LastCmd = (command == "WAKEUP") ? LastCmd.WAKEUP : LastCmd.SLEEP;
                lh.PoweredOn = (command == "WAKEUP") ? true : false;

                btDevice.Dispose();

                LogLine($"[{lh.Name}] SUCCESS command {command}");

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
                Interlocked.Exchange(ref processingCmdSync, 0);
                if (lh.TooManyErrors) BalloonMsg($"{ex.Message}");
                lh.LastCmd = LastCmd.ERROR;
                ChangeBSMsg(lh.Name, lh.PoweredOn, lh.LastCmd, lh.Action);
            }
            catch (Exception ex)
            {
                LogLine($"[{lh.Name}] ERROR Exception Processing ({lh.HowManyErrors}): {ex}");
                Interlocked.Exchange(ref processingCmdSync, 0);
                lh.LastCmd = LastCmd.ERROR;
                ChangeBSMsg(lh.Name, lh.PoweredOn, lh.LastCmd, lh.Action);
                if (lh.TooManyErrors) HandleEx(ex);
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
            shortcut.TargetPath = MyExecutablePath;
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
                    registryStart.SetValue("BSManager", Application.ExecutablePath);
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

        private string MyExecutablePath
        {
            get
            {
                string path = Application.ExecutablePath;
                string extension = Path.GetExtension(path).ToLower();
                if (String.Equals(extension, ".dll"))
                {
                    string folder = Path.GetDirectoryName(path);
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    fileName = String.Concat(fileName, ".exe");
                    path = Path.Combine(folder, fileName);
                }
                return path;
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