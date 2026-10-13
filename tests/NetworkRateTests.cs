namespace WinWidget.Tests
{
    public static class NetworkRateTests
    {
        public static void FirstUpdateIsNaN()
        {
            var r = new NetworkRate();
            r.Update(1000, 500, 1.0);
            Check.Eq(double.NaN, r.RecvBps);
            Check.Eq(double.NaN, r.SendBps);
        }

        public static void ComputesBytesPerSecondFromDelta()
        {
            var r = new NetworkRate();
            r.Update(1000, 500, 1.0);
            r.Update(3000, 800, 2.0);
            Check.Eq(1000.0, r.RecvBps);
            Check.Eq(150.0, r.SendBps);
        }

        public static void CounterDecreaseYieldsNaNThenRecovers()
        {
            var r = new NetworkRate();
            r.Update(5000, 5000, 1.0);
            r.Update(100, 100, 1.0);
            Check.Eq(double.NaN, r.RecvBps);
            r.Update(1100, 600, 1.0);
            Check.Eq(1000.0, r.RecvBps);
            Check.Eq(500.0, r.SendBps);
        }

        public static void NonPositiveElapsedYieldsNaN()
        {
            var r = new NetworkRate();
            r.Update(0, 0, 1.0);
            r.Update(100, 100, 0.0);
            Check.Eq(double.NaN, r.RecvBps);
        }

        public static void SnapshotEmptyHasNaNNetwork()
        {
            Check.Eq(double.NaN, Snapshot.Empty.NetRecvBps);
            Check.Eq(double.NaN, Snapshot.Empty.NetSendBps);
        }

        public static void SamplerReportsNonNegativeNetwork()
        {
            using (var sampler = new MetricsSampler())
            {
                sampler.Sample();
                System.Threading.Thread.Sleep(1000);
                Snapshot s = sampler.Sample();
                Check.True(double.IsNaN(s.NetRecvBps) || s.NetRecvBps >= 0, "recv invalid: " + s.NetRecvBps);
                Check.True(double.IsNaN(s.NetSendBps) || s.NetSendBps >= 0, "send invalid: " + s.NetSendBps);
            }
        }
    }
}
