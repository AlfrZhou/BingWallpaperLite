using System;

namespace BingWallpaperLite
{
    internal static class HelperEntry
    {
        [STAThread]
        public static int Main(string[] args)
        {
            // Each mode runs in its own short-lived process. The native tray never loads .NET.
            bool update = Array.IndexOf(args, "--update") >= 0 || Array.IndexOf(args, "--force") >= 0 || Array.IndexOf(args, "--download-only") >= 0;
            return update ? WallpaperWorker.Main(args) : SettingsEntry.Main(args);
        }
    }
}
