using NUnit.Framework;
using UnityEngine;

namespace TankTown.Tests
{
    public class ControlTuningTests
    {
        [Test] public void SavedControlsRestoreAndApplyToStatsAndHardware()
        {
            string key = "TankTown.Test." + System.Guid.NewGuid();
            var settings = ScriptableObject.CreateInstance<TankTownSettings>();
            try
            {
                var hardware = new MicrobitInputSettings();
                var tuning = ControlTuning.Capture(settings, hardware);
                tuning.speed = 24; tuning.turn = 120; tuning.threshold = 600; tuning.release = 200; tuning.invert = true; tuning.strikeCooldown = .2f;
                tuning.menuSteeringThreshold = .85f; tuning.menuNeutralThreshold = .2f;
                tuning.menuHoldDelay = 1.1f; tuning.menuRepeatInterval = .55f;
                tuning.Save(key);
                var restored = ControlTuning.Load(key);
                Assert.That(restored, Is.Not.Null);
                restored.Apply(settings, hardware);
                var stats = new TankStats(settings);
                Assert.That(stats.Speed, Is.EqualTo(24)); Assert.That(stats.TurnSpeed, Is.EqualTo(120));
                Assert.That(hardware.piezoThreshold, Is.EqualTo(600)); Assert.That(hardware.piezoRelease, Is.EqualTo(200));
                Assert.That(hardware.invertSteering, Is.True);
                Assert.That(hardware.piezoCooldownSeconds, Is.EqualTo(.2f).Within(.0001f));
                Assert.That(settings.menuSteeringThreshold, Is.EqualTo(.85f));
                Assert.That(settings.menuNeutralThreshold, Is.EqualTo(.2f));
                Assert.That(settings.menuHoldDelay, Is.EqualTo(1.1f));
                Assert.That(settings.menuRepeatInterval, Is.EqualTo(.55f));
                stats.Upgrade(UpgradeKind.Speed); Assert.That(stats.Speed, Is.GreaterThan(24));
                PlayerPrefs.SetString(key, "{\"speed\":-3}");
                Assert.That(ControlTuning.Load(key), Is.Null);
            }
            finally { PlayerPrefs.DeleteKey(key); Object.DestroyImmediate(settings); }
        }

        [Test] public void LegacySaveKeepsControllerCalibrationAndAddsMenuDefaults()
        {
            string key = "TankTown.Test." + System.Guid.NewGuid();
            var settings = ScriptableObject.CreateInstance<TankTownSettings>();
            try
            {
                var tuning = ControlTuning.Capture(settings, new MicrobitInputSettings());
                tuning.speed = 24; tuning.threshold = 620; tuning.center = -53; tuning.invert = true;
                // Serialize only the fields that existed before menu tuning was added.
                string legacy = System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(tuning),
                    ",\"menu(?:SteeringThreshold|NeutralThreshold|HoldDelay|RepeatInterval)\":[^,}]+", "");
                PlayerPrefs.SetString(key, legacy);
                var restored = ControlTuning.Load(key);
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.speed, Is.EqualTo(24));
                Assert.That(restored.threshold, Is.EqualTo(620));
                Assert.That(restored.center, Is.EqualTo(-53));
                Assert.That(restored.invert, Is.True);
                Assert.That(restored.menuSteeringThreshold, Is.EqualTo(.72f));
                Assert.That(restored.menuNeutralThreshold, Is.EqualTo(.32f));
                Assert.That(restored.menuHoldDelay, Is.EqualTo(.65f));
                Assert.That(restored.menuRepeatInterval, Is.EqualTo(.4f));
            }
            finally { PlayerPrefs.DeleteKey(key); Object.DestroyImmediate(settings); }
        }

        [TestCase(float.NaN, .2f, .65f, .4f)]
        [TestCase(.5f, .5f, .65f, .4f)]
        [TestCase(.72f, .32f, 0, .4f)]
        [TestCase(.72f, .32f, .65f, float.PositiveInfinity)]
        public void InvalidMenuTuningCannotBeApplied(float threshold, float neutral, float hold, float repeat)
        {
            var settings = ScriptableObject.CreateInstance<TankTownSettings>();
            try
            {
                var hardware = new MicrobitInputSettings();
                var tuning = ControlTuning.Capture(settings, hardware);
                tuning.menuSteeringThreshold = threshold; tuning.menuNeutralThreshold = neutral;
                tuning.menuHoldDelay = hold; tuning.menuRepeatInterval = repeat;
                Assert.Throws<System.ArgumentException>(() => tuning.Apply(settings, hardware));
                Assert.That(settings.menuSteeringThreshold, Is.EqualTo(.72f));
            }
            finally { Object.DestroyImmediate(settings); }
        }
    }
}
