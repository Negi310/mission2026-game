using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public sealed class BlockBuilding : MonoBehaviour
    {
        public readonly List<DestructibleBlock> Blocks = new List<DestructibleBlock>();
        public bool Collapsed { get; private set; }
        public float Width { get; set; }
        public int Floors { get; set; }
        public int Style { get; internal set; }
        public Color FacadeColor { get; internal set; }
        public ItemKind RewardKind { get; internal set; }
        public int DetachedParts { get; private set; }
        internal int DamageRevision { get; private set; }
        public float DestroyedFraction => Blocks.Count == 0 ? 0 : (float)DetachedParts / Blocks.Count;
        public bool FullyDestroyed => Blocks.Count > 0 && DetachedParts >= Blocks.Count;
        public ItemPickup Reward { get; set; }
        internal TownBuildingVisual Presentation;
        public int CollapsedFromLevel { get; private set; } = int.MaxValue;
        private TankTownGame game;
        private int supportDetached = -1;
        private readonly HashSet<DestructibleBlock> supported = new HashSet<DestructibleBlock>();
        private readonly Queue<DestructibleBlock> queue = new Queue<DestructibleBlock>();
        private readonly List<Vector2> feet = new List<Vector2>(), hull = new List<Vector2>();
        public void Initialize(TankTownGame owner) => game = owner;
        internal void RegisterDetached()
        {
            DetachedParts++;
            if (FullyDestroyed) Reward?.SpawnArea();
        }
        internal void RegisterDamage() => DamageRevision++;

        public void ReleaseUnsupported(Vector3 origin, float impulse)
        {
            if (game == null || !game.IsPlaying) return;
            if (supportDetached == DetachedParts) return;
            supported.Clear(); queue.Clear();
            int topLevel = 0;
            foreach (var block in Blocks)
            {
                if (block == null || block.Detached) continue;
                topLevel = Mathf.Max(topLevel, block.Level);
                if (!block.Foundation) continue;
                supported.Add(block);
                queue.Enqueue(block);
            }
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var next in current.Links)
                    if (next != null && !next.Detached && next.Kind != BuildingPart.Wall && supported.Add(next)) queue.Enqueue(next);
            }
            foreach (var block in Blocks)
                if (block != null && !block.Detached && block.Kind == BuildingPart.Wall)
                    foreach (var support in block.Links)
                        if (support != null && supported.Contains(support)) { supported.Add(block); break; }

            int looseLevel = int.MaxValue, looseFrameParts = 0;
            foreach (var block in Blocks)
            {
                if (block == null || block.Detached || supported.Contains(block)) continue;
                if (block.Kind != BuildingPart.Wall) { looseLevel = Mathf.Min(looseLevel, block.Level); looseFrameParts++; }
                block.Break(origin, impulse * .18f);
            }
            if (looseFrameParts > 0) MarkCollapse(looseLevel);

            // At every storey, the surviving columns must support the horizontal
            // centre of mass of everything still standing above that storey.
            for (int level = 0; level <= topLevel; level++)
            {
                feet.Clear();
                Vector2 weightSum = Vector2.zero, supportSum = Vector2.zero;
                float totalWeight = 0;
                int columns = 0;
                foreach (var block in Blocks)
                {
                    if (block == null || block.Detached || block.Level < level) continue;
                    var p = block.transform.localPosition;
                    var s = block.transform.localScale;
                    float weight = Mathf.Abs(s.x * s.y * s.z) * (block.Kind == BuildingPart.Wall ? .4f : 1);
                    weightSum += new Vector2(p.x, p.z) * weight;
                    totalWeight += weight;
                    if (block.Level != level || block.Kind != BuildingPart.Pillar) continue;
                    var center = new Vector2(p.x, p.z);
                    supportSum += center; columns++;
                    feet.Add(center + new Vector2(-s.x / 2, -s.z / 2));
                    feet.Add(center + new Vector2(s.x / 2, -s.z / 2));
                    feet.Add(center + new Vector2(s.x / 2, s.z / 2));
                    feet.Add(center + new Vector2(-s.x / 2, s.z / 2));
                }
                if (totalWeight <= 0) continue;
                Vector2 centerOfMass = weightSum / totalWeight;
                ConvexHull(feet);
                if (Inside(hull, centerOfMass, game.Settings.stabilityMargin)) continue;
                Vector2 tip = columns > 0 ? centerOfMass - supportSum / columns : centerOfMass;
                if (tip.sqrMagnitude < .01f) tip = Vector2.down;
                var direction = transform.TransformDirection(new Vector3(tip.normalized.x, 0, tip.normalized.y));
                foreach (var block in Blocks)
                {
                    if (block == null || block.Detached || block.Level < level) continue;
                    block.Break(origin, impulse * .18f);
                    block.TipDebris(direction);
                }
                MarkCollapse(level);
                break;
            }
            supportDetached = DetachedParts;
        }

        private void MarkCollapse(int level)
        {
            CollapsedFromLevel = Mathf.Min(CollapsedFromLevel, level);
            if (Collapsed) return;
            Collapsed = true;
            game.Effects.Collapse(transform.position + Vector3.up * (level * game.Settings.floorHeight + .5f), game.World.BuildingWidth);
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private void ConvexHull(List<Vector2> points)
        {
            points.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            hull.Clear();
            foreach (var p in points)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            int lower = hull.Count;
            for (int i = points.Count - 2; i >= 0; i--)
            {
                var p = points[i];
                while (hull.Count > lower && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
        }
        private static bool Inside(List<Vector2> hull, Vector2 point, float margin)
        {
            if (hull.Count < 3) return false;
            for (int i = 0; i < hull.Count; i++)
            {
                var edge = hull[(i + 1) % hull.Count] - hull[i];
                if (Cross(edge, point - hull[i]) < margin * edge.magnitude) return false;
            }
            return true;
        }
        public static void Connect(DestructibleBlock a, DestructibleBlock b) { a.Links.Add(b); b.Links.Add(a); }
    }
}
