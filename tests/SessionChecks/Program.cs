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

// Arrange / Act: standard BLE Heart Rate Measurement flags and byte order.
var narrowHeartRate = HeartRateMeasurement.Parse([6, 72]);
var wideHeartRate = HeartRateMeasurement.Parse([7, 44, 1]);

// Assert: unknown contact, invalid/truncated values and zero never fabricate a reading.
Require(narrowHeartRate == 72 && wideHeartRate == 300, "Decode 8-bit and little-endian 16-bit heart rate.");
Require(HeartRateMeasurement.Parse([0, 72]) == 72, "Unavailable contact detection does not reject a valid measurement.");
Require(HeartRateMeasurement.Parse([4, 72]) is null && HeartRateMeasurement.Parse([5, 44, 1]) is null,
    "Supported contact detection with lost contact yields unknown heart rate.");
Require(HeartRateMeasurement.Parse([]) is null && HeartRateMeasurement.Parse([1, 72]) is null &&
    HeartRateMeasurement.Parse([0, 0]) is null, "Malformed, truncated and zero readings remain unknown.");
Console.WriteLine("PASS: standard BLE heart-rate payload, 8/16-bit flags and unknown/contact-loss semantics.");

// Arrange
var policy = DistancePolicy.Pilot with { MaximumGap = TimeSpan.FromSeconds(200), MaximumAge = TimeSpan.FromSeconds(200) };
var measured = new RunDistance(policy, TimeSpan.Zero, RunPhase.Work);
static DistanceFix Fix(double seconds, double metres, double accuracy = 1)
{
    return new(TimeSpan.FromSeconds(seconds), 0,
    metres / 6371000 * 180 / Math.PI, accuracy);
}

// Act
measured.Observe(Fix(0, 0), TimeSpan.Zero, TimeSpan.Zero);
measured.Observe(Fix(100, 1000), TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(100));
measured.Observe(Fix(200, 2000), TimeSpan.FromSeconds(200), TimeSpan.FromSeconds(200));
measured.Observe(Fix(240, 2400), TimeSpan.FromSeconds(240), TimeSpan.FromSeconds(240));
measured.Boundary(RunPhase.Cooldown, TimeSpan.FromSeconds(240), TimeSpan.FromSeconds(240));
var measuredResult = measured.Snapshot;

// Assert
Require(Math.Abs(measuredResult.MainMeasuredMetres - 2400) < .001 && measuredResult.Splits.Count == 3,
    "Accepted main distance produces two kilometre splits and the actual final partial.");
Require(Math.Abs(measuredResult.Splits.Sum(static split => split.MeasuredMetres) - measuredResult.MainMeasuredMetres) < .001
    && measuredResult.Splits.Aggregate(TimeSpan.Zero, static (sum, split) => sum + split.ActiveTime) == TimeSpan.FromSeconds(240),
    "Splits conserve accepted distance and active main time.");
Require(!measuredResult.MainHasGaps && measuredResult.Splits.All(split => !split.HasGaps),
    "Finishing a completely trusted main run does not manufacture a GPS gap.");

// Arrange
var interruptedDistance = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Work);

// Act
interruptedDistance.Observe(Fix(0, 0), TimeSpan.Zero, TimeSpan.Zero);
interruptedDistance.Observe(Fix(1, 5), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
interruptedDistance.Interrupt(TimeSpan.FromSeconds(2), "Manual pause");
interruptedDistance.Observe(Fix(10, 100), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2));
interruptedDistance.Observe(Fix(11, 105), TimeSpan.FromSeconds(11), TimeSpan.FromSeconds(3));
var pausedDistance = interruptedDistance.Snapshot.MainMeasuredMetres;
interruptedDistance.Observe(Fix(11, 105), TimeSpan.FromSeconds(11), TimeSpan.FromSeconds(3));
interruptedDistance.Observe(Fix(12, 110), TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(4));
var duplicateDistance = interruptedDistance.Snapshot.MainMeasuredMetres;

// Assert
Require(Math.Abs(pausedDistance - 10) < .001 && Math.Abs(duplicateDistance - (pausedDistance + 5)) < .001,
    "Pause breaks anchors; duplicate delivery neither charges twice nor destroys the next real edge.");

// Arrange
var slowDistance = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Work);

// Act
for (var second = 0; second <= 20; second++)
    slowDistance.Observe(Fix(second, second, 3), TimeSpan.FromSeconds(second), TimeSpan.FromSeconds(second));

// Assert
Require(slowDistance.Snapshot.MainMeasuredMetres is > 15 and <= 20.001,
    "Retained anchors accumulate slow motion despite per-fix movement below accuracy hysteresis.");
Console.WriteLine("PASS: ordinary measured distance, kilometre/final splits, pause/duplicate gaps and slow-movement anchors.");

// Arrange
var multipleSplits = new RunDistance(policy, TimeSpan.Zero, RunPhase.Work);
var rejectedDistance = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Work);

// Act
multipleSplits.Observe(Fix(0, 0), TimeSpan.Zero, TimeSpan.Zero);
multipleSplits.Observe(Fix(200, 2200), TimeSpan.FromSeconds(200), TimeSpan.FromSeconds(200));
multipleSplits.Boundary(null, TimeSpan.FromSeconds(210), TimeSpan.FromSeconds(210));
rejectedDistance.Observe(Fix(0, 0), TimeSpan.Zero, TimeSpan.Zero);
rejectedDistance.Observe(Fix(1, 1000), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
rejectedDistance.Observe(Fix(2, 10), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
rejectedDistance.Observe(Fix(3, 15, 31), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
rejectedDistance.Observe(Fix(4, 20), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4));
rejectedDistance.Observe(Fix(20, 100), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
rejectedDistance.Observe(Fix(19, 95), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
rejectedDistance.Observe(Fix(21, 105), TimeSpan.FromSeconds(21), TimeSpan.FromSeconds(21));
rejectedDistance.Observe(Fix(22, 110), TimeSpan.FromSeconds(22), TimeSpan.FromSeconds(22));

// Assert
Require(multipleSplits.Snapshot.Splits.Count == 3 &&
    Math.Abs(multipleSplits.Snapshot.Splits[2].MeasuredMetres - 200) < .001 &&
    multipleSplits.Snapshot.Splits.Sum(static split => split.ActiveTime.TotalSeconds) == 210,
    "One accepted edge may cross multiple kilometres; the final active-time tail is conserved.");
Require(Math.Abs(rejectedDistance.Snapshot.MainMeasuredMetres - 10) < .001 && rejectedDistance.Snapshot.MainHasGaps,
    "Jump, poor accuracy, long gap and out-of-order fixes never bridge rejected distance.");

// Arrange
var preparationDistance = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Warmup);

// Act
preparationDistance.Observe(Fix(0, 0), TimeSpan.Zero, TimeSpan.Zero);
preparationDistance.Observe(Fix(1, 5), TimeSpan.FromSeconds(1), TimeSpan.Zero);
preparationDistance.Boundary(RunPhase.Work, TimeSpan.FromSeconds(2), TimeSpan.Zero);
preparationDistance.Observe(Fix(1.5, 8), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));
preparationDistance.Observe(Fix(3, 15), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));
preparationDistance.Observe(Fix(4, 20), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(2));
preparationDistance.Boundary(RunPhase.Recovery, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3));
preparationDistance.Observe(Fix(6, 30), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(4));
preparationDistance.Observe(Fix(7, 35), TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(5));

// Assert
Require(Math.Abs(preparationDistance.Snapshot.TotalMeasuredMetres - 25) < .001 &&
    Math.Abs(preparationDistance.Snapshot.MainMeasuredMetres - 20) < .001,
    "Preparation is excluded from main, recovery included, and a delayed old-phase fix cannot charge the current phase.");
Console.WriteLine("PASS: distance quality rejection, delayed phase boundaries and multiple split crossings.");

// Arrange
var stationaryThenJump = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Work);

// Act
for (var second = 0; second <= 60; second++)
    stationaryThenJump.Observe(Fix(second, 0), TimeSpan.FromSeconds(second), TimeSpan.FromSeconds(second));
stationaryThenJump.Observe(Fix(61, 100), TimeSpan.FromSeconds(61), TimeSpan.FromSeconds(61));

// Assert
Require(stationaryThenJump.Snapshot.MainMeasuredMetres == 0 && stationaryThenJump.Snapshot.Status == "Implausible GPS jump",
    "A long retained stationary anchor cannot dilute the speed of one sudden GPS jump.");

// Arrange
var continuousRecovery = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Work);

// Act
continuousRecovery.Observe(Fix(0, 0), TimeSpan.Zero, TimeSpan.Zero);
continuousRecovery.Observe(Fix(1, 5), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
continuousRecovery.Boundary(RunPhase.Recovery, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
continuousRecovery.Observe(Fix(3, 15), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));

// Assert
Require(Math.Abs(continuousRecovery.Snapshot.MainMeasuredMetres - 15) < .001 && !continuousRecovery.Snapshot.MainHasGaps,
    "A work-to-recovery transition preserves continuous main evidence and creates no artificial GPS gap.");

// Assert
Require(!measuredResult.MainHasGaps && measuredResult.Splits.All(static split => !split.HasGaps),
    "Finishing a completely trusted main run does not manufacture a GPS gap.");

// Arrange
var duplicateDelivery = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Work);

// Act
duplicateDelivery.Observe(Fix(0, 0), TimeSpan.Zero, TimeSpan.Zero);
duplicateDelivery.Observe(Fix(1, 5), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
duplicateDelivery.Observe(Fix(1, 5), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
duplicateDelivery.Observe(Fix(.5, 2), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
duplicateDelivery.Observe(Fix(2, 10), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

// Assert
Require(Math.Abs(duplicateDelivery.Snapshot.MainMeasuredMetres - 10) < .001 && !duplicateDelivery.Snapshot.MainHasGaps,
    "Duplicate and out-of-order packets leave newer trusted distance and quality unchanged.");

// Arrange
var motionPolicy = MovementPolicy.Pilot;
var motionRun = TimedSession.Continuous(Minutes(45), 3);
var motionRules = new RunMotion(motionPolicy);
var stationary = new DistanceFix(TimeSpan.Zero, 59, 18, 5);
var startedCandidate = motionRules.Observe(stationary, TimeSpan.Zero, 0, RunPhase.Work, PauseReason.None, false);

// Act
for (var second = 1; second <= 9; second++)
{
    motionRun.Advance(TimeSpan.FromSeconds(1));
    _ = motionRules.Observe(stationary with { MonotonicTime = TimeSpan.FromSeconds(second) },
        TimeSpan.FromSeconds(second), 0, RunPhase.Work, PauseReason.None, false);
}
var beforeThreshold = motionRules.Observe(stationary with { MonotonicTime = TimeSpan.FromSeconds(9.999) },
    TimeSpan.FromSeconds(9.999), 0, RunPhase.Work, PauseReason.None, false);
motionRun.Advance(TimeSpan.FromSeconds(3));
var delayedConfirmation = motionRules.Observe(stationary with { MonotonicTime = TimeSpan.FromSeconds(10.5) },
    TimeSpan.FromSeconds(12), 0, RunPhase.Work, PauseReason.None, false);
var autoPaused = motionRun.PauseAutomatically(TimeSpan.FromSeconds(10), motionRules.Event!.PhaseIndex);
motionRun.Advance(TimeSpan.FromSeconds(20));
var delayedEvent = motionRules.Event;

// Assert
Require(startedCandidate == MotionDecision.CandidateStarted && beforeThreshold == MotionDecision.None
    && delayedConfirmation == MotionDecision.Pause && autoPaused, "Only a continuous reliable ten-second window confirms.");
Require(motionRun.TotalActiveTime == TimeSpan.FromSeconds(2) && motionRun.MainActiveTime == TimeSpan.FromSeconds(2)
    && motionRun.Remaining == Minutes(45) - TimeSpan.FromSeconds(2),
    "Sample10.5/receipt12 excludes exactly ten seconds and retains the two-second active tail.");
Require(delayedEvent!.OnsetMonotonicTime == TimeSpan.Zero
    && delayedEvent.ExcludedThroughMonotonicTime == TimeSpan.FromSeconds(10)
    && delayedEvent.SampleMonotonicTime == TimeSpan.FromSeconds(10.5)
    && delayedEvent.DetectedMonotonicTime == TimeSpan.FromSeconds(12), "Keep all four timing meanings explicit.");

// Arrange
var extendedRun = TimedSession.Intervals(1, Minutes(3), Minutes(2), 7, 2, Minutes(8), Minutes(4));
var initialDose = extendedRun.Plan[0].Duration;

// Act
extendedRun.Advance(TimeSpan.FromSeconds(5));
var extended = extendedRun.Extend();
extendedRun.Advance(TimeSpan.FromSeconds(5));
var extendedPaused = extendedRun.PauseAutomatically(TimeSpan.FromSeconds(10), 0);
var promotedManual = extendedRun.Pause();
var automaticResumeRejected = !extendedRun.ResumeAutomatically();
var manualResumed = extendedRun.Resume();

// Assert
Require(extended && extendedPaused && extendedRun.Plan[0].Duration == initialDose + Minutes(1)
    && extendedRun.Remaining == Minutes(9), "Retrospective exclusion never restores away the real extension.");
Require(promotedManual && automaticResumeRejected && manualResumed,
    "Explicit Pause promotes automatic pause to manual; only manual Resume releases it.");

// Arrange
var interruptedMotion = new RunMotion(motionPolicy);
interruptedMotion.Observe(stationary, TimeSpan.Zero, 0, RunPhase.Work, PauseReason.None, false);

// Act
interruptedMotion.Observe(stationary with { MonotonicTime = TimeSpan.FromSeconds(2) },
    TimeSpan.FromSeconds(2), 0, RunPhase.Work, PauseReason.None, false);
interruptedMotion.Observe(stationary with { MonotonicTime = TimeSpan.FromSeconds(1) },
    TimeSpan.FromSeconds(2), 0, RunPhase.Work, PauseReason.None, false);
var retainedOnset = interruptedMotion.Candidate!.OnsetMonotonicTime;
interruptedMotion.UpdateFreshness(TimeSpan.FromSeconds(6));
var gapCleared = interruptedMotion.Candidate is null;
interruptedMotion.Boundary(TimeSpan.FromSeconds(6));
var recoveryDecision = interruptedMotion.Observe(stationary with { MonotonicTime = TimeSpan.FromSeconds(7) },
    TimeSpan.FromSeconds(7), 1, RunPhase.Recovery, PauseReason.None, false);
var finishedDecision = interruptedMotion.Observe(stationary with { MonotonicTime = TimeSpan.FromSeconds(8) },
    TimeSpan.FromSeconds(8), 2, null, PauseReason.None, false);

// Assert
Require(retainedOnset == TimeSpan.Zero && gapCleared && recoveryDecision == MotionDecision.None
    && finishedDecision == MotionDecision.None && interruptedMotion.Candidate is null,
    "Old packets preserve newer evidence; actual gaps, recovery and finish cannot confirm standstill.");

// Arrange
var oppositeCluster = new RunMotion(motionPolicy);
var precise = stationary with { AccuracyMetres = 1 };
oppositeCluster.Observe(precise, TimeSpan.Zero, 0, RunPhase.Work, PauseReason.None, false);

// Act
oppositeCluster.Observe(precise with { MonotonicTime = TimeSpan.FromSeconds(1), Latitude = 59 - (2d / 111195) },
    TimeSpan.FromSeconds(1), 0, RunPhase.Work, PauseReason.None, false);
var opposite = oppositeCluster.Observe(precise with { MonotonicTime = TimeSpan.FromSeconds(2), Latitude = 59 + (2d / 111195) },
    TimeSpan.FromSeconds(2), 0, RunPhase.Work, PauseReason.None, true);

// Assert
Require(opposite == MotionDecision.None && oppositeCluster.Candidate is null,
    "An accepted moving edge cancels even when both endpoints lie inside the stationary cluster.");
Console.WriteLine("PASS: exact ten-second retrospective autopause, delayed receipt tail, manual precedence, extension and quality barriers.");


// Arrange
var splitRollback = new RunDistance(policy, TimeSpan.Zero, RunPhase.Work);
splitRollback.Observe(Fix(0, 0), TimeSpan.Zero, TimeSpan.Zero);
splitRollback.Observe(Fix(100, 999), TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(100));
var onsetDerived = splitRollback.CaptureCandidate();
var priorMeasured = splitRollback.Snapshot.MainMeasuredMetres;

// Act
splitRollback.ConfirmStandstill(onsetDerived, TimeSpan.FromSeconds(112), true);
splitRollback.Reanchor(TimeSpan.FromSeconds(120), "Automatic resume");
splitRollback.Observe(Fix(120, 1500), TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(102));
splitRollback.Observe(Fix(121, 1506), TimeSpan.FromSeconds(121), TimeSpan.FromSeconds(103));
splitRollback.Boundary(null, TimeSpan.FromSeconds(121), TimeSpan.FromSeconds(103));
var conservedSplits = splitRollback.Snapshot;

// Assert
Require(Math.Abs(conservedSplits.MainMeasuredMetres - (priorMeasured + 6)) < .001
    && conservedSplits.Splits.Count == 2 && Math.Abs(conservedSplits.Splits[0].MeasuredMetres - 1000) < .001,
    "Onset near a kilometre keeps prior distance and does not bridge automatic-pause relocation.");
Require(conservedSplits.Splits.Aggregate(TimeSpan.Zero, static (sum, split) => sum + split.ActiveTime)
    == TimeSpan.FromSeconds(103) && conservedSplits.MainHasGaps,
    "Ten excluded seconds disappear from split totals; the two-second active unknown tail remains.");

// Arrange
var boundaryRun = TimedSession.Intervals(2, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(4), 7, 2,
    TimeSpan.Zero, TimeSpan.Zero);
var boundaryMotion = new RunMotion(motionPolicy);
var boundaryCandidate = boundaryMotion.Observe(Fix(0, 0), TimeSpan.Zero, 0, RunPhase.Work, PauseReason.None, false);

// Act
boundaryRun.Advance(TimeSpan.FromSeconds(10), (_, consumed) => boundaryMotion.Boundary(consumed));
var lateOldPhaseSample = boundaryMotion.Observe(Fix(5, 0), TimeSpan.FromSeconds(10), boundaryRun.PhaseIndex,
    boundaryRun.Current?.Kind, PauseReason.None, false);
var oldPhaseCannotRewind = !boundaryRun.PauseAutomatically(TimeSpan.FromSeconds(10), 0);
boundaryRun.Advance(TimeSpan.FromSeconds(100));
var finishedCannotRewind = !boundaryRun.PauseAutomatically(TimeSpan.FromSeconds(10), boundaryRun.PhaseIndex);

// Assert
Require(boundaryCandidate == MotionDecision.CandidateStarted && lateOldPhaseSample == MotionDecision.None
    && oldPhaseCannotRewind && finishedCannotRewind && boundaryRun.TotalActiveTime == TimeSpan.FromSeconds(16),
    "Boundary-first processing prevents old samples crossing recovery, repeated work instances or finish.");

// Arrange
var slowJog = new RunMotion(motionPolicy);
var jogEverPaused = false;
var automaticResume = new RunMotion(motionPolicy);
for (var second = 0; second <= 10; second++)
{
    _ = automaticResume.Observe(Fix(second, 0, 5), TimeSpan.FromSeconds(second), 0, RunPhase.Work, PauseReason.None, false);
}

// Act
for (var second = 0; second <= 20; second++)
{
    // Distance hysteresis may still report no accepted edge at this slow speed.
    jogEverPaused |= slowJog.Observe(Fix(second, second * .6, 5), TimeSpan.FromSeconds(second),
        0, RunPhase.Work, PauseReason.None, false) == MotionDecision.Pause;
}
automaticResume.Unavailable();
var firstReacquired = automaticResume.Observe(Fix(15, 100, 5), TimeSpan.FromSeconds(15), 0,
    RunPhase.Work, PauseReason.Automatic, false);
var confirmedResume = automaticResume.Observe(Fix(17, 111, 5), TimeSpan.FromSeconds(18), 0,
    RunPhase.Work, PauseReason.Automatic, false);
var resumeEvent = automaticResume.Event;

// Assert
Require(!jogEverPaused, "Slow movement cannot become standstill just because distance hysteresis hides individual edges.");
Require(firstReacquired == MotionDecision.None && confirmedResume == MotionDecision.Resume
    && resumeEvent!.SampleMonotonicTime == TimeSpan.FromSeconds(17)
    && resumeEvent.DetectedMonotonicTime == TimeSpan.FromSeconds(18),
    "Unknown while automatic pause waits for fresh positive movement and resumes only at receipt.");
Console.WriteLine("PASS: autopause kilometre conservation, phase-instance/final barriers, slow jog and unknown-to-resume.");

// Arrange
var officialC1 = CoreTemplate.CreateC1Session();
var legacyC1 = TimedSession.Intervals(5, Minutes(3), Minutes(2), 7, 2, Minutes(10), Minutes(5));
var legacyPlan = System.Text.Json.JsonSerializer.Serialize(legacyC1.Plan);
var legacyTemplate = CoreTemplate.C1 with { TemplateRevision = 1 };

// Act
legacyC1.Advance(Minutes(2));
officialC1.Advance(Minutes(8));
var officialMainStartsWithoutPreparationTime = officialC1.Current?.Kind == RunPhase.Work
    && officialC1.MainActiveTime == TimeSpan.Zero;
officialC1.Advance(Minutes(23));
var officialCooldown = officialC1.Current?.Kind == RunPhase.Cooldown && officialC1.Remaining == Minutes(4);
officialC1.Advance(Minutes(4));
var restoredLegacyPlan = System.Text.Json.JsonSerializer.Deserialize<TimedPhase[]>(legacyPlan)!;
var legacySaved = new TimedSession(restoredLegacyPlan);
legacySaved.Advance(Minutes(100));

// Assert
Require(CoreTemplate.C1.TemplateRevision == 2 && CoreTemplate.C2.TemplateRevision == 1,
    "Only the new official C1 template revision changes.");
Require(officialMainStartsWithoutPreparationTime && officialCooldown && officialC1.IsFinished,
    "Official C1 has an eight-minute warm-up, unchanged main and four-minute cool-down.");
Require(officialC1.TotalActiveTime == Minutes(35) && officialC1.MainActiveTime == Minutes(23)
    && officialC1.Completed.Count(static p => p.Plan.Kind == RunPhase.Work) == 5
    && officialC1.Completed.Count(static p => p.Plan.Kind == RunPhase.Recovery) == 4,
    "Official C1 totals 35 minutes with five work efforts and four recoveries in its 23-minute main.");
Require(legacyC1.Remaining == Minutes(8) && legacyTemplate.TemplateRevision == 1
    && System.Text.Json.JsonSerializer.Serialize(legacyC1.Plan) == legacyPlan,
    "Creating and advancing revision 2 cannot rewrite an existing revision 1 plan or metadata.");
Require(legacySaved.TotalActiveTime == Minutes(38) && legacySaved.MainActiveTime == Minutes(23),
    "Serialized legacy 10/5 plans remain readable with their original 38-minute dose.");
Console.WriteLine("PASS: official C1 revision 2 is 8/4 and 35 minutes; frozen revision 1 remains 10/5 and 38.");

// Arrange
var exactEnd = TimedSession.Continuous(TimeSpan.FromSeconds(10), 3);
var exactEndMotion = new RunMotion(motionPolicy);
for (var second = 0; second < 10; second++)
{
    _ = exactEndMotion.Observe(Fix(second, 0, 5), TimeSpan.FromSeconds(second), 0, RunPhase.Work, PauseReason.None, false);
}

// Act
exactEnd.Advance(TimeSpan.FromSeconds(10), (_, at) => exactEndMotion.Boundary(at));
var atEndDecision = exactEndMotion.Observe(Fix(10, 0, 5), TimeSpan.FromSeconds(10), exactEnd.PhaseIndex,
    exactEnd.Current?.Kind, PauseReason.None, false);

// Assert
Require(exactEnd.IsFinished && atEndDecision == MotionDecision.None
    && !exactEnd.PauseAutomatically(TimeSpan.FromSeconds(10), 0)
    && exactEnd.TotalActiveTime == TimeSpan.FromSeconds(10),
    "At exact phase/finish equality the already-processed terminal boundary wins over confirmation.");

// Arrange
var bracketCoverage = new RunStatistics();
var bracketDistance = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Work);
bracketCoverage.Advance(TimeSpan.Zero, TimeSpan.FromSeconds(12), RunPhase.Work, false);
bracketDistance.Observe(Fix(0, 0, 5), TimeSpan.Zero, TimeSpan.Zero);
bracketCoverage.Observe(bracketDistance.Observation);

// Act
for (var second = 1; second <= 9; second++)
{
    _ = bracketDistance.Observe(Fix(second, 0, 5), TimeSpan.FromSeconds(second), TimeSpan.FromSeconds(second));
    bracketCoverage.Observe(bracketDistance.Observation);
}
bracketDistance.Observe(Fix(10.5, 0, 5), TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(12));
bracketCoverage.Observe(bracketDistance.Observation);
bracketCoverage.Observe(bracketDistance.Observation); // Replaying a supported bracket still counts once.
bracketCoverage.Exclude(TimeSpan.Zero, TimeSpan.FromSeconds(10));
bracketCoverage.Exclude(TimeSpan.Zero, TimeSpan.FromSeconds(10));
var delayedCoverage = bracketCoverage.Snapshot(TimeSpan.FromSeconds(12), bracketDistance.Snapshot, true);

// Assert
Require(delayedCoverage.Total.ActiveTime == TimeSpan.FromSeconds(2)
    && delayedCoverage.Main.KnownGpsActiveTime == TimeSpan.FromSeconds(.5)
    && delayedCoverage.Main.UnknownGpsActiveTime == TimeSpan.FromSeconds(1.5),
    "Union confirming brackets before exact exclusion: preserve supported .5-second tail and unknown 1.5-second receipt lag.");
Require(delayedCoverage.Main.AveragePaceSecondsPerKilometre is null
    && delayedCoverage.Main.AverageHeartRate is null
    && delayedCoverage.Main.KnownHeartRateActiveTime == TimeSpan.Zero
    && delayedCoverage.Main.UnknownHeartRateActiveTime == delayedCoverage.Main.ActiveTime,
    "Incomplete/zero-distance pace and unavailable production heart rate remain unknown.");

// Arrange
var scopedCoverage = new RunStatistics();
var scopedDistance = new RunDistance(policy, TimeSpan.Zero, RunPhase.Warmup);
scopedCoverage.Advance(TimeSpan.Zero, TimeSpan.FromSeconds(10), RunPhase.Warmup, false);
scopedCoverage.Advance(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(110), RunPhase.Work, false);
scopedCoverage.Advance(TimeSpan.FromSeconds(110), TimeSpan.FromSeconds(210), RunPhase.Recovery, false);
scopedCoverage.Advance(TimeSpan.FromSeconds(210), TimeSpan.FromSeconds(220), RunPhase.Work, true);
scopedCoverage.Advance(TimeSpan.FromSeconds(220), TimeSpan.FromSeconds(240), RunPhase.Work, false);
scopedCoverage.Advance(TimeSpan.FromSeconds(240), TimeSpan.FromSeconds(250), RunPhase.Cooldown, false);

// Act
scopedDistance.Observe(Fix(0, 0), TimeSpan.Zero, TimeSpan.Zero);
scopedCoverage.Observe(scopedDistance.Observation);
scopedDistance.Observe(Fix(10, 50), TimeSpan.FromSeconds(10), TimeSpan.Zero);
scopedCoverage.Observe(scopedDistance.Observation);
scopedDistance.Boundary(RunPhase.Work, TimeSpan.FromSeconds(10), TimeSpan.Zero);
scopedDistance.Observe(Fix(10, 50), TimeSpan.FromSeconds(10), TimeSpan.Zero);
scopedCoverage.Observe(scopedDistance.Observation);
scopedDistance.Observe(Fix(110, 1050), TimeSpan.FromSeconds(110), TimeSpan.FromSeconds(100));
scopedCoverage.Observe(scopedDistance.Observation);
scopedDistance.Boundary(RunPhase.Recovery, TimeSpan.FromSeconds(110), TimeSpan.FromSeconds(100));
scopedDistance.Observe(Fix(210, 2050), TimeSpan.FromSeconds(210), TimeSpan.FromSeconds(200));
scopedCoverage.Observe(scopedDistance.Observation);
scopedDistance.Interrupt(TimeSpan.FromSeconds(210), "Manual pause");
scopedDistance.Reanchor(TimeSpan.FromSeconds(220), "Manual resume");
scopedDistance.Observe(Fix(220, 3000), TimeSpan.FromSeconds(220), TimeSpan.FromSeconds(200));
scopedCoverage.Observe(scopedDistance.Observation);
scopedDistance.Observe(Fix(240, 3200), TimeSpan.FromSeconds(240), TimeSpan.FromSeconds(220));
scopedCoverage.Observe(scopedDistance.Observation);
var liveCoverage = scopedCoverage.Snapshot(TimeSpan.FromSeconds(250), scopedDistance.Snapshot, true);
scopedDistance.Boundary(RunPhase.Cooldown, TimeSpan.FromSeconds(240), TimeSpan.FromSeconds(220));
scopedDistance.Observe(Fix(240, 3200), TimeSpan.FromSeconds(240), TimeSpan.FromSeconds(220));
scopedCoverage.Observe(scopedDistance.Observation);
scopedDistance.Observe(Fix(250, 3250), TimeSpan.FromSeconds(250), TimeSpan.FromSeconds(220));
scopedCoverage.Observe(scopedDistance.Observation);
var finalCoverage = scopedCoverage.Snapshot(TimeSpan.FromSeconds(250), scopedDistance.Snapshot, false);

// Assert
Require(liveCoverage.CurrentSplit is not null && finalCoverage.CurrentSplit is null,
    "Only the unfinished remainder is current; final partial appears once among completed splits.");
Require(finalCoverage.Total.ActiveTime == TimeSpan.FromSeconds(240)
    && finalCoverage.Main.ActiveTime == TimeSpan.FromSeconds(220)
    && finalCoverage.Main.KnownGpsActiveTime == TimeSpan.FromSeconds(220)
    && finalCoverage.Main.UnknownGpsActiveTime == TimeSpan.Zero,
    "Preparation contributes only to total, Recovery to main, pause to neither and valid brackets conserve known active time.");
Require(finalCoverage.CompletedSplits.Aggregate(TimeSpan.Zero, static (sum, split) => sum + split.ActiveTime)
    == finalCoverage.Main.ActiveTime
    && finalCoverage.CompletedSplits.Aggregate(TimeSpan.Zero, static (sum, split) => sum + split.KnownGpsActiveTime)
    == finalCoverage.Main.KnownGpsActiveTime
    && Math.Abs(finalCoverage.CompletedSplits.Sum(static split => split.MeasuredMetres) - finalCoverage.Main.MeasuredMetres) < .001,
    "Kilometre and final-partial coverage/metres use the exact existing distance split boundaries.");
Require(finalCoverage.Main.AveragePaceSecondsPerKilometre is > 0,
    "A completely supported positive-distance scope reports its active-time/distance pace.");
Console.WriteLine("PASS: bracket union, exact retrospective coverage, total/main/current/final split conservation and honest HR unknowns.");


// Arrange
var gapsCoverage = new RunStatistics();
var gapsDistance = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Work);
gapsCoverage.Advance(TimeSpan.Zero, TimeSpan.FromSeconds(20), RunPhase.Work, false);

// Act
foreach (var sample in new[] { Fix(2, 0), Fix(4, 0), Fix(3, 0), Fix(6, 0), Fix(7, 0, 100),
    Fix(8, 0), Fix(9, 0), Fix(20, 0) })
{
    _ = gapsDistance.Observe(sample, sample.MonotonicTime < TimeSpan.FromSeconds(4)
        ? TimeSpan.FromSeconds(4) : sample.MonotonicTime, sample.MonotonicTime);
    gapsCoverage.Observe(gapsDistance.Observation);
}
var honestGaps = gapsCoverage.Snapshot(TimeSpan.FromSeconds(20), gapsDistance.Snapshot, true);

// Assert
Require(honestGaps.Main.KnownGpsActiveTime == TimeSpan.FromSeconds(5)
    && honestGaps.Main.UnknownGpsActiveTime == TimeSpan.FromSeconds(15)
    && honestGaps.Main.MeasuredMetres == 0 && honestGaps.Main.AveragePaceSecondsPerKilometre is null,
    "Only valid2-6 and8-9 brackets count: initial/final tails, poor quality and excessive gap never extrapolate; old packets do not break newer evidence.");

// Arrange
var coreDefaults = CorePreferencesState.Default;
var coreExpected = new[] { ("C1.1", 1290, 1710, 10, 2), ("C1.2", 1380, 1800, 7, 3), ("C1.3", 1440, 1860, 6, 3) };

// Act
var coreResults = coreExpected.Select(expected =>
{
    var resolved = CoreTemplate.Resolve(new(expected.Item1), coreDefaults.WarmupCooldown);
    resolved.Session.Advance(TimeSpan.FromHours(2));
    return (Expected: expected, Resolved: resolved);
}).ToArray();

// Assert
foreach (var (expected, resolved) in coreResults)
{
    var (id, mainSeconds, totalSeconds, workRpe, recoveryRpe) = expected;
    Require(resolved.Session.MainActiveTime == TimeSpan.FromSeconds(mainSeconds)
        && resolved.Session.TotalActiveTime == TimeSpan.FromSeconds(totalSeconds)
        && resolved.Session.Completed.Where(static phase => phase.Plan.Kind == RunPhase.Work).All(phase => phase.Plan.TargetRpe == workRpe)
        && resolved.Session.Completed.Where(static phase => phase.Plan.Kind == RunPhase.Recovery).All(phase => phase.Plan.TargetRpe == recoveryRpe),
        "Each official protocol retains its approved dose, N−1 recoveries and fixed RPE.");
    Require(resolved.Template.TemplateRevision == 3 && resolved.Template.ProtocolId == id && resolved.Template.ProtocolRevision == 1,
        "The new C1 family cannot relabel frozen C1 revisions 1/2.");
}

// Arrange
var shortcutRequest = new CoreStartRequest("C1.1", 9, Targets: new(CoreTargetMode.Pace, 330, 420));
var savedShortcut = new CoreShortcut(Guid.NewGuid(), "My sprints", shortcutRequest);
var selectedState = coreDefaults with { SelectedProtocol = "C1.1", Shortcuts = [savedShortcut] };

// Act
selectedState.Validate();
var frozenFirst = CoreTemplate.Resolve(savedShortcut.Request, new(4, 3));
var originalFirst = System.Text.Json.JsonSerializer.Serialize(frozenFirst.Session.Plan);
var laterStart = CoreTemplate.Resolve(savedShortcut.Request, new(0, 15));
var noWarmupCooldown = CoreTemplate.Resolve(new("C1.2"), new(0, 0));
var c2New = CoreTemplate.Resolve(new("C2", ContinuousMinutes: 20), new(15, 15));
var preferencesRoundtrip = System.Text.Json.JsonSerializer.Deserialize<CorePreferencesState>(System.Text.Json.JsonSerializer.Serialize(selectedState))!;
preferencesRoundtrip.Validate();

// Assert
Require(preferencesRoundtrip.SelectedProtocol == "C1.1" && preferencesRoundtrip.Shortcuts.Single() == savedShortcut,
    "Versioned preference JSON preserves selection and named shortcut identity/count/target.");
Require(laterStart.Session.Plan[0].Kind == RunPhase.Work && laterStart.Session.Plan[^1].Duration == Minutes(15)
    && laterStart.Template.Targets == shortcutRequest.Targets && originalFirst == System.Text.Json.JsonSerializer.Serialize(frozenFirst.Session.Plan),
    "Shortcut count/target stays saved, current global warm-up/cool-down resolves at each start and earlier plans remain frozen.");
Require(noWarmupCooldown.Session.Plan.All(static phase => phase.Kind is RunPhase.Work or RunPhase.Recovery),
    "Off omits actual warm-up/cool-down phases rather than inserting zero-duration phases.");
Require(c2New.Session.Plan.Single() == new TimedPhase(RunPhase.Work, Minutes(20), 4)
    && c2New.Template.TemplateRevision == 2 && c2New.Template.ResolvedWarmupCooldown is null,
    "C2 uses the selected duration and revision2 RPE4 without inheriting global warm-up/cool-down.");

// Arrange
var invalidCoreRequests = CoreProtocols.All.SelectMany(static protocol => new[]
{
    new CoreStartRequest(protocol.Id, protocol.MinimumCount - 1), new CoreStartRequest(protocol.Id, protocol.MaximumCount + 1)
}).Concat([ new CoreStartRequest("C2", ContinuousMinutes: 19), new CoreStartRequest("C2", ContinuousMinutes: 21),
    new CoreStartRequest("C2", ContinuousMinutes: 121), new CoreStartRequest("custom"),
    new CoreStartRequest("C2", Repetitions: 5), new CoreStartRequest("C1.2", ContinuousMinutes: 45),
    new CoreStartRequest("C1.2", Targets: new(CoreTargetMode.Pace, 0)),
    new CoreStartRequest("C1.2", Targets: new(CoreTargetMode.HeartRateZone, 6)) ]).ToArray();

// Act
var coreRejected = invalidCoreRequests.Select(request =>
{
    try { _ = CoreTemplate.Resolve(request, new()); return false; }
    catch (ArgumentException) { return true; }
}).ToArray();
var boundaryCorePlans = CoreProtocols.All.SelectMany(static protocol => new[] { protocol.MinimumCount, protocol.MaximumCount }
    .Select(count => (Count: count, CoreTemplate.Resolve(new(protocol.Id, count), new()).Session.Plan))).ToArray();
var invalidSettingsRejected = new[] { new C1WarmupCooldownSettings(-1, 3), new C1WarmupCooldownSettings(16, 3),
    new C1WarmupCooldownSettings(4, -1), new C1WarmupCooldownSettings(4, 16) }.Select(settings =>
{
    try { _ = CoreTemplate.Resolve(new("C1.2"), settings); return false; }
    catch (ArgumentException) { return true; }
}).ToArray();
var fractionalRejected = false;
try { _ = System.Text.Json.JsonSerializer.Deserialize<CoreStartRequest>("{\"ProtocolId\":\"C1.2\",\"Repetitions\":4.5}"); }
catch (System.Text.Json.JsonException) { fractionalRejected = true; }
var singleCooldownOff = CoreTemplate.Resolve(new("C1.2"), new(1, 0));
var maximumSettings = CoreTemplate.Resolve(new("C1.2"), new(15, 15));
var c2Maximum = CoreTemplate.Resolve(new("C2", ContinuousMinutes: 120), new());

// Assert
Require(coreRejected.All(static rejected => rejected) && invalidSettingsRejected.All(static rejected => rejected) && fractionalRejected,
    "Unknown protocols, forbidden fields, bad targets, fractional reps, range neighbours and invalid Settings are rejected.");
Require(boundaryCorePlans.All(static result => result.Plan.Count(static phase => phase.Kind == RunPhase.Recovery) == result.Count - 1),
    "Both permitted repetition boundaries retain exactly N−1 recoveries.");
Require(singleCooldownOff.Session.Plan[0].Duration == Minutes(1) && singleCooldownOff.Session.Plan[^1].Kind == RunPhase.Work
    && maximumSettings.Session.Plan[0].Duration == Minutes(15) && maximumSettings.Session.Plan[^1].Duration == Minutes(15)
    && c2Maximum.Session.Plan.Single().Duration == Minutes(120), "Independent phase opt-outs and both upper bounds remain valid.");
Console.WriteLine("PASS: Core-first protocols, guarded repetitions/C2 duration, Settings opt-out, frozen shortcuts/targets and legacy identities.");

// Arrange
var hrConfiguration = new HeartRateConfiguration(1, null, HeartRatePolicy.Pilot,
    [new(1, 50, 99), new(2, 100, 119), new(3, 120, 139), new(4, 140, 159), new(5, 160, 220)]);
var hr = new RunHeartRate(hrConfiguration);
var hrStatistics = new RunStatistics();
var hrUtc = DateTimeOffset.Parse("2026-11-14T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
HeartRateObservation Reading(double seconds, int? bpm, string quality = "valid")
{
    return new(TimeSpan.FromSeconds(seconds), hrUtc.AddSeconds(seconds), bpm, quality);
}

var hrDistance = new RunDistance(DistancePolicy.Pilot, TimeSpan.Zero, RunPhase.Work);

// Act
hrStatistics.Advance(TimeSpan.Zero, TimeSpan.FromSeconds(12), RunPhase.Work, false);
hrStatistics.ObserveHeartRate(hr.Observe(Reading(0, 100)));
hrStatistics.ObserveHeartRate(hr.Observe(Reading(2, 140)));
hrStatistics.ObserveHeartRate(hr.Observe(Reading(2, 200)));
hrStatistics.ObserveHeartRate(hr.Observe(Reading(1, 200)));
hrStatistics.ObserveHeartRate(hr.Observe(Reading(4, 120)));
hrStatistics.ObserveHeartRate(hr.Observe(Reading(5, null, "contact-or-payload-invalid")));
hrStatistics.ObserveHeartRate(hr.Observe(Reading(6, 160)));
hrStatistics.ObserveHeartRate(hr.Observe(Reading(10.5, 180)));
hrStatistics.Exclude(TimeSpan.Zero, TimeSpan.FromSeconds(10));
var hrTail = hrStatistics.Snapshot(TimeSpan.FromSeconds(12), hrDistance.Snapshot, true);

// Assert
Require(hrTail.Total.ActiveTime == TimeSpan.FromSeconds(2) && hrTail.Total.KnownHeartRateActiveTime == TimeSpan.FromSeconds(.5)
    && hrTail.Total.UnknownHeartRateActiveTime == TimeSpan.FromSeconds(1.5) && hrTail.Total.AverageHeartRate == 160,
    "Exact automatic-pause excision retains supported HR tail and leaves receipt tail unknown; valid average does not require complete coverage.");
Require(hrTail.Total.HeartRateZones!.Single().Zone == 5 && hrTail.Total.HeartRateZones!.Single().ActiveTime == TimeSpan.FromSeconds(.5)
    && hr.Snapshot(TimeSpan.FromSeconds(16)).CurrentBpm is null,
    "Frozen personal zones classify valid time; stale live HR is null without extrapolating the last reading.");

// Arrange
var weightedHr = new RunHeartRate(HeartRateConfiguration.Empty);
var weightedStatistics = new RunStatistics();

// Act
weightedStatistics.Advance(TimeSpan.Zero, TimeSpan.FromSeconds(10), RunPhase.Warmup, false);
weightedStatistics.ObserveHeartRate(weightedHr.Observe(Reading(0, 100)));
weightedStatistics.ObserveHeartRate(weightedHr.Observe(Reading(2, 140)));
weightedStatistics.ObserveHeartRate(weightedHr.Observe(Reading(6, 180)));
weightedStatistics.ObserveHeartRate(weightedHr.Observe(Reading(20, 200)));
var weightedState = weightedStatistics.Snapshot(TimeSpan.FromSeconds(10), hrDistance.Snapshot, false);

// Assert
Require(Math.Abs(weightedState.Total.AverageHeartRate!.Value - (((100 * 2) + (140 * 4)) / 6d)) < .000001
    && weightedState.Total.KnownHeartRateActiveTime == TimeSpan.FromSeconds(6)
    && weightedState.Total.UnknownHeartRateActiveTime == TimeSpan.FromSeconds(4)
    && weightedState.Main.KnownHeartRateActiveTime == TimeSpan.Zero
    && weightedState.Total.UnclassifiedHeartRateActiveTime == TimeSpan.FromSeconds(6),
    "Time weighting uses supported valid brackets only; warm-up is total-only and missing zones remain unclassified.");
Console.WriteLine("PASS: HR bracket coverage, time weighting, nulls, duplicates, frozen zones and exact automatic-pause exclusions.");


// Arrange
var scopedHr = new RunHeartRate(HeartRateConfiguration.Empty);
var scopedStatistics = new RunStatistics();
var splitDistance = hrDistance.Snapshot with
{
    TotalMeasuredMetres = 1500,
    MainMeasuredMetres = 1500,
    Splits = [new(1, 1000, TimeSpan.FromSeconds(4), false)]
};

// Act
scopedStatistics.Advance(TimeSpan.Zero, TimeSpan.FromSeconds(2), RunPhase.Warmup, false);
scopedStatistics.Advance(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6), RunPhase.Work, false);
scopedStatistics.Advance(TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(8), RunPhase.Work, true);
scopedStatistics.Advance(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(12), RunPhase.Recovery, false);
scopedStatistics.ObserveHeartRate(scopedHr.Observe(Reading(0, 100)));
scopedStatistics.ObserveHeartRate(scopedHr.Observe(Reading(5, 200)));
scopedStatistics.ObserveHeartRate(scopedHr.Observe(Reading(10, 180)));
var scopedHrState = scopedStatistics.Snapshot(TimeSpan.FromSeconds(12), splitDistance, true);
var finalHrState = scopedStatistics.Snapshot(TimeSpan.FromSeconds(12), splitDistance with
{
    Splits = [new(1, 1000, TimeSpan.FromSeconds(4), false), new(2, 500, TimeSpan.FromSeconds(4), true)]
}, false);

// Assert
Require(scopedHrState.Total.ActiveTime == TimeSpan.FromSeconds(10) && scopedHrState.Main.ActiveTime == TimeSpan.FromSeconds(8)
    && scopedHrState.Total.KnownHeartRateActiveTime == TimeSpan.FromSeconds(8)
    && scopedHrState.Main.KnownHeartRateActiveTime == TimeSpan.FromSeconds(6)
    && scopedHrState.CompletedSplits.Single().AverageHeartRate == 125
    && scopedHrState.CurrentSplit!.AverageHeartRate == 200,
    "Supported HR brackets intersect real active scopes and split boundaries; manual pause contributes no counted HR time.");
Require(finalHrState.CurrentSplit is null && finalHrState.CompletedSplits.Sum(static split => split.KnownHeartRateActiveTime.Ticks) == finalHrState.Main.KnownHeartRateActiveTime.Ticks
    && finalHrState.CompletedSplits.Sum(static split => split.UnknownHeartRateActiveTime.Ticks) == finalHrState.Main.UnknownHeartRateActiveTime.Ticks,
    "Final partial HR coverage is attributed once and completed splits conserve main known/unknown time.");
Console.WriteLine("PASS: HR pause/scope intersections, split weighting and final partial conservation.");


// Arrange
var corruptHrPreferences = new[] { "{", "null", "{}",
    /*lang=json,strict*/
                         "{\"Version\":99,\"Policy\":{\"Version\":1,\"MaximumGap\":\"00:00:05\"},\"Zones\":[]}",
    /*lang=json,strict*/
                         "{\"Version\":1,\"Policy\":null,\"Zones\":[]}",
    /*lang=json,strict*/
                         "{\"Version\":1,\"Policy\":{\"Version\":1,\"MaximumGap\":\"00:00:05\"},\"Zones\":null}",
    /*lang=json,strict*/
                         "{\"Version\":1,\"Policy\":{\"Version\":1,\"MaximumGap\":\"00:00:05\"},\"Zones\":[null]}" };
var originalHrPreferenceBytes = corruptHrPreferences.ToArray();

// Act
var optionalHrStarts = corruptHrPreferences.Select(json =>
{
    var setup = HeartRateConfiguration.ReadOptional(json);
    var timed = CoreTemplate.Resolve(new("C2", ContinuousMinutes: 45), new()).Session;
    var unknownHr = new RunHeartRate(setup.Configuration, setup.ConfigurationError);
    timed.Advance(TimeSpan.FromSeconds(2));

    return (Session: timed, HeartRate: unknownHr.Snapshot(TimeSpan.FromSeconds(2)));
}).ToArray();
var absentHr = HeartRateConfiguration.ReadOptional(null);

// Assert
Require(optionalHrStarts.All(static start => start.Session.TotalActiveTime == TimeSpan.FromSeconds(2)
    && start.HeartRate.ConfigurationError is not null && start.HeartRate.Quality == "configuration-invalid"
    && start.HeartRate.CurrentBpm is null && start.HeartRate.Configuration.DeviceAddress is null
    && start.HeartRate.Configuration.Zones.Count == 0),
    "Corrupt, unsupported and null-nested optional HR configurations disable HR with a truthful error while the valid timed workout advances.");
Require(corruptHrPreferences.SequenceEqual(originalHrPreferenceBytes) && absentHr.ConfigurationError is null
    && absentHr.Configuration.DeviceAddress is null,
    "Reading optional HR settings does not overwrite invalid input; absent configuration remains ordinary unavailable HR.");
Console.WriteLine("PASS: corrupt optional HR settings preserve input and never block the timed workout.");
