using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public sealed class BlockBuilding : MonoBehaviour
    {
        public readonly List<DestructibleBlock> Blocks = new List<DestructibleBlock>();

        // A simplified structural graph: columns, beams and slabs must reach the
        // foundation. Facades receive support but cannot carry the frame themselves.
        public void ReleaseUnsupported(Vector3 origin, float impulse)
        {
            var supported = new HashSet<DestructibleBlock>();
            var queue = new Queue<DestructibleBlock>();
            foreach (var block in Blocks)
            {
                if (block == null || block.Detached || !block.Foundation) continue;
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
            foreach (var block in Blocks)
                if (block != null && !block.Detached && !supported.Contains(block)) block.Break(origin, impulse * .18f);
        }

        public static void Connect(DestructibleBlock a, DestructibleBlock b) { a.Links.Add(b); b.Links.Add(a); }
    }
}
