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
        [Min(1)] public int maxDebris = 280;
        [Min(1)] public float debrisLifetime = 9;
        [Range(0.05f, 1)] public float pressureThreshold = 0.55f;
        [Range(0, 0.9f)] public float pressureRelease = 0.25f;
        public int citySeed = 2026;
        [Header("City")]
        [Range(2, 5)] public int cityHalfBlocks = 3;
        [Min(24)] public float blockSpacing = 32;
        [Min(10)] public float buildingWidth = 20;
        [Min(2)] public float floorHeight = 3.6f;
        [Header("Power-ups (per level)")]
        [Range(1, 12)] public int maxUpgradeLevel = 8;
        [Min(0)] public float speedUpgrade = .08f;
        [Min(0)] public float powerUpgrade = .18f;
        [Min(0)] public float radiusUpgrade = .07f;
        [Min(0)] public float rapidUpgrade = .16f;
        [Min(0)] public float handlingUpgrade = .06f;
    }
}
