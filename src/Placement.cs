using System.Drawing;

namespace WinWidget
{
    public static class Placement
    {
        public const int Margin = 16;

        public static Point Resolve(int? x, int? y, Size size, Rectangle[] workingAreas, Rectangle primary)
        {
            if (x.HasValue && y.HasValue)
            {
                var rect = new Rectangle(x.Value, y.Value, size.Width, size.Height);
                foreach (Rectangle area in workingAreas)
                {
                    Rectangle hit = Rectangle.Intersect(area, rect);
                    if (hit.Width * 2 >= size.Width && hit.Height * 2 >= size.Height) return rect.Location;
                }
            }
            return new Point(primary.Right - size.Width - Margin, primary.Top + Margin);
        }
    }
}
