using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WinWidget
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\WinWidget.SingleInstance", out createdNew))
            {
                if (!createdNew) return;

                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new WidgetForm());
            }
        }
    }
}
