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
    motionRules.Observe(stationary with { MonotonicTime = TimeSpan.FromSeconds(second) },
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
oppositeCluster.Observe(precise with { MonotonicTime = TimeSpan.FromSeconds(1), Latitude = 59 - 2d / 111195 },
    TimeSpan.FromSeconds(1), 0, RunPhase.Work, PauseReason.None, false);
var opposite = oppositeCluster.Observe(precise with { MonotonicTime = TimeSpan.FromSeconds(2), Latitude = 59 + 2d / 111195 },
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
    automaticResume.Observe(Fix(second, 0, 5), TimeSpan.FromSeconds(second), 0, RunPhase.Work, PauseReason.None, false);
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
