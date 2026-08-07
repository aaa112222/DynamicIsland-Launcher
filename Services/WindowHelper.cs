using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DynamicIsland.Services;

/// <summary>
/// 窗口API封装：将WPF窗口配置为系统级Overlay
/// </summary>
public static class WindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;

    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_LAYERED = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_CLIPSIBLINGS = 0x04000000;
    private const int WS_CLIPCHILDREN = 0x02000000;

    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOMOVE = 0x0002;
    private const int SWP_NOACTIVATE = 0x0010;
    private const int SWP_SHOWWINDOW = 0x0040;

    private static readonly IntPtr HWND_TOPMOST = new(-1);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// 将窗口设置为系统级Overlay：置顶、无任务栏图标、不抢焦点、可接收鼠标事件
    /// </summary>
    public static void SetupAsOverlay(Window window)
    {
        var helper = new WindowInteropHelper(window);
        var hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero)
        {
            window.SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(window).Handle;
                ApplyOverlayStyles(h);
            };
        }
        else
        {
            ApplyOverlayStyles(hwnd);
        }
    }

    private static void ApplyOverlayStyles(IntPtr hwnd)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_TOOLWINDOW;
        ex |= WS_EX_TOPMOST;
        ex |= WS_EX_LAYERED;
        ex |= WS_EX_NOACTIVATE;
        ex &= ~WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, ex);

        int style = GetWindowLong(hwnd, GWL_STYLE);
        style |= WS_POPUP;
        style |= WS_VISIBLE;
        style |= WS_CLIPSIBLINGS;
        style |= WS_CLIPCHILDREN;
        SetWindowLong(hwnd, GWL_STYLE, style);

        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    /// <summary>
    /// 强制将窗口置顶（用于全屏应用之上）
    /// </summary>
    public static void BringToTopmost(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// 强制激活窗口并获取焦点（用于点击时全局聚焦，像任务栏一样）
    /// </summary>
    public static void ForceActivateWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        ex &= ~WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, ex);

        SetForegroundWindow(hwnd);

        window.Activate();
        window.Focus();
    }

    /// <summary>
    /// 恢复窗口为不抢焦点的Overlay模式
    /// </summary>
    public static void RestoreNoActivate(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, ex);
    }

    /// <summary>
    /// 获取窗口所在显示器的工作区（DPI感知）
    /// </summary>
    public static Rect GetScreenBounds(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return SystemParameters.WorkArea;
        }

        var hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref info))
        {
            return SystemParameters.WorkArea;
        }

        var rc = info.rcMonitor;
        var rect = new Rect(rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top);
        var source = PresentationSource.FromVisual(window);
        var matrix = source?.CompositionTarget?.TransformFromDevice;
        if (matrix != null)
        {
            var m = matrix.Value;
            var tl = m.Transform(rect.TopLeft);
            var br = m.Transform(rect.BottomRight);
            return new Rect(tl, br);
        }
        return rect;
    }
}