using LoopCx.Features.ActiveRun;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static TimeSpan Minutes(double value) => TimeSpan.FromMinutes(value);
static TimedSession Example() => TimedSession.Intervals(5, Minutes(3), Minutes(2), 7, 2, Minutes(3), Minutes(2));

var session = Example();
session.Advance(Minutes(28));
Require(session.IsFinished, "The full 28-minute protocol must finish.");
Require(session.MainActiveTime == Minutes(23), "Main time must include four recoveries.");
Require(session.TotalActiveTime == Minutes(28), "Total must include preparation.");
Require(session.Completed.Count(p => p.Plan.Kind == RunPhase.Work) == 5, "Five work phases.");
Require(session.Completed.Count(p => p.Plan.Kind == RunPhase.Recovery) == 4, "Four recoveries.");
session.Advance(Minutes(10));
Require(session.TotalActiveTime == Minutes(28), "Finishing must be idempotent.");

session = Example();
session.Advance(Minutes(1));
session.Extend();
session.Extend();
Require(session.Remaining == Minutes(4), "Two extensions add exactly 120 seconds.");
session.SkipPreparation();
Require(session.MainActiveTime == TimeSpan.Zero, "Skipping warmup resets main counters.");
Require(session.Completed[0].ActiveTime == Minutes(1), "Preserve actual warmup.");
session.Advance(Minutes(3));
Require(session.Current?.Kind == RunPhase.Recovery, "Work transitions to recovery.");
session.Advance(Minutes(2));
Require(session.Current?.Kind == RunPhase.Work, "Recovery advances without movement input.");
session.Pause();
session.Advance(Minutes(10));
Require(session.PhaseActiveTime == TimeSpan.Zero, "Manual pause freezes active time.");
session.Resume();
session.Advance(Minutes(18));
Require(session.Current?.Kind == RunPhase.Cooldown, "Main completes before cooldown.");
session.Extend();
Require(session.Remaining == Minutes(3), "Cooldown supports extension.");
session.Advance(Minutes(0.5));
session.SkipPreparation();
Require(session.IsFinished, "Cooldown can be skipped.");
Require(session.Completed[^1].ActiveTime == Minutes(0.5), "Preserve actual cooldown.");

var single = TimedSession.Intervals(1, Minutes(3), Minutes(2), 7, 2, TimeSpan.Zero, TimeSpan.Zero);
single.Advance(Minutes(100));
Require(single.Completed.Count == 1 && single.MainActiveTime == Minutes(3), "One repetition has no recovery.");

var stepped = Example();
for (var second = 0; second < 1680; second++) stepped.Advance(TimeSpan.FromSeconds(1));
Require(stepped.IsFinished && stepped.MainActiveTime == Minutes(23), "Tick frequency must not change the result.");

try
{
    TimedSession.Intervals(1, Minutes(3), Minutes(2), 11, 2, TimeSpan.Zero, TimeSpan.Zero);
    throw new InvalidOperationException("Invalid RPE was accepted.");
}
catch (ArgumentException) { }

Console.WriteLine("PASS: phase order, timing, extensions, skip, pause, RPE and tick independence.");

var commands = Example();
Require(!commands.Resume(), "Resume without a pause changes nothing.");
Require(commands.Pause() && !commands.Pause(), "Duplicate pause is idempotent.");
commands.Advance(Minutes(10));
Require(commands.TotalActiveTime == TimeSpan.Zero, "Duplicate pause keeps time frozen.");
Require(commands.Resume() && !commands.Resume(), "Duplicate resume is idempotent.");
Require(commands.Extend() && commands.Extend(), "Distinct extensions both apply.");
Require(commands.SkipPreparation(), "Warmup can be skipped once.");
Require(!commands.SkipPreparation() && !commands.Extend(), "Preparation actions during work change nothing.");
commands.Advance(Minutes(100));
Require(!commands.Pause() && !commands.Resume() && !commands.SkipPreparation() && !commands.Extend(),
    "Completed runs reject every interactive command.");
Console.WriteLine("PASS: effective command results distinguish state changes from duplicate or inapplicable actions.");

// Arrange
var continuous = TimedSession.Continuous(Minutes(45), 3);
var initialPhase = continuous.Current;

// Act
continuous.Advance(Minutes(20));
var paused = continuous.Pause();
continuous.Advance(Minutes(10));
var pausedMainTime = continuous.MainActiveTime;
var pausedRemaining = continuous.Remaining;
var preparationRejected = !continuous.Extend() && !continuous.SkipPreparation();
var resumed = continuous.Resume();
continuous.Advance(Minutes(100));
var finishedCommandsRejected = !continuous.Pause() && !continuous.Resume();

// Assert
Require(continuous.Plan.Count == 1 && initialPhase == new TimedPhase(RunPhase.Work, Minutes(45), 3),
    "C2 starts directly in one 45-minute work phase at RPE 3.");
Require(paused && pausedMainTime == Minutes(20) && pausedRemaining == Minutes(25),
    "Pause freezes continuous main time and countdown.");
Require(preparationRejected && resumed, "C2 has no preparation actions and resumes explicitly.");
Require(continuous.IsFinished && continuous.Completed.Count == 1 && continuous.TotalActiveTime == Minutes(45)
    && continuous.MainActiveTime == Minutes(45), "C2 finalizes once at 45 active minutes with no preparation.");
Require(finishedCommandsRejected, "Finalized continuous session rejects pause/resume.");
Console.WriteLine("PASS: continuous C2 45-minute RPE3 plan, pause/resume and bounded finalization.");
