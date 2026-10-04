using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownMobilityTests
    {
        private TankTownGame game;
        private float captureStep;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            captureStep = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1; Time.captureDeltaTime = captureStep; yield return null; }
        private void IsolateTargets()
        {
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
        }
        internal static TownTrafficCar Car(TankTownGame game, Vector3 start, Vector3 finish)
        {
            var root = game.World.Box("Moving missile test car", Vector3.zero, new Vector3(1.8f, 1.8f, 3.3f), game.World.ShotMaterial);
            var block = root.AddComponent<DestructibleBlock>(); block.CanCrush = true; block.Initialize(game);
            var explosive = root.AddComponent<ChainExplosive>(); explosive.Initialize(game, game.Settings.carExplosionRadius, vehicle: true); game.World.Explosives.Add(explosive);
            var car = root.AddComponent<TownTrafficCar>(); car.Initialize(game, new[] { start, finish }, 0, 11); game.World.TrafficCars.Add(car);
            return car;
        }
        [UnityTest]
        public IEnumerator PedestriansWalkOnSidewalksPauseAndRecoverFromABlastWithoutAddingScore()
        {
            Assert.That(game.World.Pedestrians.Count, Is.EqualTo(72));
            var person = game.World.Pedestrians[0]; var start = person.transform.position;
            yield return new WaitForSeconds(.4f);
            Assert.That(Vector3.Distance(start, person.transform.position), Is.GreaterThan(.3f));
            Assert.That(person.transform.position.y, Is.EqualTo(game.World.Terrain.HeightAt(person.transform.position) + .04f).Within(.01f));
            game.TogglePause(); var paused = person.transform.position;
            yield return new WaitForSecondsRealtime(.1f); Assert.That(person.transform.position, Is.EqualTo(paused)); game.TogglePause();
            person.transform.position = game.World.GroundPoint(new Vector3(0, 0, -60), .04f); Physics.SyncTransforms();
            int score = game.BrokenBlocks;
            game.Blast(person.transform.position + Vector3.up * 1.2f, .7f, 12);
            Assert.That(person.Walking, Is.False); Assert.That(person.Knockbacks, Is.EqualTo(1));
            Assert.That(game.BrokenBlocks, Is.EqualTo(score));
            yield return new WaitForSeconds(2.3f);
            Assert.That(person.Walking, Is.True); Assert.That(person.gameObject.activeSelf, Is.True);
        }
        [UnityTest]
        public IEnumerator MissilesFollowAnActualMovingCarRetargetAndMakeTrackedContact()
        {
            IsolateTargets();
            game.Tank.GetComponent<Rigidbody>().isKinematic = false;
            game.Tank.Warp(new Pose(new Vector3(0, 0, -112), Quaternion.identity));
            game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            var first = Car(game, new Vector3(0, 0, -86), new Vector3(0, 0, -50));
            var second = Car(game, new Vector3(0, 0, -38), new Vector3(0, 0, -10)); Physics.SyncTransforms();
            game.Items.Receive(ItemKind.Missiles); game.Input.PulsePad(0);
            var rocket = Object.FindObjectsByType<ItemMissile>(FindObjectsSortMode.None).First(x => x.TrackingTarget == first.GetComponent<TownMovingTarget>());
            Vector3 aim = rocket.AimPoint;
            yield return new WaitForSeconds(.3f);
            Assert.That(rocket.AimPoint.z, Is.GreaterThan(aim.z + .2f), "The missile must update the moving car's position");
            int retargets = rocket.Retargets; first.gameObject.SetActive(false); yield return null; yield return null;
            Assert.That(rocket.TrackingTarget, Is.EqualTo(second.GetComponent<TownMovingTarget>())); Assert.That(rocket.Retargets, Is.GreaterThan(retargets));
            for (int i = 0; i < 160 && game.Items.MissileTrackedStrikes == 0; i++) yield return new WaitForFixedUpdate();
            Assert.That(game.Items.MissileTrackedStrikes, Is.GreaterThan(0), "A guided missile must physically contact the moving target");
            Assert.That(second.GetComponent<DestructibleBlock>().Detached, Is.True);
            game.AdvanceTime(1000); yield return null;
            Assert.That(Object.FindObjectsByType<ItemMissile>(FindObjectsSortMode.None), Is.Empty);
        }
        [UnityTest]
        public IEnumerator ModeratedCarExplosionThrowsNearbyObjectsButLeavesDistantOnesIntact()
        {
            IsolateTargets(); var car = Car(game, new Vector3(180, 0, -70), new Vector3(180, 0, -30));
            Vector3 point = car.transform.position;
            var root = game.World.Box("Nearby car explosion target", point + Vector3.right * 9, Vector3.one, game.World.ShotMaterial);
            var target = root.AddComponent<DestructibleBlock>(); target.Initialize(game); Physics.SyncTransforms();
            var remote = game.World.Box("Distant car explosion target", point - Vector3.right * 12, Vector3.one, game.World.ShotMaterial);
            var distant = remote.AddComponent<DestructibleBlock>(); distant.Initialize(game); Physics.SyncTransforms();
            game.Blast(point); Assert.That(target.Detached, Is.False);
            yield return new WaitForSeconds(.35f);
            Assert.That(game.ChainExplosions, Is.EqualTo(1)); Assert.That(target.Detached, Is.True);
            Assert.That(distant.Detached, Is.False, "The former 14m car blast must be reduced");
            Assert.That(root.GetComponent<Rigidbody>().linearVelocity.magnitude, Is.GreaterThan(4));
        }
        [UnityTest]
        public IEnumerator LaserReachesWideDistantTargetsAndTurnsMoreSlowlyThanNormal()
        {
            IsolateTargets(); var body = game.Tank.GetComponent<Rigidbody>(); body.isKinematic = false; game.Tank.enabled = true;
            game.Tank.Warp(new Pose(game.World.SpawnPosition, Quaternion.identity)); game.Input.SetWheel(1);
            yield return new WaitForSeconds(.15f);
            float normal = Mathf.Abs(Mathf.DeltaAngle(0, game.Tank.Turret.localEulerAngles.y));
            game.Tank.Warp(new Pose(game.World.SpawnPosition, Quaternion.identity)); game.Input.SetWheel(1);
            var point = game.Tank.Muzzle.position + Vector3.forward * 80 + Vector3.right * 7;
            var root = game.World.Box("Wide distant laser target", point, Vector3.one, game.World.ShotMaterial);
            var target = root.AddComponent<DestructibleBlock>(); target.Initialize(game); Physics.SyncTransforms();
            game.Items.Receive(ItemKind.MegaBeam); game.Input.PulsePad(0); var start = body.position;
            yield return new WaitForSeconds(.15f);
            float laser = Mathf.Abs(Mathf.DeltaAngle(0, game.Tank.Turret.localEulerAngles.y));
            Assert.That(target.Detached, Is.True, "The new laser must reach outside the old 70m length and 5m radius");
            Assert.That(laser, Is.InRange(1, normal * .5f));
            Assert.That(Vector2.Distance(new Vector2(start.x, start.z), new Vector2(body.position.x, body.position.z)), Is.LessThan(.1f));
            Assert.That(Vector3.Angle(game.CameraRig.transform.up, Vector3.up), Is.LessThan(.01f));
            game.AdvanceTime(1000); yield return null;
            Assert.That(GameObject.Find("Mega beam"), Is.Null);
        }
    }
}
