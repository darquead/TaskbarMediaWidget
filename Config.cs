using System;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace TaskbarMediaWidget
{
    public class Config
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "TaskbarMediaWidget";

        public bool IsCompactMode { get; set; }
        public bool EnableVolumeWheel { get; set; }
        public int MarginGap { get; set; }
        public bool AutoStart { get; set; }
        public bool IsFreeMode { get; set; }
        public int FreeX { get; set; }
        public int FreeY { get; set; }
        public int OpacityPercent { get; set; }
        public string FontFamilyName { get; set; }
        public float FontSize { get; set; }
        public bool FontBold { get; set; }
        public bool FontItalic { get; set; }
        public string Language { get; set; }
        public string TargetAppId { get; set; }
        public int TaskbarCustomX { get; set; }
        public int TaskbarCustomY { get; set; }
        public bool HasCustomTaskbarPos { get; set; }

        public Config()
        {
            IsCompactMode = false;
            EnableVolumeWheel = true;
            MarginGap = 8;
            AutoStart = false;
            IsFreeMode = false;
            FreeX = 100;
            FreeY = 100;
            OpacityPercent = 100;
            FontFamilyName = "Inter";
            FontSize = 10.2f;
            FontBold = false;
            FontItalic = false;
            TargetAppId = string.Empty;
            TaskbarCustomX = -1;
            TaskbarCustomY = -1;
            HasCustomTaskbarPos = false;

            string sysLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            Language = (sysLang == "ru" || sysLang == "be" || sysLang == "uk") ? "ru" : "en";
            Loc.CurrentLanguage = Language;
        }

        public static string GetDataDirectory()
        {
            // Strict portable mode: strictly use the application executable folder only.
            try
            {
                string asmPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(asmPath))
                {
                    string dir = Path.GetDirectoryName(asmPath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        return dir;
                    }
                }
            }
            catch { }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string GetConfigPath()
        {
            return Path.Combine(GetDataDirectory(), "config.ini");
        }

        public static string GetLogPath()
        {
            return Path.Combine(GetDataDirectory(), "run.log");
        }

        public static Config Load()
        {
            Config cfg = new Config();
            string path = GetConfigPath();
            bool fileExists = File.Exists(path);

            if (fileExists)
            {
                cfg.AutoStart = IsAutoStartEnabled();
                try
                {
                    string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                    foreach (string rawLine in lines)
                    {
                        string line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";")) continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string key = line.Substring(0, eq).Trim();
                        string val = line.Substring(eq + 1).Trim();

                        if (string.Equals(key, "CompactMode", StringComparison.OrdinalIgnoreCase))
                        {
                            bool b;
                            if (bool.TryParse(val, out b)) cfg.IsCompactMode = b;
                        }
                        else if (string.Equals(key, "EnableVolumeWheel", StringComparison.OrdinalIgnoreCase))
                        {
                            bool b;
                            if (bool.TryParse(val, out b)) cfg.EnableVolumeWheel = b;
                        }
                        else if (string.Equals(key, "MarginGap", StringComparison.OrdinalIgnoreCase))
                        {
                            int g;
                            if (int.TryParse(val, out g) && g >= 0 && g <= 50) cfg.MarginGap = g;
                        }
                        else if (string.Equals(key, "AutoStart", StringComparison.OrdinalIgnoreCase))
                        {
                            bool b;
                            if (bool.TryParse(val, out b)) cfg.AutoStart = b;
                        }
                        else if (string.Equals(key, "FreeMode", StringComparison.OrdinalIgnoreCase))
                        {
                            bool b;
                            if (bool.TryParse(val, out b)) cfg.IsFreeMode = b;
                        }
                        else if (string.Equals(key, "FreeX", StringComparison.OrdinalIgnoreCase))
                        {
                            int x;
                            if (int.TryParse(val, out x)) cfg.FreeX = x;
                        }
                        else if (string.Equals(key, "FreeY", StringComparison.OrdinalIgnoreCase))
                        {
                            int y;
                            if (int.TryParse(val, out y)) cfg.FreeY = y;
                        }
                        else if (string.Equals(key, "OpacityPercent", StringComparison.OrdinalIgnoreCase))
                        {
                            int op;
                            if (int.TryParse(val, out op)) cfg.OpacityPercent = Math.Max(10, Math.Min(100, op));
                        }
                        else if (string.Equals(key, "FontFamilyName", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(val)) cfg.FontFamilyName = val;
                        }
                        else if (string.Equals(key, "FontSize", StringComparison.OrdinalIgnoreCase))
                        {
                            float fs;
                            if (float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fs))
                            {
                                if (fs >= 6f && fs <= 36f) cfg.FontSize = fs;
                            }
                        }
                        else if (string.Equals(key, "FontBold", StringComparison.OrdinalIgnoreCase))
                        {
                            bool b;
                            if (bool.TryParse(val, out b)) cfg.FontBold = b;
                        }
                        else if (string.Equals(key, "FontItalic", StringComparison.OrdinalIgnoreCase))
                        {
                            bool b;
                            if (bool.TryParse(val, out b)) cfg.FontItalic = b;
                        }
                        else if (string.Equals(key, "Language", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(val))
                            {
                                cfg.Language = string.Equals(val, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ru";
                            }
                        }
                        else if (string.Equals(key, "TargetAppId", StringComparison.OrdinalIgnoreCase))
                        {
                            cfg.TargetAppId = val;
                        }
                        else if (string.Equals(key, "HasCustomTaskbarPos", StringComparison.OrdinalIgnoreCase))
                        {
                            bool b;
                            if (bool.TryParse(val, out b)) cfg.HasCustomTaskbarPos = b;
                        }
                        else if (string.Equals(key, "TaskbarCustomX", StringComparison.OrdinalIgnoreCase))
                        {
                            int x;
                            if (int.TryParse(val, out x)) cfg.TaskbarCustomX = x;
                        }
                        else if (string.Equals(key, "TaskbarCustomY", StringComparison.OrdinalIgnoreCase))
                        {
                            int y;
                            if (int.TryParse(val, out y)) cfg.TaskbarCustomY = y;
                        }
                    }
                }
                catch
                {
                }
            }
            else
            {
                // First run: save defaults (portable: AutoStart is false by default, no registry trace)
                cfg.AutoStart = false;
                cfg.Save();
            }

            Loc.CurrentLanguage = cfg.Language ?? "ru";
            return cfg;
        }

        public void Save()
        {
            try
            {
                string path = GetConfigPath();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# TaskbarMediaWidget Configuration");
                sb.AppendLine("Language=" + (Language ?? "ru"));
                sb.AppendLine("CompactMode=" + IsCompactMode);
                sb.AppendLine("EnableVolumeWheel=" + EnableVolumeWheel);
                sb.AppendLine("MarginGap=" + MarginGap);
                sb.AppendLine("AutoStart=" + AutoStart);
                sb.AppendLine("FreeMode=" + IsFreeMode);
                sb.AppendLine("FreeX=" + FreeX);
                sb.AppendLine("FreeY=" + FreeY);
                sb.AppendLine("OpacityPercent=" + OpacityPercent);
                sb.AppendLine("FontFamilyName=" + (FontFamilyName ?? "Inter"));
                sb.AppendLine("FontSize=" + FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("FontBold=" + FontBold);
                sb.AppendLine("FontItalic=" + FontItalic);
                sb.AppendLine("TargetAppId=" + (TargetAppId ?? ""));
                sb.AppendLine("HasCustomTaskbarPos=" + HasCustomTaskbarPos);
                sb.AppendLine("TaskbarCustomX=" + TaskbarCustomX);
                sb.AppendLine("TaskbarCustomY=" + TaskbarCustomY);
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                // Only touch registry if setting differs from current system state
                bool currentAuto = IsAutoStartEnabled();
                if (AutoStart != currentAuto)
                {
                    SetAutoStart(AutoStart);
                }
            }
            catch
            {
            }
        }

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    object val = key.GetValue(AppName);
                    return val != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        string exePath = System.Windows.Forms.Application.ExecutablePath;
                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            key.SetValue(AppName, "\"" + exePath + "\"");
                        }
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
