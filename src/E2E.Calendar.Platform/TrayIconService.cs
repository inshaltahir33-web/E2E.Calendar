using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace E2E.Calendar.Platform;

/// <summary>One entry in the tray icon's native context menu.</summary>
public sealed record TrayMenuEntry(
    uint Id,
    string Text,
    bool IsSeparator = false,
    bool IsChecked = false,
    bool IsEnabled = true);

/// <summary>
/// Owns the Shell_NotifyIcon tray icon and its hidden message window. Clicks are
/// surfaced as events so the app layer can show a WinUI flyout and menu.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const uint TrayIconId = 1;
    private const int TrayCallbackMessage = 0x8001; // WM_APP + 1

    private const int WmNull = 0x0000;
    private const int WmContextMenu = 0x007B;
    private const int WmLeftButtonUp = 0x0202;
    private const int WmRightButtonUp = 0x0205;
    private const int NinSelect = 0x0400;
    private const int NinKeySelect = 0x0401;

    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NifShowTip = 0x00000080;

    private const uint MfString = 0x00000000;
    private const uint MfGrayed = 0x00000001;
    private const uint MfChecked = 0x00000008;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;

    private const uint WsPopup = 0x80000000;
    private const int WsExToolWindow = 0x00000080;
    private const int IdiApplication = 32512;
    private const int ErrorClassAlreadyExists = 1410;

    private readonly ILogger<TrayIconService> _logger;
    private readonly WindowProcedure _windowProcedure;
    private IntPtr _window;
    private IntPtr _icon;
    private bool _ownsIcon;
    private uint _taskbarCreatedMessage;
    private bool _started;

    public TrayIconService(ILogger<TrayIconService> logger)
    {
        _logger = logger;
        _windowProcedure = WindowProc;
    }

    /// <summary>Raised on left click (and keyboard selection) of the tray icon.</summary>
    public event EventHandler? Activated;

    /// <summary>Raised on right click; the app supplies the menu entries.</summary>
    public event EventHandler? ContextMenuRequested;

    /// <summary>Raised with the id of the clicked native menu entry.</summary>
    public event EventHandler<uint>? MenuCommandInvoked;

    public bool IsVisible => _started;

    public bool Start()
    {
        if (_started)
        {
            return true;
        }

        try
        {
            CreateMessageWindow();
            _icon = LoadTrayIcon();

            if (!AddIcon())
            {
                _logger.LogWarning("The tray icon could not be added to the notification area.");
                DestroyMessageWindow();

                return false;
            }

            _started = true;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The tray icon could not be created.");
            DestroyMessageWindow();

            return false;
        }
    }

    public void ShowContextMenu(IReadOnlyList<TrayMenuEntry> entries)
    {
        if (!_started || _window == IntPtr.Zero || entries.Count == 0)
        {
            return;
        }

        var menu = CreatePopupMenu();

        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            foreach (var entry in entries)
            {
                if (entry.IsSeparator)
                {
                    AppendMenu(menu, MfSeparator, UIntPtr.Zero, null);
                    continue;
                }

                var flags = MfString
                    | (entry.IsChecked ? MfChecked : 0u)
                    | (entry.IsEnabled ? 0u : MfGrayed);

                AppendMenu(menu, flags, (UIntPtr)entry.Id, entry.Text);
            }

            GetCursorPos(out var point);
            SetForegroundWindow(_window);

            var command = TrackPopupMenuEx(
                menu,
                TpmReturnCmd | TpmRightButton,
                point.X,
                point.Y,
                _window,
                IntPtr.Zero);

            PostMessage(_window, WmNull, IntPtr.Zero, IntPtr.Zero);

            if (command != 0)
            {
                MenuCommandInvoked?.Invoke(this, (uint)command);
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    public void SetTooltip(string tooltip)
    {
        if (!_started)
        {
            return;
        }

        try
        {
            var data = CreateIconData();
            data.szTip = string.IsNullOrWhiteSpace(tooltip)
                ? "E2E Calendar"
                : (tooltip.Length > 127 ? tooltip[..127] : tooltip);

            Shell_NotifyIcon(NimModify, ref data);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The tray tooltip could not be updated.");
        }
    }

    public void Dispose()
    {
        if (_started)
        {
            var data = CreateIconData();

            Shell_NotifyIcon(NimDelete, ref data);
            _started = false;
        }

        DestroyMessageWindow();

        if (_ownsIcon && _icon != IntPtr.Zero)
        {
            DestroyIcon(_icon);
            _ownsIcon = false;
        }

        _icon = IntPtr.Zero;
    }

    private void CreateMessageWindow()
    {
        if (_window != IntPtr.Zero)
        {
            return;
        }

        var className = "E2E.Calendar.TrayMessageWindow";
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
            throw new InvalidOperationException("The tray icon window class could not be registered.");
        }

        _window = CreateWindowEx(
            WsExToolWindow,
            className,
            "E2E Calendar Tray",
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
            throw new InvalidOperationException("The tray icon message window could not be created.");
        }

        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
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

    private bool AddIcon()
    {
        if (_window == IntPtr.Zero)
        {
            return false;
        }

        var data = CreateIconData();

        return Shell_NotifyIcon(NimAdd, ref data);
    }

    private NotifyIconData CreateIconData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
        hWnd = _window,
        uID = TrayIconId,
        uFlags = NifMessage | NifIcon | NifTip | NifShowTip,
        uCallbackMessage = TrayCallbackMessage,
        hIcon = _icon,
        szTip = "E2E Calendar",
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private IntPtr LoadTrayIcon()
    {
        try
        {
            var executable = Environment.ProcessPath;

            if (!string.IsNullOrEmpty(executable) &&
                ExtractIconEx(executable, 0, out var large, out var small, 1) > 0)
            {
                if (small != IntPtr.Zero)
                {
                    if (large != IntPtr.Zero)
                    {
                        DestroyIcon(large);
                    }

                    _ownsIcon = true;

                    return small;
                }

                if (large != IntPtr.Zero)
                {
                    _ownsIcon = true;

                    return large;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not extract the application icon for the tray.");
        }

        return LoadIcon(IntPtr.Zero, (IntPtr)IdiApplication);
    }

    private IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (_taskbarCreatedMessage != 0 && message == _taskbarCreatedMessage)
        {
            // Explorer restarted: the shell forgot every icon, so add ours again.
            if (_started)
            {
                AddIcon();
            }

            return IntPtr.Zero;
        }

        if (message == TrayCallbackMessage)
        {
            var notification = (int)(lParam.ToInt64() & 0xFFFF);

            switch (notification)
            {
                case WmLeftButtonUp:
                case NinSelect:
                case NinKeySelect:
                    Activated?.Invoke(this, EventArgs.Empty);

                    return IntPtr.Zero;

                case WmRightButtonUp:
                case WmContextMenu:
                    ContextMenuRequested?.Invoke(this, EventArgs.Empty);

                    return IntPtr.Zero;
            }

            return IntPtr.Zero;
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr item, string? text);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int TrackPopupMenuEx(
        IntPtr menu,
        uint flags,
        int x,
        int y,
        IntPtr hwnd,
        IntPtr parameters);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint count);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
