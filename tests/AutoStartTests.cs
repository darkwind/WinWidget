using Microsoft.Win32;

namespace WinWidget.Tests
{
    public static class AutoStartTests
    {
        private const string TestRoot = @"Software\WinWidgetTests";
        private const string TestKey = TestRoot + @"\Run";

        public static void SetEnablesAndDisables()
        {
            try
            {
                Check.Eq(false, AutoStart.IsEnabled(TestKey, "WinWidget"));
                AutoStart.Set(TestKey, "WinWidget", @"C:\Apps\WinWidget.exe", true);
                Check.Eq(true, AutoStart.IsEnabled(TestKey, "WinWidget"));
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(TestKey))
                {
                    Check.Eq("\"C:\\Apps\\WinWidget.exe\"", key.GetValue("WinWidget"));
                }
                AutoStart.Set(TestKey, "WinWidget", @"C:\Apps\WinWidget.exe", false);
                Check.Eq(false, AutoStart.IsEnabled(TestKey, "WinWidget"));
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKeyTree(TestRoot, false);
            }
        }
    }
}
