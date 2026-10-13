using System;
using System.Globalization;

namespace WinWidget
{
    public enum Level { Normal, Warn, Critical }

    public static class Format
    {
        public const double OneMb = 1048576;
        private const double OneGb = 1073741824;
        private static readonly string[] RateUnits = { "B/s", "KB/s", "MB/s", "GB/s" };

        public static string Rate(double bps)
        {
            if (double.IsNaN(bps)) return "N/A";
            double v = Math.Max(0, bps);
            int unit = 0;
            while (v >= 1024 && unit < RateUnits.Length - 1)
            {
                v /= 1024;
                unit++;
            }
            return v.ToString(unit == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + RateUnits[unit];
        }

        public static string Percent(double percent)
        {
            if (double.IsNaN(percent)) return "N/A";
            return Math.Round(percent, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        public static string Gb(double bytes)
        {
            if (double.IsNaN(bytes)) return "N/A";
            return (bytes / OneGb).ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static Level LevelOf(double percent)
        {
            if (double.IsNaN(percent) || percent < 70) return Level.Normal;
            return percent < 90 ? Level.Warn : Level.Critical;
        }

        public static double ClampPercent(double percent)
        {
            if (double.IsNaN(percent)) return double.NaN;
            return Math.Min(100, Math.Max(0, percent));
        }

        public static double ScaleMax(double maxBps)
        {
            if (double.IsNaN(maxBps) || maxBps < OneMb) return OneMb;
            return maxBps;
        }

        public static double MemPercent(double used, double total)
        {
            if (double.IsNaN(used) || double.IsNaN(total) || total <= 0) return double.NaN;
            return used / total * 100;
        }
    }
}
