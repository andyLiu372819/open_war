using System;
using System.Collections.Generic;

// Raises infantry divisions, routes them, and advances them one cell per step.
// Territory now changes hands only where a division goes: there is no national
// border attack, so a nation grows exactly as far as it can march.
public sealed partial class DivisionSystem
{
    private readonly MapData map;
    private readonly List<Division> divisions = new List<Division>();
    private readonly Dictionary<int, int> raised = new Dictionary<int, int>();
    private readonly List<Division> destroyedThisStep = new List<Division>();
    private readonly Dictionary<int, int> lossesThisStep = new Dictionary<int, int>();
    private readonly HashSet<string> engagementsThisStep = new HashSet<string>();
    // Reserve divisions draw replacements from their nation, so the roster
    // needs to reach the owning players the same way the pocket system does.
    private readonly Dictionary<int, PlayerData> players = new Dictionary<int, PlayerData>();
    public IReadOnlyList<Division> Divisions => divisions;
    public IReadOnlyList<Division> LastDestroyed => destroyedThisStep;
    public int LastLossesFor(int ownerId) => lossesThisStep.TryGetValue(ownerId, out int losses) ? losses : 0;

    public DivisionSystem(MapData map, IReadOnlyList<PlayerData> factions = null)
    {
        this.map = map ?? throw new ArgumentNullException(nameof(map));
        if (factions == null) return;
        foreach (PlayerData faction in factions) players[faction.Id] = faction;
    }

    // Stances take effect on the next simulation tick. Anything that holds
    // position drops its route: it is no longer going.
    public void SetStance(Division division, DivisionStance stance)
    {
        if (division == null) return;
        if (division.Stance == DivisionStance.Defend && stance != DivisionStance.Defend)
        {
            ReleaseEntrenchment(division);
            division.ClearHeldLine();
        }
        division.Stance = stance;
        if (stance == DivisionStance.Defend)
        {
            division.ClearOrders();
            RouteDefenderToLine(division);
        }
        else if (stance == DivisionStance.Reserve)
            division.ClearOrders();
    }

    public void SetStance(IReadOnlyList<Division> group, DivisionStance stance)
    {
        if (group == null) return;
        foreach (Division division in group) SetStance(division, stance);
    }

    public int CountFor(int ownerId)
    {
        int total = 0;
        foreach (Division division in divisions) if (division.OwnerId == ownerId) total++;
        return total;
    }

    public Division FindAt(int x, int y, int ownerId)
    {
        foreach (Division division in divisions)
            if (division.OwnerId == ownerId && division.X == x && division.Y == y) return division;
        return null;
    }

    // Nearest division of the faction within a few cells, so a click does not
    // have to land exactly on the counter.
    public Division FindNear(int x, int y, int ownerId, int radius)
    {
        Division best = null;
        int bestDistance = int.MaxValue;
        foreach (Division division in divisions)
        {
            if (division.OwnerId != ownerId) continue;
            int dx = division.X - x, dy = division.Y - y;
            int distance = dx * dx + dy * dy;
            if (distance > radius * radius || distance >= bestDistance) continue;
            bestDistance = distance;
            best = division;
        }
        return best;
    }

    // Every division of the faction inside a map-cell rectangle, for box select.
    public void FindInBox(int minX, int minY, int maxX, int maxY, int ownerId, List<Division> found)
    {
        found.Clear();
        if (minX > maxX) { int swap = minX; minX = maxX; maxX = swap; }
        if (minY > maxY) { int swap = minY; minY = maxY; maxY = swap; }
        foreach (Division division in divisions)
        {
            if (division.OwnerId != ownerId) continue;
            if (division.X < minX || division.X > maxX) continue;
            if (division.Y < minY || division.Y > maxY) continue;
            found.Add(division);
        }
    }

    // A division forms on owned ground and costs military population outright.
    public bool TryForm(PlayerData owner, out Division division)
    {
        division = null;
        if (owner == null || owner.Manpower < Division.Cost) return false;
        if (!TryFindMuster(owner.Id, out int x, out int y)) return false;
        if (!owner.TrySpendManpower(Division.Cost)) return false;
        raised.TryGetValue(owner.Id, out int number);
        number++;
        raised[owner.Id] = number;
        division = new Division(number, owner.Id, x, y, Division.Cost, map.Width);
        players[owner.Id] = owner;
        divisions.Add(division);
        return true;
    }

    // Divisions muster apart from one another. Stacking them all on the
    // territory centre would hide every counter but the last one, and would also
    // bury the territory's own population label underneath them.
    private const int MusterSpacing = 9;

    // Raises a division on a chosen cell rather than wherever there is room.
    // Scripted orders of battle need to say where each formation starts.
    public bool TryPlace(PlayerData owner, int x, int y, out Division division)
    {
        division = null;
        if (owner == null || owner.Manpower < Division.Cost) return false;
        if (!map.IsOwnedBy(x, y, owner.Id) || !map.IsWalkable(x, y)) return false;
        if (!owner.TrySpendManpower(Division.Cost)) return false;
        raised.TryGetValue(owner.Id, out int number);
        number++;
        raised[owner.Id] = number;
        division = new Division(number, owner.Id, x, y, Division.Cost, map.Width);
        players[owner.Id] = owner;
        divisions.Add(division);
        return true;
    }

    private bool TryFindMuster(int ownerId, out int x, out int y)
    {
        x = y = -1;
        // The first division of a nation forms at the middle of its territory.
        if (CountFor(ownerId) == 0 && map.TryGetTerritoryCenter(ownerId, out float cx, out float cy))
        {
            int centreX = (int)cx, centreY = (int)cy;
            if (map.IsOwnedBy(centreX, centreY, ownerId) && map.IsWalkable(centreX, centreY))
            {
                x = centreX; y = centreY;
                return true;
            }
        }
        int fallbackX = -1, fallbackY = -1;
        foreach (int id in map.OwnedCells(ownerId))
        {
            int ox = id % map.Width, oy = id / map.Width;
            if (!map.IsWalkable(ox, oy) || FindAt(ox, oy, ownerId) != null) continue;
            if (fallbackX < 0) { fallbackX = ox; fallbackY = oy; }
            if (NearestDivisionDistance(ox, oy, ownerId) < MusterSpacing * MusterSpacing) continue;
            x = ox; y = oy;
            return true;
        }
        // Cramped territory: accept any free cell rather than refuse the unit.
        if (fallbackX < 0) return false;
        x = fallbackX; y = fallbackY;
        return true;
    }

    private int NearestDivisionDistance(int x, int y, int ownerId)
    {
        int nearest = int.MaxValue;
        foreach (Division division in divisions)
        {
            if (division.OwnerId != ownerId) continue;
            int dx = division.X - x, dy = division.Y - y;
            nearest = Math.Min(nearest, dx * dx + dy * dy);
        }
        return nearest;
    }

    // One step per division. Captured cells are reported so the renderer can
    // repaint exactly what changed, and defenders so they can be charged.
    public void Step(List<int> captured, List<int> defenders)
    {
        captured.Clear();
        defenders.Clear();
        destroyedThisStep.Clear();
        lossesThisStep.Clear();
        engagementsThisStep.Clear();
        for (int i = divisions.Count - 1; i >= 0; i--)
        {
            Division division = divisions[i];
            if (division.IsDestroyed)
            {
                ReleaseEntrenchment(division);
                destroyedThisStep.Add(division);
                divisions.RemoveAt(i);
                continue;
            }
            StepOne(division, captured, defenders);
        }
        // A formation can be destroyed by a unit that acted later in the list.
        // Remove every casualty before the step is reported to the controller.
        for (int i = divisions.Count - 1; i >= 0; i--)
        {
            Division division = divisions[i];
            if (!division.IsDestroyed) continue;
            ReleaseEntrenchment(division);
            if (!destroyedThisStep.Contains(division)) destroyedThisStep.Add(division);
            divisions.RemoveAt(i);
        }
    }

    private void StepOne(Division division, List<int> captured, List<int> defenders)
    {
        // A division holds ground even when idle: it keeps the cell it stands on.
        Occupy(division, division.X, division.Y, captured);
        switch (division.Stance)
        {
            case DivisionStance.Defend:
                // A frontage assignment is also a deployment order. March to
                // the nearest cell of that line through friendly territory,
                // then begin digging in across the assigned frontage.
                if (division.HeldLine.Count > 0 && !IsOnHeldLine(division))
                {
                    if (!division.HasOrders) RouteDefenderToLine(division);
                    int defensiveMoves = HasFastTransportNext(division) ? 2 : 1;
                    for (int move = 0; move < defensiveMoves && !IsOnHeldLine(division); move++)
                        if (!TryDefensiveMove(division)) break;
                    if (!IsOnHeldLine(division)) return;
                }
                Entrench(division);
                return;
            case DivisionStance.Reserve:
                Refit(division);
                return;
            case DivisionStance.Redeploy:
                // Repositioning covers more ground because it refuses to fight.
                int redeployMoves = Division.RedeploySpeed + (HasFastTransportNext(division) ? 1 : 0);
                for (int move = 0; move < redeployMoves; move++)
                    if (!TryMove(division, captured, defenders)) break;
                return;
            default:
                int attackMoves = HasFastTransportNext(division) ? 2 : 1;
                for (int move = 0; move < attackMoves; move++)
                    if (!TryMove(division, captured, defenders)) break;
                return;
        }
    }

    // A dug-in division makes the ground it holds far costlier to take. When it
    // has been given a stretch of front, the same division covers every cell of
    // it, so a wider assignment buys less defence per cell.
    private void Entrench(Division division)
    {
        int bonus = division.LineDefense;
        if (division.HeldLine.Count == 0)
        {
            Dig(division.X, division.Y, bonus);
            return;
        }
        foreach (int id in division.HeldLine)
        {
            int x = id % map.Width, y = id / map.Width;
            // Ground lost since the line was drawn is no longer ours to hold.
            if (!map.IsOwnedBy(x, y, division.OwnerId)) continue;
            Dig(x, y, bonus);
        }
    }

    private void Dig(int x, int y, int bonus)
    {
        if (!map.InsideBorder(x, y)) return;
        MapCell cell = map.GetCell(x, y);
        int dug = TerrainRules.Defense(cell.Terrain) + bonus;
        if (cell.Defense < dug) cell.Defense = dug;
    }

    // Assigns a division to hold a stretch of front. Only owned walkable ground
    // counts, and taking the assignment puts the division on the defensive.
    public int AssignFrontage(Division division, IReadOnlyList<int> cells)
    {
        if (division == null) return 0;
        var held = new List<int>();
        if (cells != null)
            foreach (int id in cells)
            {
                int x = id % map.Width, y = id / map.Width;
                if (!map.IsOwnedBy(x, y, division.OwnerId) || !map.IsWalkable(x, y)) continue;
                if (!held.Contains(id)) held.Add(id);
            }
        ReleaseEntrenchment(division);
        division.SetHeldLine(held);
        if (held.Count > 0) SetStance(division, DivisionStance.Defend);
        else if (division.Stance == DivisionStance.Defend) division.ClearOrders();
        return held.Count;
    }

    private bool IsOnHeldLine(Division division)
    {
        int cell = division.Cell;
        foreach (int held in division.HeldLine) if (held == cell) return true;
        return false;
    }

    private bool TryDefensiveMove(Division division)
    {
        if (!division.TryPeekNext(out int nx, out int ny)) return false;
        if (!map.IsWalkable(nx, ny) || !map.IsOwnedBy(nx, ny, division.OwnerId))
        {
            division.ClearOrders();
            return false;
        }
        division.AdvanceTo(nx, ny);
        return true;
    }

    // Splits a drawn line between the divisions given, left to right along the
    // line, so a group shares the front instead of stacking on the same cells.
    public int AssignFrontage(IReadOnlyList<Division> group, IReadOnlyList<int> line)
    {
        if (group == null || group.Count == 0 || line == null || line.Count == 0) return 0;
        int assigned = 0;
        var share = new List<int>();
        for (int i = 0; i < group.Count; i++)
        {
            share.Clear();
            int from = line.Count * i / group.Count;
            int to = line.Count * (i + 1) / group.Count;
            for (int c = from; c < to; c++) share.Add(line[c]);
            assigned += AssignFrontage(group[i], share);
        }
        return assigned;
    }

    // Out of the line, a division rebuilds from the national military pool.
    private void Refit(Division division)
    {
        if (division.Strength >= Division.Cost) return;
        if (!players.TryGetValue(division.OwnerId, out PlayerData owner)) return;
        int wanted = Math.Min(Division.ReserveRefit, Division.Cost - division.Strength);
        if (wanted <= 0 || !owner.TrySpendManpower(wanted)) return;
        division.Reinforce(wanted);
    }

    // One cell of movement. Returns false when the division did not move.
    private bool TryMove(Division division, List<int> captured, List<int> defenders)
    {
        if (!division.TryPeekNext(out int nx, out int ny)) return false;
        if (!map.IsWalkable(nx, ny)) { division.ClearOrders(); return false; }
        Division opposing = FindOpposingDivision(nx, ny, division.OwnerId);
        if (opposing != null)
        {
            string engagement = CombatKey(division, opposing);
            // Opposing orders can make the same pair meet from both directions.
            // They exchange fire once per simulation tick, not twice through
            // the roster loop.
            if (!engagementsThisStep.Add(engagement)) return false;
            ResolveUnitCombat(division, opposing, nx, ny);
            if (division.IsDestroyed || !opposing.IsDestroyed) return false;
        }
        int occupant = map.GetCell(nx, ny).OwnerId;
        if (occupant >= 0 && occupant != division.OwnerId)
        {
            // Redeploying units are moving, not fighting: they stop short.
            if (division.Stance == DivisionStance.Redeploy)
            {
                division.ClearOrders();
                return false;
            }
            // Contested ground has to be fought for before the division enters.
            int cost = map.GetAdvanceCost(nx, ny, division.OwnerId);
            if (division.Manpower <= cost)
            {
                // Too weak to press the attack; hold rather than evaporate.
                division.ClearOrders();
                return false;
            }
            defenders.Add(occupant);
            if (!map.TryAdvanceCell(nx, ny, division, out bool taken))
            {
                division.ClearOrders();
                return false;
            }
            // Still grinding down the garrison; the tile has not fallen yet.
            if (!taken) return false;
            captured.Add(ny * map.Width + nx);
        }
        division.AdvanceTo(nx, ny);
        Occupy(division, nx, ny, captured);
        return true;
    }

    private bool HasFastTransportNext(Division division) =>
        division.TryPeekNext(out int x, out int y) && map.HasFastTransport(x, y);

    // Remove works when their formation leaves, changes role, or is destroyed.
    // Damage below the terrain's normal garrison value is preserved.
    private void ReleaseEntrenchment(Division division)
    {
        if (division == null) return;
        if (division.HeldLine.Count == 0)
        {
            ResetDefense(division.X, division.Y, division);
            return;
        }
        foreach (int id in division.HeldLine)
            ResetDefense(id % map.Width, id / map.Width, division);
    }

    private void ResetDefense(int x, int y, Division leaving)
    {
        if (!map.InsideBorder(x, y)) return;
        int id = y * map.Width + x;
        foreach (Division other in divisions)
        {
            if (other == leaving || other.IsDestroyed || other.OwnerId != leaving.OwnerId ||
                other.Stance != DivisionStance.Defend) continue;
            if (other.HeldLine.Count == 0 && other.Cell == id) return;
            foreach (int held in other.HeldLine) if (held == id) return;
        }
        MapCell cell = map.GetCell(x, y);
        cell.Defense = Math.Min(cell.Defense, TerrainRules.Defense(cell.Terrain));
    }

    // Claim the cell the division stands on plus its frontage, wilderness only.
    private void Occupy(Division division, int x, int y, List<int> captured)
    {
        for (int dx = -Division.Frontage; dx <= Division.Frontage; dx++)
        for (int dy = -Division.Frontage; dy <= Division.Frontage; dy++)
        {
            if (dx * dx + dy * dy > Division.Frontage * Division.Frontage) continue;
            int cx = x + dx, cy = y + dy;
            if (!map.InsideBorder(cx, cy)) continue;
            if (map.GetCell(cx, cy).OwnerId != -1) continue;
            if (map.TryClaimCell(cx, cy, division.OwnerId)) captured.Add(cy * map.Width + cx);
        }
    }
}
