using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace WinWidget.Tests
{
    public static class Check
    {
        public static void Eq(object expected, object actual)
        {
            if (!Equals(expected, actual))
                throw new Exception("expected <" + expected + "> but was <" + actual + ">");
        }

        public static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }

    static class TestRunner
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "dump") return Modes.Dump();
            if (args.Length > 1 && args[0] == "render") return Modes.Render(args[1]);

            int failed = 0, passed = 0;
            var methods = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.Name.EndsWith("Tests"))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.GetParameters().Length == 0 && m.ReturnType == typeof(void))
                .OrderBy(m => m.DeclaringType.Name + "." + m.Name);

            foreach (MethodInfo m in methods)
            {
                string name = m.DeclaringType.Name + "." + m.Name;
                try
                {
                    m.Invoke(null, null);
                    passed++;
                    Console.WriteLine("PASS " + name);
                }
                catch (TargetInvocationException ex)
                {
                    failed++;
                    Console.WriteLine("FAIL " + name + ": " + ex.InnerException.Message);
                }
            }

            Console.WriteLine(failed == 0 ? "ALL PASSED (" + passed + ")" : failed + " FAILED, " + passed + " passed");
            return failed == 0 ? 0 : 1;
        }
    }

    static partial class Modes
    {
        public static int Dump()
        {
            using (var sampler = new MetricsSampler())
            {
                for (int i = 0; i < 5; i++)
                {
                    Thread.Sleep(1000);
                    Snapshot s = sampler.Sample();
                    Console.WriteLine(string.Format("CPU {0,5}  MEM {1}/{2}GB {3,4}  R {4,11}  W {5,11}",
                        Format.Percent(s.CpuPercent),
                        Format.Gb(s.MemUsedBytes), Format.Gb(s.MemTotalBytes),
                        Format.Percent(Format.MemPercent(s.MemUsedBytes, s.MemTotalBytes)),
                        Format.Rate(s.DiskReadBps), Format.Rate(s.DiskWriteBps)));
                }
            }
            return 0;
        }

        // Feeds 60 synthetic samples (CPU ramps into warn/critical, disk bursts) and saves the widget as PNG.
        public static int Render(string path)
        {
            using (var form = new WidgetForm())
            {
                for (int i = 0; i < 60; i++)
                {
                    Snapshot s = Snapshot.Empty;
                    s.CpuPercent = 20 + 75.0 * i / 59;
                    s.MemTotalBytes = 32.0 * 1073741824;
                    s.MemUsedBytes = (10 + 3 * Math.Sin(i / 6.0)) * 1073741824;
                    s.DiskReadBps = (i % 15 == 0 ? 40 : 2) * Format.OneMb;
                    s.DiskWriteBps = (i % 20 == 5 ? 25 : 0.5) * Format.OneMb;
                    form.Push(s);
                }
                using (var bmp = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));
                    bmp.Save(path, ImageFormat.Png);
                }
            }
            Console.WriteLine("rendered " + path);
            return 0;
        }
    }
}
