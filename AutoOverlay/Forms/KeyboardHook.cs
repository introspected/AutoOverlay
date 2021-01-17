using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoOverlay.Forms
{
    public class KeyboardHook : IDisposable
    {
        private bool global;

        public event EventHandler<KeyEventArgs> KeyDown;
        public event EventHandler<KeyEventArgs> KeyUp;

        public delegate int CallbackDelegate(int Code, int W, int L);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct KBDLLHookStruct
        {
            public Int32 vkCode;
            public Int32 scanCode;
            public Int32 flags;
            public Int32 time;
            public Int32 dwExtraInfo;
        }

        [DllImport("user32", CallingConvention = CallingConvention.StdCall)]
        private static extern int SetWindowsHookEx(HookType idHook, CallbackDelegate lpfn, int hInstance, int threadId);

        [DllImport("user32", CallingConvention = CallingConvention.StdCall)]
        private static extern bool UnhookWindowsHookEx(int idHook);

        [DllImport("user32", CallingConvention = CallingConvention.StdCall)]
        private static extern int CallNextHookEx(int idHook, int nCode, int wParam, int lParam);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int GetCurrentThreadId();

        public enum HookType
        {
            WH_JOURNALRECORD = 0,
            WH_JOURNALPLAYBACK = 1,
            WH_KEYBOARD = 2,
            WH_GETMESSAGE = 3,
            WH_CALLWNDPROC = 4,
            WH_CBT = 5,
            WH_SYSMSGFILTER = 6,
            WH_MOUSE = 7,
            WH_HARDWARE = 8,
            WH_DEBUG = 9,
            WH_SHELL = 10,
            WH_FOREGROUNDIDLE = 11,
            WH_CALLWNDPROCRET = 12,
            WH_KEYBOARD_LL = 13,
            WH_MOUSE_LL = 14
        }

        private int hookID;
        private CallbackDelegate theHookCB;

        public KeyboardHook(bool global)
        {
            this.global = global;
            theHookCB = KeybHookProc;
            if (this.global)
            {
                hookID = SetWindowsHookEx(HookType.WH_KEYBOARD_LL, theHookCB,
                    0, //0 for local hook. eller hwnd til user32 for global
                    0); //0 for global hook. eller thread for hooken
            }
            else
            {
                hookID = SetWindowsHookEx(HookType.WH_KEYBOARD, theHookCB,
                    0, //0 for local hook. or hwnd to user32 for global
                    GetCurrentThreadId()); //0 for global hook. or thread for the hook
            }
        }

        ~KeyboardHook()
        {
            UnhookWindowsHookEx(hookID);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            UnhookWindowsHookEx(hookID);
        }

        //The listener that will trigger events
        private int KeybHookProc(int code, int W, int L)
        {
            new KBDLLHookStruct();
            if (code < 0)
            {
                return CallNextHookEx(hookID, code, W, L);
            }
            try
            {
                if (global)
                {
                    var kEvent = (KeyEvents)W;
                    var vkCode = (Keys)Marshal.ReadInt32((IntPtr)L);
                    var args = new KeyEventArgs(vkCode | GetCommandKeysPressed());
                    if (kEvent != KeyEvents.KeyDown && kEvent != KeyEvents.KeyUp
                        && kEvent != KeyEvents.SKeyDown && kEvent != KeyEvents.SKeyUp)
                    {
                    }
                    else if (kEvent == KeyEvents.KeyDown || kEvent == KeyEvents.SKeyDown)
                        KeyDown?.Invoke(this, args);
                    else if (kEvent == KeyEvents.KeyUp || kEvent == KeyEvents.SKeyUp)
                        KeyUp?.Invoke(this, args);
                    if (args.Handled)
                        return 1;
                }
                else
                {
                    if (code == 3)
                    {
                        int keydownup = L >> 30;
                        var args = new KeyEventArgs((Keys)W | GetCommandKeysPressed());
                        if (keydownup == 0)
                            KeyDown?.Invoke(this, args);
                        else if (keydownup == -1)
                            KeyUp?.Invoke(this, args);
                        if (args.Handled)
                            return 1;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return CallNextHookEx(hookID, code, W, L);
        }

        public enum KeyEvents
        {
            KeyDown = 0x0100,
            KeyUp = 0x0101,
            SKeyDown = 0x0104,
            SKeyUp = 0x0105
        }

        [DllImport("user32.dll")]
        public static extern short GetKeyState(Keys nVirtKey);

        public static bool GetCapslock()
        {
            return Convert.ToBoolean(GetKeyState(Keys.CapsLock)) & true;
        }

        public static bool GetNumlock()
        {
            return Convert.ToBoolean(GetKeyState(Keys.NumLock)) & true;
        }

        public static bool GetScrollLock()
        {
            return Convert.ToBoolean(GetKeyState(Keys.Scroll)) & true;
        }

        public static bool IsPressed(Keys key)
        {
            var state = GetKeyState(key);
            return state > 1 || state < -1;
        }

        public Keys GetCommandKeysPressed()
        {
            return (IsPressed(Keys.Control) || IsPressed(Keys.ControlKey)
                    || IsPressed(Keys.LControlKey) || IsPressed(Keys.RControlKey)
                       ? Keys.Control
                       : 0)
                   | (IsPressed(Keys.Shift) || IsPressed(Keys.ShiftKey)
                      || IsPressed(Keys.LShiftKey) || IsPressed(Keys.RShiftKey)
                       ? Keys.Shift
                       : 0)
                   | (IsPressed(Keys.Alt) ? Keys.Alt : 0);
        }
    }
}