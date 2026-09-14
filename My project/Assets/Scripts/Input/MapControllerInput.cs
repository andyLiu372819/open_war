using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Planning-phase input and command translation. MapController remains the scene
// composition root; this partial isolates selection and order authoring from
// startup and turn resolution without changing its serialized identity.
public partial class MapController
{
    private void ReadMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 pointer = mouse.position.ReadValue();
        bool overMap = !hud.BlocksMapInput(pointer) &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
        if (mouse.leftButton.wasPressedThisFrame)
        {
            dragStart = pointer;
            dragOnMap = overMap;
            dragging = false;
        }
        // A press becomes a box the moment it travels far enough.
        if (mouse.leftButton.isPressed && dragOnMap && !dragging &&
            (pointer - dragStart).sqrMagnitude > DragThreshold * DragThreshold)
            dragging = true;
        if (dragging) hud.ShowSelectionBox(dragStart, pointer);
        if (mouse.leftButton.wasReleasedThisFrame && dragOnMap)
        {
            if (dragging) SelectInBox(dragStart, pointer, AdditiveHeld);
            // A press that never moved keeps the old click behaviour:
            // it picks up a division, and open ground leaves the set alone.
            else if (mapRenderer.TryScreenToCell(map, mapCamera, pointer, out int pickX, out int pickY))
                TrySelectAt(pickX, pickY, AdditiveHeld);
            hud.HideSelectionBox();
            dragging = false;
            dragOnMap = false;
        }
        // Right-click sends the selection; right-drag traces a line for it to
        // hold. Both only stand during planning.
        if (mouse.rightButton.wasPressedThisFrame)
        {
            holdStart = pointer;
            holdOnMap = overMap;
            holdDragging = false;
        }
        if (mouse.rightButton.isPressed && holdOnMap && !holdDragging &&
            (pointer - holdStart).sqrMagnitude > DragThreshold * DragThreshold)
            holdDragging = true;
        if (holdDragging) hud.ShowSelectionBox(holdStart, pointer);
        if (mouse.rightButton.wasReleasedThisFrame && holdOnMap)
        {
            if (holdDragging)
            {
                hud.HideSelectionBox();
                AssignHoldLine(holdStart, pointer);
            }
            else if (mapRenderer.TryScreenToCell(map, mapCamera, pointer, out int orderX, out int orderY))
                OrderSelected(orderX, orderY);
            holdDragging = false;
            holdOnMap = false;
        }
    }

    // Shift adds to the current selection instead of replacing it.
    private static bool AdditiveHeld =>
        Keyboard.current != null &&
        (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

    private static bool ControlHeld =>
        Keyboard.current != null &&
        (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);

    private void ReadKeyboard(ref bool hudDirty)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        // Ctrl+N stores the selection; N alone recalls it.
        for (int i = 0; i < GroupKeys.Length; i++)
        {
            if (!keyboard[GroupKeys[i]].wasPressedThisFrame) continue;
            if (ControlHeld) AssignControlGroup(i + 1);
            else RecallControlGroup(i + 1);
            break;
        }
        for (int i = 0; i < StanceKeys.Length; i++)
        {
            if (!keyboard[StanceKeys[i]].wasPressedThisFrame) continue;
            SetSelectionStance((DivisionStance)i);
            break;
        }
        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            EndTurn();
            hudDirty = true;
        }
        if (keyboard.rKey.wasPressedThisFrame)
        {
            hud.RecruitMaximum();
            hudDirty = true;
        }
        if (keyboard.escapeKey.wasPressedThisFrame && selection.Count > 0) ClearSelection();
        if (keyboard.f9Key.wasPressedThisFrame)
        {
            ToggleFogDebug();
            hudDirty = true;
        }
        if (keyboard.bKey.wasPressedThisFrame)
        {
            FormDivision();
            hudDirty = true;
        }
    }

    public void AssignControlGroup(int group)
    {
        if (group < 1 || group > GroupKeys.Length) return;
        if (!controlGroups.TryGetValue(group, out List<Division> stored))
            controlGroups[group] = stored = new List<Division>();
        stored.Clear();
        stored.AddRange(selection);
        hud.SetStatus(stored.Count == 0
            ? "Control group " + group + " cleared."
            : "Control group " + group + " now holds " + stored.Count +
                (stored.Count == 1 ? " division." : " divisions."));
    }

    public int RecallControlGroup(int group)
    {
        if (!controlGroups.TryGetValue(group, out List<Division> stored) || stored.Count == 0)
        {
            hud.SetStatus("Control group " + group + " is empty.");
            return 0;
        }
        // Divisions destroyed since the group was stored simply drop out.
        stored.RemoveAll(division => division == null || division.IsDestroyed);
        selection.Clear();
        selection.AddRange(stored);
        hud.SetStatus("Control group " + group + ": " + selection.Count +
            (selection.Count == 1 ? " division selected." : " divisions selected."));
        hud.SyncDivisions(divisions.Divisions, selection);
        return selection.Count;
    }

    // Traces the dragged stretch of border and hands it to the selection. A
    // wider line is not free: the same division covers it more thinly.
    public int AssignHoldLine(Vector2 screenStart, Vector2 screenEnd)
    {
        if (!mapRenderer.TryScreenToCell(map, mapCamera, screenStart, out int ax, out int ay) ||
            !mapRenderer.TryScreenToCell(map, mapCamera, screenEnd, out int bx, out int by))
            return 0;
        return AssignHoldCells(ax, ay, bx, by);
    }

    // The same assignment in map cells, so it can be driven without a camera.
    public int AssignHoldCells(int ax, int ay, int bx, int by)
    {
        if (!turns.IsPlanning)
        {
            hud.SetStatus("Orders are locked while the turn resolves.");
            return 0;
        }
        if (selection.Count == 0)
        {
            hud.SetStatus("Select the divisions that should hold the line first.");
            return 0;
        }
        TraceLine(ax, ay, bx, by, holdLine);
        int held = divisions.AssignFrontage(selection, holdLine);
        if (held == 0)
        {
            hud.SetStatus("A line has to be drawn along ground you already hold.");
            return 0;
        }
        int perDivision = Mathf.Max(1, held / selection.Count);
        int deploying = 0;
        foreach (Division division in selection)
            if (division.Stance == DivisionStance.Defend && division.HasOrders) deploying++;
        hud.SetStatus(selection.Count + (selection.Count == 1 ? " division holds " : " divisions hold ") +
            held + " cells of front — about " + perDivision + " each, at " +
            SelectedDivision.LineDefense + " defence per cell." + (deploying > 0
                ? " " + deploying + (deploying == 1 ? " division is" : " divisions are") +
                    " moving into position."
                : " All assigned divisions are in position."));
        hud.SyncDivisions(divisions.Divisions, selection);
        return held;
    }

    // Bresenham, so a dragged line follows the cells the player drew across.
    private void TraceLine(int ax, int ay, int bx, int by, List<int> into)
    {
        into.Clear();
        int dx = Mathf.Abs(bx - ax), dy = -Mathf.Abs(by - ay);
        int stepX = ax < bx ? 1 : -1, stepY = ay < by ? 1 : -1;
        int error = dx + dy;
        int guard = dx - dy + 4;
        while (guard-- > 0)
        {
            if (map.InsideBorder(ax, ay)) into.Add(ay * map.Width + ax);
            if (ax == bx && ay == by) break;
            int doubled = error * 2;
            if (doubled >= dy) { error += dy; ax += stepX; }
            if (doubled <= dx) { error += dx; ay += stepY; }
        }
    }

    public bool SetSelectionStance(DivisionStance stance)
    {
        if (!turns.IsPlanning)
        {
            hud.SetStatus("Orders are locked while the turn resolves.");
            return false;
        }
        if (selection.Count == 0)
        {
            hud.SetStatus("Select a division before setting its stance.");
            return false;
        }
        divisions.SetStance(selection, stance);
        hud.SetStatus(selection.Count + (selection.Count == 1 ? " division" : " divisions") +
            " set to " + stance.ToString().ToUpperInvariant() + ".");
        hud.SyncDivisions(divisions.Divisions, selection);
        return true;
    }

    // Selects the nearest division of yours to the cell, if there is one.
    // Returns false and changes nothing when the press was on open ground.
    public bool TrySelectAt(int x, int y, bool additive = false)
    {
        Division found = divisions.FindNear(x, y, player.Id, 6);
        if (found == null) return false;
        if (!additive) selection.Clear();
        else if (selection.Contains(found)) { selection.Remove(found); }
        if (!selection.Contains(found)) selection.Add(found);
        hud.SetStatus(found.Number + Ordinal(found.Number) +
            " Infantry Division selected — right-click to send it.");
        hud.SyncDivisions(divisions.Divisions, selection);
        return true;
    }

    // Box select over screen coordinates, which is what the drag produces.
    public int SelectInBox(Vector2 screenStart, Vector2 screenEnd, bool additive = false)
    {
        if (!mapRenderer.TryScreenToCell(map, mapCamera, screenStart, out int ax, out int ay) ||
            !mapRenderer.TryScreenToCell(map, mapCamera, screenEnd, out int bx, out int by))
            return SelectCellBox(0, 0, -1, -1, additive);
        return SelectCellBox(ax, ay, bx, by, additive);
    }

    // The same selection in map cells, so it can be driven without a camera.
    public int SelectCellBox(int ax, int ay, int bx, int by, bool additive = false)
    {
        divisions.FindInBox(ax, ay, bx, by, player.Id, boxHits);
        if (!additive) selection.Clear();
        foreach (Division hit in boxHits)
            if (!selection.Contains(hit)) selection.Add(hit);
        hud.SetStatus(selection.Count == 0
            ? "Nothing in that box. Drag over your divisions to pick several up at once."
            : selection.Count + (selection.Count == 1 ? " division selected." : " divisions selected.") +
                " Right-click to send them.");
        hud.SyncDivisions(divisions.Divisions, selection);
        return selection.Count;
    }

    public void ClearSelection()
    {
        selection.Clear();
        hud.SetStatus("Selection cleared. Click a division to pick it up again.");
        hud.SyncDivisions(divisions.Divisions, selection);
    }

    public bool OrderSelected(int x, int y)
    {
        if (!turns.IsPlanning)
        {
            hud.SetStatus("Orders are locked while the turn resolves. Wait for the next planning phase.");
            return false;
        }
        if (selection.Count == 0)
        {
            hud.SetStatus("Select one or more divisions first, then right-click where they should go.");
            return false;
        }
        int ordered = divisions.OrderGroup(selection, x, y);
        if (ordered == 0)
        {
            hud.SetStatus("That destination cannot be reached on foot. Water and deep rivers block divisions.");
            return false;
        }
        hud.SetStatus(ordered == 1
            ? SelectedDivision.Number + Ordinal(SelectedDivision.Number) +
                " Infantry Division advancing to (" + x + ", " + y + ")."
            : ordered + " divisions advancing on (" + x + ", " + y + ").");
        return true;
    }

    // Recomputes what the player can see and repaints the mask.
}
