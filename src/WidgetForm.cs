using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace WinWidget
{
    public sealed partial class WidgetForm : Form
    {
        private const int History = 60;
        private static readonly Color BgColor = Color.FromArgb(26, 32, 44);
        private static readonly Color TrackColor = Color.FromArgb(18, 255, 255, 255);
        private static readonly Color LabelColor = Color.FromArgb(160, 174, 192);
        private static readonly Color ValueColor = Color.FromArgb(237, 242, 247);
        private static readonly Color NormalColor = Color.FromArgb(79, 209, 197);
        private static readonly Color WarnColor = Color.FromArgb(246, 173, 85);
        private static readonly Color CriticalColor = Color.FromArgb(245, 101, 101);
        private static readonly Color ReadColor = Color.FromArgb(99, 179, 237);
        private static readonly Color WriteColor = Color.FromArgb(183, 148, 244);

        private readonly MetricsSampler _sampler = new MetricsSampler();
        private readonly RingBuffer _cpu = new RingBuffer(History);
        private readonly RingBuffer _mem = new RingBuffer(History);
        private readonly RingBuffer _read = new RingBuffer(History);
        private readonly RingBuffer _write = new RingBuffer(History);
        private readonly Timer _timer = new Timer();
        private readonly Font _labelFont = new Font("Segoe UI", 8.5f);
        private readonly Font _valueFont = new Font("Segoe UI Semibold", 9.5f);
        private readonly float _scale;
        private Snapshot _last = Snapshot.Empty;

        public WidgetForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = BgColor;
            Opacity = 0.88;
            DoubleBuffered = true;
            Text = "WinWidget";

            using (Graphics g = CreateGraphics()) _scale = g.DpiX / 96f;
            ClientSize = new Size(S(240), S(164));
            using (GraphicsPath path = RoundedRect(new Rectangle(Point.Empty, ClientSize), S(10)))
                Region = new Region(path);

            Rectangle primary = Screen.PrimaryScreen.WorkingArea;
            Location = Placement.Resolve(null, null, Size, new[] { primary }, primary);

            InitDesktop();

            _timer.Interval = 1000;
            _timer.Tick += OnTick;
            _timer.Start();
        }

        partial void InitDesktop();
        partial void OnTickDesktop();
        partial void DisposeDesktop();

        internal void Push(Snapshot s)
        {
            _last = s;
            _cpu.Push(s.CpuPercent);
            _mem.Push(Format.MemPercent(s.MemUsedBytes, s.MemTotalBytes));
            _read.Push(s.DiskReadBps);
            _write.Push(s.DiskWriteBps);
            Invalidate();
        }

        private void OnTick(object sender, EventArgs e)
        {
            Push(_sampler.Sample());
            OnTickDesktop();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            float pad = S(12);
            float rowHeight = S(48);
            float width = ClientSize.Width - 2 * pad;
            double memPercent = Format.MemPercent(_last.MemUsedBytes, _last.MemTotalBytes);

            DrawRow(g, pad, pad, width, "CPU",
                Format.Percent(_last.CpuPercent),
                ColorFor(_last.CpuPercent), _cpu, 100, null, Color.Empty);

            DrawRow(g, pad, pad + rowHeight, width, "MEM",
                Format.Gb(_last.MemUsedBytes) + "/" + Format.Gb(_last.MemTotalBytes) + "GB  " + Format.Percent(memPercent),
                ColorFor(memPercent), _mem, 100, null, Color.Empty);

            double diskMax = Format.ScaleMax(MaxOf(_read.Max(), _write.Max()));
            DrawRow(g, pad, pad + 2 * rowHeight, width, "DISK",
                "R " + Format.Rate(_last.DiskReadBps) + "   W " + Format.Rate(_last.DiskWriteBps),
                ReadColor, _read, diskMax, _write, WriteColor);
        }

        private void DrawRow(Graphics g, float x, float y, float width, string label, string value,
            Color color, RingBuffer series, double max, RingBuffer series2, Color color2)
        {
            using (var labelBrush = new SolidBrush(LabelColor))
                g.DrawString(label, _labelFont, labelBrush, x, y);

            using (var valueBrush = new SolidBrush(ValueColor))
            using (var format = new StringFormat())
            {
                format.Alignment = StringAlignment.Far;
                g.DrawString(value, _valueFont, valueBrush, new RectangleF(x, y - S(1), width, S(20)), format);
            }

            var area = new RectangleF(x, y + S(20), width, S(20));
            using (var track = new SolidBrush(TrackColor))
                g.FillRectangle(track, area);

            DrawSeries(g, series, max, area, color, true);
            if (series2 != null) DrawSeries(g, series2, max, area, color2, false);
        }

        private void DrawSeries(Graphics g, RingBuffer series, double max, RectangleF area, Color color, bool fill)
        {
            PointF[] points = Sparkline.Points(series, max, area);
            if (points.Length < 2) return;

            if (fill)
            {
                var polygon = new PointF[points.Length + 2];
                points.CopyTo(polygon, 0);
                polygon[points.Length] = new PointF(points[points.Length - 1].X, area.Bottom);
                polygon[points.Length + 1] = new PointF(points[0].X, area.Bottom);
                using (var brush = new SolidBrush(Color.FromArgb(60, color)))
                    g.FillPolygon(brush, polygon);
            }

            using (var pen = new Pen(color, 1.5f * _scale))
            {
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, points);
            }
        }

        private static Color ColorFor(double percent)
        {
            switch (Format.LevelOf(percent))
            {
                case Level.Critical: return CriticalColor;
                case Level.Warn: return WarnColor;
                default: return NormalColor;
            }
        }

        private static double MaxOf(double a, double b)
        {
            if (double.IsNaN(a)) return b;
            if (double.IsNaN(b)) return a;
            return Math.Max(a, b);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private int S(float value)
        {
            return (int)Math.Round(value * _scale);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _sampler.Dispose();
                _labelFont.Dispose();
                _valueFont.Dispose();
                DisposeDesktop();
            }
            base.Dispose(disposing);
        }
    }
}
