using System;

namespace XWhip
{
    public enum PetMode { Cooling, Whipping }

    public sealed class PetState
    {
        private static readonly double[] StrikeDelays = { 3.2, 2.8, 2.4, 2.1, 1.8, 1.55 };
        private DateTime nextStrikeUtc;

        public int IntervalMinutes { get; private set; }
        public PetMode Mode { get; private set; }
        public DateTime NextDueUtc { get; private set; }
        public int StrikeCount { get; private set; }
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
            if (Mode == PetMode.Whipping) return TimeSpan.Zero;
            TimeSpan remaining = NextDueUtc - utcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        public int CountdownStage(DateTime utcNow)
        {
            if (Mode == PetMode.Whipping) return 4;
            long duration = TimeSpan.FromMinutes(IntervalMinutes).Ticks;
            long elapsed = duration - Remaining(utcNow).Ticks;
            if (elapsed * 15 < duration * 8) return 0;
            if (elapsed * 15 < duration * 12) return 1;
            if (elapsed * 15 < duration * 14) return 2;
            return 3;
        }

        public bool Tick(DateTime utcNow)
        {
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
            Mode = PetMode.Cooling;
            StrikeCount = 0;
            NextDueUtc = utcNow.AddMinutes(IntervalMinutes);
            nextStrikeUtc = NextDueUtc;
        }

        public void SetInterval(int minutes, DateTime utcNow)
        {
            IntervalMinutes = ClampInterval(minutes);
            if (Mode == PetMode.Cooling) Calm(utcNow);
        }

        public void TestWhip(DateTime utcNow)
        {
            Mode = PetMode.Whipping;
            StrikeCount = 0;
            nextStrikeUtc = utcNow;
        }

        private static int ClampInterval(int minutes)
        {
            return Math.Max(1, Math.Min(180, minutes));
        }
    }
}
