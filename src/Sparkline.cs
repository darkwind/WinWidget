using System;
using System.Collections.Generic;
using System.Drawing;

namespace WinWidget
{
    public static class Sparkline
    {
        public static PointF[] Points(RingBuffer series, double max, RectangleF area)
        {
            var points = new List<PointF>(series.Count);
            if (double.IsNaN(max) || max <= 0) return points.ToArray();

            float step = area.Width / (series.Capacity - 1);
            for (int i = 0; i < series.Count; i++)
            {
                double v = series[i];
                if (double.IsNaN(v)) continue;
                double ratio = Math.Min(1, Math.Max(0, v / max));
                float x = area.Right - (series.Count - 1 - i) * step;
                float y = area.Bottom - (float)(ratio * area.Height);
                points.Add(new PointF(x, y));
            }
            return points.ToArray();
        }
    }
}
