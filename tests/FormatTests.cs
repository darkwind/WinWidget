namespace WinWidget.Tests
{
    public static class FormatTests
    {
        public static void RateScalesUnits()
        {
            Check.Eq("0 B/s", Format.Rate(0));
            Check.Eq("512 B/s", Format.Rate(512));
            Check.Eq("1.5 KB/s", Format.Rate(1536));
            Check.Eq("12.4 MB/s", Format.Rate(12.4 * 1048576));
            Check.Eq("2.0 GB/s", Format.Rate(2.0 * 1073741824));
            Check.Eq("0 B/s", Format.Rate(-5));
            Check.Eq("N/A", Format.Rate(double.NaN));
        }

        public static void PercentRounds()
        {
            Check.Eq("23%", Format.Percent(23.4));
            Check.Eq("24%", Format.Percent(23.6));
            Check.Eq("N/A", Format.Percent(double.NaN));
        }

        public static void GbOneDecimal()
        {
            Check.Eq("9.8", Format.Gb(9.8 * 1073741824));
            Check.Eq("N/A", Format.Gb(double.NaN));
        }

        public static void LevelThresholds()
        {
            Check.Eq(Level.Normal, Format.LevelOf(69.9));
            Check.Eq(Level.Warn, Format.LevelOf(70));
            Check.Eq(Level.Warn, Format.LevelOf(89.9));
            Check.Eq(Level.Critical, Format.LevelOf(90));
            Check.Eq(Level.Normal, Format.LevelOf(double.NaN));
        }

        public static void ClampPercentBounds()
        {
            Check.Eq(100.0, Format.ClampPercent(105));
            Check.Eq(0.0, Format.ClampPercent(-1));
            Check.Eq(42.0, Format.ClampPercent(42));
            Check.Eq(double.NaN, Format.ClampPercent(double.NaN));
        }

        public static void ScaleMaxHasOneMbFloor()
        {
            Check.Eq(1048576.0, Format.ScaleMax(0));
            Check.Eq(1048576.0, Format.ScaleMax(double.NaN));
            Check.Eq(5242880.0, Format.ScaleMax(5242880));
        }

        public static void MemPercentHandlesZeroTotal()
        {
            Check.Eq(25.0, Format.MemPercent(8, 32));
            Check.Eq(double.NaN, Format.MemPercent(8, 0));
            Check.Eq(double.NaN, Format.MemPercent(double.NaN, 32));
        }
    }
}
