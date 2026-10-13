using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WinWidget
{
    // Desktop-pinning behaviour: stays behind other windows, survives Win+D, drag to move, tray menu.
    public sealed partial class WidgetForm
    {
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCAPTION = 2;
        private const int GWLP_HWNDPARENT = -8;
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private static readonly int WM_TASKBARCREATED = RegisterWindowMessage("TaskbarCreated");

        private Settings _settings;
        private NotifyIcon _tray;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _lockItem;
        private ToolStripMenuItem _autoStartItem;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        partial void InitDesktop()
        {
            _settings = Settings.Load(Settings.DefaultPath);
            Rectangle[] areas = Screen.AllScreens.Select(s => s.WorkingArea).ToArray();
            Location = Placement.Resolve(_settings.X, _settings.Y, Size, areas, Screen.PrimaryScreen.WorkingArea);

            _lockItem = new ToolStripMenuItem("위치 잠금");
            _lockItem.Checked = _settings.Locked;
            _lockItem.Click += delegate
            {
                _settings.Locked = !_settings.Locked;
                _lockItem.Checked = _settings.Locked;
                SaveSettings();
            };

            _autoStartItem = new ToolStripMenuItem("Windows 시작 시 실행");
            _autoStartItem.Checked = AutoStart.IsEnabled(AutoStart.RunKey, AutoStart.AppName);
            _autoStartItem.Click += delegate { ToggleAutoStart(); };

            var exitItem = new ToolStripMenuItem("종료");
            exitItem.Click += delegate { Close(); };

            _menu = new ContextMenuStrip();
            _menu.Items.Add(_lockItem);
            _menu.Items.Add(_autoStartItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(exitItem);
            ContextMenuStrip = _menu;

            _tray = new NotifyIcon();
            _tray.Icon = SystemIcons.Application;
            _tray.Text = "WinWidget";
            _tray.ContextMenuStrip = _menu;
            _tray.Visible = true;

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        // Pulls the widget back on screen when the monitor it sat on disappears.
        internal void EnsureOnScreen(Rectangle[] workingAreas, Rectangle primary)
        {
            Location = Placement.Resolve(Location.X, Location.Y, Size, workingAreas, primary);
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            EnsureOnScreen(Screen.AllScreens.Select(s => s.WorkingArea).ToArray(), Screen.PrimaryScreen.WorkingArea);
        }

        // Fallback in case "show desktop" or another tool still hid or minimized us.
        partial void OnTickDesktop()
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            if (!Visible) Show();
        }

        partial void DisposeDesktop()
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            if (_menu != null) _menu.Dispose();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AttachToDesktop();
        }

        // Owning the window by the desktop (Progman) keeps it visible after Win+D.
        private void AttachToDesktop()
        {
            IntPtr progman = FindWindow("Progman", null);
            if (progman != IntPtr.Zero) SetOwner(Handle, progman);
        }

        protected override void WndProc(ref Message m)
        {
            // Explorer restarted: the old Progman is gone, re-attach to the new one.
            if (m.Msg == WM_TASKBARCREATED) AttachToDesktop();

            if (m.Msg == WM_WINDOWPOSCHANGING)
            {
                var pos = (WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(WINDOWPOS));
                pos.hwndInsertAfter = HWND_BOTTOM;
                Marshal.StructureToPtr(pos, m.LParam, false);
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || _settings.Locked) return;

            // Hand the drag to Windows as a caption drag; returns when the mouse is released.
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
            SaveSettings();
        }

        private void SaveSettings()
        {
            _settings.X = Location.X;
            _settings.Y = Location.Y;
            _settings.Save(Settings.DefaultPath);
        }

        private void ToggleAutoStart()
        {
            bool enable = !AutoStart.IsEnabled(AutoStart.RunKey, AutoStart.AppName);
            try
            {
                AutoStart.Set(AutoStart.RunKey, AutoStart.AppName, Application.ExecutablePath, enable);
            }
            catch (Exception ex)
            {
                MessageBox.Show("자동 실행 설정을 변경하지 못했습니다.\n" + ex.Message, "WinWidget",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            _autoStartItem.Checked = AutoStart.IsEnabled(AutoStart.RunKey, AutoStart.AppName);
        }

        private static void SetOwner(IntPtr hwnd, IntPtr owner)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(hwnd, GWLP_HWNDPARENT, owner);
            else SetWindowLong32(hwnd, GWLP_HWNDPARENT, owner.ToInt32());
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterWindowMessage(string name);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
