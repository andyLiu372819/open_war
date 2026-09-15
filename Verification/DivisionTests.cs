using System;
using System.Collections.Generic;

static class DivisionTests
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("Divisions: " + message);
    }

    static TerrainType[,] Plains(int width, int height)
    {
        var terrain = new TerrainType[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++) terrain[x, y] = TerrainType.Land;
        return terrain;
    }

    static void Drain(DivisionSystem system, List<int> captured, List<int> defenders, int steps = 4000)
    {
        for (int i = 0; i < steps; i++)
        {
            bool moving = false;
            foreach (Division division in system.Divisions) if (division.HasOrders) moving = true;
            if (!moving) return;
            system.Step(captured, defenders);
        }
    }

    public static void Run()
    {
        var captured = new List<int>();
        var defenders = new List<int>();

        // Forming a division costs exactly 10,000 military population, and the
        // numbering counts up per faction.
        var map = new MapData(Plains(40, 30));
        for (int x = 2; x <= 6; x++)
        for (int y = 12; y <= 16; y++) map.TryClaimCell(x, y, 0);
        var system = new DivisionSystem(map);
        var player = new PlayerData(0, 25000);
        Check(!new DivisionSystem(map).TryForm(new PlayerData(0, 9999), out _),
            "Formed a division below the 10,000 cost");
        Check(system.TryForm(player, out Division first), "Could not form the first division");
        Check(player.Manpower == 15000, "Forming did not charge 10,000 military population");
        Check(first.Number == 1 && first.OwnerId == 0 && first.Strength == Division.Cost,
            "First division was numbered or equipped wrongly");
        Check(map.IsOwnedBy(first.X, first.Y, 0), "Division mustered off friendly ground");
        Check(system.TryForm(player, out Division second) && second.Number == 2,
            "Second division did not take the next number");
        Check(player.Manpower == 5000 && system.CountFor(0) == 2, "Roster or cost accounting wrong");
        Check(!system.TryForm(player, out _) && player.Manpower == 5000,
            "Formed a third division without the population for it");

        // A separate faction keeps its own numbering.
        for (int y = 12; y <= 14; y++) map.TryClaimCell(30, y, 1);
        var rival = new PlayerData(1, 10000);
        Check(system.TryForm(rival, out Division rivalFirst) && rivalFirst.Number == 1,
            "Rival numbering is not independent");
        Check(system.CountFor(0) == 2 && system.CountFor(1) == 1, "Roster is not per faction");

        // Selection by proximity, so a click need not land exactly on a counter.
        Check(system.FindAt(first.X, first.Y, 0) == first, "Exact lookup failed");
        Check(system.FindNear(first.X + 2, first.Y + 1, 0, 4) != null, "Nearby lookup failed");
        Check(system.FindNear(first.X, first.Y, 1, 4) != rivalFirst ||
            system.FindNear(first.X, first.Y, 1, 2) == null, "Lookup crossed factions");

        // Ordering a division into wilderness claims ground as it marches.
        int before = map.CountTerritory(0);
        Check(system.TryOrder(first, 20, 14), "Could not order the division into wilderness");
        Check(first.TargetX == 20 && first.TargetY == 14 && first.HasOrders, "Order was not recorded");
        Drain(system, captured, defenders);
        Check(first.X == 20 && first.Y == 14, "Division did not reach its destination");
        Check(!first.HasOrders, "Division kept its orders after arriving");
        Check(map.IsOwnedBy(20, 14, 0), "Destination was not occupied");
        Check(map.CountTerritory(0) > before + 20, "March claimed almost no wilderness");
        Check(first.Strength == Division.Cost, "Wilderness marching cost strength");

        // The frontage is what makes the advance visible rather than a thread.
        Check(map.IsOwnedBy(20, 16, 0) && map.IsOwnedBy(20, 12, 0),
            "Division did not claim its frontage either side");
        Check(!map.IsOwnedBy(20, 8, 0), "Frontage reached far beyond the division");

        // Water blocks a division exactly as it blocks everything else.
        var lake = Plains(30, 20);
        for (int y = 0; y < 20; y++) lake[15, y] = TerrainType.Water;
        var lakeMap = new MapData(lake);
        lakeMap.TryClaimCell(3, 10, 0);
        var lakeSystem = new DivisionSystem(lakeMap);
        var lakePlayer = new PlayerData(0, Division.Cost);
        Check(lakeSystem.TryForm(lakePlayer, out Division blocked), "Could not form a division by the lake");
        Check(!lakeSystem.TryOrder(blocked, 15, 10), "Ordered a division onto water");
        Check(!lakeSystem.TryOrder(blocked, 25, 10), "Ordered a division across an unbroken lake");
        Check(!blocked.HasOrders, "Rejected order left a route behind");

        // Enemy ground has to be fought for, and it is paid from the division.
        var battle = new MapData(Plains(30, 20));
        for (int y = 8; y <= 12; y++) battle.TryClaimCell(4, y, 0);
        for (int y = 0; y < 20; y++) battle.TryClaimCell(18, y, 1);
        var battleSystem = new DivisionSystem(battle);
        var attacker = new PlayerData(0, Division.Cost);
        Check(battleSystem.TryForm(attacker, out Division assault), "Could not form the assault division");
        Check(battleSystem.TryOrder(assault, 22, 10), "Could not order an attack through enemy land");
        Drain(battleSystem, captured, defenders);
        Check(assault.Strength < Division.Cost, "Fighting an enemy line cost the division nothing");
        Check(battle.IsOwnedBy(18, 10, 0), "Division failed to break the enemy line");
        Check(defenders.Count >= 0, "Defender reporting broke");

        // A division too weak to pay for the next attack holds instead of dying.
        var stand = new MapData(Plains(20, 12));
        for (int y = 4; y <= 6; y++) stand.TryClaimCell(3, y, 0);
        for (int y = 0; y < 12; y++) stand.TryClaimCell(10, y, 1);
        var standSystem = new DivisionSystem(stand);
        var weakOwner = new PlayerData(0, Division.Cost);
        Check(standSystem.TryForm(weakOwner, out Division weak), "Could not form the weak division");
        while (weak.Strength > 10) weak.TrySpendManpower(Math.Min(100, weak.Strength - 5));
        int held = weak.Strength;
        Check(standSystem.TryOrder(weak, 15, 5), "Could not order the weak division");
        Drain(standSystem, captured, defenders);
        Check(!weak.IsDestroyed && weak.Strength == held, "Weak division spent itself against a line");
        Check(stand.IsOwnedBy(10, 5, 1), "Weak division took ground it could not pay for");

        // Box selection returns exactly the faction's divisions inside the
        // rectangle, and accepts a rectangle dragged in any direction.
        var boxMap = new MapData(Plains(60, 40));
        for (int x = 2; x <= 50; x++)
        for (int y = 2; y <= 34; y++) boxMap.TryClaimCell(x, y, 0);
        for (int y = 2; y <= 10; y++) boxMap.TryClaimCell(55, y, 1);
        var boxSystem = new DivisionSystem(boxMap);
        var boxOwner = new PlayerData(0, Division.Cost * 4);
        var raisedDivisions = new List<Division>();
        for (int i = 0; i < 4; i++)
        {
            Check(boxSystem.TryForm(boxOwner, out Division formed), "Could not form division " + i);
            raisedDivisions.Add(formed);
        }
        var boxRival = new PlayerData(1, Division.Cost);
        Check(boxSystem.TryForm(boxRival, out Division enemyDivision), "Could not form the rival division");
        var picked = new List<Division>();
        boxSystem.FindInBox(0, 0, 59, 39, 0, picked);
        Check(picked.Count == 4, "Full-map box did not select all four divisions");
        Check(!picked.Contains(enemyDivision), "Box selection crossed factions");
        boxSystem.FindInBox(59, 39, 0, 0, 0, picked);
        Check(picked.Count == 4, "Box dragged bottom-right to top-left was not normalised");
        int soloX = raisedDivisions[0].X, soloY = raisedDivisions[0].Y;
        boxSystem.FindInBox(soloX - 1, soloY - 1, soloX + 1, soloY + 1, 0, picked);
        Check(picked.Count == 1 && picked[0] == raisedDivisions[0],
            "Tight box did not select exactly the division inside it");
        boxSystem.FindInBox(0, 0, 0, 0, 0, picked);
        Check(picked.Count == 0, "Empty box selected something");

        // A group order spreads its destinations instead of stacking every
        // division on the single clicked cell.
        boxSystem.FindInBox(0, 0, 59, 39, 0, picked);
        Check(boxSystem.OrderGroup(picked, 40, 20) == 4, "Group order did not reach every division");
        var seen = new HashSet<int>();
        foreach (Division division in picked)
        {
            Check(division.HasOrders, "Group member received no route");
            Check(seen.Add(division.TargetY * boxMap.Width + division.TargetX),
                "Two divisions were sent to the same cell");
            int dx = division.TargetX - 40, dy = division.TargetY - 20;
            Check(dx * dx + dy * dy <= 200, "Group destination strayed far from the click");
        }
        Check(boxSystem.OrderGroup(new List<Division>(), 40, 20) == 0, "Empty group order reported work");
        Check(boxSystem.OrderGroup(null, 40, 20) == 0, "Null group order reported work");

        // ---- stances -------------------------------------------------------
        var stanceTerrain = Plains(40, 20);
        var stanceMap = new MapData(stanceTerrain);
        for (int x = 2; x <= 8; x++)
        for (int y = 6; y <= 12; y++) stanceMap.TryClaimCell(x, y, 0);
        for (int y = 0; y < 20; y++) stanceMap.TryClaimCell(20, y, 1);
        var stanceOwner = new PlayerData(0, Division.Cost * 3);
        var stanceSystem = new DivisionSystem(stanceMap, new[] { stanceOwner });
        Check(stanceSystem.TryForm(stanceOwner, out Division unit), "Could not form a stance test division");
        Check(unit.Stance == DivisionStance.Attack, "A new division should start out attacking");

        // Defend holds position and digs the ground in.
        Check(stanceSystem.TryOrder(unit, 15, 9), "Could not order the division");
        stanceSystem.SetStance(unit, DivisionStance.Defend);
        Check(!unit.HasOrders, "Digging in did not drop the move order");
        int dugX = unit.X, dugY = unit.Y;
        int plainDefense = TerrainRules.Defense(TerrainType.Land);
        for (int i = 0; i < 5; i++) stanceSystem.Step(captured, defenders);
        Check(unit.X == dugX && unit.Y == dugY, "A defending division moved");
        Check(stanceMap.GetCell(dugX, dugY).Defense == plainDefense + Division.EntrenchBonus,
            "Defending did not entrench the ground");

        // Reserve rebuilds strength out of the national military population.
        while (unit.Strength > Division.Cost - 1200) unit.TrySpendManpower(100);
        int hurt = unit.Strength;
        int poolBefore = stanceOwner.Manpower;
        stanceSystem.SetStance(unit, DivisionStance.Reserve);
        stanceSystem.Step(captured, defenders);
        Check(unit.Strength == hurt + Division.ReserveRefit, "Reserve did not absorb replacements");
        Check(stanceOwner.Manpower == poolBefore - Division.ReserveRefit,
            "Replacements were free instead of drawn from the pool");
        for (int i = 0; i < 20; i++) stanceSystem.Step(captured, defenders);
        Check(unit.Strength == Division.Cost, "Reserve refit did not stop at full strength");
        int settled = stanceOwner.Manpower;
        stanceSystem.Step(captured, defenders);
        Check(stanceOwner.Manpower == settled, "A full reserve division kept drawing replacements");

        // An empty national pool simply cannot supply replacements.
        var brokeOwner = new PlayerData(0, Division.Cost);
        var brokeMap = new MapData(Plains(20, 20));
        for (int x = 2; x <= 6; x++)
        for (int y = 2; y <= 6; y++) brokeMap.TryClaimCell(x, y, 0);
        var brokeSystem = new DivisionSystem(brokeMap, new[] { brokeOwner });
        brokeSystem.TryForm(brokeOwner, out Division brokeUnit);
        brokeUnit.TrySpendManpower(2000);
        int brokeStrength = brokeUnit.Strength;
        brokeSystem.SetStance(brokeUnit, DivisionStance.Reserve);
        brokeSystem.Step(captured, defenders);
        Check(brokeUnit.Strength == brokeStrength && brokeOwner.Manpower == 0,
            "Refit drew replacements a bankrupt nation did not have");

        // Redeploy moves faster than an attack and refuses contested ground.
        Check(stanceSystem.TryForm(stanceOwner, out Division mover), "Could not form the redeploy division");
        stanceSystem.SetStance(mover, DivisionStance.Redeploy);
        Check(stanceSystem.TryOrder(mover, 18, mover.Y), "Could not order a redeployment");
        Check(mover.Stance == DivisionStance.Redeploy, "Ordering a redeploy silently changed the stance");
        int fromX = mover.X;
        stanceSystem.Step(captured, defenders);
        Check(mover.X - fromX == Division.RedeploySpeed, "Redeploy did not cover extra ground");
        for (int i = 0; i < 40; i++) stanceSystem.Step(captured, defenders);
        Check(mover.Strength == Division.Cost, "Redeploying cost the division strength");
        Check(!stanceMap.IsOwnedBy(20, mover.Y, 0), "A redeploying division attacked the enemy line");
        Check(mover.X < 20, "A redeploying division walked into contested ground");

        // Ordering a dug-in division somewhere puts it back on the attack.
        stanceSystem.SetStance(unit, DivisionStance.Defend);
        Check(stanceSystem.TryOrder(unit, 12, 9), "Could not re-order a dug-in division");
        Check(unit.Stance == DivisionStance.Attack && unit.HasOrders,
            "A dug-in division given a destination did not take up the advance");
        stanceSystem.SetStance(new List<Division> { unit, mover }, DivisionStance.Defend);
        Check(unit.Stance == DivisionStance.Defend && mover.Stance == DivisionStance.Defend,
            "Group stance change missed a division");
        stanceSystem.SetStance((IReadOnlyList<Division>)null, DivisionStance.Attack);
        stanceSystem.SetStance((Division)null, DivisionStance.Attack);

        // ---- holding a stretch of front ------------------------------------
        // The same division spread over a wider line defends it more thinly.
        var lineMap = new MapData(Plains(40, 30));
        for (int x = 2; x <= 20; x++)
        for (int y = 2; y <= 26; y++) lineMap.TryClaimCell(x, y, 0);
        var lineOwner = new PlayerData(0, Division.Cost * 3);
        var lineSystem = new DivisionSystem(lineMap, new[] { lineOwner });
        lineSystem.TryForm(lineOwner, out Division holder);
        Check(holder.HeldLine.Count == 0 && holder.LineDefense == Division.EntrenchBonus,
            "An unassigned division should dig in at full strength");

        var narrow = new List<int>();
        for (int y = 10; y < 10 + Division.StandardFrontage; y++) narrow.Add(y * lineMap.Width + 20);
        Check(lineSystem.AssignFrontage(holder, narrow) == Division.StandardFrontage,
            "Standard frontage was not accepted whole");
        Check(holder.Stance == DivisionStance.Defend, "Taking a line did not put the division on the defensive");
        Check(holder.LineDefense == Division.EntrenchBonus, "Standard frontage should give the full bonus");
        Check(holder.HasOrders && narrow.Contains(holder.TargetY * lineMap.Width + holder.TargetX),
            "A distant defensive line did not create a route to its nearest assigned cell");
        int deploymentX = holder.X, deploymentY = holder.Y;
        int deploymentStrength = holder.Strength;
        for (int step = 0; step < 40 && holder.HasOrders; step++)
            lineSystem.Step(captured, defenders);
        Check(holder.X != deploymentX || holder.Y != deploymentY,
            "A defending division never moved toward its assigned line");
        Check(narrow.Contains(holder.Cell), "A defending division did not arrive on its assigned line");
        Check(holder.Stance == DivisionStance.Defend && holder.Strength == deploymentStrength,
            "Defensive redeployment changed stance or spent combat strength");
        lineSystem.Step(captured, defenders);
        int plains = TerrainRules.Defense(TerrainType.Land);
        foreach (int id in narrow)
            Check(lineMap.GetCell(id % lineMap.Width, id / lineMap.Width).Defense ==
                plains + Division.EntrenchBonus, "A cell of the held line was not dug in");

        // Twice the width, half the defence per cell.
        var wide = new List<int>();
        for (int y = 6; y < 6 + Division.StandardFrontage * 2; y++) wide.Add(y * lineMap.Width + 19);
        Check(lineSystem.AssignFrontage(holder, wide) == Division.StandardFrontage * 2,
            "Wide frontage was not accepted whole");
        Check(holder.LineDefense == Division.EntrenchBonus / 2,
            "Doubling the frontage did not halve the defence per cell");
        Check(holder.HeldLine.Count == Division.StandardFrontage * 2, "Old line was not replaced");
        Check(lineSystem.AssignFrontage(holder, new List<int>()) == 0 &&
            holder.LineDefense == Division.EntrenchBonus && !holder.HasOrders,
            "Clearing the line did not restore the standard bonus or cancel relocation");

        // Ground that is not ours cannot be assigned.
        var foreign = new List<int> { 14 * lineMap.Width + 35, 14 * lineMap.Width + 20 };
        Check(lineSystem.AssignFrontage(holder, foreign) == 1,
            "Assignment accepted ground the faction does not hold");

        // A group splits the line between its members instead of stacking.
        lineSystem.TryForm(lineOwner, out Division partner);
        var shared = new List<int>();
        for (int y = 4; y < 16; y++) shared.Add(y * lineMap.Width + 20);
        var pair = new List<Division> { holder, partner };
        Check(lineSystem.AssignFrontage(pair, shared) == 12, "Group assignment lost cells");
        Check(holder.HeldLine.Count == 6 && partner.HeldLine.Count == 6,
            "A shared line was not split evenly");
        foreach (int id in holder.HeldLine)
        {
            bool shares = false;
            foreach (int other in partner.HeldLine) if (other == id) shares = true;
            Check(!shares, "Two divisions hold the same cell");
        }
        Check(lineSystem.AssignFrontage(pair, null) == 0 &&
            lineSystem.AssignFrontage((IReadOnlyList<Division>)null, shared) == 0,
            "Null group assignment reported work");

        // Ordering a holder somewhere gives up the line it was told to hold.
        lineSystem.AssignFrontage(holder, narrow);
        Check(holder.HeldLine.Count > 0, "Line assignment did not take");
        Check(lineSystem.TryOrder(holder, 30, 14), "Could not order the holder away");
        Check(holder.HeldLine.Count == 0 && holder.Stance == DivisionStance.Attack,
            "Marching off did not release the held line");

        // ---- formation combat and destruction -----------------------------
        // An enemy counter is a real combatant, not decoration. Both formations
        // take simultaneous strength losses; a destroyed holder is removed and
        // the fortifications that depended on it disappear with it.
        var clashMap = new MapData(Plains(18, 16));
        for (int x = 1; x <= 5; x++)
        for (int y = 1; y < 15; y++) clashMap.TryClaimCell(x, y, 0);
        for (int x = 6; x < 17; x++)
        for (int y = 1; y < 15; y++) clashMap.TryClaimCell(x, y, 1);
        var clashRed = new PlayerData(0, Division.Cost);
        var clashBlue = new PlayerData(1, Division.Cost);
        var clashSystem = new DivisionSystem(clashMap, new[] { clashRed, clashBlue });
        Check(clashSystem.TryPlace(clashRed, 5, 8, out Division redAttack),
            "Could not place attacking formation for unit combat");
        Check(clashSystem.TryPlace(clashBlue, 6, 8, out Division blueHold),
            "Could not place defending formation for unit combat");
        blueHold.TrySpendManpower(9200);
        var oneCellLine = new List<int> { 8 * clashMap.Width + 6 };
        clashSystem.AssignFrontage(blueHold, oneCellLine);
        clashSystem.Step(captured, defenders);
        Check(clashMap.GetCell(6, 8).Defense > plainDefense,
            "The defender did not establish works before combat");
        Check(clashSystem.TryOrder(redAttack, 8, 8), "Could not order the formation attack");
        int redBeforeCombat = redAttack.Strength;
        clashSystem.Step(captured, defenders);
        Check(redAttack.Strength < redBeforeCombat, "The defender did not return fire");
        Check(blueHold.IsDestroyed && clashSystem.CountFor(1) == 0,
            "A zero-strength enemy formation remained in the roster");
        Check(clashSystem.LastDestroyed.Count == 1 && clashSystem.LastDestroyed[0] == blueHold,
            "The destroyed formation was not reported for the combat step");
        Check(clashSystem.LastLossesFor(0) > 0 && clashSystem.LastLossesFor(1) == 800,
            "Formation casualties were not recorded by faction");
        Check(clashMap.GetCell(6, 8).Defense == plainDefense,
            "Destroyed defender left its entrenchment behind");

        // ---- functional transport -----------------------------------------
        // The surveyed endpoints lie on a straight east-west chord, but water
        // forces the built road through the only pass. Orders prefer that
        // longer road because travel on it is faster than a terrain shortcut.
        var roadScenario = new ScenarioMap("Road test", "", "", 18, 16,
            Division.Cost * 2, Division.Cost * 2);
        for (int x = 0; x < roadScenario.Width; x++)
        for (int y = 0; y < roadScenario.Height; y++)
        {
            roadScenario.SetTerrain(x, y, TerrainType.Land);
            roadScenario.SetOwner(x, y, 0);
        }
        for (int y = 0; y < roadScenario.Height; y++)
            if (y != 3) roadScenario.SetTerrain(8, y, TerrainType.Water);
        roadScenario.AddGridRoute("Pass road", InfrastructureRouteType.Road, 2, 8, 15, 8);
        MapData roadMap = roadScenario.CreateMapData();
        Check(roadMap.TransportNetwork != null && roadMap.TransportNetwork.Routes.Count == 1,
            "Scenario roads did not become a map transport network");
        MapTransportRoute builtRoad = roadMap.TransportNetwork.Routes[0];
        bool usedPass = false, enteredWater = false;
        foreach (int id in builtRoad.Cells)
        {
            int x = id % roadMap.Width, y = id / roadMap.Width;
            if (x == 8 && y == 3) usedPass = true;
            if (!roadMap.IsWalkable(x, y)) enteredWater = true;
        }
        Check(usedPass && !enteredWater && builtRoad.Cells.Count > 14,
            "The built road stayed a straight city-to-city line through blocked terrain");
        Check(roadMap.GetMovementCost(2, 8) < TerrainRules.MovementCost(TerrainType.Land),
            "Built road did not reduce movement cost");
        var roadOwner = new PlayerData(0, Division.Cost * 2);
        var roadSystem = new DivisionSystem(roadMap, new[] { roadOwner });
        Check(roadSystem.TryPlace(roadOwner, 2, 8, out Division roadUnit) &&
            roadSystem.TryOrder(roadUnit, 15, 8), "Could not order a unit along the road");
        bool orderUsesPass = false;
        foreach (int id in roadUnit.Route) if (id == 3 * roadMap.Width + 8) orderUsesPass = true;
        Check(orderUsesPass, "Attack routing ignored the longer, faster road corridor");
        int roadStart = roadUnit.Cell;
        roadSystem.Step(captured, defenders);
        int travelled = Math.Abs(roadUnit.X - roadStart % roadMap.Width) +
            Math.Abs(roadUnit.Y - roadStart / roadMap.Width);
        Check(travelled == 2, "A formation on a road did not receive its movement bonus");

        Check(roadSystem.TryPlace(roadOwner, 2, 8, out Division railMover),
            "Could not place the redeployment test formation");
        roadSystem.SetStance(railMover, DivisionStance.Redeploy);
        Check(roadSystem.TryOrder(railMover, 15, 8), "Could not order road redeployment");
        int redeployStart = railMover.Cell;
        roadSystem.Step(captured, defenders);
        int redeployed = Math.Abs(railMover.X - redeployStart % roadMap.Width) +
            Math.Abs(railMover.Y - redeployStart / roadMap.Width);
        Check(redeployed == Division.RedeploySpeed + 1,
            "Road redeployment did not outrun cross-country redeployment");

        // Destroyed divisions leave the roster on the next step.
        var gone = new MapData(Plains(12, 12));
        for (int y = 4; y <= 6; y++) gone.TryClaimCell(4, y, 0);
        var goneSystem = new DivisionSystem(gone);
        var goneOwner = new PlayerData(0, Division.Cost);
        goneSystem.TryForm(goneOwner, out Division doomed);
        Check(goneSystem.CountFor(0) == 1, "Roster did not record the division");
        doomed.TrySpendManpower(Division.Cost);
        Check(doomed.IsDestroyed, "Spending all strength did not destroy the division");
        goneSystem.Step(captured, defenders);
        Check(goneSystem.CountFor(0) == 0, "Destroyed division stayed on the roster");

        Console.WriteLine("PASS: " + checks +
            " division assertions (10,000-population cost, per-faction numbering, routing, frontage claiming, combat, and losses).");
    }
}
