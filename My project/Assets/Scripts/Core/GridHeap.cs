using System.Collections.Generic;

// Unity's .NET profile does not include System.Collections.Generic.PriorityQueue.
// A small binary min-heap for Dijkstra searches and terrain drainage.
public sealed class GridHeap
{
    private readonly List<(int cell, float priority)> entries =
        new List<(int, float)>();

    public int Count => entries.Count;

    public void Push(int cell, float priority)
    {
        int index = entries.Count;
        entries.Add((cell, priority));
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (entries[parent].priority <= priority) break;
            entries[index] = entries[parent];
            index = parent;
        }
        entries[index] = (cell, priority);
    }

    public int Pop(out float priority)
    {
        var first = entries[0];
        var last = entries[entries.Count - 1];
        entries.RemoveAt(entries.Count - 1);
        int index = 0;
        while (index * 2 + 1 < entries.Count)
        {
            int child = index * 2 + 1;
            if (child + 1 < entries.Count &&
                entries[child + 1].priority < entries[child].priority) child++;
            if (last.priority <= entries[child].priority) break;
            entries[index] = entries[child];
            index = child;
        }
        if (entries.Count > 0) entries[index] = last;
        priority = first.priority;
        return first.cell;
    }
}
