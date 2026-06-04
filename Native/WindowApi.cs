namespace NTEFishingToolCustom.Native;

using System.Runtime.InteropServices;

public static class WindowApi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointNative
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref PointNative point);

    public static bool GetPureClientRect(IntPtr hWnd, out Rect rect)
    {
        rect = default;
        if (!GetClientRect(hWnd, out var client))
        {
            return false;
        }

        var topLeft = new PointNative();
        if (!ClientToScreen(hWnd, ref topLeft))
        {
            return false;
        }

        rect = new Rect
        {
            Left = topLeft.X,
            Top = topLeft.Y,
            Right = topLeft.X + client.Right,
            Bottom = topLeft.Y + client.Bottom,
        };
        return true;
    }
}
