using Microsoft.Win32;

namespace WinWidget
{
    public static class AutoStart
    {
        public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string AppName = "WinWidget";

        public static bool IsEnabled(string keyPath, string name)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath))
            {
                return key != null && key.GetValue(name) != null;
            }
        }

        public static void Set(string keyPath, string name, string exePath, bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                if (enabled) key.SetValue(name, "\"" + exePath + "\"");
                else key.DeleteValue(name, false);
            }
        }
    }
}
