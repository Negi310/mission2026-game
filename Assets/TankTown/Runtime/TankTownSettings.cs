using UnityEngine;

namespace TankTown
{
    [CreateAssetMenu(menuName = "Tank Town/Settings")]
    public sealed class TankTownSettings : ScriptableObject
    {
        [Min(1)] public float roundSeconds = 180;
        [Min(0)] public float respawnPenalty = 3;
        [Min(0.1f)] public float respawnDelay = 1.2f;
        [Min(1)] public float driveSpeed = 9;
        [Min(1)] public float turnSpeed = 85;
        [Min(0)] public float shotCooldown = 0.35f;
        [Min(1)] public float blastRadius = 3.8f;
        [Min(0)] public float blastImpulse = 12;
        [Min(0)] public float fatalImpactSpeed = 5;
        [Min(1)] public int maxDebris = 180;
        [Min(1)] public float debrisLifetime = 9;
        [Range(0.05f, 1)] public float pressureThreshold = 0.55f;
        [Range(0, 0.9f)] public float pressureRelease = 0.25f;
        public int citySeed = 2026;
    }
}
