using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TaskbarMediaWidget
{
    public class WidgetForm : Form
    {
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey, [In] ref BLENDFUNCTION pblend, uint dwFlags);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT r);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);


        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MOUSEWHEEL = 0x020A;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; public POINT(int x, int y) { this.x = x; this.y = y; } }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx; public int cy; public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; } }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;
        private const uint ULW_ALPHA = 0x00000002;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        private const uint GW_HWNDPREV = 3;

        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOPMOST = 0x00000008;

        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTTRANSPARENT = -1;

        private const int FULL_WIDTH = 330;
        private const int COMPACT_WIDTH = 116;
        private const int WIDGET_HEIGHT = 36;

        private readonly MediaEngine _engine;
        private readonly Config _config;
        private readonly Timer _pollTimer;
        private readonly Timer _marqueeTimer;
        private readonly Timer _volumeFeedbackTimer;

        private int _hoverIndex = -1; // 0=Prev, 1=Play, 2=Next, 3=Info
        private int _pressedIndex = -1;
        private int _marqueeOffset = 0;
        private string _volumeFeedbackText = null;
        private bool _isVertical = false;

        private byte _opacityAlpha = 255;
        private bool _isDragging = false;
        private Point _dragStartCursor;
        private Point _dragStartLocation;
        private bool _hasDragged = false;
        public event Action OnLanguageChanged;

        private Font _iconFont;
        private Font _textFont;

        private IntPtr _mouseHook = IntPtr.Zero;
        private LowLevelMouseProc _mouseHookProc;

        public WidgetForm(MediaEngine engine, Config config)
        {
            _engine = engine;
            _config = config;
            _engine.TargetAppId = _config.TargetAppId;

            _opacityAlpha = (byte)Math.Max(25, (int)(_config.OpacityPercent * 255 / 100));

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;

            int initialWidth = _config.IsCompactMode ? COMPACT_WIDTH : FULL_WIDTH;
            Size = new Size(initialWidth, WIDGET_HEIGHT);

            InitFonts();

            _mouseHookProc = MouseHookCallback;
            try
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookProc, GetModuleHandle(curModule.ModuleName), 0);
                }
                Program.Log("MouseHook installed: " + (_mouseHook != IntPtr.Zero));
            }
            catch (Exception ex)
            {
                Program.Log("MouseHook install error: " + ex.Message);
            }

            _engine.OnMediaChanged += OnMediaInfoChanged;

            _pollTimer = new Timer();
            _pollTimer.Interval = 100;
            _pollTimer.Tick += (s, e) =>
            {
                UpdatePosition();
                _engine.Refresh();
            };
            _pollTimer.Start();

            _marqueeTimer = new Timer();
            _marqueeTimer.Interval = 120;
            _marqueeTimer.Tick += (s, e) =>
            {
                if (_hoverIndex == 3 && !_config.IsCompactMode && _engine.CurrentInfo.HasTrack)
                {
                    _marqueeOffset += 2;
                    Redraw();
                }
            };
            _marqueeTimer.Start();

            _volumeFeedbackTimer = new Timer();
            _volumeFeedbackTimer.Interval = 1200;
            _volumeFeedbackTimer.Tick += (s, e) =>
            {
                _volumeFeedbackText = null;
                _volumeFeedbackTimer.Stop();
                Redraw();
            };

            Redraw();
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && Visible)
            {
                int msg = (int)wParam;
                if (msg == WM_MOUSEMOVE || msg == WM_LBUTTONDOWN || msg == WM_LBUTTONUP ||
                    msg == WM_RBUTTONDOWN || msg == WM_RBUTTONUP || msg == WM_MOUSEWHEEL)
                {
                    MSLLHOOKSTRUCT s = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    Point screenPt = new Point(s.pt.x, s.pt.y);
                    Rectangle b = Bounds;

                    // If currently dragging, handle movement or release anywhere on screen
                    if (_isDragging)
                    {
                        if (msg == WM_MOUSEMOVE)
                        {
                            HandleDragMove(screenPt);
                            // CRITICAL: Do NOT return (IntPtr)1 on WM_MOUSEMOVE, otherwise Windows freezes the mouse cursor!
                        }
                        else if (msg == WM_LBUTTONUP)
                        {
                            Point clientPt = new Point(screenPt.X - Left, screenPt.Y - Top);
                            HandleMouseUp(new MouseEventArgs(MouseButtons.Left, 1, clientPt.X, clientPt.Y, 0));
                            return (IntPtr)1;
                        }
                    }

                    if (b.Contains(screenPt))
                    {
                        Point clientPt = new Point(screenPt.X - Left, screenPt.Y - Top);

                        if (msg == WM_LBUTTONDOWN)
                        {
                            HandleMouseDown(new MouseEventArgs(MouseButtons.Left, 1, clientPt.X, clientPt.Y, 0));
                            return (IntPtr)1;
                        }
                        else if (msg == WM_LBUTTONUP)
                        {
                            HandleMouseUp(new MouseEventArgs(MouseButtons.Left, 1, clientPt.X, clientPt.Y, 0));
                            return (IntPtr)1;
                        }
                        else if (msg == WM_RBUTTONDOWN)
                        {
                            ShowContextMenu(clientPt);
                            return (IntPtr)1;
                        }
                        else if (msg == WM_MOUSEWHEEL)
                        {
                            if (_config.EnableVolumeWheel)
                            {
                                short delta = (short)((s.mouseData >> 16) & 0xFFFF);
                                OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, clientPt.X, clientPt.Y, delta));
                                return (IntPtr)1;
                            }
                        }
                        else if (msg == WM_MOUSEMOVE)
                        {
                            int newIdx = GetButtonIndexAt(clientPt);
                            if (newIdx != _hoverIndex)
                            {
                                _hoverIndex = newIdx;
                                if (_hoverIndex != 3) _marqueeOffset = 0;
                                Redraw();
                            }
                        }
                    }
                    else if (msg == WM_MOUSEMOVE && _hoverIndex != -1)
                    {
                        _hoverIndex = -1;
                        _pressedIndex = -1;
                        _marqueeOffset = 0;
                        Redraw();
                    }
                }
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_config.IsFreeMode)
            {
                SetFreeDesktopMode(true);
            }
            else
            {
                UpdatePosition();
            }
            Redraw();
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);
        private const uint FR_PRIVATE = 0x10;

        private void InitFonts()
        {
            // Register local portable Inter font files if present in the application's fonts folder
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string fontsDir = Path.Combine(appDir, "fonts");
                if (Directory.Exists(fontsDir))
                {
                    string[] fontFiles = Directory.GetFiles(fontsDir, "*.ttf");
                    foreach (string fontFile in fontFiles)
                    {
                        AddFontResourceEx(fontFile, FR_PRIVATE, IntPtr.Zero);
                    }
                }
            }
            catch { }

            string iconFontFamily = "Segoe Fluent Icons";
            try
            {
                using (Font test = new Font(iconFontFamily, 10f))
                {
                    if (!string.Equals(test.Name, iconFontFamily, StringComparison.OrdinalIgnoreCase))
                    {
                        iconFontFamily = "Segoe MDL2 Assets";
                    }
                }
            }
            catch
            {
                iconFontFamily = "Segoe UI Symbol";
            }

            _iconFont = new Font(iconFontFamily, 11.5f, FontStyle.Regular);

            string textFontFamily = string.IsNullOrEmpty(_config.FontFamilyName) ? "Inter" : _config.FontFamilyName;
            float fontSize = (_config.FontSize >= 6f && _config.FontSize <= 36f) ? _config.FontSize : 10.2f;
            FontStyle style = FontStyle.Regular;
            if (_config.FontBold) style |= FontStyle.Bold;
            if (_config.FontItalic) style |= FontStyle.Italic;

            try
            {
                _textFont = new Font(textFontFamily, fontSize, style);
            }
            catch
            {
                try
                {
                    _textFont = new Font("Segoe UI", fontSize, style);
                }
                catch
                {
                    _textFont = new Font(FontFamily.GenericSansSerif, 10.2f, FontStyle.Regular);
                }
            }
        }

        public void ApplyNewFont(Font newFont)
        {
            if (newFont == null) return;
            if (_textFont != null) _textFont.Dispose();

            _textFont = new Font(newFont.FontFamily, newFont.Size, newFont.Style);
            _config.FontFamilyName = newFont.FontFamily.Name;
            _config.FontSize = newFont.Size;
            _config.FontBold = (newFont.Style & FontStyle.Bold) != 0;
            _config.FontItalic = (newFont.Style & FontStyle.Italic) != 0;
            _config.Save();
            Redraw();
        }

        public void OpenFontDialog()
        {
            using (FontDialog dlg = new FontDialog())
            {
                dlg.Font = _textFont;
                dlg.ShowColor = false;
                dlg.ShowEffects = false;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    ApplyNewFont(dlg.Font);
                }
            }
        }

        public void SetOpacityPercent(int percent)
        {
            _config.OpacityPercent = Math.Max(10, Math.Min(100, percent));
            _opacityAlpha = (byte)Math.Max(25, (int)(_config.OpacityPercent * 255 / 100));
            Redraw();
        }

        public void SetFreeDesktopMode(bool enable)
        {
            _config.IsFreeMode = enable;
            _config.Save();

            if (enable)
            {
                // DESKTOP MODE (ВКЛ):
                // 1. Ensure horizontal orientation
                _isVertical = false;
                Size = new Size(BaseWidth, BaseHeight);

                // 2. Place at saved Free coordinates (clamped to WorkingArea so it never ends up under the taskbar)
                int x = _config.FreeX;
                int y = _config.FreeY;
                Rectangle wa = Screen.FromPoint(new Point(x, y)).WorkingArea;
                if ((x <= 0 && y <= 0) || x < wa.Left || x > wa.Right - Width || y < wa.Top || y > wa.Bottom - Height)
                {
                    x = Math.Max(wa.Left, Math.Min(wa.Right - Width, (wa.Width - Width) / 2));
                    y = Math.Max(wa.Top, Math.Min(wa.Bottom - Height, wa.Top + 100));
                    _config.FreeX = x;
                    _config.FreeY = y;
                    _config.Save();
                }

                Location = new Point(x, y);

                // 3. HWND_BOTTOM places top-level window at the bottom of top-level Z-order,
                // directly ON TOP of Wallpaper Engine & Desktop icons, but UNDER all normal windows!
                SetWindowPos(Handle, HWND_BOTTOM, x, y, Width, Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
            else
            {
                // TASKBAR MODE (ВЫКЛ):
                // 1. Restore TOPMOST
                SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

                // 2. Re-anchor to taskbar
                UpdatePosition();
            }

            Redraw();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }
            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HTCLIENT;
                return;
            }

            base.WndProc(ref m);
        }

        private int BaseWidth { get { return _config.IsCompactMode ? COMPACT_WIDTH : FULL_WIDTH; } }
        private int BaseHeight { get { return WIDGET_HEIGHT; } }

        public Rectangle PrevRect { get { return new Rectangle(3, 3, 34, 30); } }
        public Rectangle PlayRect { get { return new Rectangle(41, 3, 34, 30); } }
        public Rectangle NextRect { get { return new Rectangle(79, 3, 34, 30); } }
        public Rectangle InfoRect { get { return new Rectangle(118, 2, Math.Max(10, BaseWidth - 122), 32); } }

        private Point TransformPoint(Point pt)
        {
            if (_isVertical)
            {
                // Rotated 90 degrees clockwise:
                // Rotated X was (BaseHeight - 1 - Y_base)
                // Rotated Y was X_base
                // Inverse mapping:
                // X_base = pt.Y
                // Y_base = BaseHeight - 1 - pt.X
                return new Point(pt.Y, BaseHeight - 1 - pt.X);
            }
            return pt;
        }

        private int GetButtonIndexAt(Point pt)
        {
            Point basePt = TransformPoint(pt);
            if (basePt.Y < 0 || basePt.Y >= BaseHeight) return -1;
            if (basePt.X < 0 || basePt.X >= BaseWidth) return -1;

            if (_config.IsCompactMode)
            {
                if (basePt.X < 38) return 0;       // Prev
                if (basePt.X < 76) return 1;       // Play/Pause
                return 2;                          // Next
            }
            else
            {
                if (basePt.X < 38) return 0;       // Prev
                if (basePt.X < 76) return 1;       // Play/Pause
                if (basePt.X < 114) return 2;      // Next
                return 3;                          // Info
            }
        }

        public void ToggleMode()
        {
            _config.IsCompactMode = !_config.IsCompactMode;
            _config.Save();
            ApplyModeChange();
        }

        public void SetMode(bool compact)
        {
            _config.IsCompactMode = compact;
            _config.Save();
            ApplyModeChange();
        }

        private void ApplyModeChange()
        {
            int baseW = BaseWidth;
            int baseH = BaseHeight;
            Size targetSize = _isVertical ? new Size(baseH, baseW) : new Size(baseW, baseH);
            if (Size != targetSize)
            {
                Size = targetSize;
                _marqueeOffset = 0;
                UpdatePosition();
                Redraw();
            }
        }

        private bool IsFullscreenAppRunning(IntPtr hTray, RECT trayRect)
        {
            // If mouse cursor is currently over taskbar or widget, user wants to interact with it!
            Point mousePos = Cursor.Position;
            if (mousePos.X >= trayRect.Left && mousePos.X <= trayRect.Right &&
                mousePos.Y >= trayRect.Top && mousePos.Y <= trayRect.Bottom)
            {
                return false;
            }
            if (Bounds.Contains(mousePos))
            {
                return false;
            }

            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == hTray || fg == Handle) return false;
            if (IsIconic(fg)) return false;

            uint fgPid;
            GetWindowThreadProcessId(fg, out fgPid);
            uint trayPid;
            GetWindowThreadProcessId(hTray, out trayPid);
            if (fgPid == 0 || fgPid == trayPid) return false;

            StringBuilder cls = new StringBuilder(256);
            GetClassName(fg, cls, 256);
            string clsName = cls.ToString();

            // Shell, Start Menu, Search, Notification Center, Desktops must never hide or demote the widget
            if (clsName == "WorkerW" || clsName == "Progman" ||
                clsName == "Shell_TrayWnd" || clsName == "Shell_SecondaryTrayWnd" ||
                clsName == "Windows.UI.Core.CoreWindow" ||
                clsName == "XamlExplorerHostIslandWindow" ||
                clsName == "TopLevelWindowForOverflowXamlIsland" ||
                clsName == "Shell_LightDismissOverlay" ||
                clsName == "Shell_InputSwitchTopLevelWindow" ||
                clsName == "ControlCenterWindow" ||
                clsName == "ActionCenter" || clsName == "NotificationCenter" ||
                clsName == "PopupHost" || clsName == "DV2ControlHost" ||
                clsName == "Windows.UI.Input.InputSite.WindowClass")
            {
                return false;
            }

            try
            {
                using (Process p = Process.GetProcessById((int)fgPid))
                {
                    string pName = p.ProcessName.ToLowerInvariant();
                    if (pName == "explorer" ||
                        pName == "startmenuexperiencehost" ||
                        pName == "shellexperiencehost" ||
                        pName == "shellhost" ||
                        pName == "sihost" ||
                        pName == "searchhost" ||
                        pName == "applicationframehost" ||
                        pName == "taskhostw" ||
                        pName == "dwm" ||
                        pName == "taskbarmediawidget" ||
                        pName == "systemsettings" ||
                        pName == "lockapp" ||
                        pName == "textinputhost")
                    {
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }

            RECT fgRect;
            GetWindowRect(fg, out fgRect);
            Rectangle screenBounds = Screen.FromHandle(Handle).Bounds;

            bool coversScreen = (fgRect.Left <= screenBounds.Left && fgRect.Top <= screenBounds.Top &&
                                 fgRect.Right >= screenBounds.Right && fgRect.Bottom >= screenBounds.Bottom);
            bool coversTaskbar = (fgRect.Left <= trayRect.Left && fgRect.Top <= trayRect.Top &&
                                  fgRect.Right >= trayRect.Right && fgRect.Bottom >= trayRect.Bottom);

            return coversScreen && coversTaskbar;
        }

        private bool IsStartInCorner(IntPtr hStart, RECT startRect, RECT trayRect, bool isVertical)
        {
            if (isVertical)
            {
                return (hStart == IntPtr.Zero || startRect.Top < trayRect.Top + 200);
            }

            try
            {
                object val = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "TaskbarAl", null);
                if (val != null)
                {
                    int al = Convert.ToInt32(val);
                    if (al == 0) return true;  // Explicitly Left
                    if (al == 1) return false; // Explicitly Center
                }
            }
            catch { }

            return (hStart == IntPtr.Zero || startRect.Left < trayRect.Left + 200);
        }

        public void UpdatePosition()
        {
            if (_config.IsFreeMode || _isDragging)
            {
                // In Free Desktop mode or during active drag, window position is controlled by the user
                return;
            }

            IntPtr hTray = FindWindow("Shell_TrayWnd", null);
            if (hTray == IntPtr.Zero) return;

            RECT trayRect;
            GetWindowRect(hTray, out trayRect);
            int trayHeight = trayRect.Bottom - trayRect.Top;
            int trayWidth = trayRect.Right - trayRect.Left;

            // Fullscreen application check (ignores Start Menu, Explorer, and shell popups)
            if (IsFullscreenAppRunning(hTray, trayRect))
            {
                return;
            }

            bool isVertical = (trayHeight > trayWidth);
            if (isVertical != _isVertical)
            {
                _isVertical = isVertical;
                int baseW = BaseWidth;
                int baseH = BaseHeight;
                Size = _isVertical ? new Size(baseH, baseW) : new Size(baseW, baseH);
                _marqueeOffset = 0;
                Redraw();
            }

            IntPtr hStart = FindWindowEx(hTray, IntPtr.Zero, "Start", null);
            RECT startRect = new RECT();
            if (hStart != IntPtr.Zero)
            {
                GetWindowRect(hStart, out startRect);
            }

            int targetX;
            int targetY;
            int gap = _config.MarginGap;

            if (!isVertical)
            {
                // Horizontal taskbar (Bottom or Top)
                targetY = trayRect.Top + (trayHeight - Height) / 2;

                if (_config.HasCustomTaskbarPos && _config.TaskbarCustomX >= 0)
                {
                    int minX = trayRect.Left;
                    int maxX = Math.Max(minX, trayRect.Right - Width);
                    targetX = Math.Max(minX, Math.Min(maxX, _config.TaskbarCustomX));
                }
                else
                {
                    bool isStartInCorner = IsStartInCorner(hStart, startRect, trayRect, false);
                    if (isStartInCorner)
                    {
                        // Default: center of taskbar when Start is in corner (left-aligned)
                        targetX = trayRect.Left + (trayWidth - Width) / 2;
                    }
                    else
                    {
                        // Center-aligned: place widget to the left of Start button
                        targetX = startRect.Left - Width - gap;
                    }
                }
            }
            else
            {
                // Vertical taskbar (Left or Right) -> Rotate 90 degrees
                targetX = trayRect.Left + (trayWidth - Width) / 2;

                if (_config.HasCustomTaskbarPos && _config.TaskbarCustomY >= 0)
                {
                    int minY = trayRect.Top;
                    int maxY = Math.Max(minY, trayRect.Bottom - Height);
                    targetY = Math.Max(minY, Math.Min(maxY, _config.TaskbarCustomY));
                }
                else
                {
                    bool isStartInCorner = IsStartInCorner(hStart, startRect, trayRect, true);
                    if (isStartInCorner)
                    {
                        // Start is in corner -> Center vertically on taskbar
                        targetY = trayRect.Top + (trayHeight - Height) / 2;
                    }
                    else
                    {
                        // Start is centered vertically -> Place above Start button
                        targetY = startRect.Top - Height - gap;
                    }
                }
            }

            // Always ensure the window is visible in Win32
            if (!IsWindowVisible(Handle))
            {
                ShowWindow(Handle, 8); // SW_SHOWNA
            }

            bool posChanged = (Left != targetX || Top != targetY);

            // Check if our window is not at the top of the Z-order
            IntPtr prevWnd = GetWindow(Handle, GW_HWNDPREV);
            bool notTopmost = (prevWnd != IntPtr.Zero);

            // Also check if mouse cursor is currently over our widget
            Point mousePos = Cursor.Position;
            Rectangle widgetBounds = new Rectangle(Left, Top, Width, Height);
            bool mouseOver = widgetBounds.Contains(mousePos);

            if (posChanged)
            {
                Location = new Point(targetX, targetY);
                SetWindowPos(Handle, HWND_TOPMOST, targetX, targetY, Width, Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
                Redraw();
            }
            else if (notTopmost || mouseOver)
            {
                SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        private void OnMediaInfoChanged()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(OnMediaInfoChanged));
                return;
            }
            _marqueeOffset = 0;
            Redraw();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDragging)
            {
                HandleDragMove(Cursor.Position);
                return;
            }

            if (!_config.IsFreeMode)
            {
                IntPtr prevWnd = GetWindow(Handle, GW_HWNDPREV);
                if (prevWnd != IntPtr.Zero)
                {
                    SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                }
            }

            int old = _hoverIndex;
            _hoverIndex = GetButtonIndexAt(e.Location);

            if (old != _hoverIndex)
            {
                if (_hoverIndex != 3) _marqueeOffset = 0;
                Redraw();
            }
        }

        internal void HandleDragMove(Point currentCursor)
        {
            int dx = currentCursor.X - _dragStartCursor.X;
            int dy = currentCursor.Y - _dragStartCursor.Y;
            if (Math.Abs(dx) > 2 || Math.Abs(dy) > 2)
            {
                _hasDragged = true;
            }

            if (_config.IsFreeMode)
            {
                int targetX = _dragStartLocation.X + dx;
                int targetY = _dragStartLocation.Y + dy;

                // Clamp to screen WorkingArea so the widget can NEVER be dragged behind/under the taskbar or off-screen
                Rectangle workArea = Screen.FromPoint(currentCursor).WorkingArea;
                int minX = workArea.Left;
                int maxX = Math.Max(minX, workArea.Right - Width);
                int minY = workArea.Top;
                int maxY = Math.Max(minY, workArea.Bottom - Height);

                int newX = Math.Max(minX, Math.Min(maxX, targetX));
                int newY = Math.Max(minY, Math.Min(maxY, targetY));

                Location = new Point(newX, newY);
                SetWindowPos(Handle, HWND_BOTTOM, newX, newY, Width, Height, SWP_NOSIZE | SWP_NOACTIVATE);
                _config.FreeX = newX;
                _config.FreeY = newY;
                Redraw();
            }
            else
            {
                // Dragging along the Windows Taskbar
                IntPtr hTray = FindWindow("Shell_TrayWnd", null);
                RECT trayRect = new RECT();
                bool hasTray = false;
                if (hTray != IntPtr.Zero)
                {
                    hasTray = GetWindowRect(hTray, out trayRect);
                }

                Screen currentScreen = Screen.FromPoint(currentCursor);
                if (!hasTray || (trayRect.Right - trayRect.Left <= 0))
                {
                    trayRect.Left = currentScreen.Bounds.Left;
                    trayRect.Right = currentScreen.Bounds.Right;
                    trayRect.Top = currentScreen.WorkingArea.Bottom;
                    trayRect.Bottom = currentScreen.Bounds.Bottom;
                    if (trayRect.Bottom <= trayRect.Top)
                    {
                        trayRect.Top = currentScreen.Bounds.Bottom - 48;
                        trayRect.Bottom = currentScreen.Bounds.Bottom;
                    }
                }

                int trayHeight = trayRect.Bottom - trayRect.Top;
                int trayWidth = trayRect.Right - trayRect.Left;
                bool isVertical = (trayHeight > trayWidth);

                if (!isVertical)
                {
                    int targetX = _dragStartLocation.X + dx;
                    int minX = trayRect.Left;
                    int maxX = Math.Max(minX, trayRect.Right - Width);
                    int clampedX = Math.Max(minX, Math.Min(maxX, targetX));
                    int fixedY = trayRect.Top + (trayHeight - Height) / 2;

                    Location = new Point(clampedX, fixedY);
                    SetWindowPos(Handle, HWND_TOPMOST, clampedX, fixedY, Width, Height, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

                    _config.TaskbarCustomX = clampedX;
                    _config.HasCustomTaskbarPos = true;
                    Redraw();
                }
                else
                {
                    int targetY = _dragStartLocation.Y + dy;
                    int minY = trayRect.Top;
                    int maxY = Math.Max(minY, trayRect.Bottom - Height);
                    int clampedY = Math.Max(minY, Math.Min(maxY, targetY));
                    int fixedX = trayRect.Left + (trayWidth - Width) / 2;

                    Location = new Point(fixedX, clampedY);
                    SetWindowPos(Handle, HWND_TOPMOST, fixedX, clampedY, Width, Height, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

                    _config.TaskbarCustomY = clampedY;
                    _config.HasCustomTaskbarPos = true;
                    Redraw();
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1 || _pressedIndex != -1)
            {
                _hoverIndex = -1;
                _pressedIndex = -1;
                _marqueeOffset = 0;
                Redraw();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            HandleMouseDown(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            HandleMouseDown(e);
        }

        internal void HandleMouseDown(MouseEventArgs e)
        {
            if (!_config.IsFreeMode)
            {
                SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }

            if (e.Button == MouseButtons.Left)
            {
                int index = GetButtonIndexAt(e.Location);
                _pressedIndex = index;
                Program.Log("Widget MouseDown: index=" + index + " at " + e.Location);
                Redraw();

                // Allow dragging from ANY point on the widget (works in both Full and Compact mode)
                _isDragging = true;
                _dragStartCursor = Cursor.Position;
                _dragStartLocation = Location;
                _hasDragged = false;
                Program.Log("Drag started: startCursor=" + _dragStartCursor + ", startLocation=" + _dragStartLocation);
            }
            else if (e.Button == MouseButtons.Right)
            {
                ShowContextMenu(e.Location);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                HandleMouseUp(e);
            }
        }

        internal void HandleMouseUp(MouseEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                if (_hasDragged)
                {
                    _config.Save();
                    Program.Log("Drag finished, saved TaskbarCustomX=" + _config.TaskbarCustomX);
                    Redraw();
                }
                else
                {
                    // User CLICKED without dragging: perform the clicked button action
                    if (_pressedIndex == 0)
                    {
                        Program.Log("Click -> PrevTrack");
                        _engine.PrevTrack();
                    }
                    else if (_pressedIndex == 1)
                    {
                        Program.Log("Click -> TogglePlayPause");
                        _engine.TogglePlayPause();
                    }
                    else if (_pressedIndex == 2)
                    {
                        Program.Log("Click -> NextTrack");
                        _engine.NextTrack();
                    }
                    else if (_pressedIndex == 3 && !_config.IsCompactMode)
                    {
                        Program.Log("Click -> ActivatePlayerWindow");
                        _engine.ActivatePlayerWindow();
                    }
                }
            }

            if (_pressedIndex != -1)
            {
                _pressedIndex = -1;
                Redraw();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!_config.EnableVolumeWheel) return;

            string targetApp = !string.IsNullOrEmpty(_config.TargetAppId) ? _config.TargetAppId : _engine.CurrentInfo.AppId;
            string feedback = AppVolumeManager.ChangeVolume(e.Delta, targetApp);
            _volumeFeedbackText = feedback;

            _volumeFeedbackTimer.Stop();
            _volumeFeedbackTimer.Start();
            Redraw();
        }

        public ContextMenuStrip BuildContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new ModernDarkMenuRenderer();
            menu.AutoClose = true;

            // Track info header
            string trackHeader = "🎵 " + (_engine.CurrentInfo.HasTrack
                ? (_engine.CurrentInfo.Artist + " — " + _engine.CurrentInfo.Title)
                : Loc.NoPlayback);

            ToolStripMenuItem headerItem = new ToolStripMenuItem(trackHeader);
            headerItem.Enabled = false;
            headerItem.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(headerItem);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem prevItem = new ToolStripMenuItem(Loc.PrevTrack, null, (s, e) => _engine.PrevTrack());
            ToolStripMenuItem playItem = new ToolStripMenuItem(_engine.CurrentInfo.IsPlaying ? Loc.Pause : Loc.Play, null, (s, e) => _engine.TogglePlayPause());
            ToolStripMenuItem nextItem = new ToolStripMenuItem(Loc.NextTrack, null, (s, e) => _engine.NextTrack());
            menu.Items.Add(prevItem);
            menu.Items.Add(playItem);
            menu.Items.Add(nextItem);

            menu.Items.Add(new ToolStripSeparator());

            // Источник звука / медиа (Audio Source)
            ToolStripMenuItem sourceMenu = new ToolStripMenuItem(Loc.AudioSource);

            ToolStripMenuItem autoItem = new ToolStripMenuItem(Loc.AudioSourceAuto, null, (s, e) =>
            {
                _config.TargetAppId = string.Empty;
                _engine.TargetAppId = string.Empty;
                _config.Save();
                _engine.Refresh();
                Redraw();
            });
            autoItem.Checked = string.IsNullOrEmpty(_config.TargetAppId);
            sourceMenu.DropDownItems.Add(autoItem);
            sourceMenu.DropDownItems.Add(new ToolStripSeparator());

            System.Collections.Generic.List<MediaAppItem> apps = _engine.GetAvailableMediaApps();
            if (apps.Count > 0)
            {
                foreach (MediaAppItem app in apps)
                {
                    string appId = app.Id;
                    string itemText = (app.IsPlaying ? "▶ " : "▫ ") + app.DisplayName;
                    ToolStripMenuItem appItem = new ToolStripMenuItem(itemText, null, (s, e) =>
                    {
                        _config.TargetAppId = appId;
                        _engine.TargetAppId = appId;
                        _config.Save();
                        _engine.Refresh();
                        Redraw();
                    });
                    appItem.Checked = string.Equals(_config.TargetAppId, appId, StringComparison.OrdinalIgnoreCase);
                    sourceMenu.DropDownItems.Add(appItem);
                }
            }
            else
            {
                ToolStripMenuItem noAppsItem = new ToolStripMenuItem(Loc.NoAppsFound);
                noAppsItem.Enabled = false;
                sourceMenu.DropDownItems.Add(noAppsItem);
            }

            menu.Items.Add(sourceMenu);

            menu.Items.Add(new ToolStripSeparator());

            // 1. Режимы отображения (Toggle "Свободный режим")
            ToolStripMenuItem freeModeItem = new ToolStripMenuItem(Loc.FreeMode, null, (s, e) =>
            {
                bool newState = !_config.IsFreeMode;
                ((ToolStripMenuItem)s).Checked = newState;
                SetFreeDesktopMode(newState);
            });
            freeModeItem.Checked = _config.IsFreeMode;
            menu.Items.Add(freeModeItem);

            ToolStripMenuItem compactModeItem = new ToolStripMenuItem(Loc.CompactMode, null, (s, e) => ToggleMode());
            compactModeItem.Checked = _config.IsCompactMode;
            menu.Items.Add(compactModeItem);

            menu.Items.Add(new ToolStripSeparator());

            // 2. Настройка шрифта
            ToolStripMenuItem fontItem = new ToolStripMenuItem(Loc.FontSettings, null, (s, e) => OpenFontDialog());
            menu.Items.Add(fontItem);

            // 3. Прозрачность (Slider 10% - 100%)
            OpacitySliderItem opacitySlider = new OpacitySliderItem(_config.OpacityPercent, val => SetOpacityPercent(val));
            menu.Items.Add(opacitySlider);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem wheelItem = new ToolStripMenuItem(Loc.VolumeWheel, null, (s, e) =>
            {
                _config.EnableVolumeWheel = !_config.EnableVolumeWheel;
                _config.Save();
            });
            wheelItem.Checked = _config.EnableVolumeWheel;
            menu.Items.Add(wheelItem);

            ToolStripMenuItem autoStartItem = new ToolStripMenuItem(Loc.AutoStart, null, (s, e) =>
            {
                _config.AutoStart = !_config.AutoStart;
                _config.Save();
            });
            autoStartItem.Checked = _config.AutoStart;
            menu.Items.Add(autoStartItem);

            ToolStripMenuItem resetPosItem = new ToolStripMenuItem(Loc.ResetPosition, null, (s, e) =>
            {
                if (_config.IsFreeMode)
                {
                    Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                    _config.FreeX = Math.Max(wa.Left, (wa.Width - Width) / 2);
                    _config.FreeY = Math.Max(wa.Top, wa.Top + 100);
                    _config.Save();
                    Location = new Point(_config.FreeX, _config.FreeY);
                    SetWindowPos(Handle, HWND_BOTTOM, _config.FreeX, _config.FreeY, Width, Height, SWP_NOSIZE | SWP_NOACTIVATE);
                    Redraw();
                }
                else
                {
                    _config.HasCustomTaskbarPos = false;
                    _config.TaskbarCustomX = -1;
                    _config.TaskbarCustomY = -1;
                    _config.Save();
                    UpdatePosition();
                }
            });
            menu.Items.Add(resetPosItem);

            menu.Items.Add(new ToolStripSeparator());

            // 4. Переключение языка (Language Switcher)
            ToolStripMenuItem langMenu = new ToolStripMenuItem(Loc.LanguageMenu);
            ToolStripMenuItem langRuItem = new ToolStripMenuItem(Loc.LangRu, null, (s, e) =>
            {
                _config.Language = "ru";
                Loc.CurrentLanguage = "ru";
                _config.Save();
                Redraw();
                if (OnLanguageChanged != null) OnLanguageChanged();
            });
            langRuItem.Checked = Loc.IsRussian;

            ToolStripMenuItem langEnItem = new ToolStripMenuItem(Loc.LangEn, null, (s, e) =>
            {
                _config.Language = "en";
                Loc.CurrentLanguage = "en";
                _config.Save();
                Redraw();
                if (OnLanguageChanged != null) OnLanguageChanged();
            });
            langEnItem.Checked = !Loc.IsRussian;

            langMenu.DropDownItems.Add(langRuItem);
            langMenu.DropDownItems.Add(langEnItem);
            menu.Items.Add(langMenu);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem githubItem = new ToolStripMenuItem(Loc.GitHubLink, null, (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/darquead") { UseShellExecute = true });
                }
                catch { }
            });
            menu.Items.Add(githubItem);

            ToolStripMenuItem exitItem = new ToolStripMenuItem(Loc.Exit, null, (s, e) => Application.Exit());
            menu.Items.Add(exitItem);

            return menu;
        }

        public void ShowContextMenu(Point clientLocation)
        {
            ContextMenuStrip menu = BuildContextMenu();
            menu.AutoClose = true;

            Point screenPt = PointToScreen(clientLocation);
            Size pref = menu.GetPreferredSize(Size.Empty);
            Rectangle screen = Screen.FromPoint(screenPt).WorkingArea;

            int showX = screenPt.X;
            int showY = screenPt.Y;

            // When anchored to taskbar (or near bottom of screen), popup upwards above the widget
            if (!_config.IsFreeMode || (showY + pref.Height > screen.Bottom))
            {
                showY = screenPt.Y - pref.Height - 6;
            }
            else
            {
                showY = screenPt.Y + 6;
            }

            // Ensure menu stays within screen horizontal boundaries
            if (showX + pref.Width > screen.Right)
            {
                showX = screen.Right - pref.Width - 6;
            }
            if (showX < screen.Left)
            {
                showX = screen.Left + 6;
            }
            if (showY < screen.Top)
            {
                showY = screen.Top + 6;
            }

            SetForegroundWindow(Handle);

            menu.Closed += (s, e) =>
            {
                _config.Save();
            };

            menu.Show(showX, showY);
            SetForegroundWindow(menu.Handle);
        }

        public void Redraw()
        {
            if (Width <= 0 || Height <= 0) return;

            try
            {
                int baseW = BaseWidth;
                int baseH = BaseHeight;

                using (Bitmap bmp = new Bitmap(baseW, baseH, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    // 1. Transparent subtle rounded glass background (tuned for TranslucentTB & Wallpaper Engine)
                    using (GraphicsPath bgPath = CreateRoundedRectPath(new Rectangle(0, 0, baseW - 1, baseH - 1), 5))
                    {
                        bool isHovered = (_hoverIndex != -1);

                        // Subtle transparent glass tint (lets animated wallpapers show through cleanly)
                        int bgAlpha = isHovered ? 35 : 20;
                        using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(bgAlpha, 255, 255, 255)))
                        {
                            g.FillPath(bgBrush, bgPath);
                        }

                        // Subtle dark contrast tint behind to ensure legibility over very bright wallpapers
                        int darkAlpha = isHovered ? 26 : 16;
                        using (SolidBrush darkTint = new SolidBrush(Color.FromArgb(darkAlpha, 0, 0, 0)))
                        {
                            g.FillPath(darkTint, bgPath);
                        }

                        // Delicate glass border
                        int borderAlpha = isHovered ? 48 : 26;
                        using (Pen borderPen = new Pen(Color.FromArgb(borderAlpha, 255, 255, 255), 1f))
                        {
                            g.DrawPath(borderPen, bgPath);
                        }
                    }

                    // 2. Button highlights
                    if (_hoverIndex == 0 || _pressedIndex == 0) DrawButtonGlow(g, PrevRect, _pressedIndex == 0);
                    if (_hoverIndex == 1 || _pressedIndex == 1) DrawButtonGlow(g, PlayRect, _pressedIndex == 1);
                    if (_hoverIndex == 2 || _pressedIndex == 2) DrawButtonGlow(g, NextRect, _pressedIndex == 2);

                    // 3. Draw media control glyphs (crisp and clean without drop shadow)
                    string prevGlyph = "\uE892"; // Previous
                    string playGlyph = _engine.CurrentInfo.IsPlaying ? "\uE769" : "\uE768"; // Pause / Play
                    string nextGlyph = "\uE893"; // Next

                    using (StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        using (SolidBrush iconBrush = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
                        {
                            g.DrawString(prevGlyph, _iconFont, iconBrush, PrevRect, sfCenter);
                            g.DrawString(playGlyph, _iconFont, iconBrush, PlayRect, sfCenter);
                            g.DrawString(nextGlyph, _iconFont, iconBrush, NextRect, sfCenter);
                        }
                    }

                    // 4. In Full Mode: draw divider, album cover art, and track info
                    if (!_config.IsCompactMode)
                    {
                        // Subtle vertical divider line
                        using (Pen divPen = new Pen(Color.FromArgb(28, 255, 255, 255), 1f))
                        {
                            g.DrawLine(divPen, 116, 6, 116, baseH - 6);
                        }

                        // Highlight for track info when hovered / pressed
                        if (_hoverIndex == 3 || _pressedIndex == 3)
                        {
                            Rectangle infoBg = new Rectangle(InfoRect.X + 2, InfoRect.Y, InfoRect.Width - 4, InfoRect.Height);
                            using (GraphicsPath p = CreateRoundedRectPath(infoBg, 4))
                            {
                                int alpha = (_pressedIndex == 3) ? 55 : 28;
                                using (SolidBrush hBrush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
                                {
                                    g.FillPath(hBrush, p);
                                }
                            }
                        }

                        // Volume feedback override OR Album cover art + Title
                        if (!string.IsNullOrEmpty(_volumeFeedbackText))
                        {
                            // Display volume feedback without shadow, strictly single-line
                            Rectangle volR = new Rectangle(InfoRect.X + 8, InfoRect.Y, InfoRect.Width - 12, InfoRect.Height);
                            using (StringFormat sfVol = new StringFormat { 
                                Alignment = StringAlignment.Near, 
                                LineAlignment = StringAlignment.Center,
                                FormatFlags = StringFormatFlags.NoWrap,
                                Trimming = StringTrimming.EllipsisCharacter
                            })
                            using (SolidBrush volBrush = new SolidBrush(Color.FromArgb(120, 210, 255)))
                            {
                                g.DrawString("🔊  " + _volumeFeedbackText, _textFont, volBrush, volR, sfVol);
                            }
                        }
                        else
                        {
                            // Album cover art: 26x26 px (scaled to match 36px height)
                            Rectangle coverRect = new Rectangle(InfoRect.X + 4, (baseH - 26) / 2, 26, 26);

                            if (_engine.CurrentInfo.Thumbnail != null)
                            {
                                using (GraphicsPath coverPath = CreateRoundedRectPath(coverRect, 4))
                                {
                                    g.SetClip(coverPath);
                                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                    g.SmoothingMode = SmoothingMode.HighQuality;
                                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                                    g.DrawImage(_engine.CurrentInfo.Thumbnail, coverRect);
                                    g.ResetClip();

                                    using (Pen coverBorder = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                                    {
                                        g.DrawPath(coverBorder, coverPath);
                                    }
                                }
                            }
                            else
                            {
                                // Placeholder tile when no thumbnail is provided
                                using (GraphicsPath coverPath = CreateRoundedRectPath(coverRect, 4))
                                {
                                    using (SolidBrush coverBg = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
                                    {
                                        g.FillPath(coverBg, coverPath);
                                    }
                                    using (Pen coverBorder = new Pen(Color.FromArgb(35, 255, 255, 255), 1f))
                                    {
                                        g.DrawPath(coverBorder, coverPath);
                                    }
                                    using (Font miniIcon = new Font(_iconFont.FontFamily, 7.5f, FontStyle.Regular))
                                    using (SolidBrush miniBrush = new SolidBrush(Color.FromArgb(180, 255, 255, 255)))
                                    using (StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                    {
                                        g.DrawString("\uE8D6", miniIcon, miniBrush, coverRect, sfCenter);
                                    }
                                }
                            }

                            // Build display text
                            string displayText;
                            Color textColor;

                            if (_engine.CurrentInfo.HasTrack)
                            {
                                if (!string.IsNullOrEmpty(_engine.CurrentInfo.Artist))
                                {
                                    displayText = _engine.CurrentInfo.Artist + " — " + _engine.CurrentInfo.Title;
                                }
                                else
                                {
                                    displayText = _engine.CurrentInfo.Title;
                                }

                                textColor = _engine.CurrentInfo.IsPlaying
                                    ? Color.FromArgb(245, 245, 245)
                                    : Color.FromArgb(195, 195, 200);
                            }
                            else
                            {
                                displayText = Loc.NoPlayback;
                                textColor = Color.FromArgb(145, 145, 150);
                            }

                            int textX = coverRect.Right + 8;
                            Rectangle textR = new Rectangle(textX, InfoRect.Y, InfoRect.Right - textX - 6, InfoRect.Height);

                            using (SolidBrush textBrush = new SolidBrush(textColor))
                            {
                                SizeF measured = g.MeasureString(displayText, _textFont);
                                if (measured.Width > textR.Width && _hoverIndex == 3 && _marqueeOffset > 0)
                                {
                                    // Smooth Marquee scroll when hovered
                                    g.SetClip(textR);
                                    int shift = _marqueeOffset % (int)(measured.Width + 40);
                                    g.DrawString(displayText, _textFont, textBrush, textR.X - shift, textR.Y + (textR.Height - measured.Height) / 2);
                                    g.DrawString(displayText, _textFont, textBrush, textR.X - shift + measured.Width + 40, textR.Y + (textR.Height - measured.Height) / 2);
                                    g.ResetClip();
                                }
                                else
                                {
                                    using (StringFormat sfText = new StringFormat { 
                                        Alignment = StringAlignment.Near, 
                                        LineAlignment = StringAlignment.Center, 
                                        FormatFlags = StringFormatFlags.NoWrap, 
                                        Trimming = StringTrimming.EllipsisCharacter 
                                    })
                                    {
                                        g.DrawString(displayText, _textFont, textBrush, textR, sfText);
                                    }
                                }
                            }
                        }
                    }
                }

                // If vertical taskbar, rotate the rendered widget 90 degrees clockwise
                if (_isVertical)
                {
                    bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
                }

                // Push to Windows Compositor via UpdateLayeredWindow
                IntPtr hdcScreen = GetDC(IntPtr.Zero);
                IntPtr hdcMem = CreateCompatibleDC(hdcScreen);
                IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                IntPtr hOld = SelectObject(hdcMem, hBmp);

                Point screenPos = PointToScreen(Point.Empty);
                SIZE size = new SIZE(Width, Height);
                POINT ptSrc = new POINT(0, 0);
                POINT ptDst = new POINT(screenPos.X, screenPos.Y);
                BLENDFUNCTION blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = _opacityAlpha,
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(Handle, hdcScreen, ref ptDst, ref size, hdcMem, ref ptSrc, 0, ref blend, ULW_ALPHA);

                SelectObject(hdcMem, hOld);
                DeleteObject(hBmp);
                DeleteDC(hdcMem);
                ReleaseDC(IntPtr.Zero, hdcScreen);
                }
            }
            catch (Exception ex)
            {
                Program.Log("Redraw error: " + ex);
            }
        }

        private static void DrawButtonGlow(Graphics g, Rectangle r, bool isPressed)
        {
            using (GraphicsPath path = CreateRoundedRectPath(r, 4))
            {
                int alpha = isPressed ? 70 : 38;
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
                {
                    g.FillPath(brush, path);
                }
            }
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            if (d <= 0) d = 1;

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (_mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
            if (disposing)
            {
                if (_pollTimer != null) { _pollTimer.Stop(); _pollTimer.Dispose(); }
                if (_marqueeTimer != null) { _marqueeTimer.Stop(); _marqueeTimer.Dispose(); }
                if (_volumeFeedbackTimer != null) { _volumeFeedbackTimer.Stop(); _volumeFeedbackTimer.Dispose(); }
                if (_iconFont != null) _iconFont.Dispose();
                if (_textFont != null) _textFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal class ModernDarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public ModernDarkMenuRenderer() : base(new ModernDarkColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected && e.Item.Enabled)
            {
                Rectangle r = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(55, 255, 255, 255)))
                {
                    e.Graphics.FillRectangle(b, r);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.FromArgb(240, 240, 240) : Color.FromArgb(130, 130, 135);
            base.OnRenderItemText(e);
        }
    }

    internal class ModernDarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(32, 32, 36); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(32, 32, 36); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(32, 32, 36); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(32, 32, 36); } }
        public override Color MenuBorder { get { return Color.FromArgb(60, 60, 65); } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color SeparatorDark { get { return Color.FromArgb(50, 50, 55); } }
        public override Color SeparatorLight { get { return Color.FromArgb(25, 25, 28); } }
    }

    internal class OpacitySliderItem : ToolStripControlHost
    {
        public OpacitySliderItem(int initialPercent, Action<int> onValueChanged)
            : base(CreatePanel(initialPercent, onValueChanged))
        {
            AutoSize = false;
            Size = new Size(210, 52);
        }

        private static Control CreatePanel(int initialPercent, Action<int> onValueChanged)
        {
            Panel p = new Panel
            {
                Size = new Size(210, 52),
                BackColor = Color.FromArgb(32, 32, 36),
                Padding = new Padding(8, 3, 8, 2)
            };

            Label lbl = new Label
            {
                Text = Loc.OpacityLabel(initialPercent),
                ForeColor = Color.FromArgb(235, 235, 240),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Dock = DockStyle.Top,
                Height = 18,
                TextAlign = ContentAlignment.MiddleLeft
            };

            TrackBar tb = new TrackBar
            {
                Minimum = 10,
                Maximum = 100,
                Value = Math.Max(10, Math.Min(100, initialPercent)),
                TickFrequency = 10,
                SmallChange = 5,
                LargeChange = 10,
                Dock = DockStyle.Bottom,
                Height = 26,
                AutoSize = false
            };

            tb.ValueChanged += (s, e) =>
            {
                lbl.Text = Loc.OpacityLabel(tb.Value);
                if (onValueChanged != null) onValueChanged(tb.Value);
            };

            p.Controls.Add(tb);
            p.Controls.Add(lbl);
            return p;
        }
    }
}
