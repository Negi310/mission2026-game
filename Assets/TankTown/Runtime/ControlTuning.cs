using System;
using UnityEngine;

namespace TankTown
{
    [Serializable]
    internal sealed class ControlTuning
    {
        internal const string Key = "TankTown.ControlTuning.v1";
        public float speed, acceleration, turn, response, grip, hullDelay, turretYaw, turretSpeed, cooldown;
        public float center, sensitivity, deadzone, strikeCooldown;
        public int threshold, release;
        public bool invert;
        public float menuSteeringThreshold = .72f, menuNeutralThreshold = .32f;
        public float menuHoldDelay = .65f, menuRepeatInterval = .4f;
        internal static ControlTuning Capture(TankTownSettings s, MicrobitInputSettings h) => new ControlTuning
        {
            speed = s.driveSpeed, acceleration = s.acceleration, turn = s.turnSpeed, response = s.steeringResponse,
            grip = s.tireGrip, hullDelay = s.hullTurnDelay, turretYaw = s.turretMaxYaw, turretSpeed = s.turretTurnSpeed,
            cooldown = s.shotCooldown, center = h.angleCenter, sensitivity = h.fullSteerAngle, deadzone = h.deadzoneDegrees,
            threshold = h.piezoThreshold, release = h.piezoRelease, invert = h.invertSteering, strikeCooldown = h.piezoCooldownSeconds,
            menuSteeringThreshold = s.MenuSteeringThreshold, menuNeutralThreshold = s.MenuNeutralThreshold,
            menuHoldDelay = s.MenuHoldDelay, menuRepeatInterval = s.MenuRepeatInterval
        };
        internal bool Valid() => Finite(speed, 1, 50) && Finite(acceleration, .1f, 50) && Finite(turn, 10, 240) &&
            Finite(response, .1f, 40) && Finite(grip, .1f, 30) && Finite(hullDelay, 0, .5f) &&
            Finite(turretYaw, 0, 60) && Finite(turretSpeed, 30, 500) && Finite(cooldown, .12f, 2) &&
            Finite(center, -1023, 1023) && Finite(sensitivity, .1f, 1023) && Finite(deadzone, 0, sensitivity - .01f) && Finite(strikeCooldown, 0, 2) &&
            threshold >= 1 && threshold <= 1023 && release >= 0 && release < threshold &&
            Finite(menuSteeringThreshold, .05f, .99f) && Finite(menuNeutralThreshold, 0, menuSteeringThreshold - .01f) &&
            Finite(menuHoldDelay, .05f, float.MaxValue) && Finite(menuRepeatInterval, .05f, float.MaxValue);
        private static bool Finite(float value, float min, float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
        internal void Apply(TankTownSettings s, MicrobitInputSettings h)
        {
            if (!Valid()) throw new ArgumentException("設定値が範囲外です");
            s.driveSpeed = speed; s.acceleration = acceleration; s.turnSpeed = turn; s.steeringResponse = response;
            s.tireGrip = grip; s.hullTurnDelay = hullDelay; s.turretMaxYaw = turretYaw; s.turretTurnSpeed = turretSpeed; s.shotCooldown = cooldown;
            s.menuSteeringThreshold = menuSteeringThreshold; s.menuNeutralThreshold = menuNeutralThreshold;
            s.menuHoldDelay = menuHoldDelay; s.menuRepeatInterval = menuRepeatInterval;
            ApplyHardware(h);
        }
        internal void ApplyHardware(MicrobitInputSettings h)
        {
            if (!Valid()) throw new ArgumentException("設定値が範囲外です");
            h.angleCenter = center; h.fullSteerAngle = sensitivity; h.deadzoneDegrees = deadzone;
            h.piezoThreshold = threshold; h.piezoRelease = release; h.invertSteering = invert; h.piezoCooldownSeconds = strikeCooldown;
        }
        internal void Save(string key = Key) { PlayerPrefs.SetString(key, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
        internal static ControlTuning Load(string key = Key)
        {
            try
            {
                string json = PlayerPrefs.GetString(key, "");
                if (string.IsNullOrEmpty(json)) return null;
                // Preserve existing controller calibration when an older save has no menu fields.
                var value = new ControlTuning();
                JsonUtility.FromJsonOverwrite(json, value);
                return value.Valid() ? value : null;
            }
            catch (ArgumentException) { return null; }
        }
    }
}
