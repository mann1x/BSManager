using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BSManager
{

    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        static Mutex mutex;
        [STAThread]
        static int Main(string[] args)
        {
            // Elevated helper modes of the OpenXR runtime switch: no tray app, no single-instance check
            if (args.Length >= 1 && args[0] == OpenXRRuntime.ApplyArg) return OpenXRRuntime.ApplyFromTask();
            if (args.Length >= 2 && args[0] == OpenXRRuntime.RegisterTaskArg) return OpenXRRuntime.RegisterTask(args[1]);

            mutex = new Mutex(true, "{67489549-940B-48FF-9B6E-70D31B4C6E71}");
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());

                mutex.ReleaseMutex();
            } else {
                Application.Exit();
            }
            return 0;
        }

    }
}
