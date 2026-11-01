using System;
using System.Collections.Generic;
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

    public class MediaAppItem
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public bool IsPlaying { get; set; }
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

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int WM_APPCOMMAND = 0x0319;
        private const int APPCOMMAND_MEDIA_NEXTTRACK = 11;
        private const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;
        private const int APPCOMMAND_MEDIA_STOP = 13;
        private const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;

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
        private MethodInfo _asTaskGeneric;
        private MethodInfo _asStreamMethod;
        private Type _propType;
        private Type _streamType;
        private readonly object _lock = new object();
        private bool _smtcInitialized;

        private int _isRefreshing = 0;
        private int _lastPropertyFetchTick = 0;
        private bool _forcePropertiesCheck = true;
        private string _cachedSessionAppId = null;
        private PlaybackState _cachedPlaybackState = PlaybackState.Closed;

        public static void SafeReleaseCom(object obj)
        {
            if (obj != null && Marshal.IsComObject(obj))
            {
                try { Marshal.FinalReleaseComObject(obj); } catch { }
            }
        }

        public MediaInfo CurrentInfo { get; private set; }
        public string TargetAppId { get; set; }

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
                task.Wait(1000);
                return task.Result;
            }
            catch
            {
                return null;
            }
            finally
            {
                SafeReleaseCom(asyncOp);
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
            if (props == null) return null;
            dynamic thumb = null;
            object winRtStream = null;
            try
            {
                thumb = props.Thumbnail;
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
                winRtStream = AwaitWinRt(streamOp, _streamType);
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
            finally
            {
                SafeReleaseCom(winRtStream);
                SafeReleaseCom(thumb);
            }
        }

        public void Refresh()
        {
            if (Interlocked.CompareExchange(ref _isRefreshing, 1, 0) != 0)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    lock (_lock)
                    {
                        MediaInfo info = new MediaInfo();

                        if (_smtcInitialized && _sessionManager != null)
                        {
                            dynamic session = null;
                            try
                            {
                                session = GetActiveSession();

                                if (session != null)
                                {
                                    string appId = "";
                                    try { appId = (string)session.SourceAppUserModelId; } catch { }
                                    info.AppId = appId;

                                    // 1. Get playback status
                                    dynamic playback = null;
                                    try
                                    {
                                        playback = session.GetPlaybackInfo();
                                        if (playback != null)
                                        {
                                            int statusVal = (int)playback.PlaybackStatus;
                                            // 0=Closed, 1=Opened, 2=Changing, 3=Stopped, 4=Playing, 5=Paused
                                            if (statusVal == 4) info.State = PlaybackState.Playing;
                                            else if (statusVal == 5) info.State = PlaybackState.Paused;
                                            else if (statusVal == 3) info.State = PlaybackState.Stopped;
                                            else info.State = PlaybackState.Closed;
                                        }
                                    }
                                    catch { }
                                    finally
                                    {
                                        SafeReleaseCom(playback);
                                    }

                                    // 2. Query heavyweight media properties only when track or state changes, or periodic heartbeat
                                    int now = Environment.TickCount;
                                    bool stateChanged = (info.State != _cachedPlaybackState);
                                    bool appChanged = !string.Equals(info.AppId, _cachedSessionAppId, StringComparison.OrdinalIgnoreCase);
                                    bool periodElapsed = (now - _lastPropertyFetchTick > 2500) || (_lastPropertyFetchTick == 0);
                                    bool needsFetch = _forcePropertiesCheck || appChanged || (stateChanged && info.State == PlaybackState.Playing) || periodElapsed;

                                    if (needsFetch)
                                    {
                                        dynamic propOp = null;
                                        dynamic props = null;
                                        try
                                        {
                                            propOp = session.TryGetMediaPropertiesAsync();
                                            props = AwaitWinRt(propOp, _propType);
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
                                        }
                                        catch { }
                                        finally
                                        {
                                            SafeReleaseCom(props);
                                            _lastPropertyFetchTick = now;
                                            _forcePropertiesCheck = false;
                                            _cachedSessionAppId = info.AppId;
                                            _cachedPlaybackState = info.State;
                                        }
                                    }
                                    else
                                    {
                                        // Reuse cached track metadata without invoking WinRT async properties
                                        info.Title = CurrentInfo.Title;
                                        info.Artist = CurrentInfo.Artist;
                                        info.Thumbnail = CurrentInfo.Thumbnail;
                                    }
                                }
                                else if (!string.IsNullOrEmpty(TargetAppId))
                                {
                                    string pName = CleanAppKey(TargetAppId).Replace(".exe", "");
                                    bool procFound = false;
                                    try
                                    {
                                        Process[] procs = Process.GetProcessesByName(pName);
                                        if (procs != null && procs.Length > 0)
                                        {
                                            procFound = true;
                                            string winTitle = "";
                                            foreach (Process p in procs)
                                            {
                                                try
                                                {
                                                    if (!string.IsNullOrEmpty(p.MainWindowTitle))
                                                    {
                                                        winTitle = p.MainWindowTitle;
                                                        break;
                                                    }
                                                }
                                                catch { }
                                            }
                                            foreach (Process p in procs) { try { p.Dispose(); } catch { } }

                                            info.AppId = TargetAppId;
                                            info.State = PlaybackState.Paused;
                                            if (!string.IsNullOrEmpty(winTitle))
                                            {
                                                int dash = winTitle.IndexOf(" - ");
                                                if (dash > 0)
                                                {
                                                    info.Artist = winTitle.Substring(0, dash).Trim();
                                                    info.Title = winTitle.Substring(dash + 3).Trim();
                                                }
                                                else
                                                {
                                                    info.Title = winTitle;
                                                    info.Artist = FormatFriendlyAppName(TargetAppId);
                                                }
                                            }
                                            else
                                            {
                                                info.Title = FormatFriendlyAppName(TargetAppId);
                                                info.Artist = string.Empty;
                                            }
                                        }
                                    }
                                    catch { }

                                    if (!procFound)
                                    {
                                        info.AppId = TargetAppId;
                                        info.State = PlaybackState.Closed;
                                        info.Title = string.Empty;
                                        info.Artist = string.Empty;
                                        info.Thumbnail = null;
                                    }
                                }
                            }
                            catch
                            {
                            }
                            finally
                            {
                                SafeReleaseCom(session);
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
                }
                finally
                {
                    Interlocked.Exchange(ref _isRefreshing, 0);
                }
            });
        }

        public List<MediaAppItem> GetAvailableMediaApps()
        {
            Dictionary<string, MediaAppItem> map = new Dictionary<string, MediaAppItem>(StringComparer.OrdinalIgnoreCase);

            // 1. Enumerate SMTC sessions
            if (_smtcInitialized && _sessionManager != null)
            {
                dynamic sessions = null;
                try
                {
                    sessions = _sessionManager.GetSessions();
                    if (sessions != null)
                    {
                        foreach (dynamic s in sessions)
                        {
                            if (s == null) continue;
                            try
                            {
                                string appId = (string)s.SourceAppUserModelId;
                                if (string.IsNullOrEmpty(appId)) continue;

                                string friendly = FormatFriendlyAppName(appId);
                                bool isPlaying = false;
                                dynamic pb = null;
                                try
                                {
                                    pb = s.GetPlaybackInfo();
                                    if (pb != null && (int)pb.PlaybackStatus == 4) isPlaying = true;
                                }
                                catch { }
                                finally
                                {
                                    SafeReleaseCom(pb);
                                }

                                string key = CleanAppKey(appId);
                                map[key] = new MediaAppItem
                                {
                                    Id = key,
                                    DisplayName = friendly,
                                    IsPlaying = isPlaying
                                };
                            }
                            catch { }
                            finally
                            {
                                SafeReleaseCom(s);
                            }
                        }
                    }
                }
                catch { }
                finally
                {
                    SafeReleaseCom(sessions);
                }
            }

            // 2. Query active WASAPI audio apps
            try
            {
                List<AppVolumeManager.AudioAppInfo> audioApps = AppVolumeManager.GetActiveAudioApps();
                foreach (AppVolumeManager.AudioAppInfo a in audioApps)
                {
                    string key = CleanAppKey(a.Id);
                    if (!map.ContainsKey(key))
                    {
                        map[key] = new MediaAppItem
                        {
                            Id = key,
                            DisplayName = a.DisplayName,
                            IsPlaying = a.IsActiveAudio
                        };
                    }
                    else if (a.IsActiveAudio)
                    {
                        map[key].IsPlaying = true;
                    }
                }
            }
            catch { }

            // 3. If a TargetAppId is already configured in Config but not detected right now, preserve it in the list
            if (!string.IsNullOrEmpty(TargetAppId))
            {
                string key = CleanAppKey(TargetAppId);
                if (!map.ContainsKey(key))
                {
                    map[key] = new MediaAppItem
                    {
                        Id = key,
                        DisplayName = FormatFriendlyAppName(key),
                        IsPlaying = false
                    };
                }
            }

            List<MediaAppItem> list = new List<MediaAppItem>();
            foreach (KeyValuePair<string, MediaAppItem> kvp in map)
            {
                list.Add(kvp.Value);
            }

            // Sort: playing first, then alphabetical
            list.Sort((a, b) =>
            {
                if (a.IsPlaying != b.IsPlaying) return a.IsPlaying ? -1 : 1;
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            return list;
        }

        public static string CleanAppKey(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            int slash = raw.LastIndexOf('\\');
            if (slash >= 0 && slash < raw.Length - 1) raw = raw.Substring(slash + 1);
            int bang = raw.IndexOf('!');
            if (bang >= 0) raw = raw.Substring(0, bang);
            if (!raw.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) raw += ".exe";
            return raw;
        }

        public static string FormatFriendlyAppName(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return "Unknown";
            string lower = appId.ToLowerInvariant();
            if (lower.Contains("spotify")) return "Spotify";
            if (lower.Contains("chrome")) return "Google Chrome";
            if (lower.Contains("msedge")) return "Microsoft Edge";
            if (lower.Contains("firefox")) return "Mozilla Firefox";
            if (lower.Contains("telegram")) return "Telegram";
            if (lower.Contains("yandexmusic") || lower.Contains("yandex.music")) return "Яндекс Музыка (Yandex Music)";
            if (lower.Contains("zunemusic")) return "Windows Media Player";
            if (lower.Contains("aimp")) return "AIMP";
            if (lower.Contains("foobar2000")) return "foobar2000";
            if (lower.Contains("vlc")) return "VLC Media Player";
            if (lower.Contains("discord")) return "Discord";

            string clean = CleanAppKey(appId);
            if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(0, clean.Length - 4);
            }
            return clean;
        }

        private dynamic GetActiveSession()
        {
            if (_sessionManager == null) return null;

            if (!string.IsNullOrEmpty(TargetAppId))
            {
                string search = CleanAppKey(TargetAppId).Replace(".exe", "").ToLowerInvariant();
                dynamic sessions = null;
                try
                {
                    sessions = _sessionManager.GetSessions();
                    if (sessions != null)
                    {
                        dynamic foundSession = null;
                        foreach (dynamic s in sessions)
                        {
                            if (s == null) continue;
                            try
                            {
                                if (foundSession == null)
                                {
                                    string appId = (string)s.SourceAppUserModelId;
                                    if (!string.IsNullOrEmpty(appId) && appId.ToLowerInvariant().Contains(search))
                                    {
                                        foundSession = s;
                                        continue;
                                    }
                                }
                            }
                            catch { }
                            SafeReleaseCom(s);
                        }
                        return foundSession;
                    }
                }
                catch { }
                finally
                {
                    SafeReleaseCom(sessions);
                }

                // Specific target app is selected, but it has no active SMTC session
                return null;
            }

            try
            {
                dynamic cur = _sessionManager.GetCurrentSession();
                if (cur != null) return cur;
            }
            catch { }

            dynamic allSessions = null;
            try
            {
                allSessions = _sessionManager.GetSessions();
                if (allSessions != null)
                {
                    dynamic bestSession = null;
                    foreach (dynamic s in allSessions)
                    {
                        if (s == null) continue;
                        try
                        {
                            dynamic playback = s.GetPlaybackInfo();
                            int statusVal = 0;
                            if (playback != null)
                            {
                                try { statusVal = (int)playback.PlaybackStatus; } catch { }
                                SafeReleaseCom(playback);
                            }

                            if (statusVal == 4 && bestSession == null) // Playing
                            {
                                bestSession = s;
                                continue;
                            }
                            if (statusVal == 5 && bestSession == null) // Paused
                            {
                                bestSession = s;
                                continue;
                            }
                        }
                        catch { }

                        if (s != bestSession)
                        {
                            SafeReleaseCom(s);
                        }
                    }

                    if (bestSession != null) return bestSession;

                    foreach (dynamic s in allSessions)
                    {
                        if (s != null) return s;
                    }
                }
            }
            catch { }
            finally
            {
                SafeReleaseCom(allSessions);
            }

            return null;
        }

        private void SendAppCommandToProcess(string targetAppId, int appCommand)
        {
            string search = CleanAppKey(targetAppId).Replace(".exe", "").ToLowerInvariant();
            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    if (IsWindowVisible(hWnd))
                    {
                        uint pid;
                        GetWindowThreadProcessId(hWnd, out pid);
                        if (pid != 0)
                        {
                            try
                            {
                                using (Process p = Process.GetProcessById((int)pid))
                                {
                                    string pName = p.ProcessName.ToLowerInvariant();
                                    if (pName.Contains(search))
                                    {
                                        SendMessage(hWnd, (uint)WM_APPCOMMAND, hWnd, (IntPtr)(appCommand << 16));
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }

        private void SendMediaCommand(int appCommand, byte vk)
        {
            // 1. Try sending directly to Spotify or media player window (bypasses UIPI when elevated game is active)
            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    if (IsWindowVisible(hWnd))
                    {
                        uint pid;
                        GetWindowThreadProcessId(hWnd, out pid);
                        if (pid != 0)
                        {
                            try
                            {
                                using (Process p = Process.GetProcessById((int)pid))
                                {
                                    string pName = p.ProcessName.ToLowerInvariant();
                                    if (pName == "spotify")
                                    {
                                        SendMessage(hWnd, (uint)WM_APPCOMMAND, hWnd, (IntPtr)(appCommand << 16));
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }

            // 2. Broadcast synthetic key event as universal fallback
            SendKey(vk);
        }

        public void TogglePlayPause()
        {
            Program.Log("TogglePlayPause requested");
            bool handled = false;
            dynamic session = GetActiveSession();

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
                finally
                {
                    SafeReleaseCom(session);
                }
            }

            if (!handled)
            {
                if (!string.IsNullOrEmpty(TargetAppId))
                {
                    Program.Log("TogglePlayPause executed via target window appcommand: " + TargetAppId);
                    SendAppCommandToProcess(TargetAppId, APPCOMMAND_MEDIA_PLAY_PAUSE);
                }
                else
                {
                    Program.Log("TogglePlayPause executed via fallback");
                    SendMediaCommand(APPCOMMAND_MEDIA_PLAY_PAUSE, VK_MEDIA_PLAY_PAUSE);
                }
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

            _forcePropertiesCheck = true;
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
            dynamic session = GetActiveSession();

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
                finally
                {
                    SafeReleaseCom(session);
                }
            }

            if (!handled)
            {
                if (!string.IsNullOrEmpty(TargetAppId))
                {
                    Program.Log("NextTrack executed via target window appcommand: " + TargetAppId);
                    SendAppCommandToProcess(TargetAppId, APPCOMMAND_MEDIA_NEXTTRACK);
                }
                else
                {
                    Program.Log("NextTrack executed via fallback");
                    SendMediaCommand(APPCOMMAND_MEDIA_NEXTTRACK, VK_MEDIA_NEXT_TRACK);
                }
            }

            _forcePropertiesCheck = true;
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
            dynamic session = GetActiveSession();

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
                finally
                {
                    SafeReleaseCom(session);
                }
            }

            if (!handled)
            {
                if (!string.IsNullOrEmpty(TargetAppId))
                {
                    Program.Log("PrevTrack executed via target window appcommand: " + TargetAppId);
                    SendAppCommandToProcess(TargetAppId, APPCOMMAND_MEDIA_PREVIOUSTRACK);
                }
                else
                {
                    Program.Log("PrevTrack executed via fallback");
                    SendMediaCommand(APPCOMMAND_MEDIA_PREVIOUSTRACK, VK_MEDIA_PREV_TRACK);
                }
            }

            _forcePropertiesCheck = true;
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

                    string appId = !string.IsNullOrEmpty(TargetAppId) ? TargetAppId : CurrentInfo.AppId;
                    string procSearch = "Spotify";

                    if (!string.IsNullOrEmpty(appId))
                    {
                        procSearch = CleanAppKey(appId).Replace(".exe", "");
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
