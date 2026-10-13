using System;
using System.Linq;
using System.Reflection;

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
            Console.WriteLine("dump not implemented");
            return 1;
        }

        public static int Render(string path)
        {
            Console.WriteLine("render not implemented");
            return 1;
        }
    }
}
