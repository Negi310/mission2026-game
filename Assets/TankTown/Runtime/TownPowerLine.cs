using UnityEngine;

namespace TankTown
{
    public sealed class TownPowerLine : MonoBehaviour
    {
        private DestructibleBlock first, second;
        public void Initialize(DestructibleBlock a, DestructibleBlock b) { first = a; second = b; }
        private void LateUpdate()
        {
            // A broken span disappears with its support instead of floating in the air.
            if (first == null || second == null || first.Detached || second.Detached) Destroy(gameObject);
        }
    }
}
