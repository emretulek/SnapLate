using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Widgets.Common;

namespace SnapLate
{
    public class HotKey
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        private static HwndSource? _HwndSource = null;
        private static int _HandleId = 1;
        private static Action _HookAction = () => { };

        public static void Register(string hotKey, Action action)
        {
            _HookAction = action;

            var HotKeys = ParseHotKey(hotKey);
            _HwndSource = (HwndSource)HwndSource.FromVisual(Application.Current.MainWindow);
            _HwndSource.AddHook(HotKeyHook);

            if(RegisterHotKey(_HwndSource.Handle, _HandleId, HotKeys[0], HotKeys[1]))
            {
                Logger.Info("Register hotkey success");
            }
        }

        public static void Unregister()
        {
            if (_HwndSource != null && _HwndSource.Handle != IntPtr.Zero)
            {
                if(UnregisterHotKey(_HwndSource.Handle, _HandleId))
                {
                    _HwndSource.RemoveHook(HotKeyHook);
                    Logger.Info("Unregister hotkey success");
                }
            }
        }

        private static IntPtr HotKeyHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY && (int)wParam == _HandleId)
            {
                _HookAction();
            }
            return IntPtr.Zero;
        }

        private static uint[] ParseHotKey(string stringHotKey)
        {
            var parts = stringHotKey.Split('+').Select(p => p.Trim()).ToArray();
            uint modifiers = 0;
            uint key = 0;

            foreach (var part in parts)
            {
                if ("ALT".Equals(part, StringComparison.CurrentCultureIgnoreCase))
                {
                    modifiers |= MOD_ALT;
                }
                else if ("SHIFT".Equals(part, StringComparison.CurrentCultureIgnoreCase))
                {
                    modifiers |= MOD_SHIFT;
                }
                else if ("CTRL".Equals(part, StringComparison.CurrentCultureIgnoreCase))
                {
                    modifiers |= MOD_CONTROL;
                }
                else
                {
                    key = (uint)part.ToUpper()[0];
                }
            }

            return [modifiers, key];
        }
    }
}
