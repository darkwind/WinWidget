using System.Drawing;

namespace WinWidget.Tests
{
    public static class PlacementTests
    {
        private static readonly Size WidgetSize = new Size(240, 164);
        private static readonly Rectangle Primary = new Rectangle(0, 0, 1920, 1040);
        private static readonly Rectangle Secondary = new Rectangle(1920, 0, 1920, 1040);

        public static void NoSavedPositionUsesTopRight()
        {
            Point p = Placement.Resolve(null, null, WidgetSize, new[] { Primary }, Primary);
            Check.Eq(new Point(1920 - 240 - 16, 16), p);
        }

        public static void VisiblePositionIsKept()
        {
            Point p = Placement.Resolve(100, 200, WidgetSize, new[] { Primary }, Primary);
            Check.Eq(new Point(100, 200), p);
        }

        public static void OffScreenPositionFallsBack()
        {
            Point p = Placement.Resolve(2500, 100, WidgetSize, new[] { Primary }, Primary);
            Check.Eq(new Point(1664, 16), p);
        }

        public static void MostlyHiddenPositionFallsBack()
        {
            // only 40px of 240px width visible
            Point p = Placement.Resolve(1880, 100, WidgetSize, new[] { Primary }, Primary);
            Check.Eq(new Point(1664, 16), p);
        }

        public static void PositionOnSecondMonitorIsKept()
        {
            Point p = Placement.Resolve(2500, 100, WidgetSize, new[] { Primary, Secondary }, Primary);
            Check.Eq(new Point(2500, 100), p);
        }
    }
}
