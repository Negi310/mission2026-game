using UnityEngine;

namespace TankTown
{
    [CreateAssetMenu(menuName = "Tank Town/Settings")]
    public sealed class TankTownSettings : ScriptableObject
    {
        [Min(1)] public float roundSeconds = 60;
        [Min(0)] public float respawnPenalty = 3;
        [Min(0.1f)] public float respawnDelay = .45f;
        [Header("Tank crash")]
        [Min(1)] public float crashBlastRadius = 10;
        [Min(0)] public float crashBlastImpulseMultiplier = 1.8f;
        [Header("Pickup feedback")]
        [Min(.1f)] public float pickupFlyTime = .14f;
        [Min(.5f)] public float pickupDisplayDuration = 2.2f;
        [Min(.5f)] public float pickupIconSize = 1.75f;
        [Min(1)] public float pickupIconHeight = 2.1f;
        [Min(.1f)] public float pickupIconSpacing = 1.25f;
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
        [Header("Imported models")]
        public bool importedTankModel = true;
        public bool importedCityModels = true;
        [Header("Cannon")]
        [Range(0, 10)] public float cannonElevation = 4;
        [Min(10)] public float shellSpeed = 120;
        [Min(.1f)] public float shellVisualScale = 3;
        [Min(0)] public float shotCooldown = 0.35f;
        [Min(1)] public float blastRadius = 8;
        [Min(0)] public float blastImpulse = 12;
        [Min(0)] public float blastBreakRadius = 3;
        [Min(0)] public float blastDamageMultiplier = 2.4f;
        [Header("Building durability")]
        [Min(1)] public float buildingDurability = 3;
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
        public bool randomizeCitySeed = true;
        [Header("City")]
        [Range(2, 10)] public int cityHalfBlocks = 3;
        [Min(0)] public int cityParks = 3;
        [Min(24)] public float blockSpacing = 40;
        [Min(10)] public float buildingWidth = 20;
        [Min(8)] public float roadWidth = 16;
        [Range(.6f, 3)] public float pillarWidth = 1.8f;
        [Range(0, 1)] public float stabilityMargin = .25f;
        [Min(2)] public float floorHeight = 3.6f;
        [Range(0, 5)] public float terraceHeight = 3.5f;
        [Header("City traffic")]
        [Range(6, 40)] public int trafficCarsPerCircuit = 24;
        [Range(1, 12)] public int pedestriansPerBlock = 2;
        [Range(.5f, 2)] public float pedestrianHitRadius = .95f;
        [Min(1)] public float carExplosionRadius = 10;
        [Min(1)] public float carExplosionImpulseMultiplier = 1.6f;
        [Header("Effects")]
        [Range(1, 2.5f)] public float explosionVisualScale = 1.55f;
        [Range(.25f, 1.5f)] public float chainExplosionVisualScale = .65f;
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
        [Min(4)] public float specialAreaMinRadius = 7;
        [Range(.4f, 1)] public float specialAreaWidthRatio = .65f;
        [Min(1)] public float specialAreaLightHeight = 5.5f;
        [Min(.5f)] public float megaBeamSeconds = 2;
        [Min(10)] public float megaBeamLength = 400;
        [Min(1)] public float megaBeamRadius = 9;
        [Range(0, 10)] public float megaBeamElevation = 1;
        public Vector3 megaBeamCameraOffset = new Vector3(9, 10, -15);
        [Min(10)] public float megaBeamCameraLookAhead = 40;
        [Range(.05f, 1)] public float megaBeamTurnMultiplier = .18f;
        [Min(1)] public float missileSeconds = 6;
        [Min(10)] public float missileTargetRange = 90;
        [Min(10)] public float missileSpeed = 64;
        [Min(90)] public float missileTurnSpeed = 330;
        [Min(1)] public float missileChainPowerMultiplier = 2.8f;
        [Min(1)] public float magnetSeconds = 8;
        [Min(3)] public float magnetRadius = 50;
        [Min(1)] public float magnetPullSpeed = 120;
        [Min(1)] public float bulldozerSeconds = 8;
        [Range(1.5f, 3)] public float bulldozerScale = 2.3f;
        [Range(.5f, 1)] public float bulldozerSpeedMultiplier = .9f;
        [Min(.5f)] public float gigantBombFuse = 6;
        [Min(10)] public float gigantBombThrowSpeed = 90;
        [Min(0)] public float gigantBombUpSpeed = 6;
        [Min(10)] public float gigantBombRadius = 60;
        [Min(1)] public float gigantBombImpulseMultiplier = 12;
        [Min(1)] public float highSpeedSeconds = 6;
        [Range(1.2f, 4)] public float highSpeedMultiplier = 2.2f;
    }
}
