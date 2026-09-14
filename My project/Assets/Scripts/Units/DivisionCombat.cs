using System;

// Formation-versus-formation combat is kept beside, but separate from, route
// execution. The partial preserves DivisionSystem's public API while giving the
// next command-interface pass one focused place for combat doctrine and tuning.
public sealed partial class DivisionSystem
{
    // A defender physically occupies its counter cell and, once deployed, the
    // cells of its assigned frontage. Attacking any part of that active line
    // therefore engages the division rather than an abstract tile alone.
    private Division FindOpposingDivision(int x, int y, int attackerOwner)
    {
        int cell = y * map.Width + x;
        foreach (Division candidate in divisions)
        {
            if (candidate.IsDestroyed || candidate.OwnerId == attackerOwner) continue;
            if (candidate.Cell == cell) return candidate;
        }
        foreach (Division candidate in divisions)
        {
            if (candidate.IsDestroyed || candidate.OwnerId == attackerOwner ||
                candidate.Stance != DivisionStance.Defend || !IsOnHeldLine(candidate)) continue;
            foreach (int held in candidate.HeldLine) if (held == cell) return candidate;
        }
        return null;
    }

    // Both sides take losses from their pre-combat strength, so destruction is
    // simultaneous and a doomed defender still returns fire. Terrain and dug-in
    // defence reduce losses to the holder; Reserve units are easier to disrupt.
    private void ResolveUnitCombat(Division attacker, Division defender, int x, int y)
    {
        int attackStrength = attacker.Strength;
        int defenderStrength = defender.Strength;
        int groundDefense = map.GetCell(x, y).Defense;
        double disruption = defender.Stance == DivisionStance.Reserve ? 1.25d : 1d;
        int defenderLoss = Math.Max(1, (int)Math.Round(
            (attackStrength * 0.13d + 220d) * 120d / (120d + groundDefense) * disruption));
        int attackerLoss = Math.Max(1, (int)Math.Round(defenderStrength * 0.09d + 160d));
        RecordLoss(defender.OwnerId, defender.TakeLoss(defenderLoss));
        RecordLoss(attacker.OwnerId, attacker.TakeLoss(attackerLoss));
    }

    private void RecordLoss(int ownerId, int losses)
    {
        if (losses <= 0) return;
        lossesThisStep.TryGetValue(ownerId, out int total);
        lossesThisStep[ownerId] = total + losses;
    }

    private static string CombatKey(Division a, Division b)
    {
        string first = a.OwnerId + "/" + a.Number;
        string second = b.OwnerId + "/" + b.Number;
        return string.CompareOrdinal(first, second) < 0 ? first + ":" + second : second + ":" + first;
    }
}
