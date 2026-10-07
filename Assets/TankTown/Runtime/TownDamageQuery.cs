using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    // Reused scratch data. Grow and retry a full overlap buffer so large beams and
    // bombs never silently lose targets. Each nested query rents a separate instance.
    internal sealed class TownDamageQuery
    {
        internal Collider[] Hits = new Collider[1024];
        internal int Count;
        internal readonly HashSet<BlockBuilding> Buildings = new HashSet<BlockBuilding>();
        internal readonly Dictionary<DestructibleBlock, float> Distances = new Dictionary<DestructibleBlock, float>();
        internal void Sphere(Vector3 point, float radius)
        {
            while (true)
            {
                Count = Physics.OverlapSphereNonAlloc(point, radius, Hits, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore);
                if (Count < Hits.Length) return;
                Array.Resize(ref Hits, Hits.Length * 2);
            }
        }
        internal void Box(Vector3 point, Vector3 halfSize, Quaternion rotation)
        {
            while (true)
            {
                Count = Physics.OverlapBoxNonAlloc(point, halfSize, Hits, rotation, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore);
                if (Count < Hits.Length) return;
                Array.Resize(ref Hits, Hits.Length * 2);
            }
        }
        internal void Capsule(Vector3 start, Vector3 end, float radius)
        {
            while (true)
            {
                Count = Physics.OverlapCapsuleNonAlloc(start, end, radius, Hits, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore);
                if (Count < Hits.Length) return;
                Array.Resize(ref Hits, Hits.Length * 2);
            }
        }
        internal void Clear() { Array.Clear(Hits, 0, Count); Count = 0; Buildings.Clear(); Distances.Clear(); }
    }
}
