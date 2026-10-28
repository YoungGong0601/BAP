using System.Collections.Generic;
using UnityEngine;

// grid based entity manager (focus on find entity)
// swap-back / entity has index,cellKey
public static class EntityManager
{
    public const float CellSize = 5; // grid size
    private static readonly Dictionary<Vector2Int, List<Entity>> entities = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        entities.Clear();
    }

    //━━━━━━━━━━ Entry ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // add
    public static void Add(Entity e)
    {
        var (cell, idx) = e.ManageValue;
        if (idx >= 0) return; // already added
        var currCell = GetCellPosition(e.transform.position);

        if (!entities.ContainsKey(currCell)) entities[currCell] = new();
        entities[currCell].Add(e);

        e.ManageValue = (cell: currCell, idx: entities[currCell].Count - 1);
    }

    // remove
    public static void Remove(Entity e)
    {
        var (cell, idx) = e.ManageValue;
        if (idx < 0) return;

        if (entities[cell].Count == 1) entities.Remove(cell);
        else
        {
            var list = entities[cell];
            var last = entities[cell].Count - 1;
            list[last].ManageValue = (cell: cell, idx: idx);
            list[idx] = list[last];
            list.RemoveAt(last);
        }


        e.ManageValue = (cell: new Vector2Int(int.MaxValue, int.MaxValue), idx: -1);
    }

    // update (remove-add)
    public static void Update(Entity e)
    {
        var (oldCell, oldIdx) = e.ManageValue;
        if (oldIdx < 0) return;
        var currCell = GetCellPosition(e.transform.position);
        if (oldCell == currCell) return;

        // remove
        if (entities[oldCell].Count == 1) entities.Remove(oldCell);
        else
        {
            var list = entities[oldCell];
            var last = entities[oldCell].Count - 1;
            list[last].ManageValue = (cell: oldCell, idx: oldIdx);
            list[oldIdx] = list[last];
            list.RemoveAt(last);
        }

        // add
        if (!entities.ContainsKey(currCell)) entities[currCell] = new();
        entities[currCell].Add(e);

        e.ManageValue = (cell: currCell, idx: entities[currCell].Count - 1);
    }


    public static Vector2Int GetCellPosition(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / CellSize),
            Mathf.FloorToInt(position.z / CellSize)
        );
    }



    //━━━━━━━━━━ Methods ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    // 가장 가까운 다른 팀 Entity를 반환. Ring search
    public static Entity GetNearestEntity(Vector3 position, float range, Team targetTeam, Entity exclude = null, HashSet<Entity> excludeEntities = null)
        => GetNearestEntity<Entity>(position, range, targetTeam, exclude, excludeEntities);

    public static T GetNearestEntity<T>(Vector3 position, float range, Team targetTeam, T exclude = null, HashSet<T> excludeEntities = null) where T : Entity
    {
        var center = GetCellPosition(position);
        var maxRing = Mathf.CeilToInt(range / CellSize);

        T nearest = null;
        float nearestSqr = range * range;

        for (int ring = 0; ring <= maxRing; ring++)
        {

            var ringMin = (ring - 1) * CellSize;
            if (ringMin > 0 && nearestSqr < ringMin * ringMin) break;

            if (ring == 0)
            {
                CheckCell(center);
                continue;
            }

            for (int x = -ring; x <= ring; x++)
            {
                var y = ring;
                CheckCell(center + new Vector2Int(x, y));
                CheckCell(center + new Vector2Int(x, -y));
            }

            for (int y = -ring + 1; y <= ring - 1; y++)
            {
                var x = ring;
                CheckCell(center + new Vector2Int(x, y));
                CheckCell(center + new Vector2Int(-x, y));
            }
        }

        return nearest;

        // ── func ──────────────

        void CheckCell(Vector2Int cell)
        {
            if (!entities.TryGetValue(cell, out var list)) return;

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null) continue;
                if (!e.Stat.CanBeTargeted) continue;
                if ((e.Team & targetTeam) == 0) continue;
                if (e == exclude) continue;
                if (e is not T t) continue;
                if (excludeEntities != null && excludeEntities.Contains(t)) continue;

                var sqr = (e.transform.position - position).ToXZ().sqrMagnitude;
                if (sqr <= nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = t;
                }
            }
        }
    }


    public static int GetEntitiesInRange<T>(Vector3 position, float range, Team targetTeam, List<T> result, T exclude = null, HashSet<T> excludeEntities = null) where T : Entity
    {
        result.Clear();
        var center = GetCellPosition(position);
        var maxRing = Mathf.CeilToInt(range / CellSize);
        var rangeSqr = range * range;

        for (int x = -maxRing; x <= maxRing; x++)
        {
            for (int y = -maxRing; y <= maxRing; y++)
            {
                CheckCell(center + new Vector2Int(x, y));
            }
        }

        return result.Count;

        // ── func ──────────────

        void CheckCell(Vector2Int cell)
        {
            if (!entities.TryGetValue(cell, out var list)) return;

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null) continue;
                if (!e.Stat.CanBeTargeted) continue;
                if ((e.Team & targetTeam) == 0) continue;
                if (e == exclude) continue;
                if (e is not T t) continue;
                if (excludeEntities != null && excludeEntities.Contains(t)) continue;

                var sqr = (e.transform.position - position).ToXZ().sqrMagnitude;
                if (sqr <= rangeSqr)
                {
                    result.Add(t);
                }
            }
        }
    }
}