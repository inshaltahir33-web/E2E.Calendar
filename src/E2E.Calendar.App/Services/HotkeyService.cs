using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace E2E.Calendar.App.Services;

/// <summary>
/// Registers a global hotkey (Win+Alt+C by default) so Quick Add can be opened
/// while the app is in the background or in the tray.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0xE2E1;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkC = 0x43;

    private readonly DispatcherQueue _dispatcher;
    private IntPtr _windowHandle;

    public HotkeyService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public event EventHandler? Pressed;

    public bool TryRegister(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        _windowHandle = windowHandle;

        return RegisterHotKey(windowHandle, HotkeyId, ModWin | ModAlt | ModNoRepeat, VkC);
    }

    public bool HandleMessage(int message, IntPtr wParam)
    {
        if (message != WmHotkey || wParam.ToInt64() != HotkeyId)
        {
            return false;
        }

        _dispatcher.TryEnqueue(() => Pressed?.Invoke(this, EventArgs.Empty));

        return true;
    }

    public void Dispose()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HotkeyId);
            _windowHandle = IntPtr.Zero;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
