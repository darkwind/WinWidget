using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace WinWidget
{
    public sealed class Settings
    {
        public int? X;
        public int? Y;
        public bool Locked;

        public static string DefaultPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WinWidget", "settings.ini");
            }
        }

        public static Settings Parse(string text)
        {
            var s = new Settings();
            if (text == null) return s;

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                int n;
                bool b;
                if (key == "X" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) s.X = n;
                else if (key == "Y" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) s.Y = n;
                else if (key == "Locked" && bool.TryParse(value, out b)) s.Locked = b;
            }
            return s;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            if (X.HasValue) sb.Append("X=").Append(X.Value.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            if (Y.HasValue) sb.Append("Y=").Append(Y.Value.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            sb.Append("Locked=").Append(Locked ? "true" : "false").Append("\r\n");
            return sb.ToString();
        }

        public static Settings Load(string path)
        {
            try
            {
                return File.Exists(path) ? Parse(File.ReadAllText(path)) : new Settings();
            }
            catch (IOException)
            {
                return new Settings();
            }
            catch (UnauthorizedAccessException)
            {
                return new Settings();
            }
        }

        // A failed save only loses the position; the widget keeps running.
        public void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Serialize());
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
