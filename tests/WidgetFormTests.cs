using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace WinWidget.Tests
{
    public static class WidgetFormTests
    {
        private const int GWLP_HWNDPARENT = -8;
        private const uint GW_OWNER = 4;

        public static void EnsureOnScreenMovesLostWidgetToDefault()
        {
            using (var form = new WidgetForm())
            {
                var primary = new Rectangle(0, 0, 1920, 1040);
                form.Location = new Point(5000, 100);
                form.EnsureOnScreen(new[] { primary }, primary);
                Check.Eq(new Point(1920 - form.Width - Placement.Margin, Placement.Margin), form.Location);
            }
        }

        public static void TaskbarCreatedReattachesToDesktop()
        {
            using (var form = new WidgetForm())
            {
                IntPtr h = form.Handle;
                SetWindowLongPtr(h, GWLP_HWNDPARENT, IntPtr.Zero);
                Check.Eq(IntPtr.Zero, GetWindow(h, GW_OWNER));

                SendMessage(h, RegisterWindowMessage("TaskbarCreated"), IntPtr.Zero, IntPtr.Zero);
                Check.Eq(FindWindow("Progman", null), GetWindow(h, GW_OWNER));
            }
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterWindowMessage(string name);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
