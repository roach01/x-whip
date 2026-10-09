using System;
using XWhip;

internal static class PetStateTests
{
    private static readonly DateTime Start = new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);
    private static int passed;

    private static int Main()
    {
        try
        {
            Run("Initial state and interval clamps", InitialState);
            Run("First strike exactly at deadline", Deadline);
            Run("Strike delays, escalation, and anger cap", StrikeEscalation);
            Run("Large time jumps emit one strike per tick", LargeTimeJump);
            Run("Calm resets full cooldown and strikes", CalmReset);
            Run("Cooling pause freezes and restores remaining time", CoolingPause);
            Run("Whipping pause resumes full cooldown", WhippingPause);
            Run("Cooling interval changes restart timer", CoolingInterval);
            Run("Whipping interval changes preserve strike schedule", WhippingInterval);
            Run("Paused interval changes update saved remaining time", PausedInterval);
            Run("Test whip emits on next tick", ManualWhip);
            Run("Complete increments count and calms", Completion);
            Run("Default cooldown stage boundaries", DefaultStages);
            Run("Custom cooldown stages scale proportionally", CustomStages);
            Run("Paused and whipping stage overrides", ModeStages);
            Run("Rest interrupts whipping immediately", RestInterruptsWhipping);
            Run("Resting actions cannot wake or count", RestingActions);
            Run("Resting can explicitly test whip", RestingTestWhip);
            Run("Rest clears a paused cooldown", RestFromPause);
            Run("Start work creates a new full cooldown", StartWorkCooldown);
            Run("Resting interval changes apply on next work start", RestingInterval);
            Run("Daytime work hours include start and exclude end", DaytimeWorkHours);
            Run("Overnight work hours cross midnight", OvernightWorkHours);
            Run("Next work start is strictly after the current instant", NextWorkStart);
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

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!object.Equals(expected, actual))
            throw new Exception(message + ": expected " + expected + ", got " + actual);
    }

    private static void InitialState()
    {
        var state = new PetState(15, Start);
        Equal(PetMode.Cooling, state.Mode, "mode");
        Equal(0, state.StrikeCount, "strikes");
        Equal(0, state.CompletedCount, "completions");
        Equal(1, state.AngerLevel, "anger");
        Equal(Start.AddMinutes(15), state.NextDueUtc, "deadline");
        Equal(TimeSpan.FromMinutes(15), state.Remaining(Start), "remaining");
        Equal(1, new PetState(-3, Start).IntervalMinutes, "lower clamp");
        Equal(180, new PetState(200, Start).IntervalMinutes, "upper clamp");
    }

    private static void Deadline()
    {
        var state = new PetState(15, Start);
        Equal(false, state.Tick(Start.AddMinutes(15).AddTicks(-1)), "before deadline");
        Equal(true, state.Tick(Start.AddMinutes(15)), "at deadline");
        Equal(PetMode.Whipping, state.Mode, "whipping mode");
        Equal(1, state.StrikeCount, "first strike");
        Equal(TimeSpan.Zero, state.Remaining(Start.AddMinutes(15)), "whipping remaining");
        Equal(false, state.Tick(Start.AddMinutes(15)), "duplicate tick");
    }

    private static void StrikeEscalation()
    {
        var state = new PetState(15, Start);
        state.TestWhip(Start);
        DateTime now = Start;
        double[] delays = { 3.2, 2.2, 1.3, 1.1, 0.95, 0.8 };
        for (int count = 1; count <= 24; count++)
        {
            Equal(true, state.Tick(now), "strike " + count);
            Equal(count, state.StrikeCount, "strike count");
            int anger = Math.Min(6, (count + 2) / 3);
            Equal(anger, state.AngerLevel, "anger " + count);
            double delay = delays[anger - 1];
            DateTime next = now.AddSeconds(delay);
            Equal(false, state.Tick(next.AddTicks(-1)), "before next strike");
            now = next;
        }
    }

    private static void LargeTimeJump()
    {
        var state = new PetState(15, Start);
        DateTime later = Start.AddDays(1);
        Equal(true, state.Tick(later), "overdue strike");
        Equal(1, state.StrikeCount, "no catchup burst");
        Equal(false, state.Tick(later), "same instant");
        Equal(true, state.Tick(later.AddDays(1)), "second long jump");
        Equal(2, state.StrikeCount, "still one strike");
    }

    private static void CalmReset()
    {
        var state = new PetState(15, Start);
        state.TestWhip(Start);
        state.Tick(Start);
        DateTime now = Start.AddMinutes(2);
        state.Calm(now);
        Equal(PetMode.Cooling, state.Mode, "calm mode");
        Equal(0, state.StrikeCount, "calm strikes");
        Equal(now.AddMinutes(15), state.NextDueUtc, "fresh deadline");
        Equal(false, state.Tick(now), "calm no strike");
    }

    private static void CoolingPause()
    {
        var state = new PetState(15, Start);
        state.TogglePause(Start.AddMinutes(5));
        Equal(PetMode.Paused, state.Mode, "pause mode");
        Equal(TimeSpan.FromMinutes(10), state.Remaining(Start.AddDays(1)), "frozen remaining");
        Equal(false, state.Tick(Start.AddDays(1)), "no paused strike");
        DateTime resume = Start.AddDays(1);
        state.TogglePause(resume);
        Equal(PetMode.Cooling, state.Mode, "resume mode");
        Equal(resume.AddMinutes(10), state.NextDueUtc, "saved remaining");
        Equal(false, state.Tick(resume.AddMinutes(10).AddTicks(-1)), "before resumed deadline");
        Equal(true, state.Tick(resume.AddMinutes(10)), "resumed deadline");
    }

    private static void WhippingPause()
    {
        var state = new PetState(15, Start);
        state.TestWhip(Start);
        state.Tick(Start);
        state.TogglePause(Start.AddSeconds(1));
        DateTime resume = Start.AddHours(1);
        state.TogglePause(resume);
        Equal(PetMode.Cooling, state.Mode, "whipping resumes cooling");
        Equal(0, state.StrikeCount, "resume clears strikes");
        Equal(TimeSpan.FromMinutes(15), state.Remaining(resume), "resume full interval");
    }

    private static void CoolingInterval()
    {
        var state = new PetState(15, Start);
        DateTime now = Start.AddMinutes(4);
        state.SetInterval(5, now);
        Equal(Start.AddMinutes(9), state.NextDueUtc, "interval restart");
        state.SetInterval(0, now);
        Equal(1, state.IntervalMinutes, "clamped change");
        Equal(now.AddMinutes(1), state.NextDueUtc, "clamped deadline");
    }

    private static void WhippingInterval()
    {
        var state = new PetState(15, Start);
        state.TestWhip(Start);
        state.Tick(Start);
        state.SetInterval(5, Start.AddSeconds(1));
        Equal(PetMode.Whipping, state.Mode, "still whipping");
        Equal(1, state.StrikeCount, "preserved strikes");
        Equal(false, state.Tick(Start.AddSeconds(3.2).AddTicks(-1)), "preserved schedule before");
        Equal(true, state.Tick(Start.AddSeconds(3.2)), "preserved schedule due");
        state.Calm(Start.AddSeconds(4));
        Equal(TimeSpan.FromMinutes(5), state.Remaining(Start.AddSeconds(4)), "new interval on calm");
    }

    private static void PausedInterval()
    {
        var state = new PetState(15, Start);
        state.TogglePause(Start.AddMinutes(2));
        state.SetInterval(8, Start.AddMinutes(3));
        Equal(PetMode.Paused, state.Mode, "still paused");
        Equal(TimeSpan.FromMinutes(8), state.Remaining(Start.AddHours(1)), "new saved interval");
        state.TogglePause(Start.AddHours(2));
        Equal(Start.AddHours(2).AddMinutes(8), state.NextDueUtc, "new resume deadline");
    }

    private static void ManualWhip()
    {
        var state = new PetState(15, Start);
        state.TestWhip(Start.AddMinutes(1));
        Equal(PetMode.Whipping, state.Mode, "manual mode");
        Equal(0, state.StrikeCount, "no immediate emitted strike");
        Equal(true, state.Tick(Start.AddMinutes(1)), "next tick strike");
        state.TestWhip(Start.AddMinutes(2));
        Equal(0, state.StrikeCount, "manual reset");
    }

    private static void Completion()
    {
        var state = new PetState(15, Start);
        state.TestWhip(Start);
        state.Tick(Start);
        state.Complete(Start.AddSeconds(1));
        Equal(1, state.CompletedCount, "first completion");
        Equal(PetMode.Cooling, state.Mode, "complete mode");
        Equal(0, state.StrikeCount, "complete clears strikes");
        Equal(Start.AddSeconds(1).AddMinutes(15), state.NextDueUtc, "complete deadline");
        state.Calm(Start.AddSeconds(2));
        Equal(1, state.CompletedCount, "calm does not count completion");
        state.Complete(Start.AddSeconds(3));
        Equal(2, state.CompletedCount, "second completion");
    }

    private static void DefaultStages()
    {
        var state = new PetState(15, Start);
        Equal(0, state.CountdownStage(Start), "initial stage");
        Equal(0, state.CountdownStage(Start.AddMinutes(8).AddTicks(-1)), "before stage one");
        Equal(1, state.CountdownStage(Start.AddMinutes(8)), "stage one boundary");
        Equal(1, state.CountdownStage(Start.AddMinutes(12).AddTicks(-1)), "before stage two");
        Equal(2, state.CountdownStage(Start.AddMinutes(12)), "stage two boundary");
        Equal(2, state.CountdownStage(Start.AddMinutes(14).AddTicks(-1)), "before stage three");
        Equal(3, state.CountdownStage(Start.AddMinutes(14)), "stage three boundary");
        Equal(3, state.CountdownStage(Start.AddDays(1)), "overdue before tick");
    }

    private static void CustomStages()
    {
        var state = new PetState(3, Start);
        DateTime stageOne = Start.AddSeconds(96);
        DateTime stageTwo = Start.AddSeconds(144);
        DateTime stageThree = Start.AddSeconds(168);
        Equal(0, state.CountdownStage(stageOne.AddTicks(-1)), "custom before stage one");
        Equal(1, state.CountdownStage(stageOne), "custom stage one");
        Equal(1, state.CountdownStage(stageTwo.AddTicks(-1)), "custom before stage two");
        Equal(2, state.CountdownStage(stageTwo), "custom stage two");
        Equal(2, state.CountdownStage(stageThree.AddTicks(-1)), "custom before stage three");
        Equal(3, state.CountdownStage(stageThree), "custom stage three");
    }

    private static void ModeStages()
    {
        var state = new PetState(15, Start);
        state.TogglePause(Start.AddMinutes(14));
        Equal(0, state.CountdownStage(Start.AddDays(1)), "paused stage");
        state.TestWhip(Start.AddDays(1));
        Equal(4, state.CountdownStage(Start.AddDays(1)), "whipping stage");
    }

    private static void RestInterruptsWhipping()
    {
        var state = new PetState(15, Start);
        state.TestWhip(Start);
        state.Tick(Start);
        state.Tick(Start.AddSeconds(3.2));
        state.Rest(Start.AddSeconds(4));
        Equal(PetMode.Resting, state.Mode, "rest mode");
        Equal(0, state.StrikeCount, "rest clears strikes");
        Equal(DateTime.MaxValue, state.NextDueUtc, "rest has no deadline");
        Equal(TimeSpan.Zero, state.Remaining(Start.AddDays(1)), "rest remaining");
        Equal(0, state.CountdownStage(Start.AddDays(1)), "rest countdown stage");
        Equal(false, state.Tick(Start.AddDays(1)), "rest stops strikes");
    }

    private static void RestingActions()
    {
        var state = new PetState(15, Start);
        state.Complete(Start);
        state.Rest(Start.AddMinutes(1));
        Action<DateTime>[] actions = { state.Calm, state.TogglePause, state.Complete };
        for (int index = 0; index < actions.Length; index++)
        {
            DateTime now = Start.AddMinutes(index + 2);
            actions[index](now);
            Equal(PetMode.Resting, state.Mode, "action stays resting " + index);
            Equal(1, state.CompletedCount, "action preserves count " + index);
            Equal(0, state.StrikeCount, "action preserves zero strikes " + index);
            Equal(DateTime.MaxValue, state.NextDueUtc, "action preserves no deadline " + index);
            Equal(false, state.Tick(now), "action cannot emit strike " + index);
        }
    }

    private static void RestingTestWhip()
    {
        var state = new PetState(15, Start);
        state.Rest(Start);
        state.TestWhip(Start.AddSeconds(1));
        Equal(PetMode.Whipping, state.Mode, "explicit rest test enters whipping");
        Equal(0, state.StrikeCount, "test resets strikes");
        Equal(true, state.Tick(Start.AddSeconds(1)), "test emits on next tick");
        Equal(1, state.StrikeCount, "test first strike");
        Equal(0, state.CompletedCount, "test does not record completion");
    }

    private static void RestFromPause()
    {
        var state = new PetState(15, Start);
        state.TogglePause(Start.AddMinutes(5));
        state.Rest(Start.AddMinutes(6));
        state.TogglePause(Start.AddMinutes(7));
        Equal(PetMode.Resting, state.Mode, "pause toggle cannot restore old mode");
        state.StartWork(Start.AddMinutes(8));
        Equal(TimeSpan.FromMinutes(15), state.Remaining(Start.AddMinutes(8)), "old paused remaining discarded");
    }

    private static void StartWorkCooldown()
    {
        var state = new PetState(15, Start);
        state.Rest(Start.AddMinutes(4));
        DateTime now = Start.AddDays(2);
        state.StartWork(now);
        Equal(PetMode.Cooling, state.Mode, "start work mode");
        Equal(TimeSpan.FromMinutes(15), state.Remaining(now), "start full remaining");
        Equal(now.AddMinutes(15), state.NextDueUtc, "start fresh deadline");
        state.StartWork(now.AddMinutes(1));
        Equal(now.AddMinutes(15), state.NextDueUtc, "redundant work start preserves running timer");
        Equal(false, state.Tick(now.AddMinutes(15).AddTicks(-1)), "before new deadline");
        Equal(true, state.Tick(now.AddMinutes(15)), "new deadline strike");
    }

    private static void RestingInterval()
    {
        var state = new PetState(15, Start);
        state.Rest(Start);
        state.SetInterval(5, Start.AddMinutes(1));
        Equal(PetMode.Resting, state.Mode, "interval does not wake");
        Equal(5, state.IntervalMinutes, "rest interval updated");
        Equal(DateTime.MaxValue, state.NextDueUtc, "interval does not create deadline");
        Equal(TimeSpan.Zero, state.Remaining(Start.AddMinutes(1)), "interval no rest countdown");
        DateTime workStart = Start.AddHours(1);
        state.StartWork(workStart);
        Equal(workStart.AddMinutes(5), state.NextDueUtc, "updated interval used on wake");
        state.Rest(workStart);
        state.SetInterval(300, workStart);
        Equal(180, state.IntervalMinutes, "rest interval clamps");
        state.StartWork(workStart.AddHours(1));
        Equal(TimeSpan.FromMinutes(180), state.Remaining(workStart.AddHours(1)), "clamped interval used on wake");
    }

    private static void DaytimeWorkHours()
    {
        DateTime day = Start.Date;
        Equal(false, WorkHours.IsWorking(day.AddHours(9).AddTicks(-1), 540, 1080), "before start");
        Equal(true, WorkHours.IsWorking(day.AddHours(9), 540, 1080), "at start");
        Equal(true, WorkHours.IsWorking(day.AddHours(18).AddTicks(-1), 540, 1080), "before end");
        Equal(false, WorkHours.IsWorking(day.AddHours(18), 540, 1080), "at end");
        Equal(false, WorkHours.IsWorking(day.AddHours(23), 540, 1080), "after end");
    }

    private static void OvernightWorkHours()
    {
        DateTime day = Start.Date;
        Equal(false, WorkHours.IsWorking(day.AddHours(22).AddTicks(-1), 1320, 360), "before overnight start");
        Equal(true, WorkHours.IsWorking(day.AddHours(22), 1320, 360), "at overnight start");
        Equal(true, WorkHours.IsWorking(day.AddDays(1), 1320, 360), "after midnight");
        Equal(true, WorkHours.IsWorking(day.AddDays(1).AddHours(6).AddTicks(-1), 1320, 360), "before overnight end");
        Equal(false, WorkHours.IsWorking(day.AddDays(1).AddHours(6), 1320, 360), "at overnight end");
        Equal(false, WorkHours.IsWorking(day.AddHours(12), 1320, 360), "overnight midday rest");
        Equal(true, WorkHours.IsWorking(day.AddHours(23), 1320, 0), "shift ending at midnight");
        Equal(false, WorkHours.IsWorking(day.AddDays(1), 1320, 0), "midnight end excluded");
    }

    private static void NextWorkStart()
    {
        DateTime day = Start.Date;
        DateTime nine = day.AddHours(9);
        Equal(nine, WorkHours.NextStart(nine.AddTicks(-1), 540), "before start returns today");
        Equal(nine.AddDays(1), WorkHours.NextStart(nine, 540), "exact start returns tomorrow");
        Equal(nine.AddDays(1), WorkHours.NextStart(nine.AddSeconds(1), 540), "after start returns tomorrow");
        Equal(day.AddHours(22), WorkHours.NextStart(day.AddHours(6), 1320), "overnight next start today");
        Equal(day.AddDays(1), WorkHours.NextStart(day, 0), "midnight start strictly next day");
        DateTime yearEnd = new DateTime(2026, 12, 31, 23, 0, 0);
        Equal(new DateTime(2027, 1, 1, 9, 0, 0), WorkHours.NextStart(yearEnd, 540), "year rollover");
    }
}
