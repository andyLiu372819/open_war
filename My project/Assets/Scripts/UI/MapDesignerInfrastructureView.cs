using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Functional-road and logistics preview for the map designer. Kept separate
// from page construction so transport rendering can evolve independently.
public sealed partial class GameSetupUI
{
    private void RefreshInfrastructureOverlay()
    {
        foreach (GameObject oldObject in infrastructureObjects)
        {
            if (oldObject == null) continue;
            oldObject.SetActive(false);
            Destroy(oldObject);
        }
        infrastructureObjects.Clear();
        if (design == null || infrastructureOverlay == null || design.InfrastructureRoutes.Count == 0) return;

        const float previewWidth = 1134f, previewHeight = 674f;
        MapData previewMap = design.CreateMapData();
        foreach (MapTransportRoute route in previewMap.TransportNetwork.Routes)
        {
            List<Vector2> path = BuildPreviewRoute(route.Cells, previewMap, previewWidth, previewHeight);
            for (int i = 1; i < path.Count; i++)
            {
                Vector2 a = path[i - 1];
                Vector2 b = path[i];
                if (route.Type == InfrastructureRouteType.Railway)
                {
                    CreatePreviewLine("Rail bed " + route.Name, a, b, 6f,
                        new Color32(28, 32, 34, 230));
                    CreatePreviewLine("Rail track " + route.Name, a, b, 2f,
                        new Color32(225, 225, 211, 245));
                }
                else
                {
                    CreatePreviewLine("Road " + route.Name, a, b, 4f,
                        new Color32(224, 169, 91, 225));
                }
            }
        }

        foreach (ScenarioInfrastructureSite site in design.InfrastructureSites)
        {
            Vector2 point = PreviewPoint(site.X, site.Y, previewWidth, previewHeight);
            const float size = 16f;
            var markerObject = new GameObject("Infrastructure Site " + site.Name,
                typeof(RectTransform), typeof(Image));
            markerObject.transform.SetParent(infrastructureOverlay, false);
            Place((RectTransform)markerObject.transform, point.x - size * 0.5f,
                point.y - size * 0.5f, size, size);
            Image marker = markerObject.GetComponent<Image>();
            marker.raycastTarget = false;
            marker.color = InfrastructureColor(site.Type);
            infrastructureObjects.Add(markerObject);
            TMP_Text code = CreateText(markerObject.transform, "Infrastructure code",
                InfrastructureCode(site.Type), 0f, 0f, size, size, 9,
                new Color32(7, 18, 23, 255), TextAlignmentOptions.Center, FontStyles.Bold);
            infrastructureObjects.Add(code.gameObject);

            if (site.Type == InfrastructureSiteType.Airfield)
            {
                string airfieldName = site.Name.Replace(" Airfield", "");
                TMP_Text label = CreateText(infrastructureOverlay, "Infrastructure Label " + site.Name,
                    airfieldName, point.x + 11f, point.y + 7f, 135f, 18f, 10,
                    InfrastructureColor(site.Type), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                label.outlineWidth = 0.15f;
                label.outlineColor = new Color32(5, 12, 16, 255);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                infrastructureObjects.Add(label.gameObject);
            }
        }

        GameObject legend = CreatePanel(infrastructureOverlay, "Infrastructure legend",
            8f, 8f, 730f, 25f, new Color(0.025f, 0.055f, 0.07f, 0.82f));
        legend.GetComponent<Image>().raycastTarget = false;
        infrastructureObjects.Add(legend);
        TMP_Text legendText = CreateText(legend.transform, "Infrastructure legend text",
            "TAN: ROAD   DOUBLE: RAIL   A AIRFIELD   H RAIL HUB   D DEPOT   B BRIDGE   P PORT   I INDUSTRY",
            8f, 2f, 714f, 21f, 10, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        infrastructureObjects.Add(legendText.gameObject);
    }

    private Vector2 PreviewPoint(float x, float y, float width, float height) => new Vector2(
        x / (float)(design.Width - 1) * width,
        y / (float)(design.Height - 1) * height);

    private List<Vector2> BuildPreviewRoute(IReadOnlyList<int> cells, MapData map,
        float width, float height)
    {
        var points = new List<Vector2>();
        int stride = Mathf.Max(1, cells.Count / 28);
        for (int i = 0; i < cells.Count; i += stride)
        {
            int id = cells[i];
            points.Add(PreviewPoint(id % map.Width + 0.5f, id / map.Width + 0.5f, width, height));
        }
        if (cells.Count > 0)
        {
            int last = cells[cells.Count - 1];
            Vector2 end = PreviewPoint(last % map.Width + 0.5f, last / map.Width + 0.5f, width, height);
            if (points.Count == 0 || points[points.Count - 1] != end) points.Add(end);
        }
        for (int pass = 0; pass < 2 && points.Count > 2; pass++)
        {
            var smooth = new List<Vector2>(points.Count * 2);
            smooth.Add(points[0]);
            for (int i = 0; i < points.Count - 1; i++)
            {
                smooth.Add(Vector2.Lerp(points[i], points[i + 1], 0.25f));
                smooth.Add(Vector2.Lerp(points[i], points[i + 1], 0.75f));
            }
            smooth.Add(points[points.Count - 1]);
            points = smooth;
        }
        return points;
    }

    private void CreatePreviewLine(string objectName, Vector2 start, Vector2 end,
        float thickness, Color color)
    {
        var lineObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        lineObject.transform.SetParent(infrastructureOverlay, false);
        RectTransform rect = (RectTransform)lineObject.transform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = start;
        Vector2 delta = end - start;
        rect.sizeDelta = new Vector2(delta.magnitude, thickness);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        Image image = lineObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = color;
        infrastructureObjects.Add(lineObject);
    }

    private static string InfrastructureCode(InfrastructureSiteType type) => type switch
    {
        InfrastructureSiteType.Airfield => "A",
        InfrastructureSiteType.RailHub => "H",
        InfrastructureSiteType.SupplyDepot => "D",
        InfrastructureSiteType.Bridge => "B",
        InfrastructureSiteType.Port => "P",
        _ => "I"
    };

    private static Color InfrastructureColor(InfrastructureSiteType type) => type switch
    {
        InfrastructureSiteType.Airfield => new Color32(157, 220, 239, 245),
        InfrastructureSiteType.RailHub => new Color32(231, 231, 218, 245),
        InfrastructureSiteType.SupplyDepot => new Color32(237, 166, 83, 245),
        InfrastructureSiteType.Bridge => new Color32(137, 199, 224, 245),
        InfrastructureSiteType.Port => new Color32(105, 178, 229, 245),
        _ => new Color32(255, 210, 91, 245)
    };
}
