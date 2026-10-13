using System.Threading;

namespace WinWidget.Tests
{
    public static class MetricsSamplerTests
    {
        public static void EmptySnapshotIsAllNaN()
        {
            Snapshot s = Snapshot.Empty;
            Check.Eq(double.NaN, s.CpuPercent);
            Check.Eq(double.NaN, s.MemUsedBytes);
            Check.Eq(double.NaN, s.MemTotalBytes);
            Check.Eq(double.NaN, s.DiskReadBps);
            Check.Eq(double.NaN, s.DiskWriteBps);
        }

        public static void SampleReturnsPlausibleValues()
        {
            using (var sampler = new MetricsSampler())
            {
                Thread.Sleep(1000);
                Snapshot s = sampler.Sample();
                Check.True(s.CpuPercent >= 0 && s.CpuPercent <= 100, "cpu out of range: " + s.CpuPercent);
                Check.True(s.MemTotalBytes > Format.OneMb * 1024, "mem total too small: " + s.MemTotalBytes);
                Check.True(s.MemUsedBytes > 0 && s.MemUsedBytes <= s.MemTotalBytes, "mem used invalid: " + s.MemUsedBytes);
                Check.True(s.DiskReadBps >= 0, "disk read invalid: " + s.DiskReadBps);
                Check.True(s.DiskWriteBps >= 0, "disk write invalid: " + s.DiskWriteBps);
            }
        }
    }
}
