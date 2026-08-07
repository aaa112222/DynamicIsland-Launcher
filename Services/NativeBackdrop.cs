using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DynamicIsland.Services;

/// <summary>
/// Windows 11 DWM 毛玻璃API封装
/// 参考 BlockHelm-Launcher 的实现思路，但简化为系统级 Backdrop
/// </summary>
public static class NativeBackdrop
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    private const uint DWMSBT_AUTO = 0;
    private const uint DWMSBT_NONE = 1;
    private const uint DWMSBT_MAINWINDOW = 2;
    private const uint DWMSBT_TRANSIENTWINDOW = 3;
    private const uint DWMSBT_TABBEDWINDOW = 4;

    private const int WCA_ACCENT_POLICY = 19;

    private enum ACCENT_STATE
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ACCENT_POLICY
    {
        public ACCENT_STATE AccentState;
        public uint AccentFlags;
        public uint GradientColor;
        public uint AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWCOMPOSITIONATTRIBDATA
    {
        public int Attribute;
        public IntPtr pvData;
        public int cbData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    /// <summary>
    /// 应用深色模式（黑色背景配合灵动岛）
    /// </summary>
    public static void ApplyDarkMode(Window window)
    {
        var hwnd = GetHwnd(window);
        if (hwnd == IntPtr.Zero) return;
        int value = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    /// <summary>
    /// 开启 Acrylic 亚克力毛玻璃（SetWindowCompositionAttribute 方式，兼容 Win10/Win11）
    /// gradientColor: 0xAARRGGBB
    /// </summary>
    public static void SetAcrylic(Window window, uint gradientColor)
    {
        var hwnd = GetHwnd(window);
        if (hwnd == IntPtr.Zero) return;
        SetAcrylic(hwnd, gradientColor);
    }

    /// <summary>
    /// 开启 Acrylic 亚克力毛玻璃
    /// </summary>
    public static void SetAcrylic(IntPtr hwnd, uint gradientColor)
    {
        if (hwnd == IntPtr.Zero) return;

        var accent = new ACCENT_POLICY
        {
            AccentState = ACCENT_STATE.ACCENT_ENABLE_ACRYLICBLURBEHIND,
            GradientColor = gradientColor
        };

        var data = new WINDOWCOMPOSITIONATTRIBDATA
        {
            Attribute = WCA_ACCENT_POLICY,
            cbData = Marshal.SizeOf<ACCENT_POLICY>(),
            pvData = Marshal.AllocHGlobal(Marshal.SizeOf<ACCENT_POLICY>())
        };

        try
        {
            Marshal.StructureToPtr(accent, data.pvData, false);
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(data.pvData);
        }
    }

    /// <summary>
    /// 应用 Mica/Acrylic 系统背景（Windows 11 22000+）
    /// </summary>
    public static bool TryApplySystemBackdrop(Window window, BackdropType type)
    {
        var hwnd = GetHwnd(window);
        if (hwnd == IntPtr.Zero) return false;

        int value = type switch
        {
            BackdropType.None => (int)DWMSBT_NONE,
            BackdropType.Auto => (int)DWMSBT_AUTO,
            BackdropType.Mica => (int)DWMSBT_MAINWINDOW,
            BackdropType.Acrylic => (int)DWMSBT_TRANSIENTWINDOW,
            BackdropType.Tabbed => (int)DWMSBT_TABBEDWINDOW,
            _ => (int)DWMSBT_AUTO
        };

        int hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref value, sizeof(int));
        return hr == 0;
    }

    /// <summary>
    /// 将窗口客户区扩展到全屏（让毛玻璃覆盖整个窗口）
    /// </summary>
    public static void ExtendFrame(Window window)
    {
        var hwnd = GetHwnd(window);
        if (hwnd == IntPtr.Zero) return;
        var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    private static IntPtr GetHwnd(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero) return hwnd;

        var helper = new WindowInteropHelper(window);
        hwnd = helper.EnsureHandle();
        return hwnd;
    }
}

public enum BackdropType
{
    None,
    Auto,
    Mica,
    Acrylic,
    Tabbed
}