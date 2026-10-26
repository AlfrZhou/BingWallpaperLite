using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BingWallpaperLite
{
    internal sealed class NativeClient
    {
        private const string HostClass = "BingWallpaperLite.NativeHost.1";
        internal readonly string Root;
        internal string NativeExe { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BingWallpaperLite.exe"); } }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string type, string title);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int show);
        private delegate bool WindowCallback(IntPtr window, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetProp(IntPtr window, string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetProp(IntPtr window, string name, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr RemoveProp(IntPtr window, string name);
        private string WindowKey
        {
            get
            {
                ulong hash = 1469598103934665603UL;
                unchecked { foreach (char original in Root) { char c = original >= 'A' && original <= 'Z' ? (char)(original + 32) : original; hash ^= c; hash *= 1099511628211UL; } }
                return "BingWallpaperLite.Settings." + hash.ToString("x16");
            }
        }
        internal IntPtr ExistingWindow
        {
            get { IntPtr found = IntPtr.Zero; EnumWindows(delegate(IntPtr window, IntPtr data) { if (GetProp(window, WindowKey) == IntPtr.Zero) return true; found = window; return false; }, IntPtr.Zero); return found; }
        }
        internal void Attach(IntPtr window) { SetProp(window, WindowKey, new IntPtr(1)); }
        internal void Detach(IntPtr window) { RemoveProp(window, WindowKey); }
        internal NativeClient(string root) { Root = root; }
        private IntPtr Host
        {
            get
            {
                return FindWindow(HostClass, Root);
            }
        }
        internal bool Running { get { return Host != IntPtr.Zero; } }
        private static long Query(IntPtr host, uint message)
        {
            IntPtr value;
            return host != IntPtr.Zero && SendMessageTimeout(host, message, IntPtr.Zero, IntPtr.Zero, 2, 300, out value) != IntPtr.Zero ? value.ToInt64() : 0;
        }
        internal bool Busy { get { return Query(Host, 0x8004) != 0; } }
        internal void ReadStatus(out bool running, out bool busy, out DateTime next)
        {
            IntPtr host = Host;
            running = host != IntPtr.Zero; busy = Query(host, 0x8004) != 0;
            long seconds = Query(host, 0x8006);
            next = seconds > 0 ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds).ToLocalTime() : DateTime.MinValue;
        }
        internal void Update() { PostMessage(Host, 0x0111, new IntPtr(101), IntPtr.Zero); }
        internal void Reload() { PostMessage(Host, 0x8005, IntPtr.Zero, IntPtr.Zero); }
        internal void EnsureRunning()
        {
            if (Running) return;
            Process.Start(new ProcessStartInfo(NativeExe, "--tray --no-update --data-dir \"" + Root + "\"") { UseShellExecute = false, CreateNoWindow = true });
            for (int i = 0; i < 20 && !Running; i++) Thread.Sleep(50);
        }
        internal bool Startup
        {
            get { using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return key != null && key.GetValue("BingWallpaperLite") != null; }
            set
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (value) key.SetValue("BingWallpaperLite", "\"" + NativeExe + "\" --tray", RegistryValueKind.String);
                    else key.DeleteValue("BingWallpaperLite", false);
                }
            }
        }
        internal void Folder()
        {
            string path = Path.Combine(Root, "images"); Directory.CreateDirectory(path);
            Process.Start("explorer.exe", "\"" + path + "\"");
        }
        internal void Log()
        {
            string path = Path.Combine(Root, "update.log");
            if (!File.Exists(path)) File.WriteAllText(path, "", Encoding.UTF8);
            Process.Start("notepad.exe", "\"" + path + "\"");
        }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly NativeClient client;
        private readonly bool rendering;
        private readonly System.Windows.Forms.Timer timer;
        private readonly PhotoCard photo;
        private readonly Badge badge;
        private readonly Label scheduleLabel, saveHint;
        private readonly CompactSelect market, resolution, daily, keep;
        private readonly CheckBox startup;
        private readonly Toggle automatic;
        private readonly ActionButton update, save;
        private readonly TableLayoutPanel fields;
        private string loadedPhoto;
        private bool initializing = true, dirty;
        private DateTime stateStamp;
        private UpdateState state = new UpdateState();
        private Configuration config;
        internal bool OffscreenTest;
        private IntPtr registeredWindow;

        internal SettingsForm(NativeClient native, bool render)
        {
            client = native; rendering = render;
            Text = render ? "必应轻壁纸 · 预览" : "必应轻壁纸";
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BingWallpaperLite.ico"))
                using (var icon = new Icon(stream)) Icon = (Icon)icon.Clone();
            }
            catch { Icon = SystemIcons.Application; }
            try { using (var family = new FontFamily("Noto Sans SC")) Style.Family = family.Name; } catch { }
            Font = Style.Font(9, false); ForeColor = Style.Ink; BackColor = Style.Background;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            ClientSize = new Size(360, 600); StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            try { config = Files.Load<Configuration>(Path.Combine(client.Root, "config.json")); config.Validate(); }
            catch { config = new Configuration(); }

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 10), ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 242)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            Controls.Add(layout);

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var brand = Style.Label("必应轻壁纸", 18, Style.Ink, true); brand.Dock = DockStyle.Fill; brand.Margin = new Padding(0);
            header.Controls.Add(brand, 0, 0);
            badge = new Badge { Anchor = AnchorStyles.Right, Text = "待更新", Size = new Size(90, 28) }; header.Controls.Add(badge, 1, 0);
            layout.Controls.Add(header, 0, 0);

            photo = new PhotoCard { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0) }; layout.Controls.Add(photo, 0, 1);
            scheduleLabel = Style.Label("", 8, Style.Muted, false); scheduleLabel.Dock = DockStyle.Fill; scheduleLabel.TextAlign = ContentAlignment.MiddleLeft; scheduleLabel.Margin = Padding.Empty;
            layout.Controls.Add(scheduleLabel, 0, 2);

            var card = new RoundedCard { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(14) };
            var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.White };
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 122)); settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(settings); layout.Controls.Add(card, 0, 3);
            var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.White, Margin = Padding.Empty };
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var headingTitle = Style.Label("自动更新", 12, Style.Ink, true); headingTitle.Dock = DockStyle.Fill; headingTitle.Margin = Padding.Empty;
            heading.Controls.Add(headingTitle, 0, 0);
            automatic = new Toggle { Checked = config.AutoUpdate, BackColor = Color.White, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
            heading.Controls.Add(automatic, 1, 0); settings.Controls.Add(heading, 0, 0);

            fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, BackColor = Color.White, Margin = new Padding(0, 2, 0, 0) };
            for (int i = 0; i < 2; i++) fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); fields.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); fields.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            string[] captions = { "地区", "分辨率", "更新时间", "保留张数" };
            for (int i = 0; i < 4; i++) { var label = Style.Label(captions[i], 8.3F, Style.Muted, false); label.Dock = DockStyle.Fill; label.Margin = new Padding(i % 2 == 0 ? 0 : 5, 0, 0, 0); fields.Controls.Add(label, i % 2, (i / 2) * 2); }
            market = Combo("图片地区"); market.Items.AddRange(new[] { "zh-CN  中国", "en-US  美国", "ja-JP  日本", "en-GB  英国", "de-DE  德国", "fr-FR  法国", "en-CA  加拿大", "en-IN  印度" });
            string currentMarket = market.Items.Cast<string>().FirstOrDefault(s => s.StartsWith(config.Market + " "));
            if (currentMarket == null) { currentMarket = config.Market; market.Items.Add(currentMarket); }
            market.SelectedItem = currentMarket;
            resolution = Combo("图片分辨率"); resolution.Margin = new Padding(5, 2, 0, 2); resolution.Items.AddRange(new[] { "UHD", "1920x1080", "1920x1200", "2560x1440" }); resolution.SelectedItem = config.Resolution;
            daily = Combo("每日更新时间");
            for (int h = 0; h < 24; h++) for (int m = 0; m < 60; m++) daily.Items.Add(h.ToString("00") + ":" + m.ToString("00"));
            daily.SelectedItem = config.DailyTime;
            keep = Combo("保留图片数量"); keep.Margin = new Padding(5, 2, 0, 2); for (int i = 1; i <= 30; i++) keep.Items.Add(i.ToString()); keep.SelectedItem = config.KeepImages.ToString();
            fields.Controls.Add(market, 0, 1); fields.Controls.Add(resolution, 1, 1); fields.Controls.Add(daily, 0, 3); fields.Controls.Add(keep, 1, 3); settings.Controls.Add(fields, 0, 1);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Color.White, Margin = Padding.Empty };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            startup = new StyledCheck { Text = "登录时启动", Checked = client.Startup, Dock = DockStyle.Fill, AutoSize = false, BackColor = Color.White, Font = Style.Font(8.5F, false), Margin = Padding.Empty };
            saveHint = Style.Label("", 8, Style.Muted, false); saveHint.Dock = DockStyle.Fill; saveHint.TextAlign = ContentAlignment.MiddleLeft;
            save = new ActionButton("保存", false) { Anchor = AnchorStyles.Right, Size = new Size(78, 32), Margin = Padding.Empty };
            save.Click += delegate { Save(); };
            bottom.Controls.Add(startup, 0, 0); bottom.SetColumnSpan(startup, 2); bottom.Controls.Add(saveHint, 0, 1); bottom.Controls.Add(save, 1, 1); settings.Controls.Add(bottom, 0, 2);

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 8, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            update = new ActionButton("立即更新", true) { Dock = DockStyle.Fill, Margin = Padding.Empty }; update.Click += delegate { client.Update(); Poll(); };
            footer.Controls.Add(update, 0, 0); footer.SetColumnSpan(update, 2);
            var folderLink = Link("图片"); folderLink.TextAlign = ContentAlignment.MiddleLeft; folderLink.Click += delegate { client.Folder(); };
            footer.Controls.Add(folderLink, 0, 1);
            var logLink = Link("日志"); logLink.TextAlign = ContentAlignment.MiddleRight; logLink.Click += delegate { client.Log(); };
            footer.Controls.Add(logLink, 1, 1);
            layout.Controls.Add(footer, 0, 4);

            market.SelectedIndexChanged += Changed; resolution.SelectedIndexChanged += Changed; daily.SelectedIndexChanged += Changed;
            keep.SelectedIndexChanged += Changed; startup.CheckedChanged += Changed; automatic.CheckedChanged += Changed;
            initializing = false; RefreshFile(); Poll();
            timer = new System.Windows.Forms.Timer { Interval = 900 }; timer.Tick += delegate { Poll(); }; if (!rendering) timer.Start();
            Resize += delegate { if (!rendering && WindowState == FormWindowState.Minimized) Close(); };
        }
        private CompactSelect Combo(string name)
        {
            var combo = new CompactSelect { AccessibleName = name, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 5, 2), BackColor = Color.White };
            return combo;
        }
        private LinkLabel Link(string text) { return new LinkLabel { Text = text, Dock = DockStyle.Fill, Font = Style.Font(8.5F, false), LinkColor = Style.Green, ActiveLinkColor = Style.Green, LinkBehavior = LinkBehavior.HoverUnderline, Margin = Padding.Empty }; }
        private void Changed(object sender, EventArgs e)
        {
            if (initializing) return; dirty = true; saveHint.Text = "未保存"; saveHint.ForeColor = Style.Muted; save.Enabled = rendering || !client.Busy;
        }
        private void Save()
        {
            try
            {
                var updated = new Configuration { Market = market.SelectedItem.ToString().Split(' ')[0], Resolution = resolution.SelectedItem.ToString(), DailyTime = daily.SelectedItem.ToString(), KeepImages = int.Parse(keep.SelectedItem.ToString()), AutoUpdate = automatic.Checked };
                updated.Validate(); client.Startup = startup.Checked; Files.Save(Path.Combine(client.Root, "config.json"), updated); client.Reload();
                config = updated; dirty = false; saveHint.Text = "已保存"; saveHint.ForeColor = Style.Green; Poll();
            }
            catch (Exception error) { MessageBox.Show(this, error.GetBaseException().Message, "设置未能保存", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        private void RefreshFile()
        {
            string path = Path.Combine(client.Root, "state.json");
            try { state = Files.Load<UpdateState>(path); stateStamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue; } catch { return; }
            photo.Caption = state.Title ?? "每日一图"; photo.Credit = state.Copyright ?? "";
            if (state.Copyright != null) { int author = state.Copyright.LastIndexOf("(©", StringComparison.Ordinal); if (author >= 0) photo.Credit = state.Copyright.Substring(author + 1).TrimEnd(')'); }
            photo.AccessibleDescription = state.Copyright;
            if (loadedPhoto != state.DesktopImage && File.Exists(state.DesktopImage))
            {
                try
                {
                    using (var input = new FileStream(state.DesktopImage, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var source = Image.FromStream(input))
                    {
                        int width = Math.Min(source.Width, Math.Max(360, photo.Width)), height = Math.Max(1, (int)((double)width * source.Height / source.Width));
                        var thumb = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                        using (var graphics = Graphics.FromImage(thumb)) { graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; graphics.DrawImage(source, 0, 0, width, height); }
                        photo.SetPhoto(thumb); loadedPhoto = state.DesktopImage;
                    }
                }
                catch { }
            }
            photo.Invalidate();
        }
        private void Poll()
        {
            string path = Path.Combine(client.Root, "state.json");
            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) != stateStamp) RefreshFile();
            bool running = true, busy = false; DateTime next = DateTime.MinValue;
            if (!rendering) client.ReadStatus(out running, out busy, out next);
            bool error = !string.IsNullOrEmpty(state.LastError);
            badge.SetState(busy ? "更新中" : !running ? "未运行" : error ? "同步失败" : !config.AutoUpdate ? "已暂停" : state.LastSuccess != null ? "已同步" : "待更新", busy || error, !config.AutoUpdate || !running);
            DateTimeOffset last; string recent = DateTimeOffset.TryParse(state.LastSuccess, out last) ? "上次更新 " + last.LocalDateTime.ToString("MM-dd HH:mm") : "尚未同步";
            string when = config.AutoUpdate ? (next != DateTime.MinValue ? next.ToString("MM-dd HH:mm") : config.DailyTime) : "已暂停";
            scheduleLabel.Text = busy ? "正在同步桌面与锁屏…" : error ? "更新失败 · 查看日志" : config.AutoUpdate && running ? "下次更新 " + when : recent;
            scheduleLabel.ForeColor = error ? Color.FromArgb(166, 104, 40) : Style.Muted;
            update.Enabled = running && !busy; update.Text = busy ? "更新中…" : "立即更新";
            save.Enabled = !busy && dirty; fields.Enabled = !busy; automatic.Enabled = !busy; startup.Enabled = !busy;
        }
        protected override void Dispose(bool disposing) { if (disposing && timer != null) timer.Dispose(); base.Dispose(disposing); }
        protected override bool ShowWithoutActivation { get { return rendering || OffscreenTest || base.ShowWithoutActivation; } }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (!rendering && !OffscreenTest)
            {
                Rectangle area = Screen.FromControl(this).WorkingArea;
                if (Height > area.Height - 16) Height = area.Height - 16;
                Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
            }
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); if (!rendering) { registeredWindow = Handle; client.Attach(registeredWindow); } }
        protected override void OnHandleDestroyed(EventArgs e) { if (registeredWindow != IntPtr.Zero) { client.Detach(registeredWindow); registeredWindow = IntPtr.Zero; } base.OnHandleDestroyed(e); }
    }

    internal static class SettingsEntry
    {
        [STAThread]
        public static int Main(string[] args)
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BingWallpaperLite");
            int at = Array.IndexOf(args, "--data-dir"); if (at >= 0 && at + 1 < args.Length) root = Path.GetFullPath(args[at + 1]);
            int renderAt = Array.IndexOf(args, "--render-preview"); bool render = renderAt >= 0 && renderAt + 1 < args.Length;
            bool testClose = args.Contains("--test-close"), testMinimize = args.Contains("--test-minimize");
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Directory.CreateDirectory(root); var client = new NativeClient(root);
                if (render)
                {
                    using (var form = new SettingsForm(client, true))
                    {
                        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-10000, -10000);
                        form.Show(); form.PerformLayout(); form.Update();
                        using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                        {
                            form.Controls[0].DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
                            bitmap.Save(args[renderAt + 1], ImageFormat.Png);
                        }
                        form.Close();
                    }
                    return 0;
                }
                using (var singleton = new Mutex(false, @"Local\BingWallpaperLite.Settings." + Files.Hash(root.ToLowerInvariant())))
                {
                    bool owned; try { owned = singleton.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                    if (!owned) { var old = client.ExistingWindow; if (old != IntPtr.Zero) { NativeClient.ShowWindow(old, 9); NativeClient.SetForegroundWindow(old); } return 0; }
                    try
                    {
                        client.EnsureRunning();
                        using (var form = new SettingsForm(client, false))
                        using (var lifetime = new System.Windows.Forms.Timer { Interval = 1400 })
                        {
                            if (testClose || testMinimize)
                            {
                                form.OffscreenTest = true; form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-10000, -10000);
                                lifetime.Tick += delegate { lifetime.Stop(); if (testMinimize) form.WindowState = FormWindowState.Minimized; else form.Close(); }; lifetime.Start();
                            }
                            Application.Run(form);
                        }
                    }
                    finally { singleton.ReleaseMutex(); }
                }
                return 0;
            }
            catch (Exception error) { if (!render) MessageBox.Show(error.GetBaseException().Message, "必应轻壁纸", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        }
    }
}
