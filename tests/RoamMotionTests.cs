using System;
using XWhip;

internal static class RoamMotionTests
{
    private static int passed;

    private static int Main()
    {
        try
        {
            Run("Startup waits one second", Startup);
            Run("Short walks alternate with pauses", ShortWalksAndPauses);
            Run("Same direction never exceeds two walk segments", DirectionSegments);
            Run("Every free-space turn occurs within eight seconds", TimelyTurns);
            Run("Edges reflect movement and keep position inside bounds", Boundaries);
            Run("Different frame rates produce the same movement", FrameRates);
            Run("Hold freezes position, phase, and segment timing", HoldFreeze);
            Run("Reset clears gait and stands for one second", Reset);
            Run("Stage two increases speed within the requested range", StageSpeed);
            Run("Walk phase follows real distance and stops while standing", WalkPhase);
            Run("Long sleeps advance at most a tenth of a second", LongSleep);
            Run("A fixed seed reproduces the same motion", DeterministicSeed);
            Console.WriteLine("PASS: " + passed + " tests");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            return 1;
        }
    }

    private static void Run(string name, Action test)
    {
        test();
        passed++;
        Console.WriteLine("PASS " + name);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Near(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new Exception(message + ": expected " + expected + ", got " + actual);
    }

    private static RoamMotion NewMotion(int seed)
    {
        var motion = new RoamMotion(seed);
        motion.Reset(500);
        return motion;
    }

    private static void Advance(RoamMotion motion, int frames, double delta, int stage)
    {
        for (int index = 0; index < frames; index++) motion.Tick(delta, stage, -10000, 10000);
    }

    private static void Startup()
    {
        var motion = NewMotion(7);
        Advance(motion, 9, 0.1, 0);
        Check(!motion.Moving, "startup should stand for first 0.9 seconds");
        Near(500, motion.X, 0.000001, "startup position");
        Near(0, motion.WalkPhase, 0.000001, "startup phase");
        motion.Tick(0.1, 0, -10000, 10000);
        Check(motion.Moving, "startup should begin walk after one second");
        motion.Tick(0.1, 0, -10000, 10000);
        Check(Math.Abs(motion.X - 500) > 1, "first walk moves");
    }

    private static void ShortWalksAndPauses()
    {
        var motion = NewMotion(21);
        bool previousMoving = false;
        double segmentStart = 0;
        int completedWalks = 0;
        int completedPauses = 0;
        for (int frame = 1; frame <= 3600; frame++)
        {
            motion.Tick(1.0 / 60, 0, -10000, 10000);
            double now = frame / 60.0;
            if (motion.Moving == previousMoving) continue;
            double length = now - segmentStart;
            if (previousMoving)
            {
                Check(length >= 1.2 - 0.02 && length <= 3.0 + 0.02, "walk duration range");
                completedWalks++;
            }
            else if (segmentStart > 0)
            {
                Check(length >= 0.8 - 0.02 && length <= 2.2 + 0.02, "pause duration range");
                completedPauses++;
            }
            segmentStart = now;
            previousMoving = motion.Moving;
        }
        Check(completedWalks >= 10 && completedPauses >= 10, "walk and pause cycles occur");
    }

    private static void DirectionSegments()
    {
        for (int seed = 0; seed < 40; seed++)
        {
            var motion = NewMotion(seed);
            bool previousMoving = false;
            int previousDirection = 0;
            int streak = 0;
            for (int frame = 0; frame < 3600; frame++)
            {
                motion.Tick(1.0 / 60, 0, -10000, 10000);
                if (motion.Moving && !previousMoving)
                {
                    streak = motion.Direction == previousDirection ? streak + 1 : 1;
                    Check(streak <= 2, "same direction repeated more than two segments, seed " + seed);
                    previousDirection = motion.Direction;
                }
                previousMoving = motion.Moving;
            }
        }
    }

    private static void TimelyTurns()
    {
        for (int seed = 0; seed < 64; seed++)
        {
            var motion = NewMotion(seed);
            int direction = motion.Direction;
            double sinceTurn = 0;
            int turns = 0;
            for (int frame = 0; frame < 3600; frame++)
            {
                motion.Tick(1.0 / 60, 0, -10000, 10000);
                if (motion.Direction != direction)
                {
                    sinceTurn = 0;
                    direction = motion.Direction;
                    turns++;
                }
                else sinceTurn += 1.0 / 60;
                Check(sinceTurn <= 8.02, "more than eight seconds without turning, seed " + seed);
            }
            Check(turns >= 7, "turns should recur");
        }
    }

    private static void Boundaries()
    {
        var motion = new RoamMotion(8);
        motion.Reset(10);
        int direction = motion.Direction;
        int turns = 0;
        for (int frame = 0; frame < 1500; frame++)
        {
            motion.Tick(0.02, 2, 0, 20);
            Check(motion.X >= 0 && motion.X <= 20, "position escaped bounds");
            if (motion.Direction != direction) turns++;
            direction = motion.Direction;
        }
        Check(turns > 10, "narrow bounds should reflect often");
        motion.Reset(0.5);
        for (int frame = 0; frame < 100; frame++)
        {
            motion.Tick(0.1, 2, 0, 1);
            Check(motion.X >= 0 && motion.X <= 1, "multiple bounces escaped bounds");
        }
        motion.Tick(0.1, 0, 3, 3);
        Near(3, motion.X, 0, "zero-width range position");
        Check(!motion.Moving, "zero-width range stands");
    }

    private static void FrameRates()
    {
        var slow = NewMotion(37);
        var fast = NewMotion(37);
        Advance(slow, 600, 1.0 / 20, 0);
        Advance(fast, 1800, 1.0 / 60, 0);
        Near(slow.X, fast.X, 0.00001, "frame-rate position");
        Near(slow.WalkPhase, fast.WalkPhase, 0.00001, "frame-rate phase");
        Check(slow.Direction == fast.Direction && slow.Moving == fast.Moving, "frame-rate state");
    }

    private static void HoldFreeze()
    {
        var motion = NewMotion(15);
        var reference = NewMotion(15);
        Advance(motion, 120, 1.0 / 60, 0);
        Advance(reference, 120, 1.0 / 60, 0);
        Check(motion.Moving, "hold begins during walk");
        double x = motion.X;
        double phase = motion.WalkPhase;
        for (int index = 0; index < 600; index++) motion.Hold();
        Check(!motion.Moving && motion.Speed == 0, "hold stops visible gait");
        Near(x, motion.X, 0, "hold position");
        Near(phase, motion.WalkPhase, 0, "hold phase");
        Advance(motion, 600, 1.0 / 60, 0);
        Advance(reference, 600, 1.0 / 60, 0);
        Near(reference.X, motion.X, 0.000001, "hold preserves segment timing");
        Near(reference.WalkPhase, motion.WalkPhase, 0.000001, "hold preserves resumed phase");
    }

    private static void Reset()
    {
        var motion = NewMotion(42);
        Advance(motion, 120, 1.0 / 60, 0);
        Check(motion.WalkPhase > 0, "pre-reset walk phase");
        motion.Reset(750);
        Check(!motion.Moving && motion.Speed == 0, "reset stands");
        Near(0, motion.WalkPhase, 0, "reset clears gait");
        Advance(motion, 9, 0.1, 0);
        Near(750, motion.X, 0.000001, "reset wait position");
        Check(!motion.Moving, "reset waits one second");
        Advance(motion, 2, 0.1, 0);
        Check(motion.Moving && Math.Abs(motion.X - 750) > 1, "reset resumes after wait");
    }

    private static void StageSpeed()
    {
        var motion = NewMotion(9);
        Advance(motion, 11, 0.1, 0);
        Check(motion.Speed >= 16 && motion.Speed <= 24, "normal speed range");
        double x = motion.X;
        motion.Tick(0.1, 2, -10000, 10000);
        Check(motion.Speed >= 28 && motion.Speed <= 34, "stage two speed range");
        Near(motion.Speed * 0.1, Math.Abs(motion.X - x), 0.000001, "stage two displacement");
        motion.Tick(0.1, 3, -10000, 10000);
        Check(motion.Speed >= 28 && motion.Speed <= 34, "stage three remains urgent");
        motion.Tick(0.1, 1, -10000, 10000);
        Check(motion.Speed >= 16 && motion.Speed <= 24, "earlier stage returns normal speed");
    }

    private static void WalkPhase()
    {
        var motion = NewMotion(2);
        double previousX = motion.X;
        double previousPhase = motion.WalkPhase;
        int standingFrames = 0;
        for (int frame = 0; frame < 1200; frame++)
        {
            motion.Tick(1.0 / 60, 0, -10000, 10000);
            double moved = Math.Abs(motion.X - previousX);
            Near(moved * Math.PI * 2 / 38, motion.WalkPhase - previousPhase, 0.000001, "phase matches real distance");
            if (moved < 0.0000001)
            {
                Near(previousPhase, motion.WalkPhase, 0.000001, "standing does not pedal");
                standingFrames++;
            }
            previousX = motion.X;
            previousPhase = motion.WalkPhase;
        }
        Check(standingFrames > 120, "standing intervals observed");
    }

    private static void LongSleep()
    {
        var motion = NewMotion(10);
        var reference = NewMotion(10);
        Advance(motion, 12, 0.1, 0);
        Advance(reference, 12, 0.1, 0);
        motion.Tick(100, 0, -10000, 10000);
        reference.Tick(0.1, 0, -10000, 10000);
        Near(reference.X, motion.X, 0.000001, "long sleep capped position");
        Near(reference.WalkPhase, motion.WalkPhase, 0.000001, "long sleep capped phase");
    }

    private static void DeterministicSeed()
    {
        var first = NewMotion(123);
        var second = NewMotion(123);
        for (int frame = 0; frame < 600; frame++)
        {
            first.Tick(0.025, 2, 0, 1000);
            second.Tick(0.025, 2, 0, 1000);
            Near(first.X, second.X, 0, "same seed position");
            Near(first.WalkPhase, second.WalkPhase, 0, "same seed phase");
            Check(first.Direction == second.Direction && first.Moving == second.Moving, "same seed state");
        }
    }
}
