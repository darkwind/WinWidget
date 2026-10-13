using System.Drawing;

namespace WinWidget.Tests
{
    public static class SparklineTests
    {
        private static readonly RectangleF Area = new RectangleF(0, 0, 59, 20);

        public static void PointsAreRightAligned()
        {
            var b = new RingBuffer(60);
            b.Push(0); b.Push(100);
            PointF[] pts = Sparkline.Points(b, 100, Area);
            Check.Eq(2, pts.Length);
            Check.Eq(new PointF(58, 20), pts[0]);
            Check.Eq(new PointF(59, 0), pts[1]);
        }

        public static void ValuesAboveMaxAreClampedToTop()
        {
            var b = new RingBuffer(60);
            b.Push(200);
            Check.Eq(0f, Sparkline.Points(b, 100, Area)[0].Y);
        }

        public static void NaNIsSkipped()
        {
            var b = new RingBuffer(60);
            b.Push(1); b.Push(double.NaN); b.Push(1);
            Check.Eq(2, Sparkline.Points(b, 100, Area).Length);
        }

        public static void ZeroMaxGivesNoPoints()
        {
            var b = new RingBuffer(60);
            b.Push(0);
            Check.Eq(0, Sparkline.Points(b, 0, Area).Length);
            Check.Eq(0, Sparkline.Points(b, double.NaN, Area).Length);
        }

        public static void IdleDiskDrawsFlatBottomLine()
        {
            var b = new RingBuffer(60);
            b.Push(0); b.Push(0);
            PointF[] pts = Sparkline.Points(b, Format.ScaleMax(b.Max()), Area);
            Check.Eq(20f, pts[0].Y);
            Check.Eq(20f, pts[1].Y);
        }
    }
}
