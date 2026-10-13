using System;
using System.IO;

namespace WinWidget.Tests
{
    public static class SettingsTests
    {
        public static void ParseReadsValues()
        {
            Settings s = Settings.Parse("X=100\r\nY=-200\nLocked=true");
            Check.Eq((int?)100, s.X);
            Check.Eq((int?)-200, s.Y);
            Check.Eq(true, s.Locked);
        }

        public static void ParseIgnoresGarbage()
        {
            Settings s = Settings.Parse("X=abc\n???\n=5\nLocked=maybe\nY=5");
            Check.Eq(null, s.X);
            Check.Eq((int?)5, s.Y);
            Check.Eq(false, s.Locked);
        }

        public static void ParseNullGivesDefaults()
        {
            Settings s = Settings.Parse(null);
            Check.Eq(null, s.X);
            Check.Eq(false, s.Locked);
        }

        public static void SerializeRoundTrips()
        {
            var s = new Settings();
            s.X = 10; s.Y = 20; s.Locked = true;
            Settings r = Settings.Parse(s.Serialize());
            Check.Eq((int?)10, r.X);
            Check.Eq((int?)20, r.Y);
            Check.Eq(true, r.Locked);
        }

        public static void LoadMissingFileGivesDefaults()
        {
            string path = Path.Combine(Path.GetTempPath(), "WinWidgetTests-" + Guid.NewGuid().ToString("N"), "settings.ini");
            Settings s = Settings.Load(path);
            Check.Eq(null, s.X);
        }

        public static void SaveCreatesDirectoryAndLoads()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WinWidgetTests-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "settings.ini");
            try
            {
                var s = new Settings();
                s.X = 7; s.Y = 8;
                s.Save(path);
                Settings r = Settings.Load(path);
                Check.Eq((int?)7, r.X);
                Check.Eq((int?)8, r.Y);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
