using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TaskbarMediaWidget
{
    internal static class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        public static void Log(string msg)
        {
            try
            {
                string logPath = Config.GetLogPath();
                string dir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Log rotation: cap at 1 MB to prevent unbounded growth
                FileInfo fi = new FileInfo(logPath);
                if (fi.Exists && fi.Length > 1024 * 1024)
                {
                    try
                    {
                        string oldLog = logPath + ".old";
                        if (File.Exists(oldLog)) File.Delete(oldLog);
                        File.Move(logPath, oldLog);
                    }
                    catch { }
                }

                File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        private static void Main(string[] args)
        {
            // CRITICAL: SetThreadDesktop MUST be called before ANY COM / WinForms initialization!
            IntPtr hDesk = OpenDesktop("Default", 0, false, 0x01FF);
            bool setDesk = false;
            int deskErr = 0;
            if (hDesk != IntPtr.Zero)
            {
                setDesk = SetThreadDesktop(hDesk);
                deskErr = Marshal.GetLastWin32Error();
                CloseDesktop(hDesk);
            }

            Log("Starting TaskbarMediaWidget, setDesk=" + setDesk + ", deskErr=" + deskErr);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Log("UnhandledException: " + e.ExceptionObject);
            };

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                Log("ThreadException: " + e.Exception);
            };

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\TaskbarMediaWidget.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    Log("Another instance is running. Exiting.");
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Config config = Config.Load();

                foreach (string arg in args)
                {
                    if (string.Equals(arg, "--no-autostart", StringComparison.OrdinalIgnoreCase))
                    {
                        config.AutoStart = false;
                    }
                    else if (string.Equals(arg, "--compact", StringComparison.OrdinalIgnoreCase))
                    {
                        config.IsCompactMode = true;
                    }
                }

                if (config.AutoStart)
                {
                    Config.SetAutoStart(true);
                }

                MediaEngine engine = new MediaEngine();

                try
                {
                    Log("Creating TrayContext...");
                    using (TrayContext context = new TrayContext(config, engine))
                    {
                        Log("Running Application.Run...");
                        Application.Run(context);
                        Log("Application.Run exited.");
                    }
                }
                catch (Exception ex)
                {
                    Log("Fatal Exception: " + ex);
                }
            }
        }
    }
}
