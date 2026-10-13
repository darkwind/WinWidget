using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace WinWidget
{
    public struct Snapshot
    {
        public double CpuPercent;
        public double MemUsedBytes;
        public double MemTotalBytes;
        public double DiskReadBps;
        public double DiskWriteBps;
        public double NetRecvBps;
        public double NetSendBps;

        public static Snapshot Empty
        {
            get
            {
                Snapshot s;
                s.CpuPercent = double.NaN;
                s.MemUsedBytes = double.NaN;
                s.MemTotalBytes = double.NaN;
                s.DiskReadBps = double.NaN;
                s.DiskWriteBps = double.NaN;
                s.NetRecvBps = double.NaN;
                s.NetSendBps = double.NaN;
                return s;
            }
        }
    }

    public sealed class MetricsSampler : IDisposable
    {
        private readonly PerformanceCounter _cpu;
        private readonly PerformanceCounter _diskRead;
        private readonly PerformanceCounter _diskWrite;
        private readonly NetworkRate _net = new NetworkRate();
        private readonly Stopwatch _netClock = Stopwatch.StartNew();
        private double _netLastSeconds;

        public MetricsSampler()
        {
            // "% Processor Utility" matches Task Manager; older systems only have "% Processor Time".
            _cpu = TryCreate("Processor Information", "% Processor Utility", "_Total")
                ?? TryCreate("Processor", "% Processor Time", "_Total");
            _diskRead = TryCreate("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
            _diskWrite = TryCreate("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
        }

        public Snapshot Sample()
        {
            Snapshot s = Snapshot.Empty;
            s.CpuPercent = Format.ClampPercent(Read(_cpu));
            s.DiskReadBps = Read(_diskRead);
            s.DiskWriteBps = Read(_diskWrite);

            SampleNetwork(ref s);

            var mem = new MEMORYSTATUSEX();
            mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref mem))
            {
                s.MemTotalBytes = mem.ullTotalPhys;
                s.MemUsedBytes = mem.ullTotalPhys - mem.ullAvailPhys;
            }
            return s;
        }

        private void SampleNetwork(ref Snapshot s)
        {
            long recv = 0, send = 0;
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    IPv4InterfaceStatistics st = nic.GetIPv4Statistics();
                    recv += st.BytesReceived;
                    send += st.BytesSent;
                }
            }
            catch (Exception)
            {
                return;
            }

            double now = _netClock.Elapsed.TotalSeconds;
            _net.Update(recv, send, now - _netLastSeconds);
            _netLastSeconds = now;
            s.NetRecvBps = _net.RecvBps;
            s.NetSendBps = _net.SendBps;
        }

        public void Dispose()
        {
            if (_cpu != null) _cpu.Dispose();
            if (_diskRead != null) _diskRead.Dispose();
            if (_diskWrite != null) _diskWrite.Dispose();
        }

        // The first NextValue() of a rate counter is always 0, so reading it here discards that sample.
        private static PerformanceCounter TryCreate(string category, string counter, string instance)
        {
            try
            {
                var c = new PerformanceCounter(category, counter, instance, true);
                c.NextValue();
                return c;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static double Read(PerformanceCounter counter)
        {
            if (counter == null) return double.NaN;
            try
            {
                return counter.NextValue();
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
    }
}
