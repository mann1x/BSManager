using System;
using System.Configuration;

namespace BSManager
{
    internal class Lighthouse
    {
        public string Name { get; private set; }
        public ulong Address { get; private set; }
        public bool ProcessDone { get; set; }
        public bool PoweredOn { get; set; }
        public bool V2PoweredOn { get; set; }
        public int LastPowerState { get; set; }
        public int ErrorTotal { get; set; }
        public int OpsTotal { get; set; }
        public string ErrorStrings { get; set; }

        private LastCmd _lastCmd;

        private DateTime _lastCmdStamp;

        public BSManufacturer Manufacturer;

        public DateTime LastCmdStamp
        {
            get
            {
                return _lastCmdStamp;
            }
        }

        public LastCmd LastCmd
        {
            get {
                return _lastCmd;
            }
            set {
                _lastCmd = value;
                _lastCmdStamp = DateTime.Now;
            }
        }

        public Action Action { get; set; }
        public bool V2 { get; set; }

        private int _errCnt;
        public string HowManyErrors
        {
            get
            {
                return _errCnt.ToString();
            }
        }

        public bool TooManyErrors
        {
            get {
                ErrorTotal++;
                if (_errCnt > 5)
                {
                    _errCnt = 0;
                    return true;
                } 
                else
                {
                    _errCnt++;
                    return false;
                }
            }
            set {
                _errCnt = 0;
                ErrorStrings = "";
            }
        }
        public Lighthouse(string name, ulong address)
        {
            Name = name;
            Address = address;
            PoweredOn = false;
            V2PoweredOn = false;
            LastCmd = LastCmd.NONE;
            Action = Action.NONE;
            Manufacturer = BSManufacturer.HTC;
            _errCnt = 0;
            LastPowerState = 0;
            ProcessDone = false;
            ErrorStrings = "";
            ErrorTotal = 0;
            OpsTotal = 0;
        }

        public override bool Equals(object obj)
        {
            return obj is Lighthouse lighthouse &&
                   Name == lighthouse.Name &&
                   Address == lighthouse.Address;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Address);
        }

    }

    public enum LastCmd 
    {
        NONE,
        ERROR,
        WAKEUP,
        SLEEP
    }
    public enum BSManufacturer
    {
        HTC,
        VIVE
    }
    public enum Action
    {
        NONE,
        WAKEUP,
        SLEEP
    }
}
