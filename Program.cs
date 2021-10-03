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
        static Mutex mutex = new Mutex(true, "{67489549-940B-48FF-9B6E-70D31B4C6E71}");
        [STAThread]
        static void Main()
        {
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
        }

    }
}
