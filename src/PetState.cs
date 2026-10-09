using System;

namespace XWhip
{
    public enum PetMode { Cooling, Whipping, Paused, Resting }

    public sealed class PetState
    {
        private static readonly double[] StrikeDelays = { 3.2, 2.2, 1.3, 1.1, 0.95, 0.8 };
        private DateTime nextStrikeUtc;
        private TimeSpan pausedRemaining;
        private PetMode pausedMode;

        public int IntervalMinutes { get; private set; }
        public PetMode Mode { get; private set; }
        public DateTime NextDueUtc { get; private set; }
        public int StrikeCount { get; private set; }
        public int CompletedCount { get; private set; }
        public int AngerLevel
        {
            get { return Math.Min(6, Math.Max(1, (StrikeCount + 2) / 3)); }
        }

        public PetState(int intervalMinutes, DateTime utcNow)
        {
            IntervalMinutes = ClampInterval(intervalMinutes);
            Calm(utcNow);
        }

        public TimeSpan Remaining(DateTime utcNow)
        {
            if (Mode == PetMode.Whipping || Mode == PetMode.Resting) return TimeSpan.Zero;
            TimeSpan remaining = Mode == PetMode.Paused ? pausedRemaining : NextDueUtc - utcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        public int CountdownStage(DateTime utcNow)
        {
            if (Mode == PetMode.Whipping) return 4;
            if (Mode == PetMode.Paused || Mode == PetMode.Resting) return 0;
            long duration = TimeSpan.FromMinutes(IntervalMinutes).Ticks;
            long elapsed = duration - Remaining(utcNow).Ticks;
            if (elapsed * 15 < duration * 8) return 0;
            if (elapsed * 15 < duration * 12) return 1;
            if (elapsed * 15 < duration * 14) return 2;
            return 3;
        }

        public bool Tick(DateTime utcNow)
        {
            if (Mode == PetMode.Paused || Mode == PetMode.Resting) return false;
            if (Mode == PetMode.Cooling)
            {
                if (utcNow < NextDueUtc) return false;
                Mode = PetMode.Whipping;
                nextStrikeUtc = utcNow;
            }
            if (utcNow < nextStrikeUtc) return false;
            StrikeCount++;
            double seconds = StrikeDelays[AngerLevel - 1];
            nextStrikeUtc = utcNow.AddSeconds(seconds);
            return true;
        }

        public void Calm(DateTime utcNow)
        {
            if (Mode == PetMode.Resting) return;
            Mode = PetMode.Cooling;
            StrikeCount = 0;
            NextDueUtc = utcNow.AddMinutes(IntervalMinutes);
            nextStrikeUtc = NextDueUtc;
            pausedRemaining = TimeSpan.Zero;
        }

        public void Rest(DateTime utcNow)
        {
            Mode = PetMode.Resting;
            StrikeCount = 0;
            pausedRemaining = TimeSpan.Zero;
            NextDueUtc = DateTime.MaxValue;
            nextStrikeUtc = DateTime.MaxValue;
        }

        public void StartWork(DateTime utcNow)
        {
            if (Mode != PetMode.Resting) return;
            Mode = PetMode.Cooling;
            Calm(utcNow);
        }

        public void SetInterval(int minutes, DateTime utcNow)
        {
            IntervalMinutes = ClampInterval(minutes);
            if (Mode == PetMode.Cooling) Calm(utcNow);
            else if (Mode == PetMode.Paused)
            {
                pausedRemaining = TimeSpan.FromMinutes(IntervalMinutes);
                NextDueUtc = utcNow.Add(pausedRemaining);
            }
        }

        public void TogglePause(DateTime utcNow)
        {
            if (Mode == PetMode.Resting) return;
            if (Mode == PetMode.Paused)
            {
                TimeSpan remaining = pausedMode == PetMode.Cooling
                    ? pausedRemaining : TimeSpan.FromMinutes(IntervalMinutes);
                Calm(utcNow);
                NextDueUtc = utcNow.Add(remaining);
                nextStrikeUtc = NextDueUtc;
                return;
            }
            pausedMode = Mode;
            pausedRemaining = Mode == PetMode.Cooling
                ? Remaining(utcNow) : TimeSpan.FromMinutes(IntervalMinutes);
            Mode = PetMode.Paused;
        }

        public void TestWhip(DateTime utcNow)
        {
            Mode = PetMode.Whipping;
            StrikeCount = 0;
            nextStrikeUtc = utcNow;
        }

        public void Complete(DateTime utcNow)
        {
            if (Mode == PetMode.Resting) return;
            CompletedCount++;
            Calm(utcNow);
        }

        private static int ClampInterval(int minutes)
        {
            return Math.Max(1, Math.Min(180, minutes));
        }
    }

    public static class WorkHours
    {
        public static bool IsWorking(DateTime localNow, int startMinutes, int endMinutes)
        {
            int nowMinutes = localNow.Hour * 60 + localNow.Minute;
            if (startMinutes <= endMinutes)
                return nowMinutes >= startMinutes && nowMinutes < endMinutes;
            return nowMinutes >= startMinutes || nowMinutes < endMinutes;
        }

        public static DateTime NextStart(DateTime localNow, int startMinutes)
        {
            DateTime nextStart = localNow.Date.AddMinutes(startMinutes);
            return nextStart > localNow ? nextStart : nextStart.AddDays(1);
        }
    }
}
