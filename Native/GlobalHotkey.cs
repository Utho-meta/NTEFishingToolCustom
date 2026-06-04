namespace NTEFishingToolCustom.Native;

using System.Runtime.InteropServices;

public static class GlobalHotkey
{
    public const int CloseToolHotkeyId = 9012;
    public const int WmHotkey = 0x0312;
    public const uint ModNone = 0x0000;
    public const uint VkX = 0x58;
    public const int VkCapsLock = 0x14;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    public static bool IsXPressed()
    {
        return (GetAsyncKeyState((int)VkX) & 0x8000) != 0;
    }

    public static bool IsCapsLockOn()
    {
        return (GetKeyState(VkCapsLock) & 0x0001) != 0;
    }
}
