using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public sealed class BlockBuilding : MonoBehaviour
    {
        public readonly List<DestructibleBlock> Blocks = new List<DestructibleBlock>();

        // Each vertical stack carries the blocks above it. Destroying support releases
        // those blocks as real rigidbodies, without a baked collapse animation.
        public void ReleaseUnsupported(Vector3 origin, float impulse)
        {
            var brokenLevels = new Dictionary<int, int>();
            foreach (var block in Blocks)
            {
                if (block == null || !block.Detached) continue;
                if (!brokenLevels.TryGetValue(block.Column, out int level) || block.Level < level)
                    brokenLevels[block.Column] = block.Level;
            }
            foreach (var block in Blocks)
                if (block != null && !block.Detached && brokenLevels.TryGetValue(block.Column, out int level) && block.Level > level)
                    block.Break(origin, impulse * 0.45f);
        }
    }
}
