using System;
using System.Drawing;
using System.Runtime.InteropServices;
using ReNotchPixel.Models;

namespace ReNotchPixel.Services;

public class TrayService : IDisposable
{
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;

    public const int WM_TRAYICON = 0x0400 + 101; // WM_USER + 101
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint MF_CHECKED = 0x00000008;
    private const uint MF_UNCHECKED = 0x00000000;
    private const uint MF_GRAYED = 0x00000001;

    public const uint CMD_SHAPE_ISLAND = 1001;
    public const uint CMD_SHAPE_NOTCH = 1002;
    public const uint CMD_POS_CENTER = 1003;
    public const uint CMD_POS_LEFT = 1004;
    public const uint CMD_POS_RIGHT = 1005;
    public const uint CMD_TOGGLE_EXPAND = 1006;
    public const uint CMD_SCREENSHOT = 1007;
    public const uint CMD_SETTINGS = 1009;
    public const uint CMD_DECK = 1010;
    public const uint CMD_EXIT = 1008;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, uint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lpTPMPARAMS);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private readonly IntPtr _hwnd;
    private NOTIFYICONDATA _nid;
    private bool _isAdded;
    private Icon? _icon;

    public event Action<uint>? CommandSelected;

    public TrayService(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    public void Initialize()
    {
        try
        {
            _icon = SystemIcons.Application;

            _nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _icon.Handle,
                szTip = "ReNotchPixel - Notch for Windows"
            };

            _isAdded = Shell_NotifyIcon(NIM_ADD, ref _nid);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TrayService] Init failed: {ex.Message}");
        }
    }

    public void HandleWindowMessage(int msg, IntPtr wParam, IntPtr lParam, IslandShape shape, IslandPosition pos)
    {
        if (msg == WM_TRAYICON)
        {
            int mouseMsg = (int)lParam;
            if (mouseMsg == WM_RBUTTONUP || mouseMsg == WM_LBUTTONUP)
            {
                ShowContextMenu(shape, pos);
            }
        }
    }

    private void ShowContextMenu(IslandShape shape, IslandPosition pos)
    {
        if (Win32Helper.GetCursorPos(out var pt))
        {
            IntPtr hMenu = CreatePopupMenu();
            try
            {
                AppendMenuW(hMenu, MF_STRING | MF_GRAYED, 0, "ReNotchPixel v1.0");
                AppendMenuW(hMenu, MF_SEPARATOR, 0, null);

                // Shape submenu / items
                AppendMenuW(hMenu, MF_STRING | (shape == IslandShape.Island ? MF_CHECKED : MF_UNCHECKED), CMD_SHAPE_ISLAND, "Shape: Dynamic Island (Floating)");
                AppendMenuW(hMenu, MF_STRING | (shape == IslandShape.Notch ? MF_CHECKED : MF_UNCHECKED), CMD_SHAPE_NOTCH, "Shape: MacBook Notch (Anchored)");
                AppendMenuW(hMenu, MF_SEPARATOR, 0, null);

                // Position items
                AppendMenuW(hMenu, MF_STRING | (pos == IslandPosition.Center ? MF_CHECKED : MF_UNCHECKED), CMD_POS_CENTER, "Position: Center");
                AppendMenuW(hMenu, MF_STRING | (pos == IslandPosition.Left ? MF_CHECKED : MF_UNCHECKED), CMD_POS_LEFT, "Position: Left");
                AppendMenuW(hMenu, MF_STRING | (pos == IslandPosition.Right ? MF_CHECKED : MF_UNCHECKED), CMD_POS_RIGHT, "Position: Right");
                AppendMenuW(hMenu, MF_SEPARATOR, 0, null);

                AppendMenuW(hMenu, MF_STRING, CMD_TOGGLE_EXPAND, "Toggle Expanded / Compact");
                AppendMenuW(hMenu, MF_STRING, CMD_SCREENSHOT, "Capture Screen (Snip)");
                AppendMenuW(hMenu, MF_STRING, CMD_DECK, "Open Mobile Deck Companion");
                AppendMenuW(hMenu, MF_SEPARATOR, 0, null);
                AppendMenuW(hMenu, MF_STRING, CMD_SETTINGS, "Preferences & Settings...");
                AppendMenuW(hMenu, MF_SEPARATOR, 0, null);
                AppendMenuW(hMenu, MF_STRING, CMD_EXIT, "Exit ReNotchPixel");

                SetForegroundWindow(_hwnd);
                int cmd = TrackPopupMenuEx(hMenu, 0x0100, pt.X, pt.Y, _hwnd, IntPtr.Zero);
                if (cmd > 0)
                {
                    CommandSelected?.Invoke((uint)cmd);
                }
            }
            finally
            {
                DestroyMenu(hMenu);
            }
        }
    }

    public void Dispose()
    {
        if (_isAdded)
        {
            Shell_NotifyIcon(NIM_DELETE, ref _nid);
            _isAdded = false;
        }
    }
}
