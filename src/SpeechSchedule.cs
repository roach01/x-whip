using System;

namespace XWhip
{
    public sealed class SpeechSchedule
    {
        public const double IdleDisplaySeconds = 3.0;
        public const double WhipDisplaySeconds = 2.8;

        private readonly Random random;
        private double nextAllowedSpeech;
        private double nextIdle;

        public SpeechSchedule(int seed, double nowSeconds = 0)
        {
            random = new Random(seed);
            nextAllowedSpeech = nowSeconds;
            ResetIdle(nowSeconds);
        }

        public void ResetIdle(double nowSeconds)
        {
            nextIdle = nowSeconds + 20 + random.NextDouble() * 20;
        }

        public bool TryIdle(double nowSeconds)
        {
            if (nowSeconds < nextIdle || nowSeconds < nextAllowedSpeech) return false;
            Spoke(nowSeconds);
            return true;
        }

        public bool TryWhip(double nowSeconds)
        {
            if (nowSeconds < nextAllowedSpeech) return false;
            Spoke(nowSeconds);
            return true;
        }

        private void Spoke(double nowSeconds)
        {
            nextAllowedSpeech = nowSeconds + 6;
            ResetIdle(nowSeconds);
        }
    }
}
