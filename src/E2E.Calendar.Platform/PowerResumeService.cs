using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.Platform;

/// <summary>
/// Raises <see cref="Resumed"/> when Windows reports that the machine woke from sleep,
/// so the reminder scheduler can re-check its plan without waiting for the next tick.
/// WM_POWERBROADCAST is broadcast to top-level windows, so the hidden window is not
/// message-only; a message-only window would never receive it.
/// </summary>
public sealed class PowerResumeService : IDisposable
{
    private const int WmPowerBroadcast = 0x0218;
    private const int PbtApmResumeCritical = 0x0006;
    private const int PbtApmResumeSuspend = 0x0007;
    private const int PbtApmResumeAutomatic = 0x0012;

    private const uint WsPopup = 0x80000000;
    private const int WsExToolWindow = 0x00000080;
    private const int ErrorClassAlreadyExists = 1410;

    private readonly ILogger<PowerResumeService> _logger;
    private readonly WindowProcedure _windowProcedure;
    private IntPtr _window;
    private bool _started;

    public PowerResumeService(ILogger<PowerResumeService> logger)
    {
        _logger = logger;
        _windowProcedure = WindowProc;
    }

    /// <summary>Raised on the thread that owns the message window (the UI thread).</summary>
    public event EventHandler? Resumed;

    public bool Start()
    {
        if (_started)
        {
            return true;
        }

        try
        {
            CreateMessageWindow();
            _started = true;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Power-resume notifications could not be registered.");
            DestroyMessageWindow();

            return false;
        }
    }

    public void Dispose()
    {
        _started = false;
        DestroyMessageWindow();
    }

    private void CreateMessageWindow()
    {
        if (_window != IntPtr.Zero)
        {
            return;
        }

        var className = "E2E.Calendar.PowerMessageWindow";
        var instance = GetModuleHandle(null);
        var windowClass = new WindowClass
        {
            cbSize = (uint)Marshal.SizeOf<WindowClass>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_windowProcedure),
            hInstance = instance,
            lpszClassName = className,
        };

        var atom = RegisterClassEx(ref windowClass);

        if (atom == 0 && Marshal.GetLastWin32Error() != ErrorClassAlreadyExists)
        {
            throw new InvalidOperationException("The power monitor window class could not be registered.");
        }

        _window = CreateWindowEx(
            WsExToolWindow,
            className,
            "E2E Calendar Power Monitor",
            WsPopup,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            instance,
            IntPtr.Zero);

        if (_window == IntPtr.Zero)
        {
            throw new InvalidOperationException("The power monitor window could not be created.");
        }
    }

    private void DestroyMessageWindow()
    {
        if (_window == IntPtr.Zero)
        {
            return;
        }

        DestroyWindow(_window);
        _window = IntPtr.Zero;
    }

    private IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmPowerBroadcast)
        {
            switch ((int)wParam)
            {
                case PbtApmResumeCritical:
                case PbtApmResumeSuspend:
                case PbtApmResumeAutomatic:
                    _logger.LogInformation("Power resume reported; re-checking the reminder schedule.");

                    try
                    {
                        Resumed?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Power-resume handling failed.");
                    }

                    return new IntPtr(1);
            }
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private delegate IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        int extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
