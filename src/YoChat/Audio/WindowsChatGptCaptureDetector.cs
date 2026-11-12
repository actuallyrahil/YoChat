using System.Diagnostics;
using System.Runtime.InteropServices;
using YoChat.Core;

namespace YoChat.Audio;

public sealed class WindowsChatGptCaptureDetector : IChatGptCaptureDetector
{
    private const uint DeviceStateActive = 0x00000001;
    private const uint ClsCtxAll = 23;

    public ChatGptCaptureStatus GetStatus()
    {
        try
        {
            return IsChatGptCapturing()
                ? new ChatGptCaptureStatus(ChatGptCaptureState.Capturing)
                : new ChatGptCaptureStatus(ChatGptCaptureState.NotCapturing);
        }
        catch (Exception ex)
        {
            return new ChatGptCaptureStatus(ChatGptCaptureState.Unavailable, ex.Message);
        }
    }

    private static bool IsChatGptCapturing()
    {
        IMMDeviceEnumerator? deviceEnumerator = null;
        IMMDeviceCollection? devices = null;

        try
        {
            deviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            Marshal.ThrowExceptionForHR(deviceEnumerator.EnumAudioEndpoints(
                EDataFlow.Capture,
                DeviceStateActive,
                out devices));

            Marshal.ThrowExceptionForHR(devices.GetCount(out var deviceCount));
            for (uint deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
            {
                IMMDevice? device = null;
                try
                {
                    Marshal.ThrowExceptionForHR(devices.Item(deviceIndex, out device));
                    if (DeviceHasActiveChatGptSession(device))
                    {
                        return true;
                    }
                }
                finally
                {
                    Release(device);
                }
            }

            return false;
        }
        finally
        {
            Release(devices);
            Release(deviceEnumerator);
        }
    }

    private static bool DeviceHasActiveChatGptSession(IMMDevice device)
    {
        IAudioSessionManager2? manager = null;
        IAudioSessionEnumerator? sessions = null;

        try
        {
            var managerInterface = typeof(IAudioSessionManager2).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(
                ref managerInterface,
                ClsCtxAll,
                IntPtr.Zero,
                out var managerObject));
            manager = (IAudioSessionManager2)managerObject;

            Marshal.ThrowExceptionForHR(manager.GetSessionEnumerator(out sessions));
            Marshal.ThrowExceptionForHR(sessions.GetCount(out var sessionCount));

            for (var sessionIndex = 0; sessionIndex < sessionCount; sessionIndex++)
            {
                IAudioSessionControl? session = null;
                try
                {
                    Marshal.ThrowExceptionForHR(sessions.GetSession(sessionIndex, out session));
                    var session2 = (IAudioSessionControl2)session;
                    Marshal.ThrowExceptionForHR(session2.GetState(out var state));
                    if (state != AudioSessionState.Active)
                    {
                        continue;
                    }

                    Marshal.ThrowExceptionForHR(session2.GetProcessId(out var processId));
                    if (IsChatGptProcess(processId))
                    {
                        return true;
                    }
                }
                finally
                {
                    Release(session);
                }
            }

            return false;
        }
        finally
        {
            Release(sessions);
            Release(manager);
        }
    }

    private static bool IsChatGptProcess(uint processId)
    {
        if (processId == 0 || processId > int.MaxValue)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase) ||
                   process.ProcessName.Equals("Codex", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private enum EDataFlow
    {
        Render,
        Capture,
        All
    }

    private enum AudioSessionState
    {
        Inactive,
        Active,
        Expired
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IMMDeviceCollection devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, int role, out IMMDevice device);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr client);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    private interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int Item(uint index, out IMMDevice device);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            ref Guid interfaceId,
            uint classContext,
            IntPtr activationParameters,
            [MarshalAs(UnmanagedType.IUnknown)] out object instance);

        [PreserveSig]
        int OpenPropertyStore(uint access, out IntPtr properties);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetState(out uint state);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("BFA971F1-4D5E-40BB-935E-967039BFBEE4")]
    private interface IAudioSessionManager2
    {
        [PreserveSig]
        int GetAudioSessionControl(IntPtr sessionGuid, uint streamFlags, out IntPtr sessionControl);

        [PreserveSig]
        int GetSimpleAudioVolume(IntPtr sessionGuid, uint streamFlags, out IntPtr volume);

        [PreserveSig]
        int GetSessionEnumerator(out IAudioSessionEnumerator enumerator);

        [PreserveSig]
        int RegisterSessionNotification(IntPtr notification);

        [PreserveSig]
        int UnregisterSessionNotification(IntPtr notification);

        [PreserveSig]
        int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr notification);

        [PreserveSig]
        int UnregisterDuckNotification(IntPtr notification);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig]
        int GetCount(out int count);

        [PreserveSig]
        int GetSession(int index, out IAudioSessionControl control);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    private interface IAudioSessionControl
    {
        [PreserveSig]
        int GetState(out AudioSessionState state);

        [PreserveSig]
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);

        [PreserveSig]
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr eventContext);

        [PreserveSig]
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);

        [PreserveSig]
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr eventContext);

        [PreserveSig]
        int GetGroupingParam(out Guid groupingId);

        [PreserveSig]
        int SetGroupingParam(ref Guid groupingId, IntPtr eventContext);

        [PreserveSig]
        int RegisterAudioSessionNotification(IntPtr client);

        [PreserveSig]
        int UnregisterAudioSessionNotification(IntPtr client);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    private interface IAudioSessionControl2
    {
        [PreserveSig]
        int GetState(out AudioSessionState state);

        [PreserveSig]
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);

        [PreserveSig]
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr eventContext);

        [PreserveSig]
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);

        [PreserveSig]
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr eventContext);

        [PreserveSig]
        int GetGroupingParam(out Guid groupingId);

        [PreserveSig]
        int SetGroupingParam(ref Guid groupingId, IntPtr eventContext);

        [PreserveSig]
        int RegisterAudioSessionNotification(IntPtr client);

        [PreserveSig]
        int UnregisterAudioSessionNotification(IntPtr client);

        [PreserveSig]
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetProcessId(out uint processId);

        [PreserveSig]
        int IsSystemSoundsSession();

        [PreserveSig]
        int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }
}

