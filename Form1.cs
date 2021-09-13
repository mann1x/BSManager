using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using System.Diagnostics;
using Windows.Foundation;
using Windows.System;
using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Reflection;
using System.Threading;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IWshRuntimeLibrary;

namespace BSManager
{

    public partial class Form1 : Form

    {

        static int bsCount = 0;
        static List<string> bslist = new List<string>();
        static IEnumerable<JToken> bsSerials;

        // "Magic" string for all BLE devices
        static string _aqsAllBLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";
        static string[] _requestedBLEProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.Bluetooth.Le.IsConnectable", };

        static List<DeviceInformation> _deviceList = new List<DeviceInformation>();
        static BluetoothLEDevice _selectedDevice = null;

        static List<BluetoothLEAttributeDisplay> _services = new List<BluetoothLEAttributeDisplay>();
        static BluetoothLEAttributeDisplay _selectedService = null;

        static List<BluetoothLEAttributeDisplay> _characteristics = new List<BluetoothLEAttributeDisplay>();
        static BluetoothLEAttributeDisplay _selectedCharacteristic = null;

        // Only one registered characteristic at a time.
        static List<GattCharacteristic> _subscribers = new List<GattCharacteristic>();

        // Current data format
        static DataFormat _dataFormat = DataFormat.UTF8;

        static string _versionInfo;

        // Variables for "foreach" loop implementation
        static List<string> _forEachCommands = new List<string>();
        static List<string> _forEachDeviceNames = new List<string>();
        static int _forEachCmdCounter = 0;
        static int _forEachDeviceCounter = 0;
        static bool _forEachCollection = false;
        static bool _forEachExecution = false;
        static string _forEachDeviceMask = "";
        static int _inIfBlock = 0;
        static bool _failedConditional = false;
        static bool _closingIfBlock = false;
        static int _exitCode = 0;
        static ManualResetEvent _notifyCompleteEvent = null;
        static ManualResetEvent _delayEvent = null;
        static bool _primed = false;
        static bool _doWork = true;

        static TimeSpan _timeout = TimeSpan.FromSeconds(3);

        static string steamvr_lhjson;

        static bool lhfound = false;

        public Form1()
        {

            Trace.WriteLine(" STARTED ");

            InitializeComponent();

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

                    Trace.WriteLine("DID=" + did);

                    CheckHMDOn(did);

                }

                collection.Dispose();
                return;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private void CheckHMDOn(string did)
        {
            try
            {
                if (did.Contains("VID_0483&PID_0101"))
                {
                    Trace.WriteLine(" PIMAX HMD ON ");
                    ChangeHMDStrip("PIMAX ON ");
                    BS_cmd("wakeup");
                }
                if (did.Contains("VID_2996&PID_0309"))
                {
                    Trace.WriteLine(" VIVE PRO HMD ON ");
                    ChangeHMDStrip("VIVE PRO ON ");
                    BS_cmd("wakeup");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private void CheckHMDOff(string did)
        {
            try
            {
                if (did.Contains("VID_0483&PID_0101"))
                {
                    Trace.WriteLine(" PIMAX HMD OFF ");
                    ChangeHMDStrip("PIMAX OFF ");
                    BS_cmd("sleep");
                }
                if (did.Contains("VID_2996&PID_0309"))
                {
                    Trace.WriteLine(" VIVE PRO HMD OFF ");
                    ChangeHMDStrip("VIVE PRO OFF ");
                    BS_cmd("sleep");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
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
                    //Trace.WriteLine(" INSERTED " + property.Name + " = " + property.Value);
                }
                    e.NewEvent.Dispose();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
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
                    //Trace.WriteLine(" REMOVED " + property.Name + " = " + property.Value);
                }
                e.NewEvent.Dispose();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            var name = Assembly.GetExecutingAssembly().GetName();
            _versionInfo = string.Format($"{name.Version.Major:0}.{name.Version.Minor:0}.{name.Version.Build:0}");

            bSManagerVersionToolStripMenuItem.Text = "BSManager Version " + _versionInfo;

            RegistryKey registryStart = Registry.CurrentUser.OpenSubKey
            ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (registryStart.GetValue("BSManager") == null)
            {
                toolStripRunAtStartup.Checked = false;
            }
            else
            {
                toolStripRunAtStartup.Checked = true;
            }

            this.Hide();

            registryStart.Dispose();
            
            WqlEventQuery insertQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");

            ManagementEventWatcher insertWatcher = new ManagementEventWatcher(insertQuery);
            insertWatcher.EventArrived += new EventArrivedEventHandler(DeviceInsertedEvent);
            insertWatcher.Start();

            WqlEventQuery removeQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");
            ManagementEventWatcher removeWatcher = new ManagementEventWatcher(removeQuery);
            removeWatcher.EventArrived += new EventArrivedEventHandler(DeviceRemovedEvent);
            removeWatcher.Start();

            var watcher = DeviceInformation.CreateWatcher(_aqsAllBLEDevices, _requestedBLEProperties, DeviceInformationKind.AssociationEndpoint);
            watcher.Added += (DeviceWatcher sender, DeviceInformation devInfo) =>
            {
                if (_deviceList.FirstOrDefault(d => d.Id.Equals(devInfo.Id) || d.Name.Equals(devInfo.Name)) == null) _deviceList.Add(devInfo);
            };
            watcher.Updated += (_, __) => { }; // We need handler for this event, even an empty!

            //Watch for a device being removed by the watcher
            //watcher.Removed += (DeviceWatcher sender, DeviceInformationUpdate devInfo) =>
            //{
            //    _deviceList.Remove(FindKnownDevice(devInfo.Id));
            //};

            watcher.EnumerationCompleted += (DeviceWatcher sender, object arg) => { sender.Stop(); };
            watcher.Stopped += (DeviceWatcher sender, object arg) => { _deviceList.Clear(); sender.Start(); };
            watcher.Start();

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

            BS_discover();

            USBDiscovery();

        }

        private void ChangeHMDStrip(string label)
        {
            try
            {

                this.BeginInvoke((MethodInvoker)delegate { this.ToolStripMenuItemHmd.Text = label; });

            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private void ChangeDiscoStrip()
        {
            try
            {
                ToolStripMenuItemDisco.Text = "Discovered: " + bslist.Count.ToString();
                if (bslist.Count > 0)
                {
                    foreach (string bs in bslist)
                    {
                        toolStripMenuItemBS.DropDownItems.Add(bs);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
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
                            Trace.WriteLine("STEAMVRPATH=" + steamvr_lhjson);
                            return true;
                        }
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
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
                    Trace.WriteLine("JSON LEN=" + json.Length.ToString());
                    JObject o = JObject.Parse(json);
                    Trace.WriteLine("JSON PARSED");

                    bsSerials = o.SelectTokens("$..base_serial_number");

                    Trace.WriteLine("BSSERIALS=" + string.Join(", ", bsSerials));

                    bsCount = bsSerials.Count();
                    return true;


                }
                return false;
            }
            catch (Exception ex)  //just for demonstration...it's always best to handle specific exceptions
            {
                Trace.WriteLine("ERROR=" + ex);
                //react appropriately
                return false;
            }
        }

        private async void BS_discover()
        {
            bool _doDisco = true;
            int idx = 0;
            while (_doDisco && idx < 21) { 
                bslist.Clear();
                await Process_cmd("list");
                await Process_cmd("close");
                Trace.WriteLine("BSCOUNT=" + bsCount.ToString());
                Trace.WriteLine("BSLIST=" + bslist.Count.ToString());
                if (bsCount > 0)
                {
                    if (bsCount == bslist.Count) _doDisco = false;
                } else
                {
                    if (bslist.Count > 0) _doDisco = false;
                }
                idx++;
                await Task.Delay(2500);
            }
            ChangeDiscoStrip();
        }

        private async void BS_cmd(string action)
        {

            try { 
                foreach (string bs in bslist) {
                
                    await Process_cmd("format hex");
                    await Process_cmd("open " + bs);
                    await Process_cmd("set 51968");
                    if (action == "wakeup")
                    {
                        await Process_cmd("write 51969 12 00 00 28 FF FF FF FF 00 00 00 00 00 00 00 00 00 00 00 00");
                    }
                    else
                    {
                        await Process_cmd("write 51969 12 01 00 28 FF FF FF FF 00 00 00 00 00 00 00 00 00 00 00 00");
                    }
                    await Process_cmd("close");
                }
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }

        }

        static async Task Process_cmd(string cmdStr)
        {
            string cmd = string.Empty;
            var userInput = string.Empty;
            _doWork = true;

            while (_doWork)
            {

                try
                {

                    // If we're inside "foreach" loop, process saved commands
                    if (_forEachExecution)
                    {
                        userInput = _forEachCommands[_forEachCmdCounter];
                        if (_forEachCmdCounter++ >= _forEachCommands.Count - 1)
                        {
                            _forEachCmdCounter = 0;
                            if (_forEachDeviceCounter++ > _forEachDeviceNames.Count - 1)
                            {
                                _forEachExecution = false;
                                _forEachCommands.Clear();
                                userInput = string.Empty;
                            }
                        }
                    }
                    // Otherwise read the stdin
                    else {
                        userInput = cmdStr;
                        cmdStr = string.Empty;
                    }
                    

                    // Check for the end of input
                    if (string.IsNullOrEmpty(userInput))
                    {
                        _doWork = false;
                    }
                    else userInput = userInput?.TrimStart(new char[] { ' ', '\t' });

                    string[] strs = userInput.Split(' ');
                    cmd = strs.First().ToLower();
                    string parameters = string.Join(" ", strs.Skip(1));

                    if (_forEachCollection && !cmd.Equals("endfor"))
                    {
                        _forEachCommands.Add(userInput);
                    }
                    if (cmd == "endif" || cmd == "elif" || cmd == "else")
                        _closingIfBlock = false;
                    else
                    {
                        if ((_inIfBlock > 0 && !_closingIfBlock) || _inIfBlock == 0 )
                        {
                            await HandleSwitch(cmd, parameters);
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
                catch (Exception error)
                {
                    Trace.WriteLine(error.Message);
                }

                if (cmd.Equals("write") || cmd.Equals("w"))
                    Thread.Sleep(200);

                if (!_forEachExecution && cmdStr == string.Empty)
                    _doWork = false;

                Trace.WriteLine("CMDSTR=" + cmdStr);
                _deviceList.Clear();
                _deviceList.TrimExcess();
            }
        }

        static async Task HandleSwitch(string cmd, string parameters)
        {
            Trace.WriteLine("CMD=" + cmd);
            switch (cmd)
            {
                case "if":
                    _inIfBlock++;
                    _exitCode = 0;
                    if (parameters != "")
                    {
                        string[] str = parameters.Split(' ');
                        await HandleSwitch(str[0], str.Skip(1).Aggregate((i, j) => i + " " + j));
                        _closingIfBlock = (_exitCode > 0);
                        _failedConditional = _closingIfBlock;
                    }
                    break;

                case "elif":
                    if (_failedConditional)
                    {
                        _exitCode = 0;
                        if (parameters != "")
                        {
                            string[] str = parameters.Split(' ');
                            await HandleSwitch(str[0], str.Skip(1).Aggregate((i, j) => i + " " + j));
                            _closingIfBlock = (_exitCode > 0);
                            _failedConditional = _closingIfBlock;
                        }
                    }
                    else
                        _closingIfBlock = true;
                    break;

                case "else":
                    if (_failedConditional)
                    {
                        _exitCode = 0;
                        if (parameters != "")
                        {
                            string[] str = parameters.Split(' ');
                            await HandleSwitch(str[0], str.Skip(1).Aggregate((i, j) => i + " " + j));
                        }
                    }
                    else
                        _closingIfBlock = true;
                    break;

                case "endif":
                    if (_inIfBlock > 0)
                        _inIfBlock--;
                    _failedConditional = false;
                    break;

                case "foreach":
                    _forEachCollection = true;
                    _forEachDeviceMask = parameters.ToLower();
                    break;

                case "endfor":
                    if (string.IsNullOrEmpty(_forEachDeviceMask))
                        _forEachDeviceNames = _deviceList.OrderBy(d => d.Name).Where(d => !string.IsNullOrEmpty(d.Name)).Select(d => d.Name).ToList();
                    else
                        _forEachDeviceNames = _deviceList.OrderBy(d => d.Name).Where(d => d.Name.ToLower().StartsWith(_forEachDeviceMask)).Select(d => d.Name).ToList();
                    _forEachDeviceCounter = 0;
                    _forEachCmdCounter = 0;
                    _forEachCollection = false;
                    _forEachExecution = (_forEachCommands.Count > 0);
                    break;

                case "cls":
                case "clr":
                case "clear":
                    break;

                case "st":
                case "stat":
                    ShowStatus();
                    break;

                case "p":
                case "print":
                    if (_forEachExecution && _forEachDeviceCounter > 0)
                        parameters = parameters.Replace("$", _forEachDeviceNames[_forEachDeviceCounter - 1]);

                    _exitCode += PrintInformation(parameters);
                    break;

                case "ls":
                case "list":
                    ListDevices(parameters);
                    break;

                case "open":
                    if (_forEachExecution && _forEachDeviceCounter > 0)
                        parameters = parameters.Replace("$", _forEachDeviceNames[_forEachDeviceCounter - 1]);

                    _exitCode += await OpenDevice(parameters);
                    break;

                case "timeout":
                    ChangeTimeout(parameters);
                    break;

                case "delay":
                    Delay(parameters);
                    break;

                case "close":
                    CloseDevice();
                    break;

                case "fmt":
                case "format":
                    ChangeDisplayFormat(parameters);
                    break;

                case "set":
                    _exitCode += await SetService(parameters);
                    break;

                case "r":
                case "read":
                    _exitCode += await ReadCharacteristic(parameters);
                    break;

                case "wait":
                    _notifyCompleteEvent = new ManualResetEvent(false);
                    _notifyCompleteEvent.WaitOne(_timeout);
                    _notifyCompleteEvent = null;
                    break;

                case "w":
                case "write":
                    _exitCode += await WriteCharacteristic(parameters);
                    break;

                case "subs":
                case "sub":
                    _exitCode += await SubscribeToCharacteristic(parameters);
                    break;

                case "unsub":
                case "unsubs":
                    Unsubscribe(parameters);
                    break;

                //experimental pairing function 
                case "pair":
                    PairBluetooth(parameters);
                    break;

                default:
                    Trace.WriteLine("Unknown command. Type \"?\" for help.");
                    break;
            }
        }

        static int PrintInformation(string param)
        {
            // First, we need to check output string for variables
            string[] btVars = { "%mac", "%addr", "%name", "%stat", "%id" };
            bool hasBTVars = btVars.Any(param.Contains);

            int retVal = 0;
            if (_selectedDevice == null && hasBTVars)
            {
                retVal += 1;
            }
            else
            {
                if ((_selectedDevice != null && _selectedDevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected) && hasBTVars)
                {
                    retVal += 1;
                }
                else
                {
                    param = param.Replace("%NOW", DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + " GMT " + DateTime.Now.ToLocalTime().ToString("%K"))
                                 .Replace("%now", DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString())
                                 .Replace("%HH", DateTime.Now.ToString("HH"))
                                 .Replace("%hh", DateTime.Now.ToString("hh"))
                                 .Replace("%mm", DateTime.Now.ToString("mm"))
                                 .Replace("%ss", DateTime.Now.ToString("ss"))
                                 .Replace("%D", DateTime.Now.ToLongDateString())
                                 .Replace("%d", DateTime.Now.ToShortDateString())
                                 .Replace("%T", DateTime.Now.ToLongTimeString() + " GMT " + DateTime.Now.ToLocalTime().ToString("%K"))
                                 .Replace("%t", DateTime.Now.ToShortTimeString())
                                 .Replace("%z", "GMT " + DateTime.Now.ToLocalTime().ToString("%K"))
                                 .Replace("\\t", "\t")
                                 .Replace("\\n", "\n")
                                 .Replace("\\r", "\r");

                    if (hasBTVars)
                    {
                        // This is more elegant way to get readable MAC address
                        var macAddress = Regex.Replace(_selectedDevice.BluetoothAddress.ToString("X"), @"(.{2})", "$1:").TrimEnd(':');

                        param = param.Replace("%mac", macAddress)
                                     .Replace("%addr", _selectedDevice.BluetoothAddress.ToString())
                                     .Replace("%name", _selectedDevice.Name)
                                     .Replace("%id", _selectedDevice.DeviceId)
                                     .Replace("%stat", (_selectedDevice.ConnectionStatus == BluetoothConnectionStatus.Connected).ToString());
                        //.Replace("%c", );
                    }
                }
            }

            return retVal;
        }

        static async void PairBluetooth(string param)
        {
            DevicePairingResult result = null;
            DeviceInformationPairing pairingInformation = _selectedDevice.DeviceInformation.Pairing;

            await _selectedDevice.DeviceInformation.Pairing.UnpairAsync();

            if (pairingInformation.CanPair)
                result = await _selectedDevice.DeviceInformation.Pairing.PairAsync(pairingInformation.ProtectionLevel);

        }

        static void ChangeDisplayFormat(string param)
        {
            if (!string.IsNullOrEmpty(param))
            {
                switch (param.ToLower())
                {
                    case "ascii":
                        _dataFormat = DataFormat.ASCII;
                        break;
                    case "utf8":
                        _dataFormat = DataFormat.UTF8;
                        break;
                    case "dec":
                    case "decimal":
                        _dataFormat = DataFormat.Dec;
                        break;
                    case "bin":
                    case "binary":
                        _dataFormat = DataFormat.Bin;
                        break;
                    case "hex":
                    case "hexdecimal":
                        _dataFormat = DataFormat.Hex;
                        break;
                    default:
                        break;
                }
            }
            Trace.WriteLine($"Current display format: {_dataFormat.ToString()}");
        }

        static void Delay(string param)
        {
            uint milliseconds = (uint)_timeout.TotalMilliseconds;
            uint.TryParse(param, out milliseconds);
            _delayEvent = new ManualResetEvent(false);
            _delayEvent.WaitOne((int)milliseconds, true);
            _delayEvent = null;
        }

        static void ChangeTimeout(string param)
        {
            if (!string.IsNullOrEmpty(param))
            {
                uint t;
                if (uint.TryParse(param, out t))
                {
                    if (t > 0 && t < 60)
                    {
                        _timeout = TimeSpan.FromSeconds(t);
                    }
                }
            }
            Trace.WriteLine($"Device connection timeout (sec): {_timeout.TotalSeconds}");
        }

        /// <summary>
        /// List of available BLE devices
        /// </summary>
        /// <param name="param">optional, 'w' means "wide list"</param>
        static void ListDevices(string param)
        {

            var names = _deviceList.OrderBy(d => d.Name).Where(d => !string.IsNullOrEmpty(d.Name)).Select(d => d.Name).ToList();
            
            string pattern = @"HTC BS(\s.*)";

            for (int i = 0; i < names.Count; i++) { 
                  
                Match m = Regex.Match(names[i].ToString(), pattern);
                if (m.Success) {
                    Trace.WriteLine($"FOUND BASESTATION={names[i]}");
                    bslist.Add(names[i].ToString());

                }

                Trace.WriteLine($"#{i:00}: {names[i]}");
            }
        }

        /// <summary>
        /// Show status of the currently selected BLE device
        /// </summary>
        static void ShowStatus()
        {
            if (_selectedDevice == null)
            {
                Trace.WriteLine("No device connected.");
            }
            else
            {
                if (_selectedDevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
                {
                    Trace.WriteLine($"Device {_selectedDevice.Name} is disconnected.");
                }
                else
                {
                    Trace.WriteLine($"Device {_selectedDevice.Name} is connected.");
                    if (_services.Count() > 0)
                    {
                        // List all services
                        Trace.WriteLine("Available services:");
                        for (int i = 0; i < _services.Count(); i++)
                            Trace.WriteLine($"#{i:00}: {_services[i].Name}");

                        // If service is selected,
                        if (_selectedService != null)
                        {
                            Trace.WriteLine($"Selected service: {_selectedService.Name}");

                            // List all characteristics
                            if (_characteristics.Count > 0)
                            {
                                Trace.WriteLine("Available characteristics:");

                                for (int i = 0; i < _characteristics.Count(); i++)
                                    Trace.WriteLine($"#{i:00}: {_characteristics[i].Name}\t{_characteristics[i].Chars}");

                                if (_selectedCharacteristic != null)
                                    Trace.WriteLine($"Selected characteristic: {_selectedCharacteristic.Name}");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Connect to the specific device by name or number, and make this device current
        /// </summary>
        /// <param name="deviceName"></param>
        /// <returns></returns>
        static async Task<int> OpenDevice(string deviceName)
        {
            int retVal = 0;
            if (!string.IsNullOrEmpty(deviceName))
            {
                var devs = _deviceList.OrderBy(d => d.Name).Where(d => !string.IsNullOrEmpty(d.Name)).ToList();
                string foundId = Utilities.GetIdByNameOrNumber(devs, deviceName);

                // If device is found, connect to device and enumerate all services
                if (!string.IsNullOrEmpty(foundId))
                {
                    _selectedCharacteristic = null;
                    _selectedService = null;
                    _services.Clear();

                    try
                    {
                        // only allow for one connection to be open at a time
                        if (_selectedDevice != null)
                            CloseDevice();

                        _selectedDevice = await BluetoothLEDevice.FromIdAsync(foundId).AsTask().TimeoutAfter(_timeout);
                        Trace.WriteLine($"Connecting to {_selectedDevice.Name}.");

                        var result = await _selectedDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                        if (result.Status == GattCommunicationStatus.Success)
                        {
                            Trace.WriteLine($"Found {result.Services.Count} services:");

                            for (int i = 0; i < result.Services.Count; i++)
                            {
                                var serviceToDisplay = new BluetoothLEAttributeDisplay(result.Services[i]);
                                _services.Add(serviceToDisplay);
                                Trace.WriteLine($"#{i:00}: {_services[i].Name}");
                            }
                        }
                        else
                        {
                            Trace.WriteLine($"Device {deviceName} is unreachable.");
                            retVal += 1;
                        }
                    }
                    catch
                    {
                        Trace.WriteLine($"Device {deviceName} is unreachable.");
                        retVal += 1;
                    }
                }
                else
                {
                    retVal += 1;
                }
            }
            else
            {
                Trace.WriteLine("Device name can not be empty.");
                retVal += 1;
            }
            return retVal;
        }

        /// <summary>
        /// Disconnect current device and clear list of services and characteristics
        /// </summary>
        static void CloseDevice()
        {
            // Remove all subscriptions
            if (_subscribers.Count > 0) Unsubscribe("all");

            if (_selectedDevice != null)
            {
                Trace.WriteLine($"Device {_selectedDevice.Name} is disconnected.");

                _services?.ForEach((s) => { s.service?.Dispose(); });
                _services?.Clear();
                _characteristics?.Clear();
                _selectedDevice?.Dispose();
            }
        }

        /// <summary>
        /// Set active service for current device
        /// </summary>
        /// <param name="parameters"></param>
        static async Task<int> SetService(string serviceName)
        {
            int retVal = 0;
            if (_selectedDevice != null)
            {
                if (!string.IsNullOrEmpty(serviceName))
                {
                    string foundName = Utilities.GetIdByNameOrNumber(_services, serviceName);

                    // If device is found, connect to device and enumerate all services
                    if (!string.IsNullOrEmpty(foundName))
                    {
                        var attr = _services.FirstOrDefault(s => s.Name.Equals(foundName));
                        IReadOnlyList<GattCharacteristic> characteristics = new List<GattCharacteristic>();

                        try
                        {
                            // Ensure we have access to the device.
                            var accessStatus = await attr.service.RequestAccessAsync();
                            if (accessStatus == DeviceAccessStatus.Allowed)
                            {
                                // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characterstics only 
                                // and the new Async functions to get the characteristics of unpaired devices as well. 
                                var result = await attr.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                                if (result.Status == GattCommunicationStatus.Success)
                                {
                                    characteristics = result.Characteristics;
                                    _selectedService = attr;
                                    _characteristics.Clear();
                                    Trace.WriteLine($"Selected service {attr.Name}.");

                                    if (characteristics.Count > 0)
                                    {
                                        for (int i = 0; i < characteristics.Count; i++)
                                        {
                                            var charToDisplay = new BluetoothLEAttributeDisplay(characteristics[i]);
                                            _characteristics.Add(charToDisplay);
                                            Trace.WriteLine($"#{i:00}: {charToDisplay.Name}\t{charToDisplay.Chars}");
                                        }
                                    }
                                    else
                                    {
                                        Trace.WriteLine("Service don't have any characteristic.");
                                        retVal += 1;
                                    }
                                }
                                else
                                {
                                    Trace.WriteLine("Error accessing service.");
                                    retVal += 1;
                                }
                            }
                            // Not granted access
                            else
                            {
                                Trace.WriteLine("Error accessing service.");
                                retVal += 1;
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"Restricted service. Can't read characteristics: {ex.Message}");
                            retVal += 1;
                        }
                    }
                    else
                    {
                        Trace.WriteLine("Invalid service name or number");
                        retVal += 1;
                    }
                }
                else
                {
                    Trace.WriteLine("Invalid service name or number");
                    retVal += 1;
                }
            }
            else
            {
                Trace.WriteLine("Nothing to use, no BLE device connected.");
                retVal += 1;
            }

            return retVal;
        }

        /// <summary>
        /// This function reads data from the specific BLE characteristic 
        /// </summary>
        /// <param name="param"></param>
        static async Task<int> ReadCharacteristic(string param)
        {
            int retVal = 0;
            if (_selectedDevice != null)
            {
                if (!string.IsNullOrEmpty(param))
                {
                    List<BluetoothLEAttributeDisplay> chars = new List<BluetoothLEAttributeDisplay>();

                    string charName = string.Empty;
                    var parts = param.Split('/');
                    // Do we have parameter is in "service/characteristic" format?
                    if (parts.Length == 2)
                    {
                        string serviceName = Utilities.GetIdByNameOrNumber(_services, parts[0]);
                        charName = parts[1];

                        // If device is found, connect to device and enumerate all services
                        if (!string.IsNullOrEmpty(serviceName))
                        {
                            var attr = _services.FirstOrDefault(s => s.Name.Equals(serviceName));
                            IReadOnlyList<GattCharacteristic> characteristics = new List<GattCharacteristic>();

                            try
                            {
                                // Ensure we have access to the device.
                                var accessStatus = await attr.service.RequestAccessAsync();
                                if (accessStatus == DeviceAccessStatus.Allowed)
                                {
                                    var result = await attr.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                                    if (result.Status == GattCommunicationStatus.Success)
                                        characteristics = result.Characteristics;
                                }
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine($"Restricted service. Can't read characteristics: {ex.Message}");
                                retVal += 1;
                            }

                            foreach (var c in characteristics)
                                chars.Add(new BluetoothLEAttributeDisplay(c));
                        }
                    }
                    else if (parts.Length == 1)
                    {
                        if (_selectedService == null)
                        {
                            Trace.WriteLine("No service is selected.");
                        }
                        chars = new List<BluetoothLEAttributeDisplay>(_characteristics);
                        charName = parts[0];
                    }

                    // Read characteristic
                    if (chars.Count > 0 && !string.IsNullOrEmpty(charName))
                    {
                        string useName = Utilities.GetIdByNameOrNumber(chars, charName);
                        var attr = chars.FirstOrDefault(c => c.Name.Equals(useName));
                        if (attr != null && attr.characteristic != null)
                        {
                            // Read characteristic value
                            GattReadResult result = await attr.characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

                            if (result.Status == GattCommunicationStatus.Success)
                                Trace.WriteLine(Utilities.FormatValue(result.Value, _dataFormat));
                            else
                            {
                                Trace.WriteLine($"Read failed: {result.Status}");
                                retVal += 1;
                            }
                        }
                        else
                        {
                            Trace.WriteLine($"Invalid characteristic {charName}");
                            retVal += 1;
                        }
                    }
                    else
                    {
                        Trace.WriteLine("Nothing to read, please specify characteristic name or #.");
                        retVal += 1;
                    }
                }
                else
                {
                    Trace.WriteLine("Nothing to read, please specify characteristic name or #.");
                    retVal += 1;
                }
            }
            else
            {
                Trace.WriteLine("No BLE device connected.");
                retVal += 1;
            }
            return retVal;
        }

        /// <summary>
        /// This function writes data from the specific BLE characteristic 
        /// </summary>
        /// <param name="param">
        /// parameters should be:
        ///    [char_name] or [service_name/char_name] - specific characteristics
        ///    [data_value] - data to write; data will be interpreted depending of current display format,
        ///    wrong data format will cause write fail
        /// </param>
        /// <param name="userInput">
        /// we need whole user input (trimmed from spaces on beginning) in case of text input with spaces at the end
        static async Task<int> WriteCharacteristic(string param)
        {
            int retVal = 0;
            if (_selectedDevice != null)
            {
                if (!string.IsNullOrEmpty(param))
                {
                    List<BluetoothLEAttributeDisplay> chars = new List<BluetoothLEAttributeDisplay>();

                    string charName = string.Empty;

                    // First, split data from char name (it should be a second param)
                    var parts = param.Split(' ');
                    if (parts.Length < 2)
                    {
                        Trace.WriteLine("Insufficient data for write, please provide characteristic name and data.");
                        retVal += 1;
                        return retVal;
                    }

                    // Now try to convert data to the byte array by current format
                    string data = param.Substring(parts[0].Length + 1);
                    if (string.IsNullOrEmpty(data))
                    {
                        Trace.WriteLine("Insufficient data for write.");
                        retVal += 1;
                        return retVal;
                    }
                    var buffer = Utilities.FormatData(data, _dataFormat);
                    if (buffer != null)
                    {
                        // Now process service/characteristic names
                        var charNames = parts[0].Split('/');

                        // Do we have parameter is in "service/characteristic" format?
                        if (charNames.Length == 2)
                        {
                            string serviceName = Utilities.GetIdByNameOrNumber(_services, charNames[0]);
                            charName = charNames[1];

                            // If device is found, connect to device and enumerate all services
                            if (!string.IsNullOrEmpty(serviceName))
                            {
                                var attr = _services.FirstOrDefault(s => s.Name.Equals(serviceName));
                                IReadOnlyList<GattCharacteristic> characteristics = new List<GattCharacteristic>();
                                try
                                {
                                    // Ensure we have access to the device.
                                    var accessStatus = await attr.service.RequestAccessAsync();
                                    if (accessStatus == DeviceAccessStatus.Allowed)
                                    {
                                        var result = await attr.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                                        if (result.Status == GattCommunicationStatus.Success)
                                            characteristics = result.Characteristics;
                                    }
                                    foreach (var c in characteristics)
                                        chars.Add(new BluetoothLEAttributeDisplay(c));
                                }
                                catch (Exception ex)
                                {
                                    Trace.WriteLine($"Restricted service. Can't read characteristics: {ex.Message}");
                                    retVal += 1;
                                    return retVal;
                                }
                            }
                        }
                        else if (charNames.Length == 1)
                        {
                            if (_selectedService == null)
                            {
                                Trace.WriteLine("No service is selected.");
                                retVal += 1;
                            }
                            chars = new List<BluetoothLEAttributeDisplay>(_characteristics);
                            charName = parts[0];
                        }

                        // Write characteristic
                        if (chars.Count > 0 && !string.IsNullOrEmpty(charName))
                        {
                            string useName = Utilities.GetIdByNameOrNumber(chars, charName);
                            var attr = chars.FirstOrDefault(c => c.Name.Equals(useName));
                            if (attr != null && attr.characteristic != null)
                            {
                                // Write data to characteristic
                                GattWriteResult result = await attr.characteristic.WriteValueWithResultAsync(buffer);
                                if (result.Status != GattCommunicationStatus.Success)
                                {
                                    Trace.WriteLine($"Write failed: {result.Status}");
                                    retVal += 1;
                                }
                            }
                            else
                            {
                                Trace.WriteLine($"Invalid characteristic {charName}");
                                retVal += 1;
                            }
                        }
                        else
                        {
                            Trace.WriteLine("Please specify characteristic name or # for writing.");
                            retVal += 1;
                        }
                    }
                    else
                    {
                        Trace.WriteLine("Incorrect data format.");
                        retVal += 1;
                    }
                }
            }
            else
            {
                Trace.WriteLine("No BLE device connected.");
                retVal += 1;
            }
            return retVal;
        }

        /// <summary>
        /// This function used to add "ValueChanged" event subscription
        /// </summary>
        /// <param name="param"></param>
        static async Task<int> SubscribeToCharacteristic(string param)
        {
            int retVal = 0;
            if (_selectedDevice != null)
            {
                if (!string.IsNullOrEmpty(param))
                {
                    List<BluetoothLEAttributeDisplay> chars = new List<BluetoothLEAttributeDisplay>();

                    string charName = string.Empty;
                    var parts = param.Split('/');
                    // Do we have parameter is in "service/characteristic" format?
                    if (parts.Length == 2)
                    {
                        string serviceName = Utilities.GetIdByNameOrNumber(_services, parts[0]);
                        charName = parts[1];

                        // If device is found, connect to device and enumerate all services
                        if (!string.IsNullOrEmpty(serviceName))
                        {
                            var attr = _services.FirstOrDefault(s => s.Name.Equals(serviceName));
                            IReadOnlyList<GattCharacteristic> characteristics = new List<GattCharacteristic>();

                            try
                            {
                                // Ensure we have access to the device.
                                var accessStatus = await attr.service.RequestAccessAsync();
                                if (accessStatus == DeviceAccessStatus.Allowed)
                                {
                                    var result = await attr.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                                    if (result.Status == GattCommunicationStatus.Success)
                                        characteristics = result.Characteristics;
                                }
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine($"Restricted service. Can't subscribe to characteristics: {ex.Message}");
                                retVal += 1;
                            }

                            foreach (var c in characteristics)
                                chars.Add(new BluetoothLEAttributeDisplay(c));
                        }
                    }
                    else if (parts.Length == 1)
                    {
                        if (_selectedService == null)
                        {
                            Trace.WriteLine("No service is selected.");
                            retVal += 1;
                            return retVal;
                        }
                        chars = new List<BluetoothLEAttributeDisplay>(_characteristics);
                        charName = parts[0];
                    }

                    // Read characteristic
                    if (chars.Count > 0 && !string.IsNullOrEmpty(charName))
                    {
                        string useName = Utilities.GetIdByNameOrNumber(chars, charName);
                        var attr = chars.FirstOrDefault(c => c.Name.Equals(useName));
                        if (attr != null && attr.characteristic != null)
                        {
                            // First, check for existing subscription
                            if (!_subscribers.Contains(attr.characteristic))
                            {
                                var status = await attr.characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                                if (status == GattCommunicationStatus.Success)
                                {
                                    _subscribers.Add(attr.characteristic);
                                    attr.characteristic.ValueChanged += Characteristic_ValueChanged;
                                }
                                else
                                {
                                    Trace.WriteLine($"Can't subscribe to characteristic {useName}");
                                    retVal += 1;
                                }
                            }
                            else
                            {
                                Trace.WriteLine($"Already subscribed to characteristic {useName}");
                                retVal += 1;
                            }
                        }
                        else
                        {
                            Trace.WriteLine($"Invalid characteristic {useName}");
                            retVal += 1;
                        }
                    }
                    else
                    {
                        Trace.WriteLine("Nothing to subscribe, please specify characteristic name or #.");
                        retVal += 1;
                    }
                }
                else
                {
                    Trace.WriteLine("Nothing to subscribe, please specify characteristic name or #.");
                    retVal += 1;
                }
            }
            else
            {
                Trace.WriteLine("No BLE device connected.");
                retVal += 1;
            }
            return retVal;
        }

        /// <summary>
        /// This function is used to unsubscribe from "ValueChanged" event
        /// </summary>
        /// <param name="param"></param>
        static async void Unsubscribe(string param)
        {
            if (_subscribers.Count == 0)
            {
                Trace.WriteLine("No subscription for value changes found.");
            }
            else if (string.IsNullOrEmpty(param))
            {
                Trace.WriteLine("Please specify characteristic name or # (for single subscription) or type \"unsubs all\" to remove all subscriptions");
            }
            // Unsubscribe from all value changed events
            else if (param.Replace("/", "").ToLower().Equals("all"))
            {
                foreach (var sub in _subscribers)
                {
                    await sub.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                    sub.ValueChanged -= Characteristic_ValueChanged;
                }
                _subscribers.Clear();
            }
            // unsubscribe from specific event
            else
            {

            }
        }

        /// <summary>
        /// Event handler for ValueChanged callback
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        static void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (_primed)
            {
                var newValue = Utilities.FormatValue(args.CharacteristicValue, _dataFormat);

                Trace.WriteLine($"Value changed for {sender.Uuid}: {newValue}\nBLE: ");
                if (_notifyCompleteEvent != null)
                {
                    _notifyCompleteEvent.Set();
                    _notifyCompleteEvent = null;
                }
            }
            else _primed = true;
        }

        static DeviceInformation FindKnownDevice(string deviceId)
        {
            foreach (var device in _deviceList)
            {
                if (device.Id == deviceId)
                {
                    return device;
                }
            }
            return null;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {

        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            BS_cmd("sleep");
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
                Trace.WriteLine(ex);
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

            RegistryKey registryStart = Registry.CurrentUser.OpenSubKey ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

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

            registryStart.Dispose();

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
    }
}
