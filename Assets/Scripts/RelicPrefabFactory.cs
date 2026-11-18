using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns a 3D relic display object at a given position.
/// Uses primitive shapes + RelicObject3D for now.
/// Swap CreatePrimitiveRelic() with prefab instantiation once Blender models are ready.
/// Maps specific RelicTypes to unique shapes so each relic is visually distinct.
/// </summary>
public static class RelicPrefabFactory
{
    // Swap this to a prefab dictionary once you have Blender models:
    // private static Dictionary<RelicType, GameObject> _prefabMap;

    /// <summary>
    /// Instantiate a relic display object for the given relic.
    /// Parent it and zero local position after calling this.
    /// </summary>
    public static GameObject Create(Relic relic, Transform parent)
    {
        GameObject go = CreatePrimitive(relic);
        go.name = $"Relic_{relic.type}";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(
            Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
        go.transform.localScale = GetRelicScale(relic.type);

        // Remove physics
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        // Add and initialise the visual controller
        var obj3D = go.AddComponent<RelicObject3D>();
        obj3D.Initialize(relic);

        return go;
    }

    // ── Primitive Builder ─────────────────────────────────────────────────────

    static GameObject CreatePrimitive(Relic relic)
    {
        // Each RelicType gets a distinct shape combination
        // Once you have Blender meshes, replace these with Instantiate(prefab)

        PrimitiveType baseShape  = GetBaseShape(relic.type);
        GameObject    go         = GameObject.CreatePrimitive(baseShape);

        // Some relics get a second decorative primitive child
        PrimitiveType? accentShape = GetAccentShape(relic.type);
        if (accentShape.HasValue)
        {
            var accent = GameObject.CreatePrimitive(accentShape.Value);
            accent.transform.SetParent(go.transform, false);
            accent.transform.localPosition = GetAccentOffset(relic.type);
            accent.transform.localScale    = GetAccentScale(relic.type);

            // Remove accent collider
            var acCol = accent.GetComponent<Collider>();
            if (acCol != null) Object.Destroy(acCol);

            // Accent shares the same material as parent (set by RelicObject3D)
            // So we mark it for the same renderer override
            accent.name = "Accent";
        }

        return go;
    }

    // ── Shape Mapping — each RelicType → distinct visual ─────────────────────

    static PrimitiveType GetBaseShape(RelicType type) => type switch
    {
        // Rule Changers — cube variants
        RelicType.FourthHand    => PrimitiveType.Cube,
        RelicType.DuelistsCode  => PrimitiveType.Cube,
        RelicType.PerfectGamble => PrimitiveType.Cube,
        RelicType.ComboMachine  => PrimitiveType.Cube,
        RelicType.CardCycler    => PrimitiveType.Cube,
        RelicType.TacticalDelay => PrimitiveType.Cube,

        // Decay Manipulation — sphere variants
        RelicType.RotAlchemist    => PrimitiveType.Sphere,
        RelicType.CorrosionEngine => PrimitiveType.Sphere,
        RelicType.SlowRot         => PrimitiveType.Sphere,
        RelicType.RotBurst        => PrimitiveType.Sphere,
        RelicType.Purifier        => PrimitiveType.Sphere,
        RelicType.NecromancyRelic => PrimitiveType.Sphere,

        // Generalist score relics and suit seals — recognizable placeholders
        RelicType.SpadeSeal   => PrimitiveType.Cube,
        RelicType.HeartSeal   => PrimitiveType.Sphere,
        RelicType.DiamondSeal => PrimitiveType.Cube,
        RelicType.ClubSeal    => PrimitiveType.Cylinder,
        RelicType.SmallMult   => PrimitiveType.Cylinder,
        RelicType.MediumMult  => PrimitiveType.Cube,
        RelicType.LargeMult   => PrimitiveType.Capsule,

        // Rank, parity, and pattern relics — simple readable placeholders.
        RelicType.MarkedNumbers => PrimitiveType.Cube,
        RelicType.FourthBell    => PrimitiveType.Cylinder,
        RelicType.OddEtchings   => PrimitiveType.Cube,
        RelicType.EvenEtchings  => PrimitiveType.Cylinder,
        RelicType.RoyalCourt    => PrimitiveType.Capsule,
        RelicType.SmallChange   => PrimitiveType.Cylinder,
        RelicType.AceInTheHole  => PrimitiveType.Cube,
        RelicType.BrokenRuler   => PrimitiveType.Cube,
        RelicType.TwinTeeth     => PrimitiveType.Cylinder,
        RelicType.ThreeKnocks   => PrimitiveType.Sphere,
        RelicType.UniformThread => PrimitiveType.Cylinder,

        // Combo Engines — cylinder variants
        RelicType.PairAddict     => PrimitiveType.Cylinder,
        RelicType.LowCardGenius  => PrimitiveType.Cylinder,
        RelicType.HighRoller     => PrimitiveType.Cylinder,
        RelicType.OddCollector   => PrimitiveType.Cylinder,
        RelicType.EvenMaster     => PrimitiveType.Cylinder,
        RelicType.ComboBank      => PrimitiveType.Cylinder,

        // Scaling Multipliers — capsule variants
        RelicType.LoneWolf        => PrimitiveType.Capsule,
        RelicType.TurnInvestor    => PrimitiveType.Capsule,
        RelicType.GrindCore       => PrimitiveType.Capsule,
        RelicType.PerfectHand     => PrimitiveType.Capsule,
        RelicType.HighValueInvest => PrimitiveType.Capsule,
        RelicType.TimeEngine      => PrimitiveType.Capsule,

        // Card Manipulation — cube + accent
        RelicType.AnchorCard  => PrimitiveType.Cube,
        RelicType.GoldenCard  => PrimitiveType.Cube,
        RelicType.DrawReactor => PrimitiveType.Cube,
        RelicType.ValueLock   => PrimitiveType.Cube,

        // Risk / Challenge — sphere (dangerous looking)
        RelicType.GlassCannon => PrimitiveType.Sphere,
        RelicType.DoubleDecay => PrimitiveType.Sphere,
        RelicType.InstantRot  => PrimitiveType.Sphere,
        RelicType.PlagueAll   => PrimitiveType.Sphere,

        _ => PrimitiveType.Cube
    };

    static PrimitiveType? GetAccentShape(RelicType type) => type switch
    {
        // Relics with a decorative second piece
        RelicType.DuelistsCode  => PrimitiveType.Cylinder,  // sword hilt suggestion
        RelicType.GoldenCard    => PrimitiveType.Cube,       // smaller gold cube on top
        RelicType.NecromancyRelic => PrimitiveType.Sphere,   // skull-like second sphere
        RelicType.GlassCannon   => PrimitiveType.Cylinder,   // cannon barrel
        RelicType.TimeEngine    => PrimitiveType.Sphere,     // gear + orb
        RelicType.RotBurst      => PrimitiveType.Sphere,     // orbiting decay orb
        RelicType.ComboBank     => PrimitiveType.Cube,       // coin stack suggestion
        RelicType.SpadeSeal     => PrimitiveType.Capsule,    // spade point
        RelicType.HeartSeal     => PrimitiveType.Sphere,     // paired round mark
        RelicType.DiamondSeal   => PrimitiveType.Cube,       // raised gem
        RelicType.ClubSeal      => PrimitiveType.Sphere,     // club head
        RelicType.SmallMult     => PrimitiveType.Sphere,     // small counter
        RelicType.MediumMult    => PrimitiveType.Cylinder,   // ledger dial
        RelicType.LargeMult     => PrimitiveType.Cube,       // large counter
        RelicType.MarkedNumbers => PrimitiveType.Cylinder,   // carved number
        RelicType.FourthBell    => PrimitiveType.Sphere,     // bell clapper
        RelicType.OddEtchings   => PrimitiveType.Cube,       // uneven stamp
        RelicType.EvenEtchings  => PrimitiveType.Cube,       // symmetric stamp
        RelicType.RoyalCourt    => PrimitiveType.Cube,       // crown block
        RelicType.SmallChange   => PrimitiveType.Cylinder,   // coin stack
        RelicType.AceInTheHole  => PrimitiveType.Sphere,     // hidden pip
        RelicType.BrokenRuler   => PrimitiveType.Cube,       // snapped ruler
        RelicType.TwinTeeth     => PrimitiveType.Capsule,    // second tooth
        RelicType.ThreeKnocks   => PrimitiveType.Sphere,     // knocker
        RelicType.UniformThread => PrimitiveType.Capsule,    // seam
        _ => null
    };

    static Vector3 GetAccentOffset(RelicType type) => type switch
    {
        RelicType.DuelistsCode    => new Vector3(0f,  0.6f,  0f),
        RelicType.GoldenCard      => new Vector3(0f,  0.55f, 0f),
        RelicType.NecromancyRelic => new Vector3(0f,  0.5f,  0f),
        RelicType.GlassCannon     => new Vector3(0f,  0f,    0.5f),
        RelicType.TimeEngine      => new Vector3(0.3f, 0.3f, 0f),
        RelicType.RotBurst        => new Vector3(0.4f, 0f,   0f),
        RelicType.ComboBank       => new Vector3(0f,  0.4f,  0f),
        RelicType.SpadeSeal       => new Vector3(0f,  0.50f, 0f),
        RelicType.HeartSeal       => new Vector3(0.42f, 0.25f, 0f),
        RelicType.DiamondSeal     => new Vector3(0f,  0.50f, 0f),
        RelicType.ClubSeal        => new Vector3(0f,  0.58f, 0f),
        RelicType.SmallMult       => new Vector3(0f,  0.30f, 0f),
        RelicType.MediumMult      => new Vector3(0f,  0.55f, 0f),
        RelicType.LargeMult       => new Vector3(0f,  0.65f, 0f),
        RelicType.MarkedNumbers   => new Vector3(0f,  0.52f, 0f),
        RelicType.FourthBell      => new Vector3(0f,  0.48f, 0f),
        RelicType.OddEtchings     => new Vector3(0.45f, 0.25f, 0f),
        RelicType.EvenEtchings    => new Vector3(0f,  0.50f, 0f),
        RelicType.RoyalCourt      => new Vector3(0f,  0.60f, 0f),
        RelicType.SmallChange     => new Vector3(0f,  0.50f, 0f),
        RelicType.AceInTheHole    => new Vector3(0f,  0.48f, 0f),
        RelicType.BrokenRuler     => new Vector3(0.45f, 0.30f, 0f),
        RelicType.TwinTeeth       => new Vector3(0.44f, 0.25f, 0f),
        RelicType.ThreeKnocks     => new Vector3(0f,  0.60f, 0f),
        RelicType.UniformThread   => new Vector3(0f,  0.53f, 0f),
        _ => Vector3.zero
    };

    static Vector3 GetAccentScale(RelicType type) => type switch
    {
        RelicType.DuelistsCode    => new Vector3(0.15f, 0.5f,  0.15f),
        RelicType.GoldenCard      => new Vector3(0.5f,  0.15f, 0.5f),
        RelicType.NecromancyRelic => new Vector3(0.4f,  0.4f,  0.4f),
        RelicType.GlassCannon     => new Vector3(0.2f,  0.2f,  0.8f),
        RelicType.TimeEngine      => new Vector3(0.35f, 0.35f, 0.35f),
        RelicType.RotBurst        => new Vector3(0.25f, 0.25f, 0.25f),
        RelicType.ComboBank       => new Vector3(0.7f,  0.2f,  0.7f),
        RelicType.SpadeSeal       => new Vector3(0.35f, 0.60f, 0.35f),
        RelicType.HeartSeal       => new Vector3(0.60f, 0.60f, 0.60f),
        RelicType.DiamondSeal     => new Vector3(0.48f, 0.48f, 0.48f),
        RelicType.ClubSeal        => new Vector3(0.65f, 0.45f, 0.65f),
        RelicType.SmallMult       => new Vector3(0.40f, 0.23f, 0.40f),
        RelicType.MediumMult      => new Vector3(0.48f, 0.20f, 0.48f),
        RelicType.LargeMult       => new Vector3(0.45f, 0.40f, 0.45f),
        RelicType.MarkedNumbers   => new Vector3(0.34f, 0.30f, 0.34f),
        RelicType.FourthBell      => new Vector3(0.37f, 0.37f, 0.37f),
        RelicType.OddEtchings     => new Vector3(0.42f, 0.55f, 0.42f),
        RelicType.EvenEtchings    => new Vector3(0.42f, 0.28f, 0.42f),
        RelicType.RoyalCourt      => new Vector3(0.57f, 0.20f, 0.57f),
        RelicType.SmallChange     => new Vector3(0.65f, 0.16f, 0.65f),
        RelicType.AceInTheHole    => new Vector3(0.45f, 0.45f, 0.45f),
        RelicType.BrokenRuler     => new Vector3(1.00f, 0.20f, 0.20f),
        RelicType.TwinTeeth       => new Vector3(0.34f, 0.63f, 0.34f),
        RelicType.ThreeKnocks     => new Vector3(0.35f, 0.35f, 0.35f),
        RelicType.UniformThread   => new Vector3(0.28f, 0.60f, 0.28f),
        _ => Vector3.one * 0.3f
    };

    static Vector3 GetRelicScale(RelicType type) => type switch
    {
        // Capsules need to be smaller to not look huge
        RelicType.LoneWolf        => new Vector3(0.06f, 0.08f, 0.06f),
        RelicType.TurnInvestor    => new Vector3(0.06f, 0.08f, 0.06f),
        RelicType.GrindCore       => new Vector3(0.06f, 0.08f, 0.06f),
        RelicType.PerfectHand     => new Vector3(0.06f, 0.08f, 0.06f),
        RelicType.HighValueInvest => new Vector3(0.06f, 0.08f, 0.06f),
        RelicType.TimeEngine      => new Vector3(0.06f, 0.08f, 0.06f),

        // Cylinders — flatten slightly
        RelicType.PairAddict     => new Vector3(0.08f, 0.05f, 0.08f),
        RelicType.LowCardGenius  => new Vector3(0.08f, 0.05f, 0.08f),
        RelicType.HighRoller     => new Vector3(0.08f, 0.05f, 0.08f),
        RelicType.OddCollector   => new Vector3(0.08f, 0.05f, 0.08f),
        RelicType.EvenMaster     => new Vector3(0.08f, 0.05f, 0.08f),
        RelicType.ComboBank      => new Vector3(0.08f, 0.05f, 0.08f),

        // Default uniform scale
        _ => Vector3.one * 0.07f
    };
}
