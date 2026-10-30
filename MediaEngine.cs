using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace TaskbarMediaWidget
{
    public enum PlaybackState
    {
        Unknown = 0,
        Closed = 1,
        Stopped = 2,
        Playing = 3,
        Paused = 4
    }

    public class MediaInfo
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string AppId { get; set; }
        public PlaybackState State { get; set; }
        public Bitmap Thumbnail { get; set; }

        public MediaInfo()
        {
            Title = string.Empty;
            Artist = string.Empty;
            AppId = string.Empty;
            State = PlaybackState.Unknown;
            Thumbnail = null;
        }

        public bool IsPlaying
        {
            get { return State == PlaybackState.Playing; }
        }

        public bool HasTrack
        {
            get { return !string.IsNullOrEmpty(Title); }
        }
    }

    public class MediaEngine
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const byte VK_VOLUME_MUTE = 0xAD;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_VOLUME_UP = 0xAF;
        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_STOP = 0xB2;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const int SW_RESTORE = 9;

        private dynamic _sessionManager;
        private dynamic _currentSession;
        private MethodInfo _asTaskGeneric;
        private MethodInfo _asStreamMethod;
        private Type _propType;
        private Type _streamType;
        private readonly object _lock = new object();
        private bool _smtcInitialized;

        public MediaInfo CurrentInfo { get; private set; }

        public event Action OnMediaChanged;

        public MediaEngine()
        {
            CurrentInfo = new MediaInfo();
            ThreadPool.QueueUserWorkItem(state => InitSmtc());
        }

        private void InitSmtc()
        {
            try
            {
                Assembly rtAsm = Assembly.Load("System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                Type extType = rtAsm.GetType("System.WindowsRuntimeSystemExtensions");
                foreach (MethodInfo m in extType.GetMethods())
                {
                    if (m.Name == "AsTask" && m.IsGenericMethod && m.GetParameters().Length == 1)
                    {
                        _asTaskGeneric = m;
                        break;
                    }
                }

                Type mgrType = Type.GetType("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media, ContentType=WindowsRuntime");
                _propType = Type.GetType("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties, Windows.Media, ContentType=WindowsRuntime");

                if (mgrType != null && _asTaskGeneric != null)
                {
                    MethodInfo reqMethod = mgrType.GetMethod("RequestAsync", Type.EmptyTypes);
                    object asyncOp = reqMethod.Invoke(null, null);
                    _sessionManager = AwaitWinRt(asyncOp, mgrType);
                    _smtcInitialized = true;
                }
                Program.Log("InitSmtc: ok=" + _smtcInitialized + ", mgr=" + (_sessionManager != null));
            }
            catch (Exception ex)
            {
                _smtcInitialized = false;
                Program.Log("InitSmtc ex: " + ex);
            }

            Refresh();
        }

        private object AwaitWinRt(object asyncOp, Type resultType)
        {
            if (_asTaskGeneric == null || asyncOp == null) return null;
            try
            {
                MethodInfo method = _asTaskGeneric.MakeGenericMethod(resultType);
                dynamic task = method.Invoke(null, new object[] { asyncOp });
                task.Wait(1200);
                return task.Result;
            }
            catch
            {
                return null;
            }
        }

        private Stream ConvertToNetStream(object winRtStream)
        {
            if (_asStreamMethod == null)
            {
                Assembly rtAsm = Assembly.Load("System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                Type extType = rtAsm.GetType("System.IO.WindowsRuntimeStreamExtensions");
                foreach (MethodInfo m in extType.GetMethods())
                {
                    if (m.Name == "AsStreamForRead" && m.GetParameters().Length == 1)
                    {
                        _asStreamMethod = m;
                        break;
                    }
                }
            }
            return (Stream)_asStreamMethod.Invoke(null, new object[] { winRtStream });
        }

        private Bitmap ExtractThumbnail(dynamic props)
        {
            try
            {
                if (props == null) return null;
                dynamic thumb = props.Thumbnail;
                if (thumb == null) return null;

                if (_streamType == null)
                {
                    _streamType = Type.GetType("Windows.Storage.Streams.IRandomAccessStreamWithContentType, Windows.Storage.Streams, ContentType=WindowsRuntime");
                    if (_streamType == null)
                    {
                        _streamType = Type.GetType("Windows.Storage.Streams.IRandomAccessStreamWithContentType, Windows, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime");
                    }
                }

                dynamic streamOp = thumb.OpenReadAsync();
                object winRtStream = AwaitWinRt(streamOp, _streamType);
                if (winRtStream == null) return null;

                using (Stream netStream = ConvertToNetStream(winRtStream))
                {
                    if (netStream == null || netStream.Length == 0) return null;
                    using (Image original = Image.FromStream(netStream))
                    {
                        Bitmap bmp = new Bitmap(48, 48, PixelFormat.Format32bppPArgb);
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(original, new Rectangle(0, 0, 48, 48));
                        }
                        return bmp;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log("ExtractThumbnail error: " + ex.Message);
                return null;
            }
        }

        public void Refresh()
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                lock (_lock)
                {
                    MediaInfo info = new MediaInfo();

                    if (_smtcInitialized && _sessionManager != null)
                    {
                        try
                        {
                            dynamic session = _sessionManager.GetCurrentSession();
                            _currentSession = session;

                            if (session != null)
                            {
                                info.AppId = (string)session.SourceAppUserModelId;

                                dynamic propOp = session.TryGetMediaPropertiesAsync();
                                dynamic props = AwaitWinRt(propOp, _propType);
                                if (props != null)
                                {
                                    info.Title = (string)props.Title;
                                    info.Artist = (string)props.Artist;

                                    bool trackChanged = (CurrentInfo.Title != info.Title || CurrentInfo.Artist != info.Artist || CurrentInfo.Thumbnail == null);
                                    if (trackChanged && !string.IsNullOrEmpty(info.Title))
                                    {
                                        info.Thumbnail = ExtractThumbnail(props);
                                    }
                                    else
                                    {
                                        info.Thumbnail = CurrentInfo.Thumbnail;
                                    }
                                }

                                dynamic playback = session.GetPlaybackInfo();
                                if (playback != null)
                                {
                                    int statusVal = (int)playback.PlaybackStatus;
                                    // Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus:
                                    // 0=Closed, 1=Opened, 2=Changing, 3=Stopped, 4=Playing, 5=Paused
                                    if (statusVal == 4) info.State = PlaybackState.Playing;
                                    else if (statusVal == 5) info.State = PlaybackState.Paused;
                                    else if (statusVal == 3) info.State = PlaybackState.Stopped;
                                    else info.State = PlaybackState.Closed;
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    bool changed = (CurrentInfo.Title != info.Title ||
                                    CurrentInfo.Artist != info.Artist ||
                                    CurrentInfo.State != info.State ||
                                    CurrentInfo.AppId != info.AppId ||
                                    CurrentInfo.Thumbnail != info.Thumbnail);

                    if (changed)
                    {
                        Program.Log("Media changed: " + info.Artist + " - " + info.Title + " [" + info.State + "] " + info.AppId + ", thumb=" + (info.Thumbnail != null));
                    }

                    Bitmap oldThumb = (CurrentInfo.Thumbnail != info.Thumbnail) ? CurrentInfo.Thumbnail : null;
                    CurrentInfo = info;
                    if (oldThumb != null)
                    {
                        try { oldThumb.Dispose(); } catch { }
                    }

                    if (changed && OnMediaChanged != null)
                    {
                        OnMediaChanged();
                    }
                }
            });
        }

        public void TogglePlayPause()
        {
            Program.Log("TogglePlayPause requested");
            bool handled = false;
            dynamic session = null;
            if (_sessionManager != null)
            {
                try
                {
                    session = _sessionManager.GetCurrentSession();
                }
                catch { }
            }
            if (session == null) session = _currentSession;

            if (session != null)
            {
                try
                {
                    session.TryTogglePlayPauseAsync();
                    handled = true;
                    Program.Log("TogglePlayPause executed via SMTC");
                }
                catch (Exception ex)
                {
                    Program.Log("TogglePlayPause SMTC error: " + ex.Message);
                }
            }

            if (!handled)
            {
                Program.Log("TogglePlayPause executed via VK_MEDIA_PLAY_PAUSE");
                SendKey(VK_MEDIA_PLAY_PAUSE);
            }

            // Immediately toggle visual state predictively for instant feedback
            if (CurrentInfo.State == PlaybackState.Playing)
            {
                CurrentInfo.State = PlaybackState.Paused;
            }
            else if (CurrentInfo.State == PlaybackState.Paused)
            {
                CurrentInfo.State = PlaybackState.Playing;
            }

            if (OnMediaChanged != null) OnMediaChanged();

            // Refresh after a short delay to get the true hardware/SMTC state
            Timer t = null;
            t = new Timer(s =>
            {
                Refresh();
                t.Dispose();
            }, null, 250, Timeout.Infinite);
        }

        public void NextTrack()
        {
            Program.Log("NextTrack requested");
            bool handled = false;
            dynamic session = null;
            if (_sessionManager != null)
            {
                try
                {
                    session = _sessionManager.GetCurrentSession();
                }
                catch { }
            }
            if (session == null) session = _currentSession;

            if (session != null)
            {
                try
                {
                    session.TrySkipNextAsync();
                    handled = true;
                    Program.Log("NextTrack executed via SMTC");
                }
                catch (Exception ex)
                {
                    Program.Log("NextTrack SMTC error: " + ex.Message);
                }
            }

            if (!handled)
            {
                Program.Log("NextTrack executed via VK_MEDIA_NEXT_TRACK");
                SendKey(VK_MEDIA_NEXT_TRACK);
            }

            Timer t = null;
            t = new Timer(s =>
            {
                Refresh();
                t.Dispose();
            }, null, 300, Timeout.Infinite);
        }

        public void PrevTrack()
        {
            Program.Log("PrevTrack requested");
            bool handled = false;
            dynamic session = null;
            if (_sessionManager != null)
            {
                try
                {
                    session = _sessionManager.GetCurrentSession();
                }
                catch { }
            }
            if (session == null) session = _currentSession;

            if (session != null)
            {
                try
                {
                    session.TrySkipPreviousAsync();
                    handled = true;
                    Program.Log("PrevTrack executed via SMTC");
                }
                catch (Exception ex)
                {
                    Program.Log("PrevTrack SMTC error: " + ex.Message);
                }
            }

            if (!handled)
            {
                Program.Log("PrevTrack executed via VK_MEDIA_PREV_TRACK");
                SendKey(VK_MEDIA_PREV_TRACK);
            }

            Timer t = null;
            t = new Timer(s =>
            {
                Refresh();
                t.Dispose();
            }, null, 300, Timeout.Infinite);
        }

        public void VolumeUp()
        {
            SendKey(VK_VOLUME_UP);
        }

        public void VolumeDown()
        {
            SendKey(VK_VOLUME_DOWN);
        }

        private static void SendKey(byte vk)
        {
            keybd_event(vk, 0, 0, UIntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public void ActivatePlayerWindow()
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    IntPtr hDesk = OpenDesktop("Default", 0, false, 0x01FF);
                    if (hDesk != IntPtr.Zero)
                    {
                        SetThreadDesktop(hDesk);
                        CloseDesktop(hDesk);
                    }

                    string appId = CurrentInfo.AppId;
                    string procSearch = "Spotify";

                    if (!string.IsNullOrEmpty(appId))
                    {
                        if (appId.IndexOf("Spotify", StringComparison.OrdinalIgnoreCase) >= 0) procSearch = "Spotify";
                        else if (appId.IndexOf("chrome", StringComparison.OrdinalIgnoreCase) >= 0) procSearch = "chrome";
                        else if (appId.IndexOf("msedge", StringComparison.OrdinalIgnoreCase) >= 0) procSearch = "msedge";
                        else if (appId.IndexOf("firefox", StringComparison.OrdinalIgnoreCase) >= 0) procSearch = "firefox";
                        else if (appId.IndexOf("YandexMusic", StringComparison.OrdinalIgnoreCase) >= 0) procSearch = "YandexMusic";
                        else if (appId.IndexOf("AIMP", StringComparison.OrdinalIgnoreCase) >= 0) procSearch = "AIMP";
                        else
                        {
                            int dot = appId.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                            if (dot > 0) procSearch = appId.Substring(0, dot);
                        }
                    }

                    Process[] procs = Process.GetProcessesByName(procSearch);
                    if (procs.Length == 0)
                    {
                        // Fallback: search Spotify
                        procs = Process.GetProcessesByName("Spotify");
                    }

                    IntPtr foundHwnd = IntPtr.Zero;
                    try
                    {
                        EnumWindows((hWnd, lParam) =>
                        {
                            if (!IsWindowVisible(hWnd)) return true;
                            uint pid;
                            GetWindowThreadProcessId(hWnd, out pid);
                            foreach (Process p in procs)
                            {
                                if (p.Id == pid)
                                {
                                    StringBuilder title = new StringBuilder(256);
                                    GetWindowText(hWnd, title, 256);
                                    if (title.Length > 0)
                                    {
                                        foundHwnd = hWnd;
                                        return false; // Stop enumeration
                                    }
                                }
                            }
                            return true;
                        }, IntPtr.Zero);
                    }
                    finally
                    {
                        foreach (Process p in procs)
                        {
                            try { p.Dispose(); } catch { }
                        }
                    }

                    if (foundHwnd != IntPtr.Zero)
                    {
                        ShowWindow(foundHwnd, SW_RESTORE);
                        SetForegroundWindow(foundHwnd);
                    }
                }
                catch
                {
                }
            });
        }
    }
}
