using UnityEngine;

namespace TankTown
{
    [CreateAssetMenu(menuName = "Tank Town/Settings")]
    public sealed class TankTownSettings : ScriptableObject
    {
        [Min(1)] public float roundSeconds = 180;
        [Min(0)] public float respawnPenalty = 3;
        [Min(0.1f)] public float respawnDelay = 1.2f;
        [Min(1)] public float driveSpeed = 16;
        [Min(1)] public float turnSpeed = 85;
        [Header("Driving")]
        [Min(.1f)] public float acceleration = 10;
        [Min(.1f)] public float steeringResponse = 12;
        [Header("Tank steering")]
        [Range(0, .5f)] public float hullTurnDelay = .08f;
        [Range(15, 60)] public float turretMaxYaw = 22;
        [Min(30)] public float turretTurnSpeed = 210;
        [Min(.1f)] public float tireGrip = 7;
        [Min(.8f)] public float rideHeight = 1;
        [Header("Cannon")]
        [Range(0, 10)] public float cannonElevation = 4;
        [Min(0)] public float shotCooldown = 0.35f;
        [Min(1)] public float blastRadius = 6.5f;
        [Min(0)] public float blastImpulse = 16;
        [Header("Debris destruction")]
        [Min(1)] public float fragmentSize = 3;
        [Min(1)] public float debrisImpactSpeed = 12;
        [Min(.5f)] public float debrisBlastRadius = 3.5f;
        [Range(0, 1)] public float debrisImpulseMultiplier = .5f;
        [Range(1, 6)] public int debrisChainLimit = 4;
        [Header("Fuel tanks")]
        [Min(.1f)] public float fuelIgnitionDistance = 3;
        [Min(1)] public float fuelExplosionRadius = 24;
        [Min(0)] public float fuelImpulseMultiplier = 3;
        [Min(0)] public float fatalImpactSpeed = 5;
        [Min(1)] public int maxDebris = 2000;
        [Min(1)] public float debrisLifetime = 3.6f;
        [Min(0)] public float debrisLifetimeSpread = 1.2f;
        [Range(0.05f, 1)] public float pressureThreshold = 0.55f;
        [Range(0, 0.9f)] public float pressureRelease = 0.25f;
        public int citySeed = 2026;
        [Header("City")]
        [Range(2, 5)] public int cityHalfBlocks = 3;
        [Min(24)] public float blockSpacing = 40;
        [Min(10)] public float buildingWidth = 20;
        [Min(8)] public float roadWidth = 16;
        [Range(.6f, 3)] public float pillarWidth = 1.8f;
        [Range(0, 1)] public float stabilityMargin = .25f;
        [Min(2)] public float floorHeight = 3.6f;
        [Range(0, 5)] public float terraceHeight = 3.5f;
        [Header("Effects")]
        [Range(1, 2.5f)] public float explosionVisualScale = 1.55f;
        [Header("Camera")]
        public Vector3 cameraOffset = new Vector3(0, 3.0f, -9.0f);
        [Range(50, 90)] public float cameraFieldOfView = 72;
        [Range(.1f, .8f)] public float cameraTurnSmoothTime = .3f;
        [Range(60, 180)] public float cameraMaxTurnSpeed = 135;
        [Range(60, 360)] public float cameraMaxTurnAcceleration = 240;
        [Header("Power-ups (per level)")]
        [Range(1, 12)] public int maxUpgradeLevel = 8;
        [Min(0)] public float speedUpgrade = .08f;
        [Min(0)] public float powerUpgrade = .18f;
        [Min(0)] public float radiusUpgrade = .07f;
        [Min(0)] public float rapidUpgrade = .16f;
        [Min(0)] public float handlingUpgrade = .06f;
        [Header("Special items")]
        [Range(.3f, .9f)] public float buildingRewardThreshold = .6f;
        [Min(.5f)] public float megaBeamSeconds = 2;
        [Min(10)] public float megaBeamLength = 70;
        [Min(1)] public float megaBeamRadius = 5;
        [Min(1)] public float missileSeconds = 6;
        [Min(1)] public float magnetSeconds = 8;
        [Min(3)] public float magnetRadius = 22;
        [Min(1)] public float magnetPullSpeed = 24;
        [Min(1)] public float bulldozerSeconds = 8;
        [Range(1.5f, 3)] public float bulldozerScale = 2.3f;
        [Min(.5f)] public float gigantBombFuse = 3;
        [Min(10)] public float gigantBombRadius = 30;
        [Min(1)] public float highSpeedSeconds = 6;
        [Range(1.2f, 4)] public float highSpeedMultiplier = 2.2f;
    }
}
