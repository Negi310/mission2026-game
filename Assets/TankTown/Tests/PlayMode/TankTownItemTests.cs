using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownItemTests
    {
        private TankTownGame game;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart();
            game.Input.useHardware = true; game.Input.SetWheel(0);
            // Isolate each ability from unrelated automatic pickups along the test route.
            foreach (var item in game.World.ItemPickups) item.gameObject.SetActive(false);
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1; yield return null; }
        private void Place(Vector3 point, float yaw = 0)
        {
            var body = game.Tank.GetComponent<Rigidbody>();
            body.position = game.World.GroundPoint(point, game.Settings.rideHeight * game.Tank.VehicleScale);
            body.rotation = Quaternion.Euler(0, yaw, 0); body.linearVelocity = Vector3.zero;
            game.Tank.transform.SetPositionAndRotation(body.position, body.rotation); Physics.SyncTransforms(); game.CameraRig.Snap();
        }
        private void FaceBuilding(float distance = 16)
        {
            var building = game.World.Buildings[0];
            Place(building.transform.position + new Vector3(-4.6f, 0, -building.Width / 2 - distance));
        }
        [UnityTest]
        public IEnumerator TurretLeadsHullAndActualShotUsesItsHeading()
        {
            var body = game.Tank.GetComponent<Rigidbody>();
            var alignment = game.CameraRig.gameObject.AddComponent<TankCameraMotionProbe>();
            alignment.Turret = game.Tank.Turret;
            game.Input.SetWheel(1); yield return null;
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, body.rotation.eulerAngles.y)), Is.LessThan(.4f), "The hull must wait while the turret turns first");
            yield return null; // Observe the interpolated turret after its rendered-frame update.
            Assert.That(game.Tank.Turret.localEulerAngles.y, Is.GreaterThan(8));
            Assert.That(Vector3.Angle(game.CameraRig.transform.forward, Vector3.ProjectOnPlane(game.Tank.Turret.forward, Vector3.up)), Is.GreaterThan(1), "The camera must ease into the turret lead rather than copy its sudden turn");
            var origin = game.Tank.Muzzle.position; var direction = game.Tank.Muzzle.forward;
            game.Input.PulsePad(0);
            var shot = Object.FindFirstObjectByType<TankShell>(); Assert.That(shot, Is.Not.Null);
            yield return null;
            Assert.That(Vector3.Angle(shot.transform.position - origin, direction), Is.LessThan(.1f));
            for (int i = 0; i < 9; i++) yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, body.rotation.eulerAngles.y)), Is.GreaterThan(8), "The hull must respond within about a quarter second");
            for (int i = 0; i < 16; i++) yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, body.rotation.eulerAngles.y)), Is.GreaterThan(10));
            Assert.That(game.Tank.Turret.localEulerAngles.y, Is.EqualTo(game.Settings.turretMaxYaw).Within(.1f));
            yield return new WaitForSeconds(.15f);
            Assert.That(Vector3.Angle(game.CameraRig.transform.forward, Vector3.ProjectOnPlane(game.Tank.Turret.forward, Vector3.up)), Is.LessThan(45));
            game.Input.SetWheel(0); yield return null;
            for (int i = 0; i < 13; i++) yield return new WaitForFixedUpdate();
            float settledHull = body.rotation.eulerAngles.y;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(settledHull, body.rotation.eulerAngles.y)), Is.LessThan(.4f), "The stronger response must also stop turning promptly after release");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, game.Tank.Turret.localEulerAngles.y)), Is.LessThan(.1f));
            game.Input.SetWheel(-1); yield return new WaitForSeconds(.2f);
            game.Input.SetWheel(1); yield return new WaitForSeconds(.2f);
            Assert.That(alignment.Samples, Is.GreaterThan(10));
            Assert.That(alignment.MaxHorizonAngle, Is.LessThan(.01f), "Terrain pitch and body lean must not tilt the camera");
            Assert.That(alignment.MaxRotationSpeed, Is.LessThanOrEqualTo(136));
            Assert.That(alignment.MaxRotationAcceleration, Is.LessThan(250));
        }
        [UnityTest]
        public IEnumerator CameraTurnsSmoothlyBetweenPhysicsSteps()
        {
            float fixedStep = Time.fixedDeltaTime, captureStep = Time.captureDeltaTime;
            try
            {
                // Exaggerate the real mismatch: 120 rendered frames, 12.5 physics steps.
                Time.fixedDeltaTime = .08f; Time.captureDeltaTime = 1f / 120;
                yield return new WaitForSeconds(.16f);
                var alignment = game.CameraRig.gameObject.AddComponent<TankCameraMotionProbe>();
                alignment.Turret = game.Tank.Turret;
                game.Input.SetWheel(1); yield return new WaitForSeconds(.36f);
                game.Input.SetWheel(-1); yield return new WaitForSeconds(.48f);
                Debug.Log($"CAMERA MOTION TEST: samples={alignment.Samples}, maxFrameStep={alignment.MaxFrameStep:0.000}, maxRotationSpeed={alignment.MaxRotationSpeed:0.000}, maxAcceleration={alignment.MaxRotationAcceleration:0.000}, horizonAngle={alignment.MaxHorizonAngle:0.000000}");
                Assert.That(alignment.Samples, Is.GreaterThan(50));
                Assert.That(alignment.MaxFrameStep, Is.GreaterThan(.1f), "The camera must actually turn during the check");
                Assert.That(alignment.MaxHorizonAngle, Is.LessThan(.01f));
                Assert.That(alignment.MaxRotationSpeed, Is.LessThanOrEqualTo(136));
                Assert.That(alignment.MaxRotationAcceleration, Is.LessThan(250), "Direction reversals must also ease their change in angular speed");
                game.Input.SetWheel(0); yield return new WaitForSeconds(2);
                Assert.That(Vector3.Angle(game.CameraRig.transform.forward, Vector3.ProjectOnPlane(game.Tank.Turret.forward, Vector3.up)), Is.LessThan(1));
            }
            finally { Time.fixedDeltaTime = fixedStep; Time.captureDeltaTime = captureStep; }
        }

        [UnityTest]
        public IEnumerator FieldHasSixTypesAndWeaponPickupUsesTheNextPressurePulse()
        {
            for (int i = 0; i < 6; i++) Assert.That(game.World.ItemPickups.Exists(x => x.Kind == (ItemKind)i), Is.True);
            var item = game.World.ItemPickups.Find(x => x.Kind == ItemKind.MegaBeam);
            item.gameObject.SetActive(true);
            item.transform.position = game.World.GroundPoint(game.Tank.transform.position + Vector3.forward * 3.2f, 1.2f); Physics.SyncTransforms();
            for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
            Assert.That(item.Collected, Is.True); Assert.That(game.Items.Held, Is.EqualTo(ItemKind.MegaBeam));
            Assert.That(game.Items.Weapon, Is.EqualTo(ItemKind.None));
            Assert.That(game.Items.Receive(ItemKind.Missiles), Is.False, "A full weapon slot must leave the next weapon available");
            Assert.That(game.Items.Receive(ItemKind.Magnet), Is.True, "Automatic items must not overwrite the held weapon");
            Assert.That(game.Items.Held, Is.EqualTo(ItemKind.MegaBeam));
            game.Input.PulsePad(1);
            Assert.That(game.Items.Held, Is.EqualTo(ItemKind.None)); Assert.That(game.Items.Weapon, Is.EqualTo(ItemKind.MegaBeam));
        }
        [UnityTest]
        public IEnumerator MegaBeamBurnsStructuresWhileForwardMovementStopsAndThenResumes()
        {
            FaceBuilding(); var body = game.Tank.GetComponent<Rigidbody>(); var origin = body.position;
            game.Items.Receive(ItemKind.MegaBeam); game.Input.PulsePad(0);
            Assert.That(game.Items.StopsForward, Is.True);
            for (int i = 0; i < 45; i++) yield return new WaitForFixedUpdate();
            Assert.That(Vector2.Distance(new Vector2(origin.x, origin.z), new Vector2(body.position.x, body.position.z)), Is.LessThan(.15f));
            Assert.That(game.Tank.ActualSpeed, Is.LessThan(.1f)); Assert.That(game.Items.BeamHits, Is.GreaterThan(0));
            Assert.That(game.BrokenBlocks, Is.GreaterThan(0));
            game.Input.PulsePad(0); Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Null);
            for (int i = 0; i < 155; i++) yield return new WaitForFixedUpdate();
            Assert.That(game.Items.StopsForward, Is.False);
            Assert.That(game.Tank.ActualSpeed, Is.GreaterThan(1));
        }
        [UnityTest]
        public IEnumerator FiveMissilesRiseAndContinueBombardingAWhileDriving()
        {
            FaceBuilding(); var body = game.Tank.GetComponent<Rigidbody>(); var origin = body.position;
            game.Items.Receive(ItemKind.Missiles); game.Input.PulsePad(0);
            var rockets = Object.FindObjectsByType<ItemMissile>(FindObjectsSortMode.None); Assert.That(rockets.Length, Is.EqualTo(5));
            var launch = rockets[0].transform.position;
            yield return new WaitForSeconds(.35f);
            Assert.That(rockets[0].transform.position.y, Is.GreaterThan(launch.y + 3));
            yield return new WaitForSeconds(3.6f);
            Assert.That(game.Items.MissileStrikes, Is.GreaterThanOrEqualTo(10));
            Assert.That(game.BrokenBlocks, Is.GreaterThan(0));
            Assert.That(Vector3.Distance(body.position, origin), Is.GreaterThan(10));
            Assert.That(game.Items.Weapon, Is.EqualTo(ItemKind.Missiles));
        }
        [UnityTest]
        public IEnumerator MagnetPullsAndCollectsOnlyNearbyUpgrades()
        {
            Place(game.World.SpawnPosition + Vector3.right * 6);
            var pickup = game.World.Pickups.Find(x => x.Kind == UpgradeKind.Speed && Vector3.Distance(x.transform.position, game.Tank.transform.position) < 15);
            Assert.That(pickup, Is.Not.Null);
            var far = game.World.Pickups.Find(x => Vector3.Distance(x.transform.position, game.Tank.transform.position) > 60);
            var farPosition = far.transform.position; int before = game.Stats.Level(UpgradeKind.Speed);
            game.Items.Receive(ItemKind.Magnet);
            Assert.That(game.Items.Held, Is.EqualTo(ItemKind.None));
            yield return new WaitForSeconds(.7f);
            Assert.That(pickup.Collected, Is.True); Assert.That(game.Stats.Level(UpgradeKind.Speed), Is.GreaterThan(before));
            Assert.That(far.Collected, Is.False); Assert.That(Vector3.Distance(far.transform.position, farPosition), Is.LessThan(.01f));
        }
        [UnityTest]
        public IEnumerator GiantBulldozerChangesItsColliderAndRamsWithoutExploding()
        {
            FaceBuilding(12); game.Items.Receive(ItemKind.Bulldozer);
            Assert.That(game.Tank.VehicleScale, Is.EqualTo(game.Settings.bulldozerScale));
            Assert.That(game.Tank.HullCollider.size.x, Is.GreaterThan(8));
            Assert.That(game.Tank.Visual.Find("Bulldozer blade and cabin").gameObject.activeSelf, Is.True);
            yield return new WaitForSeconds(3);
            Assert.That(game.Crashes, Is.Zero); Assert.That(game.Tank.Respawning, Is.False); Assert.That(game.BrokenBlocks, Is.GreaterThan(5));
            game.AdvanceTime(1000); yield return null;
            Assert.That(game.Tank.VehicleScale, Is.EqualTo(1)); Assert.That(game.Tank.HullCollider.size.x, Is.EqualTo(2.6f));
        }
        [UnityTest]
        public IEnumerator GigantBombCrushesBeforeItsFuseAndThenBlastsDistantObjects()
        {
            FaceBuilding(12); game.Items.Receive(ItemKind.GigantBomb); game.Input.PulsePad(0);
            var bomb = Object.FindFirstObjectByType<GigantBomb>(); Assert.That(bomb, Is.Not.Null);
            var body = bomb.GetComponent<Rigidbody>(); Assert.That(body.isKinematic, Is.False); Assert.That(body.linearVelocity.y, Is.GreaterThan(5));
            yield return new WaitForSeconds(1.1f);
            Assert.That(bomb.Contacts, Is.GreaterThan(0)); Assert.That(bomb.Detonated, Is.False);
            Assert.That(game.BrokenBlocks, Is.GreaterThan(0), "Physical bomb contact must destroy structures before detonation");
            var box = game.World.Box("Distant bomb witness", game.World.GroundPoint(body.position + Vector3.right * 22, .6f), Vector3.one * 1.2f, game.World.ShotMaterial);
            var witness = box.AddComponent<DestructibleBlock>(); witness.CanCrush = true; witness.Initialize(game);
            body.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(game.Items.Remaining(ItemKind.GigantBomb) + .15f);
            Assert.That(witness.Detached, Is.True, "The delayed explosion must reach beyond the physical impact radius");
            Assert.That(game.Items.Weapon, Is.EqualTo(ItemKind.None)); Assert.That(bomb == null, Is.True);
        }
        [UnityTest]
        public IEnumerator HighSpeedIsAutomaticInvincibleAndProtectedAtTheFieldEdge()
        {
            FaceBuilding(12); game.Items.Receive(ItemKind.HighSpeed);
            Assert.That(game.Items.Invincible, Is.True); Assert.That(game.Items.Held, Is.EqualTo(ItemKind.None));
            yield return new WaitForSeconds(1.6f);
            Assert.That(game.Tank.ActualSpeed, Is.GreaterThan(game.Stats.Speed * 1.7f)); Assert.That(game.Crashes, Is.Zero);
            Place(new Vector3(game.World.HalfExtent - .7f, 0, 0), 90);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(game.Tank.Relocating, Is.True, "The protected return must cover the view before changing its position");
            yield return new WaitForSeconds(.25f);
            Assert.That(game.Crashes, Is.Zero);
            Assert.That(Mathf.Abs(game.Tank.GetComponent<Rigidbody>().position.x), Is.LessThan(game.World.HalfExtent - 6));
        }
        [UnityTest]
        public IEnumerator AutomaticEffectsExpireIndependentlyAndBeamOverridesTheSpeedBoost()
        {
            game.Items.Receive(ItemKind.Magnet); game.Items.Receive(ItemKind.Bulldozer); game.Items.Receive(ItemKind.HighSpeed);
            game.Items.Receive(ItemKind.MegaBeam); game.Input.PulsePad(0);
            yield return new WaitForSeconds(.25f);
            Assert.That(game.Tank.ActualSpeed, Is.LessThan(.1f), "Beam must stop even a boosted giant tank");
            Assert.That(game.Items.Invincible, Is.True);
            yield return new WaitForSeconds(3.5f);
            Assert.That(game.Items.StopsForward, Is.False); Assert.That(game.Items.Remaining(ItemKind.Bulldozer), Is.GreaterThan(3));
            yield return new WaitForSeconds(2.6f);
            Assert.That(game.Items.Remaining(ItemKind.HighSpeed), Is.Zero);
            Assert.That(game.Items.Remaining(ItemKind.Magnet), Is.GreaterThan(1)); Assert.That(game.Tank.VehicleScale, Is.EqualTo(game.Settings.bulldozerScale));
            yield return new WaitForSeconds(2);
            Assert.That(game.Items.Active, Is.EqualTo(ItemKind.None)); Assert.That(game.Items.Invincible, Is.False);
            Assert.That(game.Tank.VehicleScale, Is.EqualTo(1)); Assert.That(game.Tank.HullCollider.size.x, Is.EqualTo(2.6f));
            Assert.That(game.Tank.Visual.Find("Bulldozer blade and cabin").gameObject.activeSelf, Is.False);
            Assert.That(game.Tank.ActualSpeed, Is.LessThan(game.Stats.Speed * 1.2f));
        }
        [UnityTest]
        public IEnumerator PauseFreezesAllDurationsAndRoundEndCleansEveryEffect()
        {
            game.Items.Receive(ItemKind.Magnet); game.Items.Receive(ItemKind.Bulldozer); game.Items.Receive(ItemKind.HighSpeed);
            game.Items.Receive(ItemKind.Missiles); game.Input.PulsePad(0);
            float before = game.Items.Remaining(ItemKind.Missiles); game.TogglePause();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(game.Items.Remaining(ItemKind.Missiles), Is.EqualTo(before)); Assert.That(game.Items.Invincible, Is.True);
            game.TogglePause(); game.Items.Receive(ItemKind.GigantBomb);
            game.AdvanceTime(1000); yield return null;
            Assert.That(game.Items.Held, Is.EqualTo(ItemKind.None)); Assert.That(game.Items.Active, Is.EqualTo(ItemKind.None));
            Assert.That(game.Items.Invincible, Is.False); Assert.That(game.Tank.VehicleScale, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<ItemMissile>(FindObjectsSortMode.None), Is.Empty);
        }
    }

    [DefaultExecutionOrder(10000)]
    public sealed class TankCameraMotionProbe : MonoBehaviour
    {
        public Transform Turret;
        public int Samples { get; private set; }
        public float MaxHorizonAngle { get; private set; }
        public float MaxFrameStep { get; private set; }
        public float MaxRotationSpeed { get; private set; }
        public float MaxRotationAcceleration { get; private set; }
        private float previousYaw, previousSpeed, previousDelta;
        private void LateUpdate()
        {
            if (Turret == null) return;
            if (Samples > 0 && Time.deltaTime > 0)
            {
                float step = Mathf.DeltaAngle(previousYaw, transform.eulerAngles.y);
                float speed = step / Time.deltaTime;
                MaxFrameStep = Mathf.Max(MaxFrameStep, Mathf.Abs(step));
                MaxRotationSpeed = Mathf.Max(MaxRotationSpeed, Mathf.Abs(speed));
                // Sub-millisecond quantization and deliberate hitch limiting are not normal-rate acceleration samples.
                if (Samples > 1 && previousDelta >= 1f / 240 && previousDelta <= 1f / 30 && Time.deltaTime >= 1f / 240 && Time.deltaTime <= 1f / 30)
                    MaxRotationAcceleration = Mathf.Max(MaxRotationAcceleration, Mathf.Abs(speed - previousSpeed) / ((previousDelta + Time.deltaTime) * .5f));
                previousSpeed = speed;
            }
            previousYaw = transform.eulerAngles.y;
            previousDelta = Time.deltaTime;
            Samples++;
            MaxHorizonAngle = Mathf.Max(MaxHorizonAngle, Vector3.Angle(transform.up, Vector3.up));
        }
    }
}
