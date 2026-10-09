using System;

namespace XWhip
{
    public sealed class RoamMotion
    {
        private readonly Random random;
        private bool walking;
        private double remaining = 1.0;
        private double speedFraction;
        private double sinceTurn;
        private int sameDirectionSegments;

        public double X { get; private set; }
        public bool Moving { get; private set; }
        public int Direction { get; private set; }
        public double Speed { get; private set; }
        public double WalkPhase { get; private set; }

        public RoamMotion(int seed)
        {
            random = new Random(seed);
            Direction = 1;
        }

        public void Reset(double x)
        {
            X = x;
            walking = false;
            Moving = false;
            Speed = 0;
            WalkPhase = 0;
            remaining = 1.0;
            sinceTurn = 0;
            sameDirectionSegments = 0;
        }

        public void Hold()
        {
            Moving = false;
            Speed = 0;
        }

        public void Tick(double delta, int stage, double minX, double maxX)
        {
            double timeLeft = Math.Max(0, Math.Min(delta, 0.1));
            X = Math.Max(minX, Math.Min(maxX, X));
            if (maxX <= minX || timeLeft == 0)
            {
                Moving = false;
                Speed = 0;
                return;
            }
            Moving = walking;
            Speed = walking ? GetSpeed(stage) : 0;
            while (timeLeft > 0)
            {
                double step = Math.Min(timeLeft, remaining);
                if (walking) Move(Speed * step, minX, maxX);
                sinceTurn += step;
                remaining -= step;
                timeLeft -= step;
                if (remaining <= 0.000000001)
                {
                    if (walking)
                    {
                        walking = false;
                        remaining = 0.8 + random.NextDouble() * 1.4;
                        Speed = 0;
                    }
                    else BeginWalk(stage);
                    Moving = walking;
                }
            }
        }

        private void BeginWalk(int stage)
        {
            remaining = 1.2 + random.NextDouble() * 1.8;
            int nextDirection = random.Next(2) == 0 ? -1 : 1;
            if (nextDirection == Direction &&
                (sameDirectionSegments >= 2 || sinceTurn + remaining + 2.2 >= 8))
                nextDirection = -Direction;
            if (nextDirection != Direction)
            {
                Direction = nextDirection;
                sinceTurn = 0;
                sameDirectionSegments = 1;
            }
            else sameDirectionSegments++;
            speedFraction = random.NextDouble();
            walking = true;
            Speed = GetSpeed(stage);
        }

        private double GetSpeed(int stage)
        {
            return stage >= 2 ? 28 + speedFraction * 6 : 16 + speedFraction * 8;
        }

        private void Move(double distance, double minX, double maxX)
        {
            WalkPhase += distance * Math.PI * 2 / 38;
            while (distance > 0)
            {
                double room = Direction > 0 ? maxX - X : X - minX;
                double step = Math.Min(distance, room);
                X += Direction * step;
                distance -= step;
                if (step >= room)
                {
                    Direction = -Direction;
                    sinceTurn = 0;
                    sameDirectionSegments = 1;
                }
            }
        }
    }
}
