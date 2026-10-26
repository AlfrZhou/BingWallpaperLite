using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using BingWallpaperLite;

internal static class Tests
{
    private static int count;
    private static void Assert(bool condition, string message)
    { if (!condition) throw new Exception(message); count++; }
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch { rejected = true; }
        Assert(rejected, message);
    }
    public static int Main(string[] args)
    {
        string root = Path.Combine(args[0], "test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            new Configuration().Validate();
            Assert(new Configuration().Resolution == "Auto", "Default to display resolution detection.");
            Assert(WindowsImages.SelectResolution(new[] { new Size(1366, 768) }) == "1920x1080", "Cover an HD display.");
            Assert(WindowsImages.SelectResolution(new[] { new Size(1920, 1200) }) == "1920x1200", "Cover a 16:10 display.");
            Assert(WindowsImages.SelectResolution(new[] { new Size(1920, 1080), new Size(2560, 1440) }) == "2560x1440", "Account for every monitor.");
            Assert(WindowsImages.SelectResolution(new[] { new Size(3840, 2160) }) == "UHD", "Use UHD for a 4K display.");
            Assert(WindowsImages.SelectResolution(new[] { new Size(1080, 1920) }) == "UHD", "Use UHD for a portrait display.");
            Assert(WindowsImages.SelectResolution(new[] { new Size(2560, 1080) }) == "UHD", "Use UHD for an ultrawide display.");
            Assert(WindowsImages.SelectResolution(new Size[0]) == "UHD", "Keep a fallback when display detection is unavailable.");
            Assert(WindowsImages.ImageResolution("1920x1200") == "1920x1200", "Preserve manual resolution.");
            Reject(() => new Configuration { KeepImages = 0 }.Validate(), "Never allow zero retained images.");
            Reject(() => new Configuration { Market = "zh-CN&n=8" }.Validate(), "Reject metadata query injection.");
            Reject(() => new Configuration { DailyTime = "25:70" }.Validate(), "Reject invalid trigger time.");
            Reject(() => Network.BingUri("https://bing.com.evil.invalid/a"), "Reject lookalike hosts.");
            Reject(() => Network.BingUri("http://www.bing.com/a"), "Require HTTPS.");
            Assert(Network.BingUri("https://cn.bing.com/th?id=OHR.test").Host == "cn.bing.com", "Allow Bing China CDN.");
            string html = Path.Combine(root, "download-test.tmp");
            File.WriteAllText(html, "<html>service unavailable</html>");
            Reject(() => Files.ValidateImage(html), "Reject a successful HTTP response containing HTML.");
            string current = Path.Combine(root, "bing-20260101-aaaaaaaaaaaaaaaa.jpg");
            string old = Path.Combine(root, "bing-20260102-bbbbbbbbbbbbbbbb.jpg");
            string unrelated = Path.Combine(root, "family.jpg");
            string deceptive = Path.Combine(root, "bing-important.jpg");
            using (var image = new Bitmap(800, 450)) image.Save(current, ImageFormat.Jpeg);
            Files.ValidateImage(current);
            File.WriteAllText(old, "old");
            File.WriteAllText(unrelated, "personal");
            File.WriteAllText(deceptive, "personal");
            string child = Path.Combine(root, "child");
            Directory.CreateDirectory(child);
            File.WriteAllText(Path.Combine(child, Path.GetFileName(old)), "outside cleanup scope");
            Files.Cleanup(root, current, 1);
            Assert(File.Exists(current), "Keep active picture even if its timestamp is older.");
            Assert(!File.Exists(old), "Remove only previous generated pictures.");
            Assert(File.Exists(unrelated) && File.Exists(deceptive), "Preserve unrelated pictures.");
            Assert(File.Exists(Path.Combine(child, Path.GetFileName(old))), "Never recurse during cleanup.");
            string json = Path.Combine(root, "state.json");
            Files.Save(json, new UpdateState { Title = "中文壁纸", LastSuccess = "old" });
            Files.Save(json, new UpdateState { Title = "新壁纸", LastSuccess = "new" });
            Assert(Files.Load<UpdateState>(json).Title == "新壁纸", "Atomic state replacement preserves Unicode.");
            Console.WriteLine("PASS: " + count + " assertions, plus image decoding.");
            File.Delete(Path.Combine(child, Path.GetFileName(old)));
            Directory.Delete(child);
            return 0;
        }
        finally
        {
            foreach (string file in Directory.GetFiles(root)) File.Delete(file);
            if (Directory.GetDirectories(root).Length == 0) Directory.Delete(root);
        }
    }
}
