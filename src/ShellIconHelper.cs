using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TorrentViewer
{
    public static class ShellIconHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        public static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

        public const int TVM_SETEXTENDEDSTYLE = 0x1100 + 44;
        public const int TVS_EX_DOUBLEBUFFER = 0x0004;
        public const int TVS_EX_AUTOHSCROLL = 0x0020;
        public const int TVS_EX_FADEINOUTEXPANDOS = 0x0040;

        public const int LVM_SETEXTENDEDLISTVIEWSTYLE = 0x1000 + 54;
        public const int LVS_EX_DOUBLEBUFFER = 0x00010000;
        public const int LVM_GETHEADER = 0x1000 + 31;

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint SHGFI_TYPENAME = 0x000000400;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        public static ImageList SmallImageList { get; private set; }
        public static ImageList LargeImageList { get; private set; }

        public static int FolderIconIndex { get; private set; }
        public static int UpFolderIconIndex { get; private set; }
        public static int DefaultFileIconIndex { get; private set; }

        public static Image FolderLargeImage { get; private set; }
        public static Image UpFolderLargeImage { get; private set; }
        public static Image DefaultFileLargeImage { get; private set; }

        private static readonly Dictionary<string, int> ExtensionSmallIconCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Image> ExtensionLargeIconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> ExtensionTypeNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static ShellIconHelper()
        {
            // 16x16 standard native icon size for pixel-perfect Windows Classic look
            SmallImageList = new ImageList();
            SmallImageList.ImageSize = new Size(16, 16);
            SmallImageList.ColorDepth = ColorDepth.Depth32Bit;

            LargeImageList = new ImageList();
            LargeImageList.ImageSize = new Size(32, 32);
            LargeImageList.ColorDepth = ColorDepth.Depth32Bit;

            // 1. Native Folder Icon
            Icon folderSmall = ExtractShellIcon("dummy", FILE_ATTRIBUTE_DIRECTORY, true);
            Icon folderLarge = ExtractShellIcon("dummy", FILE_ATTRIBUTE_DIRECTORY, false);

            FolderIconIndex = SmallImageList.Images.Count;
            SmallImageList.Images.Add(PadIconToSize(folderSmall != null ? folderSmall : SystemIcons.Application, 16, 16));
            FolderLargeImage = folderLarge != null ? folderLarge.ToBitmap() : SystemIcons.Application.ToBitmap();
            LargeImageList.Images.Add(FolderLargeImage);

            if (folderSmall != null) folderSmall.Dispose();
            if (folderLarge != null) folderLarge.Dispose();

            // 2. Up Folder Icon (Folder icon with clean up-arrow badge)
            Bitmap upSmallBmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(upSmallBmp))
            {
                g.DrawImage(SmallImageList.Images[FolderIconIndex], 0, 0);
                using (Brush brush = new SolidBrush(Color.FromArgb(16, 110, 210)))
                {
                    Point[] pts = new Point[] {
                        new Point(4, 2),
                        new Point(1, 6),
                        new Point(7, 6)
                    };
                    g.FillPolygon(brush, pts);
                    g.FillRectangle(brush, 3, 6, 3, 4);
                }
            }
            UpFolderIconIndex = SmallImageList.Images.Count;
            SmallImageList.Images.Add(upSmallBmp);

            Bitmap upLargeBmp = new Bitmap(FolderLargeImage);
            using (Graphics g = Graphics.FromImage(upLargeBmp))
            {
                using (Brush brush = new SolidBrush(Color.FromArgb(16, 110, 210)))
                {
                    Point[] pts = new Point[] {
                        new Point(8, 2),
                        new Point(2, 10),
                        new Point(14, 10)
                    };
                    g.FillPolygon(brush, pts);
                    g.FillRectangle(brush, 6, 10, 5, 7);
                }
            }
            UpFolderLargeImage = upLargeBmp;

            // 3. Default Generic File Icon
            Icon fileSmall = ExtractShellIcon(".genericfile", FILE_ATTRIBUTE_NORMAL, true);
            Icon fileLarge = ExtractShellIcon(".genericfile", FILE_ATTRIBUTE_NORMAL, false);

            DefaultFileIconIndex = SmallImageList.Images.Count;
            SmallImageList.Images.Add(PadIconToSize(fileSmall != null ? fileSmall : SystemIcons.Application, 16, 16));
            DefaultFileLargeImage = fileLarge != null ? fileLarge.ToBitmap() : SystemIcons.Application.ToBitmap();
            LargeImageList.Images.Add(DefaultFileLargeImage);

            if (fileSmall != null) fileSmall.Dispose();
            if (fileLarge != null) fileLarge.Dispose();
        }

        public static int GetSmallIconIndexForExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return DefaultFileIconIndex;
            }

            string ext = extension.ToLowerInvariant();
            if (!ext.StartsWith(".")) ext = "." + ext;

            int cachedIndex;
            if (ExtensionSmallIconCache.TryGetValue(ext, out cachedIndex))
            {
                return cachedIndex;
            }

            Icon smallIcon = ExtractShellIcon(ext, FILE_ATTRIBUTE_NORMAL, true);
            if (smallIcon != null)
            {
                int index = SmallImageList.Images.Count;
                SmallImageList.Images.Add(PadIconToSize(smallIcon, 16, 16));
                ExtensionSmallIconCache[ext] = index;
                smallIcon.Dispose();
                return index;
            }

            return DefaultFileIconIndex;
        }

        public static Image GetLargeImageForExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return DefaultFileLargeImage;
            }

            string ext = extension.ToLowerInvariant();
            if (!ext.StartsWith(".")) ext = "." + ext;

            Image cachedImg;
            if (ExtensionLargeIconCache.TryGetValue(ext, out cachedImg))
            {
                return cachedImg;
            }

            Icon largeIcon = ExtractShellIcon(ext, FILE_ATTRIBUTE_NORMAL, false);
            if (largeIcon != null)
            {
                Bitmap bmp = largeIcon.ToBitmap();
                ExtensionLargeIconCache[ext] = bmp;
                largeIcon.Dispose();
                return bmp;
            }

            return DefaultFileLargeImage;
        }

        public static string GetFileTypeName(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return Strings.TypeDefaultFile;
            }

            string ext = extension.ToLowerInvariant();
            if (!ext.StartsWith(".")) ext = "." + ext;

            string cachedName;
            if (ExtensionTypeNameCache.TryGetValue(ext, out cachedName))
            {
                return cachedName;
            }

            SHFILEINFO fi = new SHFILEINFO();
            uint flags = SHGFI_TYPENAME | SHGFI_USEFILEATTRIBUTES;
            IntPtr res = SHGetFileInfo(ext, FILE_ATTRIBUTE_NORMAL, ref fi, (uint)Marshal.SizeOf(fi), flags);

            string typeName = fi.szTypeName;
            if (string.IsNullOrEmpty(typeName))
            {
                typeName = ext.Substring(1).ToUpperInvariant() + " " + Strings.TypeDefaultFile;
            }

            ExtensionTypeNameCache[ext] = typeName;
            return typeName;
        }

        private static Icon ExtractShellIcon(string path, uint attributes, bool small)
        {
            SHFILEINFO fi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;
            flags |= small ? SHGFI_SMALLICON : SHGFI_LARGEICON;

            IntPtr res = SHGetFileInfo(path, attributes, ref fi, (uint)Marshal.SizeOf(fi), flags);
            if (fi.hIcon != IntPtr.Zero)
            {
                try
                {
                    Icon icon = (Icon)Icon.FromHandle(fi.hIcon).Clone();
                    return icon;
                }
                finally
                {
                    DestroyIcon(fi.hIcon);
                }
            }
            return null;
        }

        private static Image PadIconToSize(Icon icon, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                int y = Math.Max(0, (height - icon.Height) / 2);
                int x = Math.Max(0, (width - icon.Width) / 2);
                g.DrawIcon(icon, x, y);
            }
            return bmp;
        }

        /// <summary>
        /// Applies the authentic Windows Classic theme to TreeView or ListView controls.
        /// By clearing the visual style (passing empty strings to SetWindowTheme), the control reverts
        /// to native classic Win32 drawing: TreeView gets classic dotted lines and [+] / [-] boxes,
        /// and ListView gets classic 3D raised button column headers.
        /// </summary>
        public static void ApplyClassicTheme(Control control)
        {
            if (control != null && control.IsHandleCreated)
            {
                SetWindowTheme(control.Handle, "", "");

                if (control is TreeView)
                {
                    SendMessage(control.Handle, TVM_SETEXTENDEDSTYLE, (IntPtr)TVS_EX_DOUBLEBUFFER, (IntPtr)TVS_EX_DOUBLEBUFFER);
                }
                else if (control is ListView)
                {
                    IntPtr hHeader = SendMessage(control.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
                    if (hHeader != IntPtr.Zero)
                    {
                        try
                        {
                            SetWindowTheme(hHeader, "", "");
                        }
                        catch { }
                    }
                    SendMessage(control.Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, (IntPtr)LVS_EX_DOUBLEBUFFER, (IntPtr)LVS_EX_DOUBLEBUFFER);
                }
            }
        }

        public static void ApplyExplorerTheme(Control control)
        {
            ApplyClassicTheme(control);
        }

        public static void ApplyDarkTheme(Control control)
        {
            if (control != null && control.IsHandleCreated)
            {
                try
                {
                    SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
                }
                catch { }

                if (control is TreeView)
                {
                    SendMessage(control.Handle, TVM_SETEXTENDEDSTYLE, (IntPtr)TVS_EX_DOUBLEBUFFER, (IntPtr)TVS_EX_DOUBLEBUFFER);
                }
                else if (control is ListView)
                {
                    IntPtr hHeader = SendMessage(control.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
                    if (hHeader != IntPtr.Zero)
                    {
                        try
                        {
                            SetWindowTheme(hHeader, "DarkMode_ItemsView", null);
                        }
                        catch { }
                    }
                    SendMessage(control.Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, (IntPtr)LVS_EX_DOUBLEBUFFER, (IntPtr)LVS_EX_DOUBLEBUFFER);
                }
            }
        }
    }
}
