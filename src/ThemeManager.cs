using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TorrentViewer
{
    public static class ThemeManager
    {
        // Colors for Dark Theme
        public static readonly Color DarkBackground = Color.FromArgb(32, 32, 32);
        public static readonly Color DarkContentBg = Color.FromArgb(24, 24, 24);
        public static readonly Color DarkHeaderBg = Color.FromArgb(38, 38, 38);
        public static readonly Color DarkBorder = Color.FromArgb(56, 56, 56);
        public static readonly Color DarkTextPrimary = Color.FromArgb(235, 235, 235);
        public static readonly Color DarkTextSecondary = Color.FromArgb(160, 160, 160);
        public static readonly Color DarkTextMuted = Color.FromArgb(130, 130, 130);
        public static readonly Color DarkButtonBg = Color.FromArgb(48, 48, 48);
        public static readonly Color DarkButtonBorder = Color.FromArgb(72, 72, 72);
        public static readonly Color DarkTreeLine = Color.FromArgb(70, 70, 70);

        // Windows DWM Dark Mode P/Invoke
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public static void SetWindowDarkMode(IntPtr handle, bool dark)
        {
            if (handle == IntPtr.Zero) return;
            int val = dark ? 1 : 0;
            try
            {
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref val, sizeof(int));
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref val, sizeof(int));
            }
            catch
            {
                // Silently fallback on older Windows versions
            }
        }

        // Persistence for theme preference
        private static string GetConfigPath()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Strings.AppName);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return Path.Combine(dir, "theme.cfg");
            }
            catch
            {
                return null;
            }
        }

        // Theme Auto-Detection
        public static bool IsSystemInDarkMode()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val is int)
                        {
                            return ((int)val) == 0;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static ThemePreference LoadThemePreference()
        {
            try
            {
                string path = GetConfigPath();
                if (path != null && File.Exists(path))
                {
                    string text = File.ReadAllText(path).Trim();
                    if (string.Equals(text, "dark", StringComparison.OrdinalIgnoreCase))
                    {
                        return ThemePreference.Dark;
                    }
                    if (string.Equals(text, "light", StringComparison.OrdinalIgnoreCase))
                    {
                        return ThemePreference.Light;
                    }
                }
            }
            catch { }
            // Default: Auto-detect system theme!
            return ThemePreference.Auto;
        }

        public static void SaveThemePreference(ThemePreference pref)
        {
            try
            {
                string path = GetConfigPath();
                if (path != null)
                {
                    string val = "auto";
                    if (pref == ThemePreference.Dark) val = "dark";
                    else if (pref == ThemePreference.Light) val = "light";
                    File.WriteAllText(path, val);
                }
            }
            catch { }
        }

        public static bool ResolveEffectiveDarkMode(ThemePreference pref)
        {
            if (pref == ThemePreference.Dark) return true;
            if (pref == ThemePreference.Light) return false;
            return IsSystemInDarkMode();
        }
    }

    public enum ThemePreference
    {
        Auto = 0,
        Light = 1,
        Dark = 2
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        private static readonly Color BgDark = Color.FromArgb(32, 32, 32);
        private static readonly Color BgMenu = Color.FromArgb(42, 42, 42);
        private static readonly Color BgHover = Color.FromArgb(58, 58, 58);
        private static readonly Color BgSelected = Color.FromArgb(68, 68, 68);
        private static readonly Color Border = Color.FromArgb(60, 60, 60);
        private static readonly Color Separator = Color.FromArgb(60, 60, 60);

        public override Color MenuStripGradientBegin { get { return BgDark; } }
        public override Color MenuStripGradientEnd { get { return BgDark; } }
        public override Color ToolStripDropDownBackground { get { return BgMenu; } }
        public override Color MenuBorder { get { return Border; } }
        public override Color MenuItemBorder { get { return Border; } }
        public override Color MenuItemSelected { get { return BgHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return BgHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return BgHover; } }
        public override Color MenuItemPressedGradientBegin { get { return BgSelected; } }
        public override Color MenuItemPressedGradientMiddle { get { return BgSelected; } }
        public override Color MenuItemPressedGradientEnd { get { return BgSelected; } }
        public override Color CheckBackground { get { return BgSelected; } }
        public override Color CheckSelectedBackground { get { return BgHover; } }
        public override Color CheckPressedBackground { get { return BgSelected; } }
        public override Color ImageMarginGradientBegin { get { return BgMenu; } }
        public override Color ImageMarginGradientMiddle { get { return BgMenu; } }
        public override Color ImageMarginGradientEnd { get { return BgMenu; } }
        public override Color SeparatorDark { get { return Separator; } }
        public override Color SeparatorLight { get { return Color.Transparent; } }
    }

    public class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkColorTable())
        {
            this.RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.FromArgb(230, 230, 230) : Color.FromArgb(120, 120, 120);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Enabled ? Color.FromArgb(210, 210, 210) : Color.FromArgb(100, 100, 100);
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = new Rectangle(e.ImageRectangle.Location, e.ImageRectangle.Size);
            r.Inflate(1, 1);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(65, 65, 65)))
            {
                e.Graphics.FillRectangle(b, r);
            }
            using (Pen p = new Pen(Color.FromArgb(90, 90, 90)))
            {
                e.Graphics.DrawRectangle(p, r);
            }
            using (Pen checkPen = new Pen(Color.FromArgb(0, 180, 255), 2))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawLines(checkPen, new Point[] {
                    new Point(r.Left + 3, r.Top + 7),
                    new Point(r.Left + 6, r.Top + 10),
                    new Point(r.Left + 12, r.Top + 3)
                });
            }
        }
    }
}
