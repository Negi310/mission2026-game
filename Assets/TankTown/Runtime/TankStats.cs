using System;
using UnityEngine;

namespace TankTown
{
    public enum UpgradeKind { Speed, Power, Rapid, Handling }

    // Round-scoped upgrades survive a crash; a new scene starts at level zero.
    public sealed class TankStats
    {
        private readonly TankTownSettings settings;
        private readonly int[] levels = new int[4];
        public event Action<UpgradeKind> Changed;
        public TankStats(TankTownSettings settings) { this.settings = settings; }
        public int Level(UpgradeKind kind) => levels[(int)kind];
        public float Speed => settings.driveSpeed * (1 + Level(UpgradeKind.Speed) * settings.speedUpgrade);
        public float TurnSpeed => settings.turnSpeed * (1 + Level(UpgradeKind.Handling) * settings.handlingUpgrade);
        public float BlastImpulse => settings.blastImpulse * (1 + Level(UpgradeKind.Power) * settings.powerUpgrade);
        public float BlastRadius => settings.blastRadius * (1 + Level(UpgradeKind.Power) * settings.radiusUpgrade);
        public float ShotCooldown => Mathf.Max(.12f, settings.shotCooldown / (1 + Level(UpgradeKind.Rapid) * settings.rapidUpgrade));
        public bool Upgrade(UpgradeKind kind)
        {
            if (levels[(int)kind] >= settings.maxUpgradeLevel) return false;
            levels[(int)kind]++;
            Changed?.Invoke(kind);
            return true;
        }
        public static string Label(UpgradeKind kind) => kind == UpgradeKind.Speed ? "スピード" : kind == UpgradeKind.Power ? "破壊力" : kind == UpgradeKind.Rapid ? "連射" : "旋回";
        public static Color Color(UpgradeKind kind) => kind == UpgradeKind.Speed ? new Color(.15f, .86f, 1) : kind == UpgradeKind.Power ? new Color(1, .28f, .16f) : kind == UpgradeKind.Rapid ? new Color(1, .78f, .06f) : new Color(.73f, .36f, 1);
    }

    public static class TownPhysics
    {
        public const int TankLayer = 2;
        public const int DebrisLayer = 8;
        public const int StructureMask = ~((1 << TankLayer) | (1 << DebrisLayer));
    }
}
