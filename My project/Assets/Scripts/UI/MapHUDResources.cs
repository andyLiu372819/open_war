using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Resource-bar construction, procedural economy icons, and recruitment refresh.
// Kept as part of MapHUD through a partial so scene serialization is unchanged.
public partial class MapHUD
{
    private void CreateResourceBar(Transform parent)
    {
        var panel = new GameObject("Resource bar", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.05f, 0.09f, 0.12f, 0.85f);
        background.raycastTarget = false;
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -82f);
        rect.sizeDelta = new Vector2(-40f, 64f);
        string[] names = { "Military", "Civilians", "Gold", "Industry", "Land" };
        Color32[] colors = { new Color32(244, 244, 235, 255), new Color32(226, 198, 236, 255),
            new Color32(255, 213, 111, 255), new Color32(152, 218, 238, 255),
            new Color32(174, 225, 163, 255) };
        for (int i = 0; i < resourceTexts.Length; i++)
        {
            bool hasIcon = i == 2 || i == 3;
            if (hasIcon)
            {
                string iconName = i == 2 ? "Gold coin icon" : "Industry wrench icon";
                var iconObject = new GameObject(iconName, typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(panel.transform, false);
                Image icon = iconObject.GetComponent<Image>();
                icon.sprite = CreateResourceIcon(i == 2);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(i / 5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(31f, 0f);
                iconRect.sizeDelta = new Vector2(42f, 42f);
            }

            TMP_Text text = Instantiate(manpowerText, panel.transform);
            text.name = "Resource " + names[i];
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Left;
            text.fontSize = 22;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12;
            text.fontSizeMax = 22;
            text.color = colors[i];
            text.outlineWidth = 0f;
            RectTransform label = text.rectTransform;
            label.anchorMin = new Vector2(i / 5f, 0f);
            label.anchorMax = new Vector2((i + 1) / 5f, 1f);
            label.offsetMin = new Vector2(hasIcon ? 58f : 14f, 4f);
            label.offsetMax = new Vector2(-8f, -4f);
            resourceTexts[i] = text;
        }
    }

    // These small procedural sprites stay sharp, work with the existing UI
    // material, and avoid relying on emoji glyphs that the TMP font may lack.
    private Sprite CreateResourceIcon(bool coin)
    {
        const int size = 64;
        const int samplesPerAxis = 4;
        const float inverseSamples = 1f / (samplesPerAxis * samplesPerAxis);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = coin ? "Gold coin icon" : "Industry wrench icon";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color total = Color.clear;
                for (int sampleY = 0; sampleY < samplesPerAxis; sampleY++)
                {
                    for (int sampleX = 0; sampleX < samplesPerAxis; sampleX++)
                    {
                        float u = ((x + (sampleX + 0.5f) / samplesPerAxis) / size) * 2f - 1f;
                        float v = ((y + (sampleY + 0.5f) / samplesPerAxis) / size) * 2f - 1f;
                        total += coin ? SampleCoin(u, v) : SampleWrench(u, v);
                    }
                }
                pixels[y * size + x] = total * inverseSamples;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = texture.name;
        generatedIconTextures.Add(texture);
        generatedIconSprites.Add(sprite);
        return sprite;
    }

    private static Color SampleCoin(float x, float y)
    {
        float radius = Mathf.Sqrt(x * x + y * y);
        if (radius > 0.9f) return Color.clear;
        if (radius > 0.77f) return new Color(0.52f, 0.29f, 0.05f, 1f);

        float lighting = Mathf.Clamp01(0.55f + (y - x) * 0.22f);
        Color face = Color.Lerp(new Color(0.9f, 0.55f, 0.06f, 1f),
            new Color(1f, 0.88f, 0.32f, 1f), lighting);
        if (Mathf.Abs(radius - 0.62f) < 0.045f)
            face = Color.Lerp(face, new Color(0.7f, 0.39f, 0.04f, 1f), 0.72f);
        if (x < -0.2f && y > 0.15f && x * x + y * y < 0.27f)
            face = Color.Lerp(face, Color.white, 0.28f);
        return face;
    }

    private static Color SampleWrench(float x, float y)
    {
        const float diagonal = 0.70710678f;
        float along = (x + y) * diagonal;
        float across = (y - x) * diagonal;
        float handleRadius = Mathf.Sqrt((along + 0.66f) * (along + 0.66f) + across * across);
        float jawRadius = Mathf.Sqrt((along - 0.5f) * (along - 0.5f) + across * across);

        bool shaft = along > -0.62f && along < 0.49f && Mathf.Abs(across) < 0.14f;
        bool handle = handleRadius < 0.3f && handleRadius > 0.12f;
        bool jaw = jawRadius < 0.42f && jawRadius > 0.2f &&
            !(along > 0.5f && Mathf.Abs(across) < 0.21f);
        if (!shaft && !handle && !jaw) return Color.clear;

        bool highlight = (shaft && Mathf.Abs(across) < 0.08f) ||
            (handle && handleRadius > 0.17f && handleRadius < 0.25f) ||
            (jaw && jawRadius > 0.25f && jawRadius < 0.36f);
        return highlight
            ? new Color(0.7f, 0.82f, 0.86f, 1f)
            : new Color(0.2f, 0.34f, 0.4f, 1f);
    }

    private void RefreshResources()
    {
        if (resourceTexts[0] == null || localPlayer == null) return;
        int land = map.CountTerritory(localPlayer.Id);
        resourceTexts[0].text = "<size=16>MILITARY</size>\n" + localPlayer.Manpower.ToString("N0") +
            "  (RECRUITED)";
        resourceTexts[1].text = "<size=16>CIVILIANS</size>\n" + localPlayer.Civilians.ToString("N0") +
            "  (+" + PlayerData.CiviliansPerSecond(land).ToString("N0") + "/sim sec)";
        resourceTexts[2].text = "<size=16>GOLD</size>\n" + localPlayer.Gold.ToString("N0") +
            "  (+" + PlayerData.GoldPerSecond(land).ToString("N0") + "/sim sec)";
        resourceTexts[3].text = "<size=16>INDUSTRY</size>\n" + localPlayer.Industry.ToString("N0") +
            "  (+" + PlayerData.IndustryPerSecond(land).ToString("N0") + "/sim sec)";
        resourceTexts[4].text = "<size=16>OWNED LAND</size>\n" + land.ToString("N0") + " tiles";
        UpdateRecruitLabel();
        UpdateDivisionLabel();
    }

    // Recruiting converts civilians into soldiers for coins. Both ledgers move
    // together, so the button always offers whichever of the two runs out first.
    private void UpdateRecruitLabel()
    {
        if (recruitLabel == null || localPlayer == null) return;
        int available = localPlayer.AffordableRecruits();
        if (available > 0)
        {
            recruitLabel.text = "RECRUIT  " + available.ToString("N0") + "  ·  " +
                available.ToString("N0") + " COINS";
            return;
        }
        // Name the input that actually ran out rather than listing both.
        recruitLabel.text = localPlayer.Gold < PlayerData.GoldPerRecruit
            ? "RECRUIT  —  NEED COINS"
            : "RECRUIT  —  NEED CIVILIANS";
    }

    private void UpdateDivisionLabel()
    {
        if (divisionLabel == null || localPlayer == null) return;
        divisionAffordable = localPlayer.Manpower / Division.Cost;
        divisionLabel.text = divisionAffordable > 0
            ? "RAISE DIVISION  \u00b7  " + Division.Cost.ToString("N0") + " MIL"
            : "RAISE DIVISION  \u2014  NEED " + Division.Cost.ToString("N0") + " MIL";
    }

    // Also reachable from the R key so recruiting does not require the mouse.
    public void RecruitMaximum()
    {
        if (localPlayer == null) return;
        int troops = localPlayer.AffordableRecruits();
        if (troops <= 0)
        {
            SetStatus("No recruits available — recruiting needs both civilians and coins.");
            return;
        }
        localPlayer.TryRecruit(troops);
        SetStatus("Recruited " + troops.ToString("N0") + " soldiers for " +
            (troops * PlayerData.GoldPerRecruit).ToString("N0") + " coins.");
        Refresh();
    }

    public void Refresh()
    {
        foreach (TerritoryLabel label in labels)
        {
            label.Text.text = label.Player.Manpower.ToString("N0");
            int version = map.GetTerritoryVersion(label.Player.Id);
            if (label.Version == version) continue;
            label.Version = version;
            label.HasTerritory = map.TryGetTerritoryCenter(label.Player.Id, out float x, out float y);
            label.Text.enabled = label.HasTerritory;
            // Grid coordinates stay independent of the texture's visual detail.
            label.LocalCenter = new Vector3((x - map.Width * 0.5f) / MapRenderer.CellsPerUnit,
                (y - map.Height * 0.5f) / MapRenderer.CellsPerUnit, 0f);
        }
        UpdateCommitmentLabel();
        RefreshResources();
    }

    private void UpdateCommitmentLabel()
    {
        if (commitmentLabel == null) return;
        int troops = localPlayer == null ? 0 :
            Mathf.Clamp(Mathf.RoundToInt((localPlayer.Manpower + recallableTroops) * Commitment), 0, localPlayer.Manpower + recallableTroops);
        int percent = Mathf.RoundToInt(Commitment * 100f);
        if (troops == displayedCommitment && percent == displayedPercent) return;
        displayedCommitment = troops; displayedPercent = percent;
        commitmentLabel.text = "NEXT  " + percent + "%   ·   " +
            troops.ToString("N0") + " troops";
    }

    // Built in code because the scene has no UI prefabs: the canvas already
    // carries a GraphicRaycaster and a scaler, so the slider only needs its
    // standard background / fill / handle rects wired up.
}
