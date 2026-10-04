namespace SimpleNvidiaUndervolt;

/// <summary>
/// Reset and write of the GPU tuning that MSI Afterburner programs into the driver, plus the curve
/// reads, plausibility checks and interpolation the writes depend on.
///
/// On Ada/Blackwell the tuning lives in several places: the core V/F curve offset in the
/// ClkVfPoints control table, the memory offset baked into the absolute P0 memory clock in
/// pstates 2.0, and the core voltage boost percentage. <see cref="TuningSnapshot"/> reads the same
/// knobs back for display.
/// </summary>
internal static class GpuTuning
{
    /// <summary>Resets tuning to stock, returning a human-readable line per action. Refuses on a card
    /// whose curve doesn't validate — the reset writes zeroes straight into the hardware-specific
    /// control-table offsets, so it needs the same recognized-curve guard as the undervolt write.
    /// A step the driver rejects (e.g. when not elevated) throws, so a partial reset surfaces as a
    /// failure the caller can report instead of masquerading as success.</summary>
    public static IReadOnlyList<string> Clear(IntPtr gpu)
    {
        IReadOnlyList<(int Mv, int Mhz)> curve = NvApi.GetVfCurve(gpu);
        if (!CurveVoltsPlausible(curve))
        {
            throw UnrecognizedCurveError(gpu, "reset the tuning");
        }

        // Zero every per-point core-clock offset the control table can hold (equivalent to the
        // driver's own reset) - the whole table, not just the anchors visible in this status decode,
        // so a foreign delta past a truncated read is cleared too - then the P0 clock offsets and
        // the voltage boost.
        int[] deltas = NvApi.GetCurveFreqDeltasKhz(gpu, NvApi.MaxCurveAnchors);
        NvApi.SetCurveFreqDeltasKhz(gpu, new int[NvApi.MaxCurveAnchors]);
        int cleared = deltas.Count(d => d != 0);
        NvApi.SetPstate0Offsets(gpu, graphicsDeltaKhz: 0, memoryDeltaKhz: 0, coreVoltageDeltaUv: 0);
        NvApi.SetCoreVoltageBoostPercent(gpu, 0);

        return new[]
        {
            cleared > 0 ? $"Core V/F curve: cleared {cleared} offset point(s)." : "Core V/F curve: already stock.",
            "Memory & core clock offsets: reset to 0.",
            "Core voltage boost: reset to 0%.",
        };
    }

    /// <summary>The stable phrase every rejected-transitional-read error carries. The e2e suite keys its
    /// retry-skip on it, so a reworded message must keep this fragment.</summary>
    internal const string TransientReadMarker = "didn't read back cleanly";

    /// <summary>The diagnosis shared by the write refusal below and status's read-only warning, so the
    /// two reports of the same condition can't drift apart.</summary>
    internal const string UnrecognizedCurvePhrase = "the V/F curve didn't read as a recognized NVIDIA table";

    /// <summary>Where an offsets-don't-fit error sends the user, shared by both errors that diagnose it.</summary>
    private const string PortingDocPointer = "See DEVELOPMENT.md (Porting the write offsets to a new card).";

    /// <summary>The refusal every write path uses when <see cref="CurveVoltsPlausible"/> rejects the
    /// curve: the tuning-buffer byte offsets are hardware-specific, so writing to an unrecognized card
    /// would land in unknown fields. Carries the detected-layout report, so the refusal itself is the
    /// bug report to file.</summary>
    public static CliError UnrecognizedCurveError(IntPtr gpu, string action) => new(
        $"Refusing to {action}: {UnrecognizedCurvePhrase} on this GPU, so the tuning-buffer "
        + $"offsets likely don't match this hardware. {PortingDocPointer}\n"
        + string.Join('\n', DetectedLayoutReport(gpu)));

    // --- Undervolt / overclock ---

    /// <summary>The factory base memory clock (MHz) — the reference a memory offset applies to. It is a
    /// static factory value, so unlike the core V/F curve it is readable regardless of power state.</summary>
    public static int BaseMemoryClockMhz(IntPtr gpu)
    {
        uint khz = NvApi.GetClockFrequencyKhz(gpu, NvApi.CLOCK_FREQ_TYPE_BASE, NvApi.CLOCK_DOMAIN_MEMORY);
        if (khz == 0)
        {
            throw new CliError("The base memory clock is unavailable.");
        }

        return (int)(khz / 1000);
    }

    /// <summary>The stock curve a real run plans from: resets all tuning first (each real run starts
    /// from stock anyway) and reads the curve directly, so the plan never depends on the
    /// live-minus-deltas recovery that an applied tuning distorts. <see cref="Clear"/>'s own guard
    /// refuses an unrecognized card before anything is written.</summary>
    public static IReadOnlyList<(int Mv, int Mhz)> ResetAndReadStock(IntPtr gpu)
    {
        Clear(gpu);
        return ReadUntilFreqsReadable(gpu);
    }

    /// <summary>Resets to stock and returns the live curve a reference-planned real run verifies its
    /// write against. The plan itself comes from the saved reference, but the write must still be
    /// checked against the card's own curve — that check is the only guard against control-table
    /// offsets that don't fit this GPU — so a read that never comes clean stops the run here rather
    /// than let it write unverifiably, exactly as an unreadable stock read stops a live-planned run
    /// in <see cref="BuildCurvePlan"/>.</summary>
    public static IReadOnlyList<(int Mv, int Mhz)> ResetAndReadVerificationBaseline(IntPtr gpu)
    {
        Clear(gpu);
        return ReadStockOrThrow(gpu);
    }

    /// <summary>The stock curve a dry run plans from, recovered read-only (live minus applied deltas).
    /// On a tuned card the driver re-shapes the effective curve it reports (bin snapping, thermal
    /// shift, smoothing around the flatten's cliff), so the recovery can stay distorted for as long as
    /// the tuning is active — that is diagnosed as such rather than polled as if it were a brief
    /// transition. An untuned card's unreadable read is the usual transition and is ridden out.</summary>
    public static IReadOnlyList<(int Mv, int Mhz)> RecoverStockReadOnly(IntPtr gpu)
    {
        IReadOnlyList<(int Mv, int Mhz)> curve = NvApi.GetVfCurve(gpu);
        if (!CurveVoltsPlausible(curve))
        {
            throw UnrecognizedCurveError(gpu, "compute the plan");
        }

        int[] deltas = NvApi.GetCurveFreqDeltasKhz(gpu, curve.Count);
        IReadOnlyList<(int Mv, int Mhz)> stock = SubtractDeltas(curve, deltas);
        if (CurveFreqsReadable(stock))
        {
            return stock;
        }

        if (deltas.Any(d => d != 0))
        {
            throw new CliError($"The V/F curve {TransientReadMarker}: a curve tuning is applied and "
                + "the driver re-shapes the effective curve around it, which can distort the "
                + "recovered stock curve for as long as the tuning is active. Run 'clear' first, or "
                + "drop --dry-run (a real run resets to stock before planning).");
        }

        return ReadUntilFreqsReadable(gpu);
    }

    /// <summary>The result of a <c>set-reference-curve</c> capture: the stock curve, the log of what was
    /// done, and — when the applied tuning had to be reset and couldn't be put back — the restore
    /// failure the caller reports after saving the (still valid) reference.</summary>
    internal sealed record ReferenceCapture(IReadOnlyList<(int Mv, int Mhz)> Stock,
        IReadOnlyList<string> Log, string? RestoreFailure);

    /// <summary>The stock curve a <c>set-reference-curve</c> run captures. The card is always
    /// reset to stock for a direct read: with a tuning applied the true stock curve isn't
    /// observable (the driver re-shapes the effective curve around the tuning, so the recovered
    /// live-minus-deltas curve can stay distorted), and a knob only a foreign tool sets can shape
    /// the read while this tool's own knobs all read stock — a P0 graphics offset moves the whole
    /// reported curve with zero curve deltas, and skipping the reset would bake it into the
    /// reference. The applied state is snapshotted raw first and the identical raw state written
    /// back after (<see cref="AppliedTuning"/>; the reset leaves foreign knobs at stock, as
    /// documented there). A capture that fails restores the tuning and throws; a restore that
    /// fails falls back to stock rather than leave a partial tuning behind.</summary>
    public static ReferenceCapture CaptureStockForReference(IntPtr gpu)
    {
        IReadOnlyList<(int Mv, int Mhz)> curve = NvApi.GetVfCurve(gpu);
        if (!CurveVoltsPlausible(curve))
        {
            throw UnrecognizedCurveError(gpu, "save a reference curve");
        }

        AppliedTuning applied = AppliedTuning.Read(gpu);
        var log = new List<string>
        {
            applied.IsStock
                ? "Resetting to stock for the capture."
                : "A tuning is applied - resetting to stock for the capture, restoring it after.",
        };

        IReadOnlyList<(int Mv, int Mhz)> stock;
        try
        {
            // The reset is inside the guard: it writes three knobs, so one the driver rejects
            // half-way leaves a partial tuning that must be put back like any other failure.
            Clear(gpu);
            stock = ReadStockOrThrow(gpu);
        }
        catch (Exception primary) when (!applied.IsStock)
        {
            // The capture failed - put the card back as found. Its own error is the one to surface;
            // a restore that also fails must not vanish behind it, so the two report together.
            if (TryRestore(gpu, applied) is { } restoreFailure)
            {
                throw new CliError($"{ErrorReporter.Describe(primary)}\nAlso, {restoreFailure}.");
            }

            throw;
        }

        if (applied.IsStock)
        {
            // Nothing to write back: the snapshot is all zeros, which the reset just wrote.
            return new(stock, log, null);
        }

        string? failure = TryRestore(gpu, applied);
        if (failure is null)
        {
            log.Add("Previous tuning restored.");
        }

        return new(stock, log, failure);
    }

    /// <summary>Puts a snapshotted tuning back, returning null on success or a description of what
    /// the card is left holding otherwise. A failed restore falls back to a reset — stock is the safe
    /// known state — and says whether even that landed, since the caller tells the user what state
    /// the GPU is in and must not claim a reset that didn't happen.</summary>
    private static string? TryRestore(IntPtr gpu, AppliedTuning applied)
    {
        try
        {
            applied.Restore(gpu);
            return null;
        }
        catch (Exception ex)
        {
            string failure = $"restoring the previous tuning failed ({ex.Message})";
            return TryClear(gpu) is { } clearFailure
                ? $"{failure}, and the reset to stock also failed ({clearFailure}) - the GPU may "
                  + "hold a partial tuning; run 'clear'"
                : $"{failure} - the GPU is reset to stock";
        }
    }

    /// <summary>Resets to stock without throwing, returning null on success or the driver's message.
    /// For paths already handling a failure: the caller decides whether the reset's own failure is
    /// worth reporting alongside the one it is recovering from.</summary>
    private static string? TryClear(IntPtr gpu)
    {
        try
        {
            Clear(gpu);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>A clean stock read for the reference, or the transient-read refusal.</summary>
    private static IReadOnlyList<(int Mv, int Mhz)> ReadStockOrThrow(IntPtr gpu)
    {
        IReadOnlyList<(int Mv, int Mhz)> stock = ReadUntilFreqsReadable(gpu);
        if (!CurveFreqsReadable(stock))
        {
            throw new CliError($"The V/F curve {TransientReadMarker} (usually a brief power-state "
                               + "transition) - retry in a moment.");
        }

        return stock;
    }

    /// <summary>Reads the V/F curve, re-reading for up to 3 s while the frequency column is collapsed.
    /// The status curve is the *live* curve: around a power-state change its frequency column can
    /// briefly read back collapsed (dips or garbage), which would corrupt any frequency-dependent
    /// computation. At a steady state - deep idle included - it reads cleanly (on Blackwell only the
    /// lowest anchors pin at a floor clock, still monotonic), so polling rides out a transition. If the
    /// read never comes clean, the last read is returned and the caller judges it
    /// (<see cref="BuildCurvePlan"/> rejects it; <see cref="ConfirmWrite"/> reports no verdict).</summary>
    private static IReadOnlyList<(int Mv, int Mhz)> ReadUntilFreqsReadable(IntPtr gpu)
    {
        const int pollIntervalMs = 50;
        const int maxWaitMs = 3000;
        IReadOnlyList<(int Mv, int Mhz)> curve = NvApi.GetVfCurve(gpu);
        for (int waited = 0; waited < maxWaitMs && !CurveFreqsReadable(curve); waited += pollIntervalMs)
        {
            Thread.Sleep(pollIntervalMs);
            curve = NvApi.GetVfCurve(gpu);
        }

        return curve;
    }

    /// <summary>The stock curve recovered from a live read minus the applied per-anchor deltas. On a
    /// tuned card the frequencies can carry the driver's re-shaping around the tuning (see
    /// <see cref="RecoverStockReadOnly"/>); the voltage column is exact regardless.</summary>
    internal static IReadOnlyList<(int Mv, int Mhz)> SubtractDeltas(
        IReadOnlyList<(int Mv, int Mhz)> curve, int[] deltas)
    {
        var stock = new List<(int Mv, int Mhz)>(curve.Count);
        for (int i = 0; i < curve.Count; i++)
        {
            // Round the kHz delta, don't truncate: our own writes are whole MHz, but a foreign
            // (Afterburner) delta needn't be, and truncation would skew the recovered stock point.
            stock.Add((curve[i].Mv, curve[i].Mhz - (int)Math.Round(deltas[i] / 1000.0)));
        }

        return stock;
    }

    /// <summary>
    /// Writes the undervolt onto the freshly reset card (the caller planned from
    /// <see cref="ResetAndReadStock"/>, whose reset this relies on) in the order the driver requires:
    /// the memory offset first (<see cref="NvApi.SetPstate0Offsets"/> re-derives the perf table and
    /// wipes curve deltas), then the curve flatten that caps the voltage. Under load the boost
    /// settles one voltage step below the flat top's first anchor; the plan aims the cap→flat
    /// segment so the clock there is the requested one (see <see cref="BuildCurvePlan"/>), and the
    /// band built into <paramref name="plan"/> cushions a deeper undershoot. The driver's bin
    /// rounding can still realize the written flat as a staircase that moves the settle a step up,
    /// so the write is refined against its own read-back (<see cref="CurveRefinement"/>,
    /// rebuilding probe shapes from <paramref name="planStock"/> — the curve the plan's deltas are
    /// relative to); an adjusted write that lands is copied into the plan's delta array, so what
    /// the caller persists is what the card holds. Ends by reading the
    /// effective curve back and verifying the write actually
    /// landed (see <see cref="ConfirmWrite"/>), reverting to stock and throwing if it didn't. Any step
    /// the driver rejects throws too — so the caller persists and reports "done" only on a real,
    /// verified undervolt.
    /// <paramref name="referenceBaseline"/> marks a run whose plan came from the saved reference
    /// curve, and carries the live stock curve the reset exposed: the plan's own frequencies were
    /// measured at the reference's temperature, so both the verification and the realized-cap report
    /// judge against this baseline instead. The refinement stays within a bin of the plan: it
    /// absorbs the bin rounding measured against the live read, cannot chase a larger
    /// reference-to-live thermal shift (that stays with the report's note), and its sub-bin
    /// adjustment — made at this run's thermal slice — is what persistence stores and the logon
    /// replay reproduces.
    /// </summary>
    public static IReadOnlyList<string> Apply(IntPtr gpu, CurvePlan plan,
        IReadOnlyList<(int Mv, int Mhz)> planStock, int? targetMhz, int? memoryDeltaKhz,
        IReadOnlyList<(int Mv, int Mhz)>? referenceBaseline = null)
    {
        if (memoryDeltaKhz is { } delta)
        {
            NvApi.SetPstate0Offsets(gpu, graphicsDeltaKhz: 0, memoryDeltaKhz: delta, coreVoltageDeltaUv: 0);
        }

        CurveRefinement.Outcome? refined = null;
        try
        {
            NvApi.SetCurveFreqDeltasKhz(gpu, plan.DeltasKhz);

            // The refinement needs readable read-backs to judge probes; a curve legitimately
            // flattened below the boost floor never reads clean, so it goes straight to the plain
            // confirmation instead of probing blind.
            if (plan.FlatMhz >= NvApi.MinBoostClockKhz / 1000)
            {
                IReadOnlyList<(int Mv, int Mhz)> planReadBack = ReadUntilFreqsReadable(gpu);
                refined = CurveRefinement.Refine(
                    probe =>
                    {
                        NvApi.SetCurveFreqDeltasKhz(gpu, probe);
                        return ReadUntilFreqsReadable(gpu);
                    },
                    planStock, plan, planReadBack);
            }
        }
        catch
        {
            // Don't leave a partial apply behind: the memory offset is already live, and without the
            // cap it isn't the tuning that was asked for. The revert is best-effort - the write's own
            // error is the one to surface.
            TryClear(gpu);
            throw;
        }

        if (refined is { ExtraWrites: > 0 })
        {
            Array.Copy(refined.DeltasKhz, plan.DeltasKhz, plan.DeltasKhz.Length);
        }

        IReadOnlyList<string> log = ConfirmWrite(gpu, plan, targetMhz, referenceBaseline,
            refined is { ExtraWrites: > 0 });
        return refined is { ExtraWrites: > 0 } r ? [DescribeRefinement(r), .. log] : log;
    }

    /// <summary>The one-line account of a refinement that adjusted the write. A probe sequence
    /// that never landed still kept the closest realization — named here, since the confirmation
    /// line that follows re-reads and can say less on an unclean read.</summary>
    private static string DescribeRefinement(CurveRefinement.Outcome refined) => refined.OnTarget
        ? $"Refined the curve write against its read-back ({refined.ExtraWrites} extra write(s)) - "
          + "the driver's bin rounding had moved the operating point."
        : $"Probed {refined.ExtraWrites} adjusted curve write(s); none landed the planned operating "
          + "point exactly - kept the closest"
          + (refined.Realized is { } p ? $" ({p.Mv} mV / ~{p.Mhz} MHz)." : ".");

    /// <summary>
    /// Writes a tuning document's offsets onto a fresh stock reset. There is no plan to build: the
    /// anchors resolve against the live table (see
    /// <see cref="TuningDocuments.ResolveCurveOffsetsKhz"/>; an absolute-clock anchor resolves its
    /// offset against <paramref name="referenceStock"/> when one is saved, else the stock read) and
    /// the deltas are applied as data, in the order the driver requires
    /// (<see cref="AppliedTuning.Restore"/>). The document's anchors are matched against
    /// <paramref name="live"/> — the caller's pre-reset read — before anything is written, so a
    /// structurally foreign document refuses while the card still holds its current tuning; the
    /// write itself resolves against the clean post-reset read and is confirmed against the
    /// effective curve exactly like a planned one (<see cref="ConfirmWrite"/>), with a write that
    /// provably didn't land reverting to stock and throwing. A document with no tuned anchors is
    /// the memory-only tuning and applies as one, with no curve baseline to read or verify.
    /// </summary>
    public static IReadOnlyList<string> ApplyExact(IntPtr gpu, TuningDoc doc,
        IReadOnlyList<(int Mv, int Mhz)>? referenceStock, IReadOnlyList<(int Mv, int Mhz)> live,
        string what)
    {
        // Structural validation against the live table before the reset below wipes the applied
        // tuning: the anchor voltages are power-state independent, so a document naming anchors
        // this card can't hold refuses while the card still holds its current tuning. The
        // resolved-clock bound waits for the post-reset resolution - measured against the tuned
        // live clocks it would misjudge (an applied offset would count twice).
        TuningDocuments.MatchAnchors(doc, live, what);

        if (doc.Curve!.Length == 0)
        {
            return ApplyMemoryOnly(gpu, doc.MemoryOffset * 1000);
        }

        IReadOnlyList<(int Mv, int Mhz)> baseline = ResetAndReadVerificationBaseline(gpu);
        int[] deltasKhz = TuningDocuments.ResolveCurveOffsetsKhz(doc, baseline,
            () => referenceStock ?? baseline, what);
        var tuning = new AppliedTuning(deltasKhz, doc.MemoryOffset * 1000);

        try
        {
            tuning.Restore(gpu);
        }
        catch
        {
            // Don't leave a partial apply behind - the write's own error is the one to surface.
            TryClear(gpu);
            throw;
        }

        // The same below-the-boost-floor judgment the planned path makes from its plan's flat
        // clock: a curve the document flattens below the floor can never read back clean, so it
        // takes one read straight to the no-verdict path.
        int topMhz = 0;
        for (int i = 0; i < baseline.Count; i++)
        {
            topMhz = Math.Max(topMhz, baseline[i].Mhz + deltasKhz[i] / 1000);
        }

        return ConfirmWrite(gpu, DeriveChanges(baseline, deltasKhz),
            pollForReadable: topMhz >= NvApi.MinBoostClockKhz / 1000, baseline,
            (verdict, effective) => DescribeReplayResult(effective, verdict, baseline, deltasKhz));
    }

    /// <summary>The applied deltas a curve anchor can name: anchor 0 is excluded (it has no
    /// control entry, so its delta never lands), as are deltas past the visible curve (no anchor
    /// voltage to name them by). The export (<see cref="TuningDocuments.MakeTuningDoc"/>) and the
    /// replay verification (<see cref="DeriveChanges"/>) share this rule — the verification judges
    /// exactly the changes the export promises.</summary>
    internal static IEnumerable<(int Index, int DeltaKhz)> NameableTunedAnchors(int anchorCount,
        int[] deltasKhz)
    {
        for (int i = 1; i < deltasKhz.Length && i < anchorCount; i++)
        {
            if (deltasKhz[i] != 0)
            {
                yield return (i, deltasKhz[i]);
            }
        }
    }

    /// <summary>The per-anchor changes a raw delta write makes to a stock curve — what
    /// <see cref="VerifyWriteReachedCurve"/> checks for a replay, where no plan built them.</summary>
    internal static IReadOnlyList<CurveChange> DeriveChanges(IReadOnlyList<(int Mv, int Mhz)> stock,
        int[] deltasKhz)
    {
        var changes = new List<CurveChange>();
        foreach ((int i, int deltaKhz) in NameableTunedAnchors(stock.Count, deltasKhz))
        {
            changes.Add(new CurveChange(stock[i].Mv, stock[i].Mhz,
                stock[i].Mhz + (int)Math.Round(deltaKhz / 1000.0), deltaKhz));
        }

        return changes;
    }

    /// <summary>Writes a memory-only tuning: resets everything to stock first (a real run sets the
    /// whole tuning state, exactly like the curve path's reset), then writes the memory offset. The
    /// curve is never read beyond <see cref="Clear"/>'s own guard, so this works at any power state.</summary>
    public static IReadOnlyList<string> ApplyMemoryOnly(IntPtr gpu, int memoryDeltaKhz)
    {
        var log = new List<string>(Clear(gpu));
        NvApi.SetPstate0Offsets(gpu, graphicsDeltaKhz: 0, memoryDeltaKhz: memoryDeltaKhz, coreVoltageDeltaUv: 0);
        log.Add("Memory clock offset written.");
        return log;
    }

    /// <summary>Describes the change <see cref="Apply"/> would make, for <c>--dry-run</c> — writes nothing.</summary>
    public static IReadOnlyList<string> DescribePlan(CurvePlan plan, int? targetMhz)
    {
        string action = $"pin the boost at {plan.SettleMv} mV / {plan.SettleMhz} MHz"
            + (targetMhz is null ? " (the stock clock at the cap)" : string.Empty)
            + $"; cap anchor {plan.CapMv} mV / {plan.CapMhz} MHz, flat from {plan.FlatMv} mV / {plan.FlatMhz} MHz";

        var log = new List<string> { $"[dry run] Would {action}; {plan.Changes.Count} point(s) change:" };
        foreach (CurveChange c in plan.Changes)
        {
            log.Add($"[dry run]   {c.Mv,4} mV: {c.OldMhz} -> {c.NewMhz} MHz "
                    + $"(delta {c.NewDeltaKhz / 1000.0:+0;-0} MHz)");
        }

        if (plan.Changes.Count == 0)
        {
            log.Add("[dry run]   curve already matches; nothing to write.");
        }

        return log;
    }

    /// <summary>A single per-anchor curve change, for reporting.</summary>
    internal readonly record struct CurveChange(int Mv, int OldMhz, int NewMhz, int NewDeltaKhz);

    /// <summary>The computed curve write: the per-point frequency deltas (kHz, index-aligned with the
    /// curve), the operating point the write pins the boost to
    /// (<see cref="SettleMv"/>/<see cref="SettleMhz"/> — one boost step below the flat start,
    /// holding the requested clock), the cap anchor with its written clock, the flat top's start
    /// (<see cref="FlatMv"/>/<see cref="FlatMhz"/>: the anchor above the cap), a description of
    /// every point that moves, and the geometry (<see cref="CapIndex"/>, <see cref="CapPoints"/>)
    /// the post-write refinement rebuilds probe shapes from. <see cref="DeltasKhz"/> always holds
    /// the deltas the card ends up with: <see cref="Apply"/> updates the array in place when the
    /// refinement lands on an adjusted pair, so persistence and exports store the write that
    /// actually happened. <see cref="Changes"/> and the clock fields stay as planned — the
    /// refinement moves the written pair by at most a bin, while the reductions the write
    /// verification judges from <see cref="Changes"/> span hundreds of MHz, far beyond its
    /// reach.</summary>
    internal sealed record CurvePlan(int SettleMv, int SettleMhz, int CapMv, int CapMhz,
        int FlatMv, int FlatMhz, IReadOnlyList<CurveChange> Changes, int[] DeltasKhz,
        int CapIndex, int CapPoints);

    /// <summary>The boost algorithm's voltage granularity (mV): under load it settles this far below
    /// the flat top's first anchor, on a curve interpolated finer than the anchor table — measured
    /// behavior, see DEVELOPMENT.md ("Where the boost settles").</summary>
    internal const int BoostStepMv = 5;

    /// <summary>The clock spread (MHz) written across the cap→flat-start segment: two clock bins.
    /// Below this the driver snaps the pair into one plateau whose first anchor is the cap itself,
    /// dropping the settle a boost step lower (a written 8 MHz rise measurably folds; 16 holds).
    /// Wider only grows the realized-clock error, which tracks the segment's middle.</summary>
    private const int FlatSpreadMhz = 16;

    /// <summary>
    /// Builds the curve write that caps voltage at <paramref name="capMv"/>, with every delta measured
    /// from the <paramref name="stock"/> curve. Under load the boost settles one
    /// <see cref="BoostStepMv"/> step below the flat top's first anchor, at the clock the effective
    /// curve holds there — on the cap anchor when the anchor above is one step away, inside the gap
    /// when the anchors sit further apart. So the cap→flat-start segment is written as a straight
    /// line through (settle voltage, F) — F being <paramref name="targetMhz"/> if given, else the
    /// stock clock at the cap — spanning <see cref="FlatSpreadMhz"/>: the smallest rise the driver
    /// reliably keeps distinct, which also bounds how far the realized clock (mid-segment, within a
    /// bin) can miss F. A cap without two anchors of room above it can't form the plateau and is
    /// refused. A band of <paramref name="capPoints"/> anchors counting down from the cap (the cap
    /// itself plus the points below it) carries the cap anchor's frequency offset, so when the boost
    /// settles deeper below the cap under load the clock doesn't fall off a steep (overclocked)
    /// curve back to stock. Everything below the band stays at stock. Finally the curve is made
    /// non-decreasing (which the driver requires).
    /// </summary>
    internal static CurvePlan BuildCurvePlan(IReadOnlyList<(int Mv, int Mhz)> stock,
        int capMv, int? targetMhz, int capPoints)
    {
        int n = stock.Count;

        // Flattening reads the curve's per-anchor frequencies (the flat top's deltas are measured from
        // them), so every cap needs a clean read, not just one that also sets a clock - a cap computed
        // from a collapsed (transitional) read would write wrong deltas.
        if (!CurveFreqsReadable(stock))
        {
            throw new CliError($"The V/F curve {TransientReadMarker} (usually a brief power-state "
                + "transition) - retry in a moment, or retry under a 3D load.");
        }

        int k = NearestAnchorIndex(stock, capMv);

        // The flat top has to span at least two anchors above the cap to be a plateau the boost can
        // stop under, so the cap needs two anchors of room. With less, no plateau forms and nothing
        // would pin the boost - the write would be a silent no-op reported as a cap. These top
        // anchors sit above every generation's load voltage anyway, so the request is refused
        // rather than approximated.
        if (k >= n - 2)
        {
            throw new CliError($"A {stock[k].Mv} mV cap lands within two anchors of the top of this "
                + $"GPU's V/F curve (its highest is {stock[^1].Mv} mV), leaving no room above it to "
                + "flatten - nothing would be capped. Cap lower: the voltage the card actually runs "
                + "at under load is well below the curve's top (read it with 'watch' under a "
                + "sustained load).");
        }

        // The settle voltage is one boost step below the flat start, floored at the cap anchor (a
        // gap no wider than the step leaves it on the anchor itself; anchors can also truncate to
        // the same millivolt, which the guard on the division treats the same way). The cap→flat
        // segment is the line through (settleMv, f) spanning FlatSpreadMhz, and the band of
        // `capPoints` anchors ending at the cap carries the cap anchor's offset. The stock curve's
        // own slope across the pair plays no part: the flat sits a fixed rise above the cap anchor,
        // so a level or dipping stock pair (the pinned floor clock, a read wobble) can't drag the
        // flat below the cap and shave the request.
        int f = targetMhz ?? stock[k].Mhz;
        int flatten = k + 1;
        int settleMv = Math.Max(stock[k].Mv, stock[flatten].Mv - BoostStepMv);
        int gapMv = stock[flatten].Mv - stock[k].Mv;
        double intoGap = gapMv == 0 ? 0 : (double)(settleMv - stock[k].Mv) / gapMv;
        int capAnchorMhz = f - (int)Math.Round(FlatSpreadMhz * intoGap);
        int[] newMhz = CapShapeMhz(stock, k, capAnchorMhz, capAnchorMhz + FlatSpreadMhz, capPoints);

        // The write path skips anchor 0 (see NvApi.SetCurveFreqDeltasKhz), so leave its delta at 0 and out
        // of the change list - the plan, and --dry-run, then reflect exactly what a real apply produces.
        var deltasKhz = new int[n];
        var changes = new List<CurveChange>();
        for (int i = 1; i < n; i++)
        {
            int deltaKhz = (newMhz[i] - stock[i].Mhz) * 1000;
            deltasKhz[i] = deltaKhz;
            if (deltaKhz != 0)
            {
                changes.Add(new CurveChange(stock[i].Mv, stock[i].Mhz, newMhz[i], deltaKhz));
            }
        }

        // The promised operating point, read off the final segment: exactly f by construction,
        // except when the floors above reshaped the segment (a cap clock at or below the curve's
        // minimum) - then the report must carry what the write actually holds there.
        int settleMhz = SegmentClockAt((stock[k].Mv, newMhz[k]), (stock[flatten].Mv, newMhz[flatten]), settleMv);

        return new CurvePlan(settleMv, settleMhz, stock[k].Mv, newMhz[k],
            stock[flatten].Mv, newMhz[flatten], changes, deltasKhz, k, capPoints);
    }

    /// <summary>
    /// The written curve shape for a voltage cap, as absolute clocks: the band of
    /// <paramref name="capPoints"/> anchors ending at anchor <paramref name="k"/> rides the cap
    /// anchor's offset, everything above holds <paramref name="flatMhz"/>, and everything below
    /// stays stock — shared by <see cref="BuildCurvePlan"/> and the post-write refinement
    /// (<see cref="CurveRefinement"/>), so a probed (cap, flat) pair reproduces the plan's shape
    /// exactly. Two floors close it out: the curve is clamped non-decreasing below the flatten
    /// (the driver requires it; this only bites at the band's edges when the cap's clock lands
    /// below stock), and every point is floored at anchor 0's stock clock — anchor 0 has no
    /// control entry, so writable anchors must not fall below it or the effective curve would dip
    /// there, and flooring anchor 0 itself keeps the reported cap honest when the cap sits on it.
    /// </summary>
    internal static int[] CapShapeMhz(IReadOnlyList<(int Mv, int Mhz)> stock, int k,
        int capAnchorMhz, int flatMhz, int capPoints)
    {
        int n = stock.Count;
        int flatten = k + 1;
        int capDeltaMhz = capAnchorMhz - stock[k].Mhz;
        int bandStart = Math.Max(0, k - (capPoints - 1));
        var newMhz = new int[n];
        for (int i = 0; i < n; i++)
        {
            newMhz[i] = i >= flatten ? flatMhz
                : i >= bandStart ? stock[i].Mhz + capDeltaMhz
                : stock[i].Mhz;
        }

        for (int i = flatten - 1; i >= 0; i--)
        {
            newMhz[i] = Math.Min(newMhz[i], newMhz[i + 1]);
        }

        for (int i = 0; i < n; i++)
        {
            newMhz[i] = Math.Max(newMhz[i], stock[0].Mhz);
        }

        return newMhz;
    }

    /// <summary>The index of the curve anchor closest to a voltage — which anchor a requested cap
    /// actually lands on.</summary>
    internal static int NearestAnchorIndex(IReadOnlyList<(int Mv, int Mhz)> curve, int mv)
    {
        int best = 0;
        for (int i = 1; i < curve.Count; i++)
        {
            if (Math.Abs(curve[i].Mv - mv) < Math.Abs(curve[best].Mv - mv))
            {
                best = i;
            }
        }

        return best;
    }

    // --- V/F curve reads: readability and plausibility checks, layout reporting, interpolation ---

    /// <summary>Whether the curve's frequency column is a clean, usable read. Around a power-state
    /// change the live status can briefly read back collapsed — sometimes wholesale (every clock tiny),
    /// sometimes dips in the steep idle->boost region. At a steady state, deep idle included, the read
    /// passes. A usable read is a full, monotonic, plausible curve that reaches a boost clock and
    /// stays below any real core clock — the upper bound also keeps every downstream delta
    /// computation (whose kHz values scale the clocks by 1000) inside int range, so no plan or
    /// document arithmetic needs to reason about overflow.</summary>
    public static bool CurveFreqsReadable(IReadOnlyList<(int Mv, int Mhz)> curve)
    {
        if (curve.Count < 16)
        {
            return false;
        }

        int max = 0;
        for (int i = 0; i < curve.Count; i++)
        {
            if (curve[i].Mhz < 100)
            {
                return false; // a collapsed/garbage point
            }

            if (curve[i].Mhz > TuneRequest.MaxPlausibleCoreClockMhz)
            {
                return false; // beyond any real core clock - a garbage read or crafted file
            }

            if (i > 0 && curve[i].Mhz < curve[i - 1].Mhz - MaxBenignDipMhz)
            {
                return false; // not monotonic - a corrupt read
            }

            max = Math.Max(max, curve[i].Mhz);
        }

        return max >= NvApi.MinBoostClockKhz / 1000; // reaches a real boost clock
    }

    /// <summary>The adjacent-anchor dip a clean read may legitimately contain: the driver re-shapes
    /// the curve it reports (bin snapping, thermal shift, smoothing around an applied flatten), which
    /// can wobble neighbouring anchors by a few 7-8 MHz bins — most visibly in the stock curve
    /// recovered from a tuned card, where live-minus-delta seams at the flatten band's edges. A
    /// transitional collapse dips by hundreds of MHz, so it still fails.</summary>
    private const int MaxBenignDipMhz = 25;

    /// <summary>Whether an anchor reads at the curve's floor clock — within a read-back bin
    /// (<see cref="CurveBinMhz"/>) of anchor 0. At deep idle the driver pins the lowest anchors
    /// at an idle floor clock (~200 MHz) instead of their stock clocks — a steady state
    /// <see cref="CurveFreqsReadable"/> accepts — so a floor-tied clock may be that power-state
    /// artifact, with the anchor's stock clock unobservable behind it: a judgment that needs the
    /// stock clock skips or refuses the anchor. Keyed to anchor 0, not an absolute threshold
    /// (idle floors vary by card), and per anchor, not a leading run — a clean read may wobble a
    /// pinned anchor up a bin (<see cref="MaxBenignDipMhz"/>), which must not strip the anchors
    /// after it. On an active read only anchor 0 and any same-bin neighbours tie the floor, and
    /// those genuinely read the curve's minimum clock.</summary>
    internal static bool AtFloorClock(IReadOnlyList<(int Mv, int Mhz)> curve, int i)
        => curve[i].Mhz <= curve[0].Mhz + CurveBinMhz;

    /// <summary>Whether the curve's <em>voltage</em> axis looks like a real NVIDIA V/F table: a long,
    /// ascending run of plausible core voltages. The voltage column is power-state
    /// independent (unlike the frequency column <see cref="CurveFreqsReadable"/> guards), so this
    /// stays true on a supported card even at idle, but reads false when the status-buffer offsets
    /// don't match the hardware and the bytes decode as garbage (a short or narrow list). It gates
    /// <em>writing</em>: the tuning-buffer offsets are hardware-specific, so if the curve we target
    /// isn't one we recognize, no tuning should be written at all.</summary>
    public static bool CurveVoltsPlausible(IReadOnlyList<(int Mv, int Mhz)> curve)
    {
        // A real table has 80-130ish anchors; a mismatched layout breaks out after a few points.
        if (curve.Count < 16)
        {
            return false;
        }

        // Ascending in voltage. The raw table ascends in microvolts, so adjacent anchors can truncate
        // to the same millivolt - equality is a real table, a decrease is a garbage read off an
        // unrecognized layout. (Checking it here also makes the span below well-defined rather than
        // assuming the caller pre-sorted.)
        for (int i = 1; i < curve.Count; i++)
        {
            if (curve[i].Mv < curve[i - 1].Mv)
            {
                return false;
            }
        }

        // ...and it spans up into real boost-voltage territory (tolerant of low-voltage mobile tables).
        int span = curve[^1].Mv - curve[0].Mv;
        return span >= 200 && curve[^1].Mv >= 850;
    }

    // The line triple every layout report prints — the warning attached to a failed plausibility check
    // and the 'layout' command — so the two renderings of the same facts can't drift apart.
    internal static string BuildLine => $"build: {Product.Name} {Product.Version}";
    internal static string DetectedLine(CurveLayout d) => $"detected: {d.DescribeColumns()}";
    internal static string CompiledLine => $"compiled: {CurveLayout.DescribeCompiled()}";

    /// <summary>When <see cref="CurveVoltsPlausible"/> rejects a read, re-detects the status buffer's
    /// actual layout and describes it next to the offsets this build compiled in — diagnostic detail to
    /// drop into a bug report when a card's tuning-buffer layout isn't the one we expect. Best-effort: any
    /// read failure is reported as a line rather than thrown.</summary>
    public static IReadOnlyList<string> DetectedLayoutReport(IntPtr gpu)
    {
        try
        {
            string detected = CurveLayout.TryDetect(NvApi.ReadVfCurveStatusRaw(gpu), out CurveLayout d)
                ? DetectedLine(d)
                : "detected: no V/F curve found in the status buffer";
            return new[] { BuildLine, detected, CompiledLine };
        }
        catch (Exception ex)
        {
            return new[] { BuildLine, $"detected: layout read failed ({ex.Message})" };
        }
    }

    /// <summary>The stock frequency (MHz) at a given voltage, linearly interpolated over the
    /// ascending, non-empty curve, clamping at the ends.</summary>
    public static double FreqAtVoltage(IReadOnlyList<(int Mv, int Mhz)> curve, double mv)
    {
        if (mv <= curve[0].Mv)
        {
            return curve[0].Mhz;
        }

        if (mv >= curve[^1].Mv)
        {
            return curve[^1].Mhz;
        }

        // The clamps leave curve[0].Mv < mv < curve[^1].Mv, so a bracketing pair exists.
        int i = 1;
        while (curve[i].Mv < mv)
        {
            i++;
        }

        var (mv0, mhz0) = curve[i - 1];
        var (mv1, mhz1) = curve[i];
        return mhz0 + (mv - mv0) / (mv1 - mv0) * (mhz1 - mhz0);
    }

    /// <summary>The clock on the straight segment between two curve points at a voltage between them,
    /// clamping outside the pair — how the driver's finer-grained curve fills an anchor gap, so also
    /// how a settle voltage inside one maps to a clock.</summary>
    internal static int SegmentClockAt((int Mv, int Mhz) a, (int Mv, int Mhz) b, int mv)
    {
        if (mv <= a.Mv)
        {
            return a.Mhz;
        }

        if (mv >= b.Mv)
        {
            return b.Mhz;
        }

        return a.Mhz + (int)Math.Round((double)(b.Mhz - a.Mhz) * (mv - a.Mv) / (b.Mv - a.Mv));
    }

    /// <summary>The V/F curve's bin granularity: clock read-backs land on ~15 MHz steps, so a comparison
    /// against an intended clock tolerates this much noise (and a change smaller than it isn't real).</summary>
    private const int CurveBinMhz = 15;

    /// <summary>The no-numbers report the planned write's confirmation prints when the post-apply
    /// read-back can't be rendered.</summary>
    private const string UncleanReadBackLine =
        "Curve: the clock read-back wasn't clean (usually a power-state change mid-apply).";

    /// <summary>
    /// Reads the effective curve back to (1) verify the write actually reached it and (2) report the
    /// realized cap. The control table's byte offsets are hardware-specific and — unlike the status read,
    /// which self-validates via <see cref="CurveVoltsPlausible"/> — have nothing to check themselves
    /// against, so on a card they don't fit the deltas land in reserved bytes the driver ignores and the
    /// curve comes back at stock. The effective (status) curve is independently validated, so
    /// cross-checking the write against it is the only genuine confirmation the control layout is right.
    /// A write that provably didn't land reverts to stock and throws rather than reporting a cap that
    /// isn't there; a read we simply couldn't take is reported but not treated as failure.
    /// </summary>
    private static IReadOnlyList<string> ConfirmWrite(IntPtr gpu, CurvePlan plan, int? targetMhz,
        IReadOnlyList<(int Mv, int Mhz)>? referenceBaseline, bool refined = false)
        // A curve legitimately flattened below the boost floor can never read clean, so it takes one
        // read straight to the no-verdict path instead of waiting out the full poll window.
        => ConfirmWrite(gpu, plan.Changes,
            pollForReadable: plan.FlatMhz >= NvApi.MinBoostClockKhz / 1000, referenceBaseline,
            (verdict, effective) => DescribeRealizedOperatingPoint(effective, plan, targetMhz, verdict,
                planFromReference: referenceBaseline is not null, refined));

    /// <summary>The confirmation skeleton the planned and replay writes share: read the effective
    /// curve back, judge whether the write reached it, revert-and-throw on a proven miss, and hand
    /// the verdict to <paramref name="describe"/> for the path's own report.</summary>
    private static IReadOnlyList<string> ConfirmWrite(IntPtr gpu, IReadOnlyList<CurveChange> changes,
        bool pollForReadable, IReadOnlyList<(int Mv, int Mhz)>? baseline,
        Func<WriteVerification, IReadOnlyList<(int Mv, int Mhz)>, IReadOnlyList<string>> describe)
    {
        var (effective, unreadableReport) = ReadBackEffective(gpu, pollForReadable);
        if (effective is null)
        {
            return unreadableReport!;
        }

        WriteVerification verdict = VerifyWriteReachedCurve(changes, effective, baseline);
        if (verdict == WriteVerification.NotReflected)
        {
            throw WriteNotReflectedError(gpu);
        }

        return describe(verdict, effective);
    }

    /// <summary>The replay counterpart of <see cref="DescribeRealizedOperatingPoint"/>: a document
    /// names no requested cap or clock target to compare against, but the written curve
    /// (<paramref name="baseline"/> plus <paramref name="deltasKhz"/> — this run's own write)
    /// locates its flat start exactly, so the operating point is read from the effective curve at
    /// anchors the write names; the tilt-prone shape inference stays <c>status</c>-only. A write
    /// with no plateau (a document that only raises scattered anchors) pins no operating point,
    /// and an unreadable or anchor-less read-back can't report one — both report the verdict
    /// alone.</summary>
    private static IReadOnlyList<string> DescribeReplayResult(
        IReadOnlyList<(int Mv, int Mhz)> effective, WriteVerification verdict,
        IReadOnlyList<(int Mv, int Mhz)> baseline, int[] deltasKhz)
    {
        string line = verdict == WriteVerification.Confirmed
            ? "Confirming curve write: the offsets reached the effective curve"
            : "Curve offsets written (no measurable reduction to verify the write against)";

        var written = new List<(int Mv, int Mhz)>(baseline.Count);
        for (int i = 0; i < baseline.Count; i++)
        {
            written.Add((baseline[i].Mv, baseline[i].Mhz + (int)Math.Round(deltasKhz[i] / 1000.0)));
        }

        int flatStart = FlatStartIndex(written);
        if (flatStart == 0 || flatStart == written.Count - 1
            || !CurveFreqsReadable(effective)
            || PointAtVoltage(effective, written[flatStart - 1].Mv) is not { } cap
            || PointAtVoltage(effective, written[flatStart].Mv) is not { } flat)
        {
            return new[] { $"{line}." };
        }

        int settleMv = Math.Max(cap.Mv, flat.Mv - BoostStepMv);
        string notes = FoldNote(written[flatStart].Mhz - written[flatStart - 1].Mhz, flat.Mhz - cap.Mhz)
                       + SnapNote(effective, flat);
        return new[] { $"{line}; operating point {settleMv} mV / ~{SegmentClockAt(cap, flat, settleMv)} MHz{notes}." };
    }

    /// <summary>The effective read-back a write confirmation judges, or null with the no-verdict
    /// report the caller returns as-is: a failed read-back is a read glitch, not positive evidence
    /// the write missed (a layout mismatch reads back fine — just unchanged — and the verdict
    /// catches it), so it must never revert. <paramref name="pollForReadable"/> rides out a
    /// power-state transition like the pre-write read does — a collapsed read-back yields no
    /// verdict, so polling keeps the confirmation real.</summary>
    private static (IReadOnlyList<(int Mv, int Mhz)>? Effective, IReadOnlyList<string>? Report)
        ReadBackEffective(IntPtr gpu, bool pollForReadable)
    {
        try
        {
            return (pollForReadable ? ReadUntilFreqsReadable(gpu) : NvApi.GetVfCurve(gpu), null);
        }
        catch (Exception ex)
        {
            return (null,
                new[] { $"Curve: unreadable, can't confirm the write ({ex.Message}); verify with 'status'." });
        }
    }

    /// <summary>The refusal for a write the read-back proves never reached the curve. Undoes the
    /// wrong write first: our zero-write hits the same reserved bytes the delta write did, and the
    /// real delta field was never touched, so stock is restored either way. The mismatch is the
    /// primary error - a revert that itself fails must not replace it.</summary>
    private static CliError WriteNotReflectedError(IntPtr gpu)
    {
        string reverted = TryClear(gpu) is { } clearFailure
            ? $"the revert to stock also failed ({clearFailure}), run 'clear'"
            : "reverted to stock";

        return new CliError(
            "The curve didn't change after writing, so the control-table offsets don't match this GPU "
            + $"- {reverted}. {PortingDocPointer}");
    }

    /// <summary>Whether the effective read-back reflects a curve write (see <see cref="VerifyWriteReachedCurve"/>).</summary>
    internal enum WriteVerification
    {
        /// <summary>The reduced anchors came down as intended — the control write reached the curve.</summary>
        Confirmed,

        /// <summary>Every anchor the plan reduces still reads at stock — the write didn't land where
        /// expected.</summary>
        NotReflected,

        /// <summary>Nothing reliable to check (no bin-sized reductions to observe, or the read-back
        /// didn't include the edited anchors) — the caller proceeds without a verdict.</summary>
        Unverifiable,
    }

    /// <summary>
    /// Cross-checks a curve write against the effective read-back. Every real voltage cap flattens the
    /// anchors above the cap <em>down</em> to the cap clock, and the driver always honors a clock
    /// reduction (a raise it may clamp to its own ceiling, a reduction it doesn't), so those anchors are
    /// the reliable signal: a majority that came down confirms the write reached the curve; every one
    /// still at stock means it didn't (the control-table offsets don't fit this GPU); a moved minority
    /// is read-back wobble, not evidence either way, and yields no verdict.
    /// Matching by voltage rather than by index also ties each reduction to the exact anchor it targeted.
    /// A plan with no bin-sized reductions (a pure overclock, or a cap so shallow every reduction is
    /// sub-bin) leaves nothing this can assert without fighting the driver's clamping, so it reports
    /// <see cref="WriteVerification.Unverifiable"/>.
    /// <paramref name="liveStock"/> is the pre-write curve to measure against, for a plan built from
    /// the saved reference: its own frequencies come from a curve captured at another temperature, and
    /// judging a read-back against those would let the thermal shift alone pass for a landed write on
    /// exactly the unported card this exists to catch. Omitted, the plan's own frequencies are the
    /// baseline — for a plan built from a live read they are that same curve.
    /// </summary>
    internal static WriteVerification VerifyWriteReachedCurve(
        CurvePlan plan, IReadOnlyList<(int Mv, int Mhz)> effective,
        IReadOnlyList<(int Mv, int Mhz)>? liveStock = null)
        => VerifyWriteReachedCurve(plan.Changes, effective, liveStock);

    /// <summary>The change-list form of the cross-check above, for writes whose changes no plan
    /// built — a replayed tuning's, derived by <see cref="DeriveChanges"/>.</summary>
    internal static WriteVerification VerifyWriteReachedCurve(
        IReadOnlyList<CurveChange> changes, IReadOnlyList<(int Mv, int Mhz)> effective,
        IReadOnlyList<(int Mv, int Mhz)>? liveStock = null)
    {
        // A collapsed (transitional) read-back can't be judged either way: every clock reads far below
        // stock, so each reduction would count as "moved" and a missed write would pass as landed. No
        // verdict is the honest answer. (This also skips verification for a cap flattened below the
        // boost-clock floor CurveFreqsReadable requires - a conservative trade for such extreme caps.)
        if (!CurveFreqsReadable(effective))
        {
            return WriteVerification.Unverifiable;
        }

        // A baseline that doesn't read cleanly can't be measured against any more than a collapsed
        // read-back can, so it yields no verdict rather than a judgement on noise.
        if (liveStock is not null && !CurveFreqsReadable(liveStock))
        {
            return WriteVerification.Unverifiable;
        }

        Dictionary<int, int>? stockByMv = null;
        HashSet<int>? stockAmbiguous = null;
        if (liveStock is not null)
        {
            (stockByMv, stockAmbiguous) = IndexByVoltage(liveStock);
        }

        var (effectiveByMv, effectiveAmbiguous) = IndexByVoltage(effective);

        int checkable = 0, moved = 0;
        foreach (CurveChange c in changes)
        {
            // Judge only reductions of at least one bin: a smaller written delta (near the top of a
            // real curve the anchors sit only 7-8 MHz apart) can never register as moved against the
            // bin-sized read-back noise, so counting it could only produce a false NotReflected - and
            // a spurious revert. The written delta says this independently of which curve the plan
            // measured from, so a reference-planned run is filtered the same way.
            if (c.NewDeltaKhz > -CurveBinMhz * 1000)
            {
                continue;
            }

            // A millivolt that repeats in the table can't be attributed to one anchor by voltage,
            // so judging it would measure a same-mV neighbour's clock - enough to convict a landed
            // write when such an anchor is the only checkable change. It yields no judgment; the
            // unique millivolts carry the verdict.
            if (effectiveAmbiguous.Contains(c.Mv) || stockAmbiguous?.Contains(c.Mv) == true)
            {
                continue;
            }

            int stockMhz = c.OldMhz;
            if (stockByMv is not null && !stockByMv.TryGetValue(c.Mv, out stockMhz))
            {
                continue; // the baseline doesn't cover this anchor; can't judge it
            }

            if (!effectiveByMv.TryGetValue(c.Mv, out int realizedMhz))
            {
                continue; // this anchor isn't in the read-back; can't judge it
            }

            checkable++;
            if (realizedMhz <= stockMhz - CurveBinMhz)
            {
                moved++;
            }
        }

        if (checkable == 0)
        {
            return WriteVerification.Unverifiable;
        }

        // A true layout mismatch leaves EVERY anchor at stock (the deltas landed in reserved bytes the
        // driver ignores), so only a read-back where nothing moved convicts the write. A majority moved
        // confirms it; the mixed band in between is read-back wobble (thermal shift, bin snapping
        // between the pre-write read and this one) - no verdict beats a spurious revert of a good
        // undervolt on the strength of noise.
        if (moved == 0)
        {
            return WriteVerification.NotReflected;
        }

        return moved * 2 >= checkable ? WriteVerification.Confirmed : WriteVerification.Unverifiable;
    }

    /// <summary>A voltage -> frequency map over a curve, plus the set of millivolts more than one
    /// anchor truncates to — those can't be attributed to one anchor by voltage, so the verdict
    /// above skips them rather than judge against a neighbour's clock.</summary>
    private static (Dictionary<int, int> ClockByMv, HashSet<int> Ambiguous) IndexByVoltage(
        IReadOnlyList<(int Mv, int Mhz)> curve)
    {
        var byMv = new Dictionary<int, int>();
        var ambiguous = new HashSet<int>();
        foreach (var (mv, mhz) in curve)
        {
            if (!byMv.TryAdd(mv, mhz))
            {
                ambiguous.Add(mv);
            }
        }

        return (byMv, ambiguous);
    }

    /// <summary>The curve's operating point — where the boost settles under load — inferred from the
    /// effective curve's shape alone: one <see cref="BoostStepMv"/> below the flat top's first
    /// anchor (floored at the anchor below it), at the clock the segment holds there. The flat top
    /// is the curve's maximal run of equal top clocks — the driver reports an applied flatten
    /// exactly level, while the anchor below it sits a distinct rise lower by design
    /// (<see cref="BuildCurvePlan"/>); a fully flat curve has no anchor below and reports its first
    /// point. A top of a <em>single</em> anchor is no flatten at all — a stock curve's peak, or one
    /// bin of read-back noise on the flat's top edge — and inferring a near-peak "operating point"
    /// from it would fabricate a number, so it reports none. For the <c>status</c> line, which has
    /// only a read to go on; a run that just wrote the tuning knows its anchors and reports from
    /// them instead (<see cref="DescribeRealizedOperatingPoint"/>). A read taken while the card
    /// sits far from the reference temperature can tilt the flat into a staircase of bin-sized
    /// plateaus, which walks this estimate up toward the curve's top or (fully tilted) suppresses
    /// it — a transient a re-read clears, and the reason the post-apply lines don't rely on this.
    /// Null when the frequency column isn't a clean read, since the numbers would be meaningless.</summary>
    internal static (int Mv, int Mhz)? EffectiveOperatingPoint(IReadOnlyList<(int Mv, int Mhz)> effective)
    {
        if (!CurveFreqsReadable(effective))
        {
            return null;
        }

        int flatStart = FlatStartIndex(effective);
        if (flatStart == effective.Count - 1)
        {
            return null; // a single-anchor top is not a plateau - nothing pins the boost there
        }

        if (flatStart == 0)
        {
            return effective[0];
        }

        (int Mv, int Mhz) below = effective[flatStart - 1];
        (int Mv, int Mhz) flat = effective[flatStart];
        int mv = Math.Max(below.Mv, flat.Mv - BoostStepMv);
        return (mv, SegmentClockAt(below, flat, mv));
    }

    /// <summary>The first index of the curve's flat top — the maximal run of anchors equal to the
    /// last anchor's clock. 0 when the whole curve is level.</summary>
    private static int FlatStartIndex(IReadOnlyList<(int Mv, int Mhz)> curve)
    {
        int flatStart = curve.Count - 1;
        while (flatStart > 0 && curve[flatStart - 1].Mhz == curve[^1].Mhz)
        {
            flatStart--;
        }

        return flatStart;
    }

    /// <summary>The warning appended when a written cap→flat rise reads back collapsed: the driver
    /// snapped the pair into one plateau — the rise landed under a clock bin on this card, whether
    /// from thermal re-shaping between the planning curve and the live one or a generation with
    /// coarser bins — which moves the plateau's first anchor down onto the cap and the settle one
    /// boost step lower. A squeezed-but-intact read-back still shows about a bin of rise while a
    /// fold reads level, so the threshold sits between the two; a written rise under a bin never
    /// promised separation, so it is not judged.</summary>
    private static string FoldNote(int writtenRiseMhz, int readBackRiseMhz)
        => writtenRiseMhz >= 8 && readBackRiseMhz < 4
            ? $" - the cap-to-flat rise reads back collapsed, so the boost may settle {BoostStepMv} mV lower"
            : string.Empty;

    /// <summary>The <see cref="FoldNote"/> mirror for the other direction: the driver rounds every
    /// anchor onto a clock bin, and a flat written near a bin edge can keep its first anchor(s) at
    /// the written clock while the tail rounds a bin up. The plateau pinning the boost is then the
    /// tail, and the boost settles one boost step below <em>its</em> first anchor — above the
    /// written flat start, at the tail's clock. On the planned path the refinement usually probes
    /// this away (<see cref="CurveRefinement"/>), so the note marks the writes it can't reach — a
    /// replayed document's, or one the probe budget couldn't land. Anchors also step individually
    /// with temperature (see DEVELOPMENT.md, "Where the boost settles"), so the note speaks for
    /// the read's own thermal slice — "may", and a later <c>status</c> read shows where the boost
    /// sits now. <paramref name="flat"/> is the effective point at the written flat start.</summary>
    internal static string SnapNote(IReadOnlyList<(int Mv, int Mhz)> effective, (int Mv, int Mhz) flat)
    {
        // The written flat runs level to the curve's top, so a top anchor reading a near-bin rise
        // above the flat start's own read-back means the flat came back staircased; below that it
        // is read wobble, not a plateau split.
        if (effective.Count == 0 || effective[^1].Mhz < flat.Mhz + 4)
        {
            return string.Empty;
        }

        // A top of a single anchor is no plateau - one bin of noise on the curve's last anchor
        // would otherwise fabricate a near-peak settle, the exact inference EffectiveOperatingPoint
        // refuses for the same reason.
        int top = FlatStartIndex(effective);
        if (top <= 0 || top == effective.Count - 1 || effective[top].Mv <= flat.Mv)
        {
            return string.Empty;
        }

        (int Mv, int Mhz) below = effective[top - 1];
        int settleMv = Math.Max(below.Mv, effective[top].Mv - BoostStepMv);
        return $" - the flatten's tail reads back a bin higher from {effective[top].Mv} mV up, so the "
               + $"boost may settle at {settleMv} mV / ~{SegmentClockAt(below, effective[top], settleMv)} MHz";
    }

    /// <summary>The effective curve's point at an anchor voltage the plan names (the cap anchor, the
    /// flat start), carrying the clock the read-back shows there. The plan names its anchors
    /// outright, so unlike <see cref="EffectiveOperatingPoint"/> this needs no inference from the
    /// curve's shape and a tilted read (a card far from its reference temperature) can't walk it off
    /// the anchor. Matched by voltage, keeping the first of anchors that truncate to the same
    /// millivolt — the one a plan change at that millivolt targeted, as in
    /// <see cref="IndexByVoltage"/>. Null when the read-back doesn't cover the anchor.</summary>
    internal static (int Mv, int Mhz)? PointAtVoltage(
        IReadOnlyList<(int Mv, int Mhz)> effective, int mv)
    {
        foreach (var point in effective)
        {
            if (point.Mv == mv)
            {
                return point;
            }
        }

        return null;
    }

    /// <summary>Reports the realized operating point from the effective read-back: the settle
    /// voltage's clock on the cap→flat segment as the card now holds it (the anchors located by
    /// <see cref="PointAtVoltage"/>). Flags an explicit frequency target that wasn't reached —
    /// measured against the plan's own settle clock, so a target the plan itself floored (a clock
    /// below the lowest anchor's stock clock) is reported as floored, not blamed on the driver. A
    /// plan built from the saved reference curve intentionally reads back shifted by the live
    /// curve's thermal offset from the reference, so its note names that instead of a smoothing
    /// failure.</summary>
    internal static IReadOnlyList<string> DescribeRealizedOperatingPoint(
        IReadOnlyList<(int Mv, int Mhz)> effective, CurvePlan plan, int? targetMhz,
        WriteVerification verdict, bool planFromReference, bool refined = false)
    {
        // The verdict on the write is already in (see ConfirmWrite); this reports the realized clock,
        // which lives in the live frequency column. If that column reads collapsed (usually a power-state
        // change mid-apply), the numbers would be meaningless and an explicit target would look "not
        // reached", so skip the readout rather than print a misleading point.
        if (!CurveFreqsReadable(effective)
            || PointAtVoltage(effective, plan.CapMv) is not { } cap
            || PointAtVoltage(effective, plan.FlatMv) is not { } flat)
        {
            return new[] { UncleanReadBackLine };
        }

        // The clock carries a ~ because the segment's read-back is one bin-snap away from what the
        // boost realizes; the voltage is the measured settle law and prints plain.
        int realizedMhz = SegmentClockAt(cap, flat, plan.SettleMv);

        // Only a confirmed verdict may read as one: a plan with nothing measurable to check (a pure
        // overclock, or a cap whose every reduction is sub-bin) reports the realized point without
        // claiming the write was verified against the curve.
        string line = verdict == WriteVerification.Confirmed
            ? $"Confirming operating point: {plan.SettleMv} mV / ~{realizedMhz} MHz"
            : $"Operating point: {plan.SettleMv} mV / ~{realizedMhz} MHz (no measurable reduction "
              + "to verify the write against)";
        if (targetMhz is { } f)
        {
            if (Math.Abs(plan.SettleMhz - f) > CurveBinMhz)
            {
                line += $" - the requested {f} MHz was floored to the curve's minimum clock";
            }
            else if (Math.Abs(realizedMhz - plan.SettleMhz) > CurveBinMhz)
            {
                line += planFromReference
                    ? refined
                        ? $" - off the {f} MHz target at the current temperature (the refined "
                          + "write sits within a bin of the reference's)"
                        : $" - off the {f} MHz target at the current temperature (the written "
                          + "offset matches the reference exactly)"
                    : $" - target {f} not reached (driver smoothed the flatten)";
            }
        }

        return new[] { line + FoldNote(plan.FlatMhz - plan.CapMhz, flat.Mhz - cap.Mhz) + SnapNote(effective, flat) };
    }

    /// <summary>The per-anchor frequency deltas currently applied, index-aligned with the curve.</summary>
    internal static int[] CurveDeltasKhz(IntPtr gpu)
        => NvApi.GetCurveFreqDeltasKhz(gpu, NvApi.GetVfCurve(gpu).Count);

    // --- Pstates parsing ---

    /// <summary>The populated clock and base-voltage entries of the P0 pstate — the 3D performance
    /// state the tuning lives in — with the driver's counts clamped to the struct bounds. Both lists
    /// are empty when the info holds no P0. The one place a P0 field lookup walks the pstates.</summary>
    internal static (IReadOnlyList<Pstate20ClockEntry> Clocks, IReadOnlyList<Pstate20BaseVoltageEntry> BaseVoltages)
        P0Entries(Pstates20InfoV1 info)
    {
        int numPstates = (int)Math.Min(info.NumPstates, (uint)Pstates20InfoV1.MaxPstates);
        for (int p = 0; p < numPstates; p++)
        {
            if (info.Pstates[p].PstateId == 0)
            {
                return (
                    info.Pstates[p].Clocks
                        .Take((int)Math.Min(info.NumClocks, (uint)Pstate20.MaxClocks)).ToArray(),
                    info.Pstates[p].BaseVoltages
                        .Take((int)Math.Min(info.NumBaseVoltages, (uint)Pstate20.MaxBaseVoltages)).ToArray());
            }
        }

        return (Array.Empty<Pstate20ClockEntry>(), Array.Empty<Pstate20BaseVoltageEntry>());
    }

    /// <summary>The P0 clock entry for a domain, or null when the pstate holds none.</summary>
    internal static Pstate20ClockEntry? P0Clock(Pstates20InfoV1 info, uint domainId)
    {
        foreach (Pstate20ClockEntry entry in P0Entries(info).Clocks)
        {
            if (entry.DomainId == domainId)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>The P0 base-voltage entry for a domain, or null when the pstate holds none.</summary>
    internal static Pstate20BaseVoltageEntry? P0BaseVoltage(Pstates20InfoV1 info, uint domainId)
    {
        foreach (Pstate20BaseVoltageEntry entry in P0Entries(info).BaseVoltages)
        {
            if (entry.DomainId == domainId)
            {
                return entry;
            }
        }

        return null;
    }
}
