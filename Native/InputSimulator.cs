namespace NTEFishingToolCustom.Native;

using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class ScanCode
{
    public const byte Escape = 0x01;
    public const byte A = 0x1E;
    public const byte D = 0x20;
    public const byte F = 0x21;
    public const byte Q = 0x10;
    public const byte E = 0x12;
    public const byte R = 0x13;
}

public static class InputSimulator
{
    private const int InputMouse = 0;
    private const uint MouseEventFLeftDown = 0x0002;
    private const uint MouseEventFLeftUp = 0x0004;
    private const uint KeyEventFKeyUp = 0x02;
    private const uint KeyEventFScanCode = 0x08;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    public static void KeyDown(byte scanCode)
    {
        keybd_event(0, scanCode, KeyEventFScanCode, 0);
    }

    public static void KeyUp(byte scanCode)
    {
        keybd_event(0, scanCode, KeyEventFScanCode | KeyEventFKeyUp, 0);
    }

    public static void Press(byte scanCode, int holdMs = 55)
    {
        KeyDown(scanCode);
        Thread.Sleep(holdMs);
        KeyUp(scanCode);
    }

    public static void Click(Point point, int holdMs = 160, int repeat = 1)
    {
        SetCursorPos(point.X, point.Y);
        Cursor.Position = point;
        Thread.Sleep(220);

        for (var i = 0; i < repeat; i++)
        {
            SendMouse(MouseEventFLeftDown);
            Thread.Sleep(holdMs);
            SendMouse(MouseEventFLeftUp);
            Thread.Sleep(180);
        }
    }

    private static void SendMouse(uint flags)
    {
        var input = new Input
        {
            Type = InputMouse,
            Mouse = new MouseInput { Flags = flags },
        };

        SendInput(1, [input], Marshal.SizeOf<Input>());
    }
}
