using System;

namespace WinWidget.Tests
{
    public static class RingBufferTests
    {
        public static void KeepsInsertionOrder()
        {
            var b = new RingBuffer(5);
            b.Push(1); b.Push(2); b.Push(3);
            Check.Eq(3, b.Count);
            Check.Eq(1.0, b[0]);
            Check.Eq(3.0, b[2]);
        }

        public static void DropsOldestWhenFull()
        {
            var b = new RingBuffer(3);
            for (int i = 1; i <= 5; i++) b.Push(i);
            Check.Eq(3, b.Count);
            Check.Eq(3.0, b[0]);
            Check.Eq(5.0, b[2]);
        }

        public static void MaxIgnoresNaN()
        {
            var b = new RingBuffer(4);
            Check.Eq(double.NaN, b.Max());
            b.Push(double.NaN); b.Push(2); b.Push(7); b.Push(double.NaN);
            Check.Eq(7.0, b.Max());
        }

        public static void IndexOutOfRangeThrows()
        {
            var b = new RingBuffer(3);
            b.Push(1);
            bool threw = false;
            try { double unused = b[1]; }
            catch (ArgumentOutOfRangeException) { threw = true; }
            Check.True(threw, "expected ArgumentOutOfRangeException");
        }
    }
}
