using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownCityTests
    {
        private TankTownGame game;
        private float captureStep;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            captureStep = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart();
            game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1; Time.captureDeltaTime = captureStep; yield return null; }

        [UnityTest]
        public IEnumerator TrafficDrivesOnTheRoadAndFreezesWhenPaused()
        {
            Assert.That(game.World.TrafficCars.Count, Is.EqualTo(18));
            var starts = game.World.TrafficCars.Select(x => x.GetComponent<Rigidbody>().position).ToArray();
            yield return new WaitForSeconds(2);
            int moved = 0;
            for (int i = 0; i < starts.Length; i++)
            {
                var car = game.World.TrafficCars[i];
                Assert.That(car.GetComponent<DestructibleBlock>().Detached, Is.False, "Traffic should not explode without a shot or ram");
                var position = car.GetComponent<Rigidbody>().position;
                if (Vector3.Distance(starts[i], position) > 2) moved++;
                Assert.That(position.y, Is.EqualTo(game.World.Terrain.HeightAt(position) + .72f).Within(.2f));
            }
            Assert.That(moved, Is.GreaterThanOrEqualTo(12));
            game.TogglePause();
            var before = game.World.TrafficCars[0].GetComponent<Rigidbody>().position;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(game.World.TrafficCars[0].GetComponent<Rigidbody>().position, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator ActualCannonHitsACarAndItsExplosionIgnitesTheNextCar()
        {
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            var car = game.World.TrafficCars[0]; var next = game.World.TrafficCars[1];
            foreach (var target in new[] { car, next }) { target.gameObject.SetActive(true); target.enabled = false; }
            var point = new Vector3(0, 0, -85);
            var body = car.GetComponent<Rigidbody>(); body.position = game.World.GroundPoint(point, .72f); body.rotation = Quaternion.identity;
            var neighbour = next.GetComponent<Rigidbody>(); neighbour.position = game.World.GroundPoint(point + Vector3.forward * 9, .72f); neighbour.rotation = Quaternion.identity;
            var tankBody = game.Tank.GetComponent<Rigidbody>(); tankBody.isKinematic = false;
            game.Tank.Warp(new Pose(point - Vector3.forward * 6, Quaternion.identity)); tankBody.isKinematic = true; Physics.SyncTransforms();
            game.Input.PulsePad(0);
            Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Not.Null);
            yield return new WaitForSeconds(.12f);
            Assert.That(car.GetComponent<DestructibleBlock>().Detached, Is.True, "The real elevated shell must hit the car roof");
            Assert.That(car.GetComponents<Rigidbody>().Length, Is.EqualTo(1));
            Assert.That(body.isKinematic, Is.False);
            Assert.That(next.GetComponent<DestructibleBlock>().Detached, Is.False, "The second car is outside the cannon blast and needs the chain explosion");
            yield return new WaitForSeconds(.9f);
            Assert.That(game.ChainExplosions, Is.EqualTo(2));
            Assert.That(game.LongestChain, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator FuelIgnitionUsesSurfaceDistanceEvenWithFullyUpgradedBlastRadius()
        {
            var fuel = CreateIsolatedFuel();
            for (int i = 0; i < game.Settings.maxUpgradeLevel; i++) game.Stats.Upgrade(UpgradeKind.Power);
            var collider = fuel.GetComponent<Collider>();
            Vector3 edge = collider.ClosestPoint(fuel.transform.position + Vector3.right * 100);
            Vector3 far = edge + Vector3.right * (game.Settings.fuelIgnitionDistance + .25f);
            Assert.That(Vector3.Distance(far, edge), Is.LessThan(game.Stats.BlastRadius));
            game.Blast(far);
            Assert.That(fuel.Primed, Is.False, "The edge of an upgraded blast must not trigger a fuel tank");
            Assert.That(fuel.GetComponent<DestructibleBlock>().Detached, Is.False, "The target must remain available for a closer shot");
            yield return new WaitForSeconds(.3f);
            Assert.That(game.ChainExplosions, Is.Zero);
            game.Blast(edge + Vector3.right * (game.Settings.fuelIgnitionDistance - .25f));
            Assert.That(fuel.Primed, Is.True, "A blast just inside the surface distance must ignite");
            yield return new WaitForSeconds(.4f);
            Assert.That(game.ChainExplosions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FuelExplosionBreaksAndThrowsStructuresBeyondTheFormerRadius()
        {
            var fuel = CreateIsolatedFuel();
            var target = game.World.Box("Remote fuel blast target", fuel.transform.position + Vector3.right * 18, Vector3.one, game.World.ShotMaterial);
            var block = target.AddComponent<DestructibleBlock>(); block.Initialize(game);
            Physics.SyncTransforms();
            game.Blast(fuel.transform.position);
            Assert.That(block.Detached, Is.False, "The initial cannon blast cannot reach this target");
            yield return new WaitForSeconds(.4f);
            Assert.That(game.ChainExplosions, Is.EqualTo(1));
            Assert.That(block.Detached, Is.True, "The fuel blast must reach structures outside the old 12m radius");
            Assert.That(block.GetComponent<Rigidbody>().linearVelocity.magnitude, Is.GreaterThan(8), "Distant debris must be visibly thrown");
        }

        [UnityTest]
        public IEnumerator ActualCannonDirectlyHitsAndIgnitesAFuelTank()
        {
            var fuel = CreateIsolatedFuel();
            var body = game.Tank.GetComponent<Rigidbody>(); body.isKinematic = false;
            Vector3 start = game.World.GroundPoint(fuel.transform.position + Vector3.right * 6, game.Settings.rideHeight);
            game.Tank.Warp(new Pose(start, Quaternion.LookRotation(Vector3.left))); body.isKinematic = true;
            Physics.SyncTransforms();
            game.Input.PulsePad(0);
            Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Not.Null);
            yield return new WaitForSeconds(.12f);
            Assert.That(fuel != null && fuel.Primed, Is.True, "The elevated shell must hit the actual tank collider");
            yield return new WaitForSeconds(.5f);
            Assert.That(game.ChainExplosions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FallingDebrisAndRammingDoNotCauseAFuelExplosion()
        {
            var fuel = CreateIsolatedFuel();
            var fragment = game.World.Box("Falling fuel test debris", fuel.transform.position + Vector3.up * 6, Vector3.one * .8f, game.World.ShotMaterial);
            var body = fragment.AddComponent<Rigidbody>();
            float start = body.position.y;
            yield return new WaitForSeconds(1.3f);
            Assert.That(body.position.y, Is.LessThan(start - 3), "The debris must actually fall onto the tank");
            Assert.That(fuel.Primed, Is.False);
            Assert.That(fuel.GetComponent<DestructibleBlock>().Detached, Is.False);
            game.Items.Ram(fuel.transform.position);
            Assert.That(fuel.GetComponent<DestructibleBlock>().Detached, Is.True, "A ram still pushes the small object aside");
            Assert.That(fuel.Primed, Is.False, "Physical crushing is not an explosion");
            yield return new WaitForSeconds(.4f);
            Assert.That(game.ChainExplosions, Is.Zero);
        }

        private ChainExplosive CreateIsolatedFuel()
        {
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            Vector3 point = game.World.GroundPoint(new Vector3(180, 0, 0), 1.1f);
            var root = game.World.Box("Isolated test fuel tank", point, new Vector3(2.2f, 2.2f, 2.8f), game.World.ShotMaterial);
            var block = root.AddComponent<DestructibleBlock>(); block.CanCrush = true; block.Initialize(game);
            var fuel = root.AddComponent<ChainExplosive>(); fuel.Initialize(game, game.Settings.fuelExplosionRadius, true);
            Physics.SyncTransforms();
            return fuel;
        }

        [UnityTest]
        public IEnumerator ARedSignalStopsTrafficAndGreenLetsItContinue()
        {
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            var signal = game.World.Signals.Find(x => x.Intersection.sqrMagnitude < 1);
            var bulbs = signal.GetComponentsInChildren<Renderer>().Where(x => x.name == "Traffic light").ToArray();
            signal.Initialize(game, Vector3.zero, bulbs, -Time.time + 6);
            var root = new GameObject("Signal test car"); root.AddComponent<BoxCollider>().size = new Vector3(1.8f, 1, 3.3f);
            var block = root.AddComponent<DestructibleBlock>(); block.CanCrush = true; block.Initialize(game);
            var car = root.AddComponent<TownTrafficCar>(); car.Initialize(game, new[] { new Vector3(3.6f, 0, -20), new Vector3(3.6f, 0, 30) }, 0, 8);
            yield return new WaitForSeconds(4);
            var body = car.GetComponent<Rigidbody>();
            Assert.That(car.Speed, Is.LessThan(.1f)); Assert.That(body.position.z, Is.InRange(-9, -3));
            float stopped = body.position.z;
            yield return new WaitForSeconds(.6f); Assert.That(body.position.z, Is.EqualTo(stopped).Within(.05f));
            yield return new WaitForSeconds(3);
            Assert.That(body.position.z, Is.GreaterThan(stopped + 3));
            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator StreetFurnitureBreaksAndBrokenPowerLinesLeaveNoFloatingSpan()
        {
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            var pole = GameObject.Find("Crushable power pole").GetComponent<DestructibleBlock>();
            int wires = Object.FindObjectsByType<TownPowerLine>(FindObjectsSortMode.None).Length;
            game.Blast(pole.transform.position + Vector3.up * 2, 1, 0);
            Assert.That(pole.Detached, Is.True); Assert.That(pole.GetComponent<Rigidbody>(), Is.Not.Null);
            yield return null; yield return null;
            Assert.That(Object.FindObjectsByType<TownPowerLine>(FindObjectsSortMode.None).Length, Is.LessThan(wires));
            var signal = game.World.Signals[0]; game.Blast(signal.transform.position + Vector3.up * 2, 1, 0);
            Assert.That(signal.GetComponent<DestructibleBlock>().Detached, Is.True);
            Assert.That(RenderSettings.skybox.shader.name, Is.EqualTo("TankTown/Sky Gradient"));
            Assert.That(GameObject.Find("Ocean horizon"), Is.Not.Null);
            Assert.That(GameObject.Find("Soft toy cloud").GetComponentsInChildren<Collider>().Length, Is.Zero);
        }
    }
}
