namespace WinWidget
{
    // Turns cumulative byte totals into bytes/sec. Any non-monotonic step (adapter set changed) is NaN.
    public sealed class NetworkRate
    {
        private bool _hasBase;
        private long _recv;
        private long _send;

        public double RecvBps = double.NaN;
        public double SendBps = double.NaN;

        public void Update(long recvTotal, long sendTotal, double elapsedSeconds)
        {
            RecvBps = double.NaN;
            SendBps = double.NaN;
            if (_hasBase && elapsedSeconds > 0 && recvTotal >= _recv && sendTotal >= _send)
            {
                RecvBps = (recvTotal - _recv) / elapsedSeconds;
                SendBps = (sendTotal - _send) / elapsedSeconds;
            }
            _hasBase = true;
            _recv = recvTotal;
            _send = sendTotal;
        }
    }
}
