using System;
using System.Runtime.InteropServices;
using System.Text;

namespace XWhip
{
    internal sealed class WindowShake : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] struct Rect { internal int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int max);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        const uint MoveOnly = 0x0001 | 0x0004 | 0x0010 | 0x0200;
        IntPtr target;
        int originX, originY, lastX, lastY, level;
        double started;
        internal bool Active { get { return target != IntPtr.Zero; } }

        internal void Begin(IntPtr pet, int anger, double now)
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == pet) return;
            BeginOnWindow(foreground, anger, now);
        }

        internal void BeginOnWindow(IntPtr window, int anger, double now)
        {
            Stop();
            if (anger < 4 || window == IntPtr.Zero || !IsWindow(window)) return;
            var name = new StringBuilder(128); GetClassName(window, name, name.Capacity);
            if (name.ToString() == "Progman" || name.ToString() == "WorkerW" || name.ToString().StartsWith("Shell_")) return;
            Rect rect;
            if (!GetWindowRect(window, out rect) || rect.Right - rect.Left < 100 || rect.Bottom - rect.Top < 100) return;
            target = window; originX = lastX = rect.Left; originY = lastY = rect.Top;
            level = Math.Min(6, anger); started = now;
        }

        internal void Tick(double now)
        {
            if (!Active) return;
            Rect rect;
            if (!IsWindow(target) || !GetWindowRect(target, out rect)) { target = IntPtr.Zero; return; }
            // If the user or target app moved the window, leave its new placement alone.
            if (rect.Left != lastX || rect.Top != lastY) { target = IntPtr.Zero; return; }
            double age = now - started;
            if (age >= .22 || age < 0) { Stop(); return; }
            double amplitude = (level * 2 - 4) * (1 - age / .22);
            int x = originX + (int)Math.Round(Math.Sin(age * 105) * amplitude);
            int y = originY + (int)Math.Round(Math.Sin(age * 137) * amplitude * .6);
            if (SetWindowPos(target, IntPtr.Zero, x, y, 0, 0, MoveOnly)) { lastX = x; lastY = y; }
            else Stop();
        }

        internal void Stop()
        {
            if (!Active) return;
            Rect rect;
            if (IsWindow(target) && GetWindowRect(target, out rect) && rect.Left == lastX && rect.Top == lastY)
                SetWindowPos(target, IntPtr.Zero, originX, originY, 0, 0, MoveOnly);
            target = IntPtr.Zero;
        }

        public void Dispose() { Stop(); }
    }
}
