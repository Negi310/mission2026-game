using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownPerformanceTests
    {
        private TankTownGame game;
        [UnitySetUp] public IEnumerator SetUp()
        {
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart();
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
        }
        [UnityTearDown] public IEnumerator TearDown() { Time.timeScale = 1; yield return null; }
        [UnityTest] public IEnumerator FarBuildingsKeepPhysicsAndDetachedPartsAreRendered()
        {
            var building = game.World.Buildings[0]; var visual = building.Presentation;
            Vector3 far = building.transform.position + Vector3.right * 200;
            visual.Tick(far); Assert.That(visual.Detailed, Is.False);
            var wall = building.Blocks.First(x => x.Kind == BuildingPart.Wall);
            Assert.That(wall.GetComponent<Collider>().enabled, Is.True); Assert.That(wall.GetComponent<Renderer>().enabled, Is.False);
            int originalVertices = visual.MapMesh.vertexCount;
            wall.Break(wall.transform.position, 12); visual.Tick(far);
            Assert.That(wall.Detached, Is.True); Assert.That(visual.MapMesh.vertexCount, Is.LessThan(originalVertices));
            Assert.That(wall.GetComponentsInChildren<Renderer>().All(x => x.enabled), Is.True);
            visual.Tick(building.transform.position + Vector3.up * 5); Assert.That(visual.Detailed, Is.True);
            Assert.That(building.Blocks.Where(x => !x.Detached).All(x => x.GetComponent<Renderer>().enabled), Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator MapUsesLightweightMeshesAndKeepsThemUntilTheCityChanges()
        {
            var map = game.GetComponent<TankTownHUD>().Minimap;
            Assert.That(map.View.cullingMask, Is.EqualTo((1 << TownPhysics.MapLayer) | TownPhysics.GroundMask));
            Assert.That(Camera.main.cullingMask & (1 << TownPhysics.MapLayer), Is.Zero);
            var building = game.World.Buildings[0]; var mesh = building.Presentation.MapMesh;
            long originalIndices = building.Blocks.SelectMany(x => x.GetComponentsInChildren<MeshFilter>()).Sum(x => (long)x.sharedMesh.GetIndexCount(0));
            long mapIndices = 0; for (int i = 0; i < mesh.subMeshCount; i++) mapIndices += mesh.GetIndexCount(i);
            Assert.That(mapIndices, Is.LessThan(originalIndices / 5));
            building.Presentation.Tick(building.transform.position + Vector3.right * 200);
            Assert.That(building.Presentation.MapMesh, Is.SameAs(mesh), "Moving the camera must not rebuild static meshes");
            building.Presentation.Tick(building.transform.position);
            building.Blocks.First(x => x.Kind == BuildingPart.Wall).Break(building.transform.position, 0);
            Assert.That(building.Presentation.MapMesh, Is.SameAs(mesh), "A nearby building should rebuild only when the map needs it");
            map.RenderNow(); Assert.That(building.Presentation.MapMesh, Is.Not.SameAs(mesh));
            yield return null;
        }
        [UnityTest] public IEnumerator CarSearchFindsAQueueAcrossGridBoundariesAndIgnoresAnInactiveLeader()
        {
            foreach (var car in game.World.TrafficCars) car.gameObject.SetActive(false);
            TownTrafficCar Car(Vector3 position)
            {
                var root = game.World.Box("Traffic grid test car", position, Vector3.one, game.World.ShotMaterial);
                var block = root.AddComponent<DestructibleBlock>(); block.Initialize(game);
                var car = root.AddComponent<TownTrafficCar>(); car.Initialize(game, new[] { position, position + Vector3.forward * 80 }, 0, 8);
                game.World.TrafficCars.Add(car); return car;
            }
            var follower = Car(new Vector3(25.5f, 0, -50)); var leader = Car(new Vector3(26.5f, 0, -44));
            yield return new WaitForFixedUpdate();
            Assert.That(game.World.TrafficShouldStop(follower, follower.PhysicsPosition, Vector3.forward), Is.True);
            leader.gameObject.SetActive(false); yield return new WaitForFixedUpdate();
            Assert.That(game.World.TrafficShouldStop(follower, follower.PhysicsPosition, Vector3.forward), Is.False);
        }
        [UnityTest] public IEnumerator GroundCollisionHeightsRemainContinuousAcrossChunkEdges()
        {
            float boundary = -game.World.HalfExtent + 64;
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(new Vector3(boundary - .05f, 40, 10), Vector3.down, out var left, 60, TownPhysics.GroundMask), Is.True);
            Assert.That(Physics.Raycast(new Vector3(boundary + .05f, 40, 10), Vector3.down, out var right, 60, TownPhysics.GroundMask), Is.True);
            Assert.That(Mathf.Abs(left.point.y - right.point.y), Is.LessThan(.05f));
            Assert.That(game.World.GetComponentsInChildren<MeshCollider>().Count(x => x.gameObject.layer == TownPhysics.GroundLayer), Is.GreaterThan(1));
            yield return null;
        }
    }
}
