using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TorrentViewer
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                // Enable Per-Monitor / System DPI Awareness for crisp fonts & icons
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    SetProcessDPIAware();
                }
            }
            catch
            {
                // Fallback gracefully if DPI call fails on older OS
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            MainForm mainForm = new MainForm();

            // Support opening file from command line (e.g. torr.exe "path\to\file.torrent")
            if (args != null && args.Length > 0)
            {
                string targetFile = args[0];
                if (File.Exists(targetFile))
                {
                    mainForm.Shown += (s, e) => {
                        mainForm.LoadTorrent(targetFile);
                    };
                }
            }

            Application.Run(mainForm);
        }
    }
}
