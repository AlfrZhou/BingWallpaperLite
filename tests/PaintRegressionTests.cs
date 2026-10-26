using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    internal static class PaintRegressionTests
    {
        private static readonly Point Offset = new Point(29, 23);
        private static int failures;
        private static int checks;
        private static Bitmap Canvas(Control control, bool dirty)
        {
            var bitmap = new Bitmap(control.Width + 70, control.Height + 60);
            using (var g = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(dirty ? Style.Accent : Color.White))
            { g.Clear(Color.Magenta); g.FillRectangle(brush, new Rectangle(Offset, control.Size)); }
            return bitmap;
        }
        private static void Paint(Control control, Bitmap bitmap, Rectangle clip)
        {
            using (var g = Graphics.FromImage(bitmap))
            using (var args = new PaintEventArgs(g, clip))
            {
                g.TranslateTransform(Offset.X, Offset.Y); g.SetClip(clip);
                control.GetType().GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { args });
            }
        }
        private static int Difference(Bitmap left, Bitmap right)
        {
            int changed = 0;
            for (int y = 0; y < left.Height; y++) for (int x = 0; x < left.Width; x++)
                if (left.GetPixel(x, y).ToArgb() != right.GetPixel(x, y).ToArgb()) changed++;
            return changed;
        }
        private static void Check(Control control)
        {
            checks += 3;
            using (control)
            using (var parent = new Panel { BackColor = Color.White })
            {
                parent.Controls.Add(control);
                using (var clean = Canvas(control, false))
                using (var dirty = Canvas(control, true))
                {
                    Paint(control, clean, control.ClientRectangle); Paint(control, dirty, control.ClientRectangle);
                    int difference = Difference(clean, dirty);
                    if (difference != 0) { failures++; Console.WriteLine("FAIL " + control.GetType().Name + ": reused paint buffer differs in " + difference + " pixels."); }
                    Rectangle bounds = new Rectangle(Offset, control.Size);
                    int escaped = 0;
                    for (int y = 0; y < clean.Height; y++) for (int x = 0; x < clean.Width; x++)
                        if (!bounds.Contains(x, y) && clean.GetPixel(x, y).ToArgb() != Color.Magenta.ToArgb()) escaped++;
                    if (escaped != 0) { failures++; Console.WriteLine("FAIL " + control.GetType().Name + ": painting escaped its bounds in " + escaped + " pixels."); }
                    using (var partial = (Bitmap)clean.Clone())
                    {
                        var clip = new Rectangle(0, 0, Math.Max(1, control.Width / 2), control.Height);
                        using (var g = Graphics.FromImage(partial)) using (var brush = new SolidBrush(Style.Accent))
                            g.FillRectangle(brush, new Rectangle(Offset.X, Offset.Y, clip.Width, clip.Height));
                        Paint(control, partial, clip);
                        difference = Difference(clean, partial);
                        if (difference != 0)
                        {
                            failures++; Console.WriteLine("FAIL " + control.GetType().Name + ": partial repaint differs in " + difference + " pixels.");
                            if (control is PhotoCard) { clean.Save("build/paint-photo-clean.png"); partial.Save("build/paint-photo-partial.png"); }
                        }
                    }
                }
            }
        }
        [STAThread]
        public static int Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Check(new StyledCheck { Text = "登录 Windows 后自动启动", Checked = true, Size = new Size(300, 28), BackColor = Color.White });
            Check(new StyledCheck { Text = "登录 Windows 后自动启动", Checked = false, Enabled = false, Size = new Size(300, 28), BackColor = Color.White });
            Check(new Toggle { Checked = true, BackColor = Color.White });
            Check(new Toggle { Checked = false, Enabled = false, BackColor = Color.White });
            Check(new ActionButton("立即更新壁纸", true) { Size = new Size(328, 38) });
            Check(new ActionButton("正在更新…", true) { Enabled = false, Size = new Size(328, 38) });
            Check(new ActionButton("保存设置", false) { Size = new Size(98, 32) });
            Check(new Badge { Text = "自动更新", Size = new Size(106, 28), Busy = false, Paused = false });
            Check(new Badge { Text = "更新中", Size = new Size(90, 28), Busy = true });
            Check(new Badge { Text = "已同步", Size = new Size(90, 28) });
            var select = new CompactSelect { Size = new Size(145, 34) }; select.Items.Add("08:05"); select.SelectedIndex = 0; Check(select);
            Check(new PhotoCard { Size = new Size(328, 180) });
            Check(new RoundedCard { Size = new Size(328, 268) });
            Console.WriteLine(failures == 0 ? "PASS: " + checks + " reused-buffer, bounds and partial repaint checks." : "FAILED: " + failures + " painting checks.");
            return failures == 0 ? 0 : 1;
        }
    }
}
