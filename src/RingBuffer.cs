using System;

namespace WinWidget
{
    public sealed class RingBuffer
    {
        private readonly double[] _items;
        private int _start;
        private int _count;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException("capacity");
            _items = new double[capacity];
        }

        public int Capacity { get { return _items.Length; } }

        public int Count { get { return _count; } }

        public void Push(double value)
        {
            _items[(_start + _count) % _items.Length] = value;
            if (_count < _items.Length) _count++;
            else _start = (_start + 1) % _items.Length;
        }

        // 0 = oldest sample
        public double this[int i]
        {
            get
            {
                if (i < 0 || i >= _count) throw new ArgumentOutOfRangeException("i");
                return _items[(_start + i) % _items.Length];
            }
        }

        public double Max()
        {
            double max = double.NaN;
            for (int i = 0; i < _count; i++)
            {
                double v = this[i];
                if (double.IsNaN(v)) continue;
                if (double.IsNaN(max) || v > max) max = v;
            }
            return max;
        }
    }
}
