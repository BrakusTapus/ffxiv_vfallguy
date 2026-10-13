using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace vfallguy;

// Stage 2: everything seen so far lives in x [-229, -168], z [212, 273], and the floor is flat (y = 6).
//
// DATA SOURCE: one ~79s logged run. All timings are averages with ~0.05s frame jitter - tune them in game.
//
// SeqDelay convention in this file: seconds since the PREVIOUS element of the sequence fired.
// That is what UpdateSequence does when activateIn == 0 (effect events). Single-element sequences
// ("periodic" spots) don't care, which is why most mechanics below are modelled that way: each spot
// gets its own sequence, so the order in which a simultaneous batch of events arrives doesn't matter.
public class Map2 : Map
{
    // ---- TODO: shapes and radii are NOT in the log. Placeholders - measure in game or read the Action sheet. ----
    private const float R34800 = 5;
    private const float R34799Pair = 4;
    private const float R34799Triple = 4;
    private const float R34716 = 3;
    private const float R34774 = 3;

    // 34800 is the only mechanic with a [cast] event; the effect lands this long after it
    // (the castTime parameter in the hook reads 0, so we hardcode it like Map3 does)
    private const float Cast34800ToEffect = 3.48f;

    private readonly AOESequence _spot34800;
    private readonly List<AOESequence> _seq34799 = []; // the pair + the six triple spots
    private readonly List<AOESequence> _seq34716 = []; // three sweeping lines
    private readonly List<AOESequence> _seq34774 = []; // sixteen independent spots

    private static PathfindMap BuildBaseMap()
    {
        // TODO: no obstacle data yet. Grid is centered on the logged area; add Block* calls once we know the layout.
        return new PathfindMap(0.5f, 0.25f, new(-200, 0, 242), 35, 35, 60);
    }

    // flat floor: HeightAt() returns the last entry's y for any z above it, the first entry's y below it
    private static readonly List<(float z, float y)> _heightProfile = [(0, 6.0f)];

    // TODO: goal is unknown (Map3 runs toward low z; 34800 sits at z=212, so this is a guess)
    public Map2(GameEvents events) : base(events, BuildBaseMap(), _heightProfile, 205)
    {
        // --- 34800: single spot, cast -> effect 3.48s later, repeats every 5.62s ---
        _spot34800 = CreatePeriodic(R34800, new(-200, 6.04f, 212), 5.62f);

        // --- 34799 (pair): two spots 8 apart alternate. -196 -> -204 takes 1.93s, -204 -> -196 takes 1.77s ---
        _seq34799.Add(CreateSequence(AOEShape.Circle, R34799Pair, 0,
        [
            (new Vector3(-196, 6, 216.5f), 0.0f, 1.77f),
            (new Vector3(-204, 6, 216.5f), 0.0f, 1.93f),
        ]));

        // --- 34799 (triples): two groups of three that alternate (G_a, then G_b 1.96s later, then G_a 2.15s later).
        // Every spot simply repeats every 4.11s, so each one is its own periodic sequence. ---
        foreach (var p in new Vector3[]
        {
            new(-228.55f, 6, 263.90f), new(-212.73f, 6, 259.40f), new(-224.56f, 6, 270.85f), // G_a
            new(-226.60f, 6, 267.39f), new(-214.68f, 6, 255.91f), new(-210.69f, 6, 262.86f), // G_b
        })
            _seq34799.Add(CreatePeriodic(R34799Triple, p, 4.11f));

        // --- 34716: three lines, each stepping 3 yalms. A line sweeps p1 -> p2 -> p3, pauses, sweeps back, pauses. ---
        _seq34716.Add(CreateSweepLine(new(-203.00f, 6, 226.90f), new(-200.00f, 6, 226.90f), new(-197.00f, 6, 226.90f)));
        _seq34716.Add(CreateSweepLine(new(-168.04f, 6, 266.99f), new(-169.54f, 6, 269.59f), new(-171.03f, 6, 272.19f)));
        _seq34716.Add(CreateSweepLine(new(-187.13f, 6, 255.99f), new(-188.63f, 6, 258.59f), new(-190.13f, 6, 261.19f)));

        // --- 34774: groups A, B, D, E fire in that order, 2s apart, so each spot repeats every ~8s (measured 7.97).
        // X and C are two pairs that alternate every 3s, so each spot repeats every ~6s (measured 5.98). ---
        Vector3[] ring =
        [
            new(-221.05f, 6, 269.96f), new(-188.52f, 6, 260.36f), new(-169.26f, 6, 264.55f), // A
            new(-218.43f, 6, 268.46f), new(-188.97f, 6, 263.57f), new(-169.69f, 6, 267.76f), // B
            new(-226.12f, 6, 261.13f), new(-171.19f, 6, 270.34f), new(-184.53f, 6, 255.73f), // D
            new(-223.50f, 6, 259.66f), new(-173.75f, 6, 272.36f), new(-187.00f, 6, 257.77f), // E
        ];
        Vector3[] sixSecond =
        [
            new(-207.20f, 6, 261.92f), new(-176.56f, 6, 259.66f), // X
            new(-212.24f, 6, 253.16f), new(-181.66f, 6, 268.48f), // C
        ];
        foreach (var p in ring)
            _seq34774.Add(CreatePeriodic(R34774, p, 7.97f));
        foreach (var p in sixSecond)
            _seq34774.Add(CreatePeriodic(R34774, p, 5.98f));
    }

    // TODO: needs obstacles + a goal + a logged safe path before this can do anything useful
    protected override List<Waypoint> RebuildPath(Vector3 startPos, DateTime startTime) => [];

    protected override void OnActionEffect(uint actionId, Vector3 casterPos)
    {
        switch (actionId)
        {
            case 34800:
                UpdateSequence(casterPos, 0, _spot34800);
                break;
            case 34799:
                UpdateSequence(casterPos, 0, _seq34799);
                break;
            case 34716:
                UpdateSequence(casterPos, 0, _seq34716);
                break;
            case 34774:
                UpdateSequence(casterPos, 0, _seq34774);
                break;
        }
    }

    protected override void OnStartCast(uint actionId, Vector3 casterPos)
    {
        if (actionId == 34800)
            UpdateSequence(casterPos, Cast34800ToEffect, _spot34800);
    }

    // one spot that fires every `period` seconds
    private AOESequence CreatePeriodic(float r, Vector3 pos, float period)
    {
        return CreateSequence(AOEShape.Circle, r, 0, [(pos, 0.0f, period)]);
    }

    // p1 -> p2 -> p3, pause, p3 -> p2 -> p1, pause. Origins repeat on purpose: FindIndexInSequence
    // resolves the duplicates by preferring the element we expect to fire next.
    // delays are measured from the previous element: 1.39 + 0.41 + 0.30 + 1.39 + 0.42 + 0.30 = 4.21s cycle
    private AOESequence CreateSweepLine(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        return CreateSequence(AOEShape.Circle, R34716, 0,
        [
            (p1, 0.0f, 1.39f), // start of the outward sweep, 1.39s after the return sweep ended
            (p2, 0.0f, 0.41f),
            (p3, 0.0f, 0.30f),
            (p3, 0.0f, 1.39f), // start of the return sweep
            (p2, 0.0f, 0.42f),
            (p1, 0.0f, 0.30f),
        ]);
    }
}
