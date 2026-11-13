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
                tuning.Save(key);
                var restored = ControlTuning.Load(key);
                Assert.That(restored, Is.Not.Null);
                restored.Apply(settings, hardware);
                var stats = new TankStats(settings);
                Assert.That(stats.Speed, Is.EqualTo(24)); Assert.That(stats.TurnSpeed, Is.EqualTo(120));
                Assert.That(hardware.piezoThreshold, Is.EqualTo(600)); Assert.That(hardware.piezoRelease, Is.EqualTo(200));
                Assert.That(hardware.invertSteering, Is.True);
                Assert.That(hardware.piezoCooldownSeconds, Is.EqualTo(.2f).Within(.0001f));
                stats.Upgrade(UpgradeKind.Speed); Assert.That(stats.Speed, Is.GreaterThan(24));
                PlayerPrefs.SetString(key, "{\"speed\":-3}");
                Assert.That(ControlTuning.Load(key), Is.Null);
            }
            finally { PlayerPrefs.DeleteKey(key); Object.DestroyImmediate(settings); }
        }
    }
}
