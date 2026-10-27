using System;
using System.Drawing;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public sealed class PomodoroForm : Form
{
    readonly TimerEngine engine = new TimerEngine();
    readonly Timer timer = new Timer();
    readonly TimerDisplay clock = new TimerDisplay();
    readonly Label status = new Label(), count = new Label();
    readonly Button start = new Button();
    readonly Button[] modes = new Button[3];
    readonly CheckBox auto = new CheckBox(), sound = new CheckBox(), pin = new CheckBox();
    readonly ProgressBar progress = new ProgressBar();
    readonly NotifyIcon tray = new NotifyIcon();
    readonly Color bg = Color.FromArgb(24, 28, 38), panel = Color.FromArgb(40, 46, 60), accent = Color.FromArgb(250, 115, 99);
    readonly Icon tomato;
    bool compact, dragging;
    Point dragOrigin, windowOrigin;
    Size fullSize;
    Rectangle fullClockBounds;
    Font fullClockFont;
    readonly Font compactFont = new Font("Segoe UI", 38, FontStyle.Regular);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

    public PomodoroForm()
    {
        Text = "番茄时钟"; ClientSize = new Size(420, 530);
        BackColor = bg; ForeColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 10);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
        tomato = MakeIcon(); Icon = tomato;
        Label title = LabelAt("番茄时钟", 26, 22, 360, 38, 21);
        LabelAt("一次只做一件事，让专注更轻松。", 28, 65, 365, 28, 10).ForeColor = Color.LightGray;
        string[] names = { "专注 25 分钟", "短休 5 分钟", "长休 15 分钟" };
        for (int i = 0; i < 3; i++) {
            int phase = i;
            modes[i] = ButtonAt(names[i], 26 + i * 124, 110, 118, 36);
            modes[i].Click += delegate {
                if ((engine.Running || engine.Remaining < TimerEngine.Duration(engine.Phase)) &&
                    MessageBox.Show(this, "切换阶段会清除当前倒计时进度，继续吗？", "切换阶段", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                engine.Select((Phase)phase); RefreshView();
            };
        }
        clock.SetBounds(20, 163, 380, 88); clock.Font = new Font("Segoe UI", 52, FontStyle.Bold);
        Controls.Add(clock);
        status.SetBounds(20, 258, 380, 28); status.TextAlign = ContentAlignment.MiddleCenter;
        status.ForeColor = Color.LightGray; Controls.Add(status);
        progress.SetBounds(28, 299, 364, 6); progress.Maximum = 1500; Controls.Add(progress);
        start.SetBounds(28, 326, 238, 44); Style(start); Controls.Add(start);
        start.Click += delegate {
            ToggleTimer();
        };
        Button reset = ButtonAt("重置", 278, 326, 114, 44);
        reset.Click += delegate { engine.Reset(); RefreshView(); };
        count.SetBounds(26, 389, 368, 26); count.TextAlign = ContentAlignment.MiddleCenter; Controls.Add(count);
        SetupCheck(pin, "窗口置顶", 28, 432, 100, false);
        SetupCheck(sound, "声音提醒", 143, 432, 100, true);
        SetupCheck(auto, "自动下一阶段", 255, 432, 140, false);
        LabelAt("数字变化", 28, 479, 90, 28, 10);
        ComboBox displayMode = new ComboBox(); displayMode.DropDownStyle = ComboBoxStyle.DropDownList;
        displayMode.SetBounds(132, 476, 260, 30); displayMode.Items.AddRange(new object[] { "正常倒计时", "单个数字翻页" }); displayMode.SelectedIndex = 0;
        displayMode.SelectedIndexChanged += delegate { clock.Flip = displayMode.SelectedIndex == 1; }; Controls.Add(displayMode);
        pin.CheckedChanged += delegate { TopMost = pin.Checked; };
        tray.Icon = tomato; tray.Text = "番茄时钟"; tray.Visible = true;
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("显示番茄时钟", null, delegate { RestoreWindow(); });
        menu.Items.Add("设置 / 完整界面", null, delegate { SetCompact(false); RestoreWindow(); });
        menu.Items.Add("退出", null, delegate { Close(); }); tray.ContextMenuStrip = menu;
        ContextMenuStrip compactMenu = new ContextMenuStrip();
        compactMenu.Items.Add("暂停 / 继续（空格）", null, delegate { ToggleTimer(); });
        compactMenu.Items.Add("设置 / 完整界面（双击）", null, delegate { SetCompact(false); });
        compactMenu.Items.Add("退出", null, delegate { Close(); });
        clock.ContextMenuStrip = compactMenu;
        clock.DoubleClick += delegate { SetCompact(!compact); };
        DoubleClick += delegate { if (!compact) SetCompact(true); };
        foreach (Control control in Controls) {
            // Text and empty areas toggle the view; settings controls keep their usual actions.
            if (control is Label) control.DoubleClick += delegate { if (!compact) SetCompact(true); };
        }
        clock.MouseDown += delegate(object sender, MouseEventArgs e) {
            if (compact && e.Button == MouseButtons.Left) { dragging = true; dragOrigin = Cursor.Position; windowOrigin = Location; clock.Capture = true; }
        };
        clock.MouseMove += delegate { if (dragging) Location = new Point(windowOrigin.X + Cursor.Position.X - dragOrigin.X, windowOrigin.Y + Cursor.Position.Y - dragOrigin.Y); };
        clock.MouseUp += delegate { dragging = false; clock.Capture = false; };
        KeyPreview = true;
        KeyDown += delegate(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Escape && compact) { SetCompact(false); e.Handled = e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Space && compact) { ToggleTimer(); e.Handled = e.SuppressKeyPress = true; }
        };
        tray.DoubleClick += delegate { RestoreWindow(); };
        tray.BalloonTipClicked += delegate { RestoreWindow(); };
        Resize += delegate { if (WindowState == FormWindowState.Minimized) { Hide(); tray.ShowBalloonTip(2000, "番茄时钟仍在运行", "双击托盘中的番茄图标可恢复窗口。", ToolTipIcon.Info); } };
        timer.Interval = 200; timer.Tick += delegate { Advance(DateTime.UtcNow); RefreshView(); }; timer.Start();
        FormClosed += delegate { timer.Stop(); timer.Dispose(); tray.Visible = false; tray.Dispose(); tomato.Dispose(); compactFont.Dispose(); compactMenu.Dispose(); };
        RefreshView();
    }
    void ToggleTimer()
    {
        DateTime now = DateTime.UtcNow;
        Advance(now);
        if (engine.Running) engine.Pause(now);
        else { engine.Start(now); SetCompact(true); }
        RefreshView();
    }
    void SetCompact(bool value)
    {
        if (compact == value) return;
        SuspendLayout();
        Point center = new Point(Left + Width / 2, Top + Height / 2);
        if (value) {
            fullSize = ClientSize; fullClockBounds = clock.Bounds; fullClockFont = clock.Font;
            compact = true;
            foreach (Control control in Controls) control.Visible = control == clock;
            FormBorderStyle = FormBorderStyle.None;
            float scale = DeviceScale();
            ClientSize = new Size((int)(220 * scale), (int)(100 * scale));
            clock.Compact = true; clock.BackColor = Color.FromArgb(40,39,36); BackColor = clock.BackColor;
            clock.Font = compactFont; clock.Bounds = ClientRectangle;
            using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath()) {
                int d = (int)(18 * scale);
                path.AddArc(0, 0, d, d, 180, 90); path.AddArc(Width-d, 0, d, d, 270, 90);
                path.AddArc(Width-d, Height-d, d, d, 0, 90); path.AddArc(0, Height-d, d, d, 90, 90); path.CloseFigure();
                Region = new Region(path);
            }
        } else {
            compact = false;
            clock.Compact = false; clock.BackColor = bg; BackColor = bg;
            Region previous = Region; Region = null; if (previous != null) previous.Dispose();
            FormBorderStyle = FormBorderStyle.FixedSingle; ClientSize = fullSize;
            clock.Font = fullClockFont; clock.Bounds = fullClockBounds;
            foreach (Control control in Controls) control.Visible = true;
        }
        Rectangle area = Screen.FromPoint(center).WorkingArea;
        Location = new Point(Math.Max(area.Left, Math.Min(area.Right - Width, center.X - Width/2)), Math.Max(area.Top, Math.Min(area.Bottom - Height, center.Y - Height/2)));
        ResumeLayout(); RefreshView();
    }
    float DeviceScale() { using (Graphics g = CreateGraphics()) return g.DpiX / 96f; }
    void Advance(DateTime now)
    {
        Phase previous = engine.Phase;
        if (!engine.Tick(now, auto.Checked)) return;
        string message = (previous == Phase.Focus ? "专注完成！" : "休息结束！") + "接下来：" + PhaseName(engine.Phase) + (engine.Running ? "，已开始计时。" : "，点击开始继续。");
        tray.ShowBalloonTip(10000, "番茄时钟 · 阶段完成", message, ToolTipIcon.Info);
        if (sound.Checked) SystemSounds.Exclamation.Play();
        if (!engine.Running) SetCompact(false);
    }
    static string PhaseName(Phase phase) { return phase == Phase.Focus ? "专注" : phase == Phase.ShortBreak ? "短休息" : "长休息"; }
    void RefreshView()
    {
        int seconds = (int)Math.Ceiling(engine.Remaining);
        clock.Text = string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60);
        clock.ForeColor = compact && !engine.Running ? Color.FromArgb(145, 140, 129) : Color.FromArgb(223, 214, 198);
        status.Text = PhaseName(engine.Phase) + (engine.Running ? "进行中" : engine.Remaining < TimerEngine.Duration(engine.Phase) ? "已暂停" : " · 准备好就开始");
        start.Text = engine.Running ? "暂停" : engine.Remaining < TimerEngine.Duration(engine.Phase) ? "继续" : "开始";
        start.BackColor = accent; start.ForeColor = bg;
        for (int i = 0; i < 3; i++) modes[i].BackColor = i == (int)engine.Phase ? Color.FromArgb(135, 61, 56) : panel;
        count.Text = string.Format("本次已完成 {0} 个番茄  ·  每 4 个进入长休息", engine.Completed);
        progress.Maximum = TimerEngine.Duration(engine.Phase);
        progress.Value = Math.Max(0, Math.Min(progress.Maximum, progress.Maximum - seconds));
        tray.Text = "番茄时钟 · " + PhaseName(engine.Phase) + " " + clock.Text + (engine.Running ? "" : "（暂停）");
    }
    void RestoreWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    void SetupCheck(CheckBox box, string text, int x, int y, int width, bool value) { box.Text = text; box.SetBounds(x, y, width, 28); box.Checked = value; Controls.Add(box); }
    Label LabelAt(string text, int x, int y, int w, int h, int size) { Label label = new Label(); label.Text = text; label.SetBounds(x,y,w,h); label.Font = new Font("Microsoft YaHei UI", size); Controls.Add(label); return label; }
    void Style(Button b) { b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.BackColor = panel; b.ForeColor = Color.White; b.Cursor = Cursors.Hand; }
    Button ButtonAt(string text, int x, int y, int w, int h) { Button b = new Button(); b.Text = text; b.SetBounds(x,y,w,h); Style(b); Controls.Add(b); return b; }
    static Icon MakeIcon() {
        using (Bitmap bitmap = new Bitmap(32,32)) {
            using (Graphics g = Graphics.FromImage(bitmap)) {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent); using (Brush red = new SolidBrush(Color.Tomato)) g.FillEllipse(red, 3, 8, 26, 23);
                using (Pen green = new Pen(Color.SeaGreen, 4)) { g.DrawLine(green,16,11,16,2); g.DrawLine(green,16,10,7,5); g.DrawLine(green,16,10,25,5); }
            }
            IntPtr handle = bitmap.GetHicon(); try { return (Icon)Icon.FromHandle(handle).Clone(); } finally { DestroyIcon(handle); }
        }
    }
    [STAThread] public static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new PomodoroForm()); }
}
