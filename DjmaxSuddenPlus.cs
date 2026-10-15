using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DjmaxSuddenPlus
{
    internal sealed class Settings
    {
        public int Monitor = 0;
        public int X = -1;
        public int Width = 520;
        public int Height = 180;
        public int LaneStart = 0;
        public int JudgeY = -1;
        public decimal Speed = 5.0m;
        // FHD DJMAX measurements: 754 px gear / 560 px per 120 BPM bar (2 s).
        public int TravelMs = 2693;
        public bool Visible = true;
        public bool ShowNumber = true;
        public bool Dimmed = false;

        public static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DjmaxSuddenPlus", "settings.ini"); }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            if (!File.Exists(FilePath)) return s;
            foreach (string line in File.ReadAllLines(FilePath))
            {
                int n = line.IndexOf('=');
                if (n < 1) continue;
                string key = line.Substring(0, n), value = line.Substring(n + 1);
                int i; decimal d; bool b;
                if (key == "Monitor" && int.TryParse(value, out i)) s.Monitor = i;
                if (key == "X" && int.TryParse(value, out i)) s.X = i;
                if (key == "Width" && int.TryParse(value, out i)) s.Width = i;
                if (key == "Height" && int.TryParse(value, out i)) s.Height = i;
                if (key == "LaneStart" && int.TryParse(value, out i)) s.LaneStart = i;
                if (key == "JudgeY" && int.TryParse(value, out i)) s.JudgeY = i;
                if (key == "Speed" && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out d)) s.Speed = d;
                if (key == "TravelMs" && int.TryParse(value, out i)) s.TravelMs = i;
                if (key == "Visible" && bool.TryParse(value, out b)) s.Visible = b;
                if (key == "ShowNumber" && bool.TryParse(value, out b)) s.ShowNumber = b;
                if (key == "Dimmed" && bool.TryParse(value, out b)) s.Dimmed = b;
            }
            return s;
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllLines(FilePath, new string[] {
                "Monitor=" + Monitor, "X=" + X, "Width=" + Width, "Height=" + Height,
                "LaneStart=" + LaneStart, "JudgeY=" + JudgeY,
                "Speed=" + Speed.ToString(CultureInfo.InvariantCulture), "TravelMs=" + TravelMs,
                "Visible=" + Visible, "ShowNumber=" + ShowNumber, "Dimmed=" + Dimmed
            });
        }
    }

    internal sealed class CoverForm : Form
    {
        const int WS_EX_NOACTIVATE = 0x08000000;
        const int SWP_NOMOVE = 0x2, SWP_NOSIZE = 0x1, SWP_NOACTIVATE = 0x10;
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        readonly Settings settings;
        public Action Changed;
        bool dragging;
        bool resizing;
        Point dragScreen;
        int dragHeight;
        int dragX;

        public CoverForm(Settings settings)
        {
            this.settings = settings;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(10, 13, 19);
            Opacity = settings.Dimmed ? 0.2 : 1.0;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += delegate { dragging = false; Capture = false; };
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p = base.CreateParams;
                p.ExStyle |= WS_EX_NOACTIVATE;
                return p;
            }
        }

        public void ApplyBounds()
        {
            Screen[] screens = Screen.AllScreens;
            int index = Math.Max(0, Math.Min(settings.Monitor, screens.Length - 1));
            Rectangle b = screens[index].Bounds;
            int width = Math.Max(40, Math.Min(settings.Width, b.Width));
            int x = settings.X < 0 ? (b.Width - width) / 2 : Math.Max(0, Math.Min(settings.X, b.Width - width));
            Bounds = new Rectangle(b.Left + x, b.Top, width, Math.Max(20, Math.Min(settings.Height, b.Height)));
            Invalidate();
        }

        public void KeepAbove()
        {
            if (Visible && IsHandleCreated)
                SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(21, 29, 40)))
                g.FillRectangle(brush, ClientRectangle);
            using (SolidBrush brush = new SolidBrush(Color.DeepSkyBlue))
                g.FillRectangle(brush, 0, Height - 8, Width, 8);
            if (settings.ShowNumber)
            {
                Screen screen = Screen.AllScreens[Math.Max(0, Math.Min(settings.Monitor, Screen.AllScreens.Length - 1))];
                int judge = settings.JudgeY < 0 ? screen.Bounds.Height - 120 : settings.JudgeY;
                double distance = Math.Max(1, judge - settings.LaneStart);
                double visible = Math.Max(0, Math.Min(1, (judge - settings.Height) / distance));
                int green = (int)Math.Round(settings.TravelMs * visible / (double)settings.Speed * 0.6);
                string label = "GREEN " + green;
                using (Font font = new Font("Segoe UI", 13, FontStyle.Bold))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(120, 245, 157)))
                {
                    SizeF size = g.MeasureString(label, font);
                    g.DrawString(label, font, brush, (Width - size.Width) / 2, Math.Max(3, Height - size.Height - 10));
                }
            }
        }

        void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                settings.Dimmed = !settings.Dimmed;
                Opacity = settings.Dimmed ? 0.2 : 1.0;
                if (Changed != null) Changed();
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            resizing = e.Y >= Height - 24;
            dragScreen = Cursor.Position;
            dragHeight = settings.Height;
            Screen screen = Screen.AllScreens[Math.Max(0, Math.Min(settings.Monitor, Screen.AllScreens.Length - 1))];
            dragX = Left - screen.Bounds.Left;
            Capture = true;
        }
        void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging)
            {
                Cursor = e.Y >= Height - 24 ? Cursors.SizeNS : Cursors.SizeWE;
                return;
            }
            Screen screen = Screen.AllScreens[Math.Max(0, Math.Min(settings.Monitor, Screen.AllScreens.Length - 1))];
            if (resizing)
                settings.Height = Math.Max(20, Math.Min(screen.Bounds.Height, dragHeight + Cursor.Position.Y - dragScreen.Y));
            else
                settings.X = Math.Max(0, Math.Min(screen.Bounds.Width - Math.Min(settings.Width, screen.Bounds.Width), dragX + Cursor.Position.X - dragScreen.X));
            ApplyBounds();
            if (Changed != null) Changed();
        }
    }

    internal sealed class ControlForm : Form
    {
        const int MOD_CONTROL = 0x2, MOD_ALT = 0x1, WM_HOTKEY = 0x312;
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        readonly Settings s;
        readonly CoverForm cover;
        ComboBox monitor;
        NumericUpDown x, width, height, judge, start, speed, travel;
        CheckBox showNumber;
        Label result;
        bool updating;

        public ControlForm()
        {
            s = Settings.Load();
            Text = "DJMAX Sudden+";
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            ShowInTaskbar = true;
            ClientSize = new Size(450, 495);
            Font = new Font("Yu Gothic UI", 9);
            cover = new CoverForm(s);
            cover.Changed = delegate { SyncFromSettings(); SaveAndRefresh(); };
            Timer zOrderTimer = new Timer { Interval = 2000 };
            zOrderTimer.Tick += delegate { cover.KeepAbove(); if (WindowState != FormWindowState.Minimized) BringToFront(); };
            zOrderTimer.Start();
            BuildUi();
            SyncFromSettings();
            Shown += delegate { cover.ApplyBounds(); cover.Show(); cover.Visible = s.Visible; TopMost = true; BringToFront(); };
            FormClosed += delegate { try { s.Save(); } catch { } cover.Close(); };
        }

        NumericUpDown AddNumber(string title, int row, int min, int max, decimal increment, int decimalPlaces)
        {
            Label label = new Label { Text = title, AutoSize = false, Location = new Point(16, 17 + row * 34), Size = new Size(225, 27) };
            NumericUpDown box = new NumericUpDown { Location = new Point(253, 12 + row * 34), Size = new Size(170, 27), Minimum = min, Maximum = max, Increment = increment, DecimalPlaces = decimalPlaces };
            box.ValueChanged += delegate { if (!updating) ReadInputs(); };
            Controls.Add(label); Controls.Add(box);
            return box;
        }

        void BuildUi()
        {
            Label monitorLabel = new Label { Text = "表示モニター", Location = new Point(16, 17), Size = new Size(225, 27) };
            monitor = new ComboBox { Location = new Point(253, 12), Size = new Size(170, 27), DropDownStyle = ComboBoxStyle.DropDownList };
            for (int i = 0; i < Screen.AllScreens.Length; i++) monitor.Items.Add((i + 1) + ": " + Screen.AllScreens[i].Bounds.Width + " × " + Screen.AllScreens[i].Bounds.Height);
            monitor.SelectedIndexChanged += delegate { if (!updating) ReadInputs(); };
            Controls.Add(monitorLabel); Controls.Add(monitor);
            x = AddNumber("左位置（モニター内 px）", 1, 0, 10000, 1, 0);
            width = AddNumber("カバー幅（px）", 2, 40, 10000, 1, 0);
            height = AddNumber("カバー下辺の高さ（px）", 3, 20, 10000, 1, 0);
            speed = AddNumber("DJMAX ハイスピード", 4, 1, 9, 0.1m, 1);
            start = AddNumber("ノーツ出現位置 Y（px）", 5, 0, 10000, 1, 0);
            judge = AddNumber("判定ライン Y（px）", 6, 1, 10000, 1, 0);
            travel = AddNumber("1.0x 全長落下時間（ms）", 7, 100, 20000, 10, 0);
            showNumber = new CheckBox { Text = "カバーに緑数字を表示", Location = new Point(16, 290), Size = new Size(220, 30) };
            showNumber.CheckedChanged += delegate { if (!updating) ReadInputs(); };
            Controls.Add(showNumber);
            result = new Label { Location = new Point(16, 327), Size = new Size(415, 43), Font = new Font("Yu Gothic UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(30, 125, 70) };
            Controls.Add(result);
            Label dragHelp = new Label { Text = "カバー本体を左右ドラッグ：横移動\nカバー下辺を上下ドラッグ：高さ調整\nカバーを右クリック：不透明度 100% / 20% 切替", Location = new Point(16, 377), Size = new Size(425, 63) };
            Controls.Add(dragHelp);
            Label help = new Label { Text = "Ctrl+Alt+F7: 設定画面を前面に表示　Ctrl+Alt+F8: カバー表示切替\nCtrl+Alt+↑/↓: 高さ ±5px　←/→: HS ±0.1", Location = new Point(16, 443), Size = new Size(425, 44) };
            Controls.Add(help);
        }

        void SyncFromSettings()
        {
            updating = true;
            int screenIndex = Math.Max(0, Math.Min(s.Monitor, Screen.AllScreens.Length - 1));
            s.Monitor = screenIndex;
            Screen screen = Screen.AllScreens[screenIndex];
            monitor.SelectedIndex = screenIndex;
            x.Maximum = Math.Max(0, screen.Bounds.Width);
            width.Maximum = screen.Bounds.Width;
            height.Maximum = screen.Bounds.Height;
            judge.Maximum = screen.Bounds.Height;
            start.Maximum = screen.Bounds.Height;
            x.Value = Math.Max(x.Minimum, Math.Min(x.Maximum, s.X < 0 ? (screen.Bounds.Width - s.Width) / 2 : s.X));
            width.Value = Math.Max(width.Minimum, Math.Min(width.Maximum, s.Width));
            height.Value = Math.Max(height.Minimum, Math.Min(height.Maximum, s.Height));
            speed.Value = Math.Max(speed.Minimum, Math.Min(speed.Maximum, s.Speed));
            start.Value = Math.Max(start.Minimum, Math.Min(start.Maximum, s.LaneStart));
            judge.Value = Math.Max(judge.Minimum, Math.Min(judge.Maximum, s.JudgeY < 0 ? screen.Bounds.Height - 120 : s.JudgeY));
            travel.Value = Math.Max(travel.Minimum, Math.Min(travel.Maximum, s.TravelMs));
            showNumber.Checked = s.ShowNumber;
            updating = false;
            UpdateResult();
        }

        void ReadInputs()
        {
            s.Monitor = monitor.SelectedIndex < 0 ? 0 : monitor.SelectedIndex;
            s.X = (int)x.Value;
            s.Width = (int)width.Value;
            s.Height = (int)height.Value;
            s.Speed = speed.Value;
            s.LaneStart = (int)start.Value;
            s.JudgeY = (int)judge.Value;
            s.TravelMs = (int)travel.Value;
            s.ShowNumber = showNumber.Checked;
            SaveAndRefresh();
        }

        void SaveAndRefresh()
        {
            cover.ApplyBounds();
            UpdateResult();
            s.Save();
        }

        void UpdateResult()
        {
            double distance = Math.Max(1, s.JudgeY - s.LaneStart);
            double visible = Math.Max(0, Math.Min(1, (s.JudgeY - s.Height) / distance));
            double milliseconds = s.TravelMs * visible / (double)s.Speed;
            result.Text = "緑数字（推定）: " + Math.Round(milliseconds * 0.6) + "    /    " + Math.Round(milliseconds) + " ms";
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            uint mods = MOD_CONTROL | MOD_ALT;
            RegisterHotKey(Handle, 1, mods, (uint)Keys.F7);
            RegisterHotKey(Handle, 2, mods, (uint)Keys.F8);
            RegisterHotKey(Handle, 3, mods, (uint)Keys.Up);
            RegisterHotKey(Handle, 4, mods, (uint)Keys.Down);
            RegisterHotKey(Handle, 5, mods, (uint)Keys.Left);
            RegisterHotKey(Handle, 6, mods, (uint)Keys.Right);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            for (int i = 1; i <= 6; i++) UnregisterHotKey(Handle, i);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == 1) { if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Show(); Activate(); BringToFront(); }
                if (id == 2) { s.Visible = !s.Visible; cover.Visible = s.Visible; s.Save(); }
                if (id == 3 || id == 4) { s.Height = Math.Max(20, Math.Min(Screen.AllScreens[s.Monitor].Bounds.Height, s.Height + (id == 3 ? -5 : 5))); SyncFromSettings(); SaveAndRefresh(); }
                if (id == 5 || id == 6) { s.Speed = Math.Max(1m, Math.Min(9m, s.Speed + (id == 5 ? -0.1m : 0.1m))); SyncFromSettings(); SaveAndRefresh(); }
            }
            base.WndProc(ref m);
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ControlForm());
        }
    }
}
