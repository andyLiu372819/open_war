using System.Collections.Generic;
using UnityEngine;

// Draws scenario transport corridors in map-local space so they stay anchored
// during panning and zooming. Point facilities are rendered by MapHUD, where
// their names can remain legible at different camera scales.
public sealed class MapInfrastructureRenderer : MonoBehaviour
{
    private readonly List<GameObject> routeObjects = new List<GameObject>();
    private Material lineMaterial;

    public void Draw(ScenarioMap scenario, MapData map, MapRenderer mapRenderer)
    {
        Clear();
        if (scenario == null) return;
        if (lineMaterial == null)
        {
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            lineMaterial.name = "Infrastructure lines";
        }

        MapTransportNetwork network = map.TransportNetwork ?? MapTransportNetwork.Build(scenario, map);
        foreach (MapTransportRoute route in network.Routes)
        {
            if (route.Type == InfrastructureRouteType.Railway)
            {
                CreateLine(route, map, mapRenderer, "Rail bed ", 0.075f,
                    new Color32(28, 32, 34, 235), 2);
                CreateLine(route, map, mapRenderer, "Rail track ", 0.024f,
                    new Color32(220, 221, 208, 245), 3);
            }
            else
            {
                CreateLine(route, map, mapRenderer, "Road ", 0.052f,
                    new Color32(222, 166, 91, 225), 2);
            }
        }
    }

    private void CreateLine(MapTransportRoute route, MapData map,
        MapRenderer mapRenderer, string prefix, float width, Color color, int sortingOrder)
    {
        var child = new GameObject(prefix + route.Name);
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        var line = child.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.numCornerVertices = 5;
        line.numCapVertices = 2;
        line.sortingOrder = sortingOrder;
        List<Vector3> points = SmoothRoute(route.Cells, map, mapRenderer);
        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            line.SetPosition(i, points[i]);
        }
        routeObjects.Add(child);
    }

    // The network itself is cell-accurate and orthogonal. Chaikin subdivision
    // rounds only the display polyline, keeping the road terrain-following
    // without rendering it as a staircase.
    private static List<Vector3> SmoothRoute(IReadOnlyList<int> cells, MapData map,
        MapRenderer mapRenderer)
    {
        var points = new List<Vector3>();
        foreach (int id in cells)
        {
            Vector3 point = mapRenderer.CellToLocal(map,
                id % map.Width + 0.5f, id / map.Width + 0.5f);
            point.z = -0.015f;
            points.Add(point);
        }
        for (int pass = 0; pass < 2 && points.Count > 2; pass++)
        {
            var smooth = new List<Vector3>(points.Count * 2);
            smooth.Add(points[0]);
            for (int i = 0; i < points.Count - 1; i++)
            {
                smooth.Add(Vector3.Lerp(points[i], points[i + 1], 0.25f));
                smooth.Add(Vector3.Lerp(points[i], points[i + 1], 0.75f));
            }
            smooth.Add(points[points.Count - 1]);
            points = smooth;
        }
        return points;
    }

    private void Clear()
    {
        foreach (GameObject routeObject in routeObjects)
            if (routeObject != null) Destroy(routeObject);
        routeObjects.Clear();
    }

    private void OnDestroy()
    {
        Clear();
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
