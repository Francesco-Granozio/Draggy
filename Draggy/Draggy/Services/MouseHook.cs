using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace Draggy.Services
{
    public class MouseHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        public event Action<Point> LeftButtonDown;
        public event Action<Point> MouseMove;
        public event Action<Point> LeftButtonUp;
        public event Action<Point> PotentialDragStart; // our high-level event

        private bool _leftDown = false;
        private Point _downPos;

        public MouseHook()
        {
            _proc = HookCallback;
            _hookId = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process cur = Process.GetCurrentProcess())
            using (ProcessModule mod = cur.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(mod.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var msg = (MouseMessages)wParam;
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var pt = new Point(hookStruct.pt.x, hookStruct.pt.y);

                if (msg == MouseMessages.WM_LBUTTONDOWN)
                {
                    _leftDown = true;
                    _downPos = pt;
                    LeftButtonDown?.Invoke(pt);
                }
                else if (msg == MouseMessages.WM_MOUSEMOVE)
                {
                    if (_leftDown)
                    {
                        var dx = Math.Abs(pt.X - _downPos.X);
                        var dy = Math.Abs(pt.Y - _downPos.Y);
                        bool ignore = false;
                        if (Application.Current is Draggy.App app)
                        {
                            // Ignora i drag mentre la finestra si muove o viene ridimensionata
                            var isMovingField = typeof(Draggy.App).GetField("_isWindowBeingMoved", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            var isResizingField = typeof(Draggy.App).GetField("_isWindowBeingResized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            bool isMoving = (bool)(isMovingField?.GetValue(app) ?? false);
                            bool isResizing = (bool)(isResizingField?.GetValue(app) ?? false);
                            ignore = isMoving || isResizing;
                        }

                        if (!ignore && (dx > SystemParameters.MinimumHorizontalDragDistance ||
                                        dy > SystemParameters.MinimumVerticalDragDistance))
                        {
                            // Consider this a potential drag start
                            PotentialDragStart?.Invoke(_downPos);
                        }
                    }
                    MouseMove?.Invoke(pt);
                }
                else if (msg == MouseMessages.WM_LBUTTONUP)
                {
                    _leftDown = false;
                    LeftButtonUp?.Invoke(pt);
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
                UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        #region WinAPI
        private enum MouseMessages { WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_MOUSEMOVE = 0x0200, }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        #endregion
    }

}
