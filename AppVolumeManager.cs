using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TaskbarMediaWidget
{
    public static class AppVolumeManager
    {
        private const uint ClsCtxAll = 23;

        [DllImport("ole32.dll", ExactSpelling = true)]
        private static extern int CoCreateInstance(
            [In] ref Guid rclsid,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnkOuter,
            uint dwClsContext,
            [In] ref Guid riid,
            [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid id, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
            [PreserveSig] int OpenPropertyStore(uint stgmAccess, out IntPtr properties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out IntPtr strId);
            [PreserveSig] int GetState(out int state);
        }

        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            [PreserveSig] int GetAudioSessionControl(ref Guid audioSessionGuid, uint streamFlags, out IAudioSessionControl sessionControl);
            [PreserveSig] int GetSimpleAudioVolume(ref Guid audioSessionGuid, uint streamFlags, out ISimpleAudioVolume audioVolume);
            [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);
        }

        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            [PreserveSig] int GetCount(out int sessionCount);
            [PreserveSig] int GetSession(int sessionIndex, out IAudioSessionControl session);
        }

        [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl
        {
            [PreserveSig] int GetState(out int state);
            [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);
        }

        [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            [PreserveSig] int GetState(out int state);
            [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);
            [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);
            [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);
            [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);
            [PreserveSig] int GetGroupingParam(out Guid groupingParam);
            [PreserveSig] int SetGroupingParam(ref Guid groupingParam, ref Guid eventContext);
            [PreserveSig] int RegisterAudioSessionNotification(IntPtr newNotifications);
            [PreserveSig] int UnregisterAudioSessionNotification(IntPtr newNotifications);
            [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);
            [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);
            [PreserveSig] int GetProcessId(out uint retVal);
            [PreserveSig] int IsSystemSoundsSession();
            [PreserveSig] int SetDuckingPreference(bool optOut);
        }

        [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISimpleAudioVolume
        {
            [PreserveSig] int SetMasterVolume(float level, ref Guid eventContext);
            [PreserveSig] int GetMasterVolume(out float level);
            [PreserveSig] int SetMute(bool muted, ref Guid eventContext);
            [PreserveSig] int GetMute(out bool muted);
        }

        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioMeterInformation
        {
            [PreserveSig] int GetPeakValue(out float peak);
            [PreserveSig] int GetMeteringChannelCount(out uint channelCount);
            [PreserveSig] int GetChannelsPeakValues(uint channelCount, [Out] float[] peakValues);
            [PreserveSig] int QueryHardwareSupport(out uint hardwareSupportMask);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr client);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr client);
            [PreserveSig] int GetChannelCount(out uint channelCount);
            [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
            [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetMute(bool muted, ref Guid eventContext);
            [PreserveSig] int GetMute(out bool muted);
        }

        private class AppSessionInfo
        {
            public uint Pid;
            public string ProcessName;
            public string DisplayTitle;
            public float Peak;
            public float Volume;
            public bool Muted;
            public ISimpleAudioVolume VolumeCtrl;
        }

        public static string ChangeVolume(int delta, string mediaAppId)
        {
            try
            {
                Guid clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
                Guid iid = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
                object obj;
                int hr = CoCreateInstance(ref clsid, null, ClsCtxAll, ref iid, out obj);
                if (hr < 0 || obj == null) return FallbackMaster(delta);

                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)obj;
                IMMDevice dev;
                hr = enumerator.GetDefaultAudioEndpoint(0, 0, out dev);
                if (hr < 0 || dev == null) return FallbackMaster(delta);

                Guid mgrIid = typeof(IAudioSessionManager2).GUID;
                object mgrObj;
                hr = dev.Activate(ref mgrIid, ClsCtxAll, IntPtr.Zero, out mgrObj);
                if (hr < 0 || mgrObj == null) return FallbackMaster(delta);

                IAudioSessionManager2 mgr = (IAudioSessionManager2)mgrObj;
                IAudioSessionEnumerator sessionEnum;
                hr = mgr.GetSessionEnumerator(out sessionEnum);
                if (hr < 0 || sessionEnum == null) return FallbackMaster(delta);

                int count;
                sessionEnum.GetCount(out count);

                List<AppSessionInfo> sessions = new List<AppSessionInfo>();

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl ctrl;
                    if (sessionEnum.GetSession(i, out ctrl) < 0 || ctrl == null) continue;

                    IAudioSessionControl2 ctrl2 = ctrl as IAudioSessionControl2;
                    ISimpleAudioVolume vol = ctrl as ISimpleAudioVolume;
                    IAudioMeterInformation meter = ctrl as IAudioMeterInformation;

                    uint pid = 0;
                    if (ctrl2 != null) ctrl2.GetProcessId(out pid);
                    if (pid == 0) continue; // Skip system audio sounds session

                    int state = 0;
                    ctrl.GetState(out state);
                    // 1 = AudioSessionStateActive, 0 = Inactive, 2 = Expired
                    if (state == 2) continue;

                    float peak = 0f;
                    if (meter != null) meter.GetPeakValue(out peak);

                    float curVol = 1f;
                    if (vol != null) vol.GetMasterVolume(out curVol);

                    bool muted = false;
                    if (vol != null) vol.GetMute(out muted);

                    string pName = "";
                    string title = "";
                    try
                    {
                        using (Process p = Process.GetProcessById((int)pid))
                        {
                            pName = p.ProcessName;
                            try
                            {
                                string desc = p.MainModule.FileVersionInfo.FileDescription;
                                title = !string.IsNullOrEmpty(desc) ? desc.Trim() : pName;
                            }
                            catch
                            {
                                title = pName;
                            }
                        }
                    }
                    catch
                    {
                        pName = "App_" + pid;
                        title = pName;
                    }

                    sessions.Add(new AppSessionInfo
                    {
                        Pid = pid,
                        ProcessName = pName,
                        DisplayTitle = title,
                        Peak = peak,
                        Volume = curVol,
                        Muted = muted,
                        VolumeCtrl = vol
                    });
                }

                // Selection criteria:
                // 1. App actively producing sound output (Peak > 0.0001f)
                AppSessionInfo chosen = null;
                AppSessionInfo loudest = null;
                float maxPeak = 0.0001f;

                foreach (AppSessionInfo s in sessions)
                {
                    if (s.Peak > maxPeak)
                    {
                        maxPeak = s.Peak;
                        loudest = s;
                    }
                }

                // If mediaAppId is active and has sound, prioritize it
                if (!string.IsNullOrEmpty(mediaAppId))
                {
                    string search = mediaAppId.Replace(".exe", "").ToLowerInvariant();
                    foreach (AppSessionInfo s in sessions)
                    {
                        if (s.ProcessName.ToLowerInvariant().Contains(search) && s.Peak > 0.0001f)
                        {
                            chosen = s;
                            break;
                        }
                    }
                }

                // Else pick the session with the loudest output
                if (chosen == null && loudest != null)
                {
                    chosen = loudest;
                }

                // 2. If no app has active sound right now (e.g. paused music or quiet moment),
                // match the currently displayed media player:
                if (chosen == null && !string.IsNullOrEmpty(mediaAppId))
                {
                    string search = mediaAppId.Replace(".exe", "").ToLowerInvariant();
                    foreach (AppSessionInfo s in sessions)
                    {
                        if (s.ProcessName.ToLowerInvariant().Contains(search))
                        {
                            chosen = s;
                            break;
                        }
                    }
                }

                // 3. If still null, pick the first non-background/wallpaper session
                if (chosen == null)
                {
                    foreach (AppSessionInfo s in sessions)
                    {
                        string lower = s.ProcessName.ToLowerInvariant();
                        if (!lower.Contains("wallpaper") && !lower.Contains("shellexperience") && !lower.Contains("translucenttb"))
                        {
                            chosen = s;
                            break;
                        }
                    }
                }

                // Apply volume change
                if (chosen != null && chosen.VolumeCtrl != null)
                {
                    float step = 0.05f; // 5% per mouse wheel tick
                    float newVol = chosen.Volume + (delta > 0 ? step : -step);
                    if (newVol < 0f) newVol = 0f;
                    if (newVol > 1f) newVol = 1f;

                    Guid ctx = Guid.Empty;
                    chosen.VolumeCtrl.SetMasterVolume(newVol, ref ctx);
                    if (newVol > 0f && chosen.Muted)
                    {
                        chosen.VolumeCtrl.SetMute(false, ref ctx);
                    }

                    int percent = (int)Math.Round(newVol * 100);
                    Program.Log(string.Format("AppVolume changed: {0} = {1}% (delta={2})", chosen.DisplayTitle, percent, delta));
                    return string.Format("{0}: {1}%", chosen.DisplayTitle, percent);
                }

                // Fallback to Master Volume on default endpoint
                return FallbackEndpointVolume(dev, delta);
            }
            catch (Exception ex)
            {
                Program.Log("AppVolumeManager error: " + ex.Message);
            }

            return FallbackMaster(delta);
        }

        private static string FallbackEndpointVolume(IMMDevice dev, int delta)
        {
            try
            {
                Guid epIid = typeof(IAudioEndpointVolume).GUID;
                object epObj;
                if (dev.Activate(ref epIid, ClsCtxAll, IntPtr.Zero, out epObj) >= 0 && epObj != null)
                {
                    IAudioEndpointVolume epVol = (IAudioEndpointVolume)epObj;
                    float curMaster;
                    epVol.GetMasterVolumeLevelScalar(out curMaster);
                    float step = 0.02f;
                    float newMaster = curMaster + (delta > 0 ? step : -step);
                    if (newMaster < 0f) newMaster = 0f;
                    if (newMaster > 1f) newMaster = 1f;
                    Guid ctx = Guid.Empty;
                    epVol.SetMasterVolumeLevelScalar(newMaster, ref ctx);
                    int percent = (int)Math.Round(newMaster * 100);
                    return Loc.SystemVolume(percent);
                }
            }
            catch { }

            return FallbackMaster(delta);
        }

        private static string FallbackMaster(int delta)
        {
            return delta > 0 ? Loc.VolumeUp : Loc.VolumeDown;
        }
    }
}
