using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Unit-symbol vocabulary for future counters and echelon marks:
// https://commons.wikimedia.org/wiki/NATO_Military_Map_Symbols#Friendly_Force_Unit_Symbols
// Draws orders in the idiom of a real operations map: a tapering
// axis-of-advance arrow with a solid head for each friendly attack, and a rigid
// crenellated defensive trace for each observed held front. Everything
// lives in map-local space so it pans and zooms with the terrain, and it is
// rebuilt only when the plan itself changes.
public sealed class MapOrderRenderer : MonoBehaviour
{
    private const float ToothWidth = 0.115f;
    private static readonly Color FriendlyBlue = new Color32(52, 174, 235, 255);
    private static readonly Color AlliedGreen = new Color32(74, 201, 112, 255);
    private static readonly Color EnemyRed = new Color32(239, 68, 68, 255);

    private readonly List<GameObject> drawn = new List<GameObject>();
    private readonly StringBuilder signature = new StringBuilder();
    private readonly List<Vector3> scratch = new List<Vector3>();
    private Material lineMaterial;
    private string lastPlan = "";

    public int DrawnCount => drawn.Count;

    public void Draw(IReadOnlyList<Division> divisions, int ownerId, MapData map, MapRenderer mapRenderer)
    {
        Draw(divisions, ownerId, map, mapRenderer, null, null);
    }

    public void Draw(IReadOnlyList<Division> divisions, int ownerId, MapData map,
        MapRenderer mapRenderer, FogOfWar fog, IReadOnlyCollection<int> alliedOwners)
    {
        if (map == null || mapRenderer == null) return;
        string plan = Describe(divisions, ownerId, fog, alliedOwners);
        if (plan == lastPlan) return;
        lastPlan = plan;
        Clear();
        if (lineMaterial == null)
        {
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            lineMaterial.name = "Order overlay";
        }
        foreach (Division division in divisions)
        {
            bool friendly = division.OwnerId == ownerId;
            bool visible = friendly || fog == null || fog.CanSee(division);
            if (!visible) continue;
            if (division.HeldLine.Count > 0)
            {
                map.TryGetTerritoryCenter(division.OwnerId, out float homeX, out float homeY);
                bool allied = IsAllied(division.OwnerId, alliedOwners);
                DrawDefenceTrace(division, map, mapRenderer, homeX, homeY,
                    DefenceColour(friendly, allied), friendly, allied);
            }
            // Rival intentions remain hidden; only observed held ground is
            // shown. A defender's internal route to its line is not an attack.
            if (friendly && division.Route.Count > 0 && division.Stance != DivisionStance.Defend)
                DrawAxisOfAdvance(division, map, mapRenderer);
        }
    }

    // Only the things a plan is made of, so panning or fighting does not
    // needlessly rebuild the overlay.
    private string Describe(IReadOnlyList<Division> divisions, int ownerId,
        FogOfWar fog, IReadOnlyCollection<int> alliedOwners)
    {
        signature.Clear();
        foreach (Division division in divisions)
        {
            bool friendly = division.OwnerId == ownerId;
            if (!friendly && fog != null && !fog.CanSee(division)) continue;
            signature.Append(division.OwnerId).Append('/').Append(division.Number).Append(':')
                .Append(division.X).Append(',')
                .Append(division.Y).Append('>').Append(division.TargetX).Append(',')
                .Append(division.TargetY).Append('#').Append(division.Route.Count)
                .Append('|').Append(division.HeldLine.Count).Append('*')
                .Append(division.LineDefense).Append('@')
                .Append(IsAllied(division.OwnerId, alliedOwners) ? 'A' : friendly ? 'F' : 'E');
            foreach (int held in division.HeldLine) signature.Append(',').Append(held);
            signature.Append(';');
        }
        return signature.ToString();
    }

    // An axis of advance: a shaft that broadens toward the objective, finished
    // with a solid arrowhead rather than an open V.
    private void DrawAxisOfAdvance(Division division, MapData map, MapRenderer mapRenderer)
    {
        Color colour = FriendlyBlue;
        colour.a = 0.92f;
        scratch.Clear();
        scratch.Add(Local(mapRenderer, map, division.X + 0.5f, division.Y + 0.5f));
        IReadOnlyList<int> route = division.Route;
        int stride = Mathf.Max(1, route.Count / 24);
        for (int i = 0; i < route.Count; i += stride)
            scratch.Add(Local(mapRenderer, map, route[i] % map.Width + 0.5f, route[i] / map.Width + 0.5f));
        Vector3 objective = Local(mapRenderer, map, division.TargetX + 0.5f, division.TargetY + 0.5f);
        if (scratch[scratch.Count - 1] != objective) scratch.Add(objective);
        if (scratch.Count < 2) return;

        List<Vector3> smooth = Smooth(scratch, 3);

        // Trim along the curve itself. Moving only the final vertex backwards
        // would make a short smoothed segment double back and create a hook.
        const float headLength = 0.22f;
        Vector3 headBase = TrimEnd(smooth, objective, headLength, out Vector3 heading);
        if (heading.sqrMagnitude < 1e-6f) return;

        LineRenderer shaft = CreateLine("Attack shaft " + division.Number, smooth, 0.02f, colour, 7);
        shaft.numCornerVertices = 6;
        // Tapered: narrow where the formation is now, full width at the front.
        shaft.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 1f));
        shaft.widthMultiplier = 0.02f;

        // A two-point line whose far width is zero renders as a solid triangle.
        scratch.Clear();
        scratch.Add(headBase);
        scratch.Add(objective);
        LineRenderer head = CreateLine("Attack head " + division.Number, scratch, 0.02f, colour, 8);
        head.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        head.widthMultiplier = 0.075f;
        head.numCapVertices = 0;
    }

    // Repeated corner cutting keeps the exact start and objective but turns a
    // grid path into a continuous staff-work axis. Orders remain cell-accurate;
    // only their briefing overlay is smoothed.
    private static List<Vector3> Smooth(IReadOnlyList<Vector3> source, int passes)
    {
        var points = new List<Vector3>(source);
        for (int pass = 0; pass < passes && points.Count > 2; pass++)
        {
            var next = new List<Vector3>(points.Count * 2);
            next.Add(points[0]);
            for (int i = 0; i < points.Count - 1; i++)
            {
                next.Add(Vector3.Lerp(points[i], points[i + 1], 0.25f));
                next.Add(Vector3.Lerp(points[i], points[i + 1], 0.75f));
            }
            next.Add(points[points.Count - 1]);
            points = next;
        }
        return points;
    }

    private static Vector3 TrimEnd(List<Vector3> points, Vector3 objective,
        float distance, out Vector3 heading)
    {
        heading = Vector3.zero;
        float remaining = distance;
        Vector3 current = objective;
        for (int i = points.Count - 2; i >= 0; i--)
        {
            Vector3 span = current - points[i];
            float length = span.magnitude;
            if (length < 1e-6f) continue;
            Vector3 direction = span / length;
            if (length >= remaining)
            {
                Vector3 cut = current - direction * remaining;
                points.RemoveRange(i + 1, points.Count - i - 1);
                if ((points[points.Count - 1] - cut).sqrMagnitude > 1e-10f) points.Add(cut);
                heading = direction;
                return cut;
            }
            remaining -= length;
            current = points[i];
        }
        heading = (objective - points[0]).normalized;
        points.RemoveRange(1, points.Count - 1);
        return points[0];
    }

    // A forward defensive line made from hard rectangular teeth, matching the
    // rigid field-boundary style used in the supplied operations-map reference.
    private void DrawDefenceTrace(Division division, MapData map, MapRenderer mapRenderer,
        float homeX, float homeY, Color colour, bool friendly, bool allied)
    {
        float strength = Mathf.Clamp01(division.LineDefense / (float)Division.EntrenchBonus);
        float width = Mathf.Lerp(0.022f, 0.040f, strength);
        float depth = Mathf.Lerp(0.050f, 0.090f, strength);

        var spine = new List<Vector3>();
        float sumX = 0f, sumY = 0f;
        foreach (int id in division.HeldLine)
        {
            float cx = id % map.Width + 0.5f, cy = id / map.Width + 0.5f;
            spine.Add(Local(mapRenderer, map, cx, cy));
            sumX += cx; sumY += cy;
        }
        if (spine.Count == 0) return;
        float middleX = sumX / division.HeldLine.Count, middleY = sumY / division.HeldLine.Count;
        var outward = new Vector2(middleX - homeX, middleY - homeY);
        if (outward.sqrMagnitude < 1e-4f) outward = Vector2.right;
        outward.Normalize();
        if (spine.Count == 1)
        {
            // A single held cell still needs enough spine for one square tooth.
            Vector3 only = spine[0];
            var sideways = new Vector3(-outward.y, outward.x, 0f) * (ToothWidth * 0.5f);
            spine.Clear();
            spine.Add(only - sideways);
            spine.Add(only + sideways);
        }
        string relation = friendly ? "Defence line " : allied ? "Allied defence line " : "Enemy defence line ";
        LineRenderer trace = CreateLine(relation + division.Number,
            Crenellate(spine, new Vector3(outward.x, outward.y, 0f), depth), width, colour, 9);
        trace.numCornerVertices = 0;
        trace.numCapVertices = 0;
    }

    // Replaces every straight run with a square wave: short base, perpendicular
    // rise, flat outward face, perpendicular return. No curves or soft lobes.
    private static List<Vector3> Crenellate(List<Vector3> spine, Vector3 outward, float depth)
    {
        var shaped = new List<Vector3>();
        for (int i = 0; i < spine.Count - 1; i++)
        {
            Vector3 from = spine[i], to = spine[i + 1];
            Vector3 span = to - from;
            float length = span.magnitude;
            if (length < 1e-5f) continue;
            Vector3 along = span / length;
            // Build every tooth perpendicular to its own local segment. The
            // territory-wide outward vector only chooses which side it faces;
            // using it as the rise itself would skew corners on a bent line.
            var normal = new Vector3(-along.y, along.x, 0f);
            if (Vector3.Dot(normal, outward) < 0f) normal = -normal;
            int teeth = Mathf.Max(1, Mathf.RoundToInt(length / ToothWidth));
            float toothLength = length / teeth;
            for (int tooth = 0; tooth < teeth; tooth++)
            {
                Vector3 start = from + along * (toothLength * tooth);
                AddDistinct(shaped, start);
                AddDistinct(shaped, start + along * (toothLength * 0.22f));
                AddDistinct(shaped, start + along * (toothLength * 0.22f) + normal * depth);
                AddDistinct(shaped, start + along * (toothLength * 0.78f) + normal * depth);
                AddDistinct(shaped, start + along * (toothLength * 0.78f));
                AddDistinct(shaped, start + along * toothLength);
            }
        }
        if (shaped.Count < 2) shaped.AddRange(spine);
        return shaped;
    }

    private static void AddDistinct(List<Vector3> points, Vector3 point)
    {
        if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude > 1e-10f)
            points.Add(point);
    }

    private static bool IsAllied(int ownerId, IReadOnlyCollection<int> alliedOwners)
    {
        if (alliedOwners == null) return false;
        foreach (int ally in alliedOwners) if (ally == ownerId) return true;
        return false;
    }

    public static Color DefenceColour(bool friendly, bool allied) =>
        friendly ? FriendlyBlue : allied ? AlliedGreen : EnemyRed;

    private Vector3 Local(MapRenderer mapRenderer, MapData map, float x, float y)
    {
        Vector3 local = mapRenderer.CellToLocal(map, x, y);
        local.z = -0.02f;
        return local;
    }

    private LineRenderer CreateLine(string objectName, List<Vector3> points, float width,
        Color colour, int sorting)
    {
        var child = new GameObject(objectName);
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        var line = child.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = colour;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        line.sortingOrder = sorting;
        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);
        drawn.Add(child);
        return line;
    }

    private void Clear()
    {
        foreach (GameObject item in drawn)
        {
            if (item == null) continue;
            // Destroy is deferred until frame end. Hide the retired plan now so
            // it cannot flash for a frame or be mistaken for its replacement.
            item.SetActive(false);
            Destroy(item);
        }
        drawn.Clear();
    }

    // Forces the next Draw to rebuild, for when the map itself is replaced.
    public void Invalidate()
    {
        lastPlan = "";
        Clear();
    }

    private void OnDestroy()
    {
        Clear();
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
