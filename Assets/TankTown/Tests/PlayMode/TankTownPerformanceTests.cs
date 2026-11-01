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
            Vector3 far = building.transform.position + Vector3.right * 400;
            visual.Tick(far); Assert.That(visual.Detailed, Is.False);
            var wall = building.Blocks.First(x => x.Kind == BuildingPart.Wall);
            Assert.That(wall.GetComponent<Collider>().enabled, Is.True); Assert.That(wall.GetComponent<Renderer>().enabled, Is.False);
            int originalVertices = visual.MapMesh.vertexCount;
            wall.Break(wall.transform.position, 12); visual.Tick(far);
            Assert.That(wall.Detached, Is.True); Assert.That(visual.MapMesh.vertexCount, Is.LessThan(originalVertices));
            Assert.That(wall.GetComponentsInChildren<Renderer>().All(x => x.enabled), Is.True);
            visual.Tick(building.transform.position + Vector3.up * 5); Assert.That(visual.Detailed, Is.True);
            Assert.That(building.Blocks.Where(x => !x.Detached).All(x => !x.GetComponent<Renderer>().enabled), Is.True);
            Assert.That(building.transform.Find("Batched building mesh").GetComponent<Renderer>().enabled, Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator BuildingDetailsStayVisibleFartherFromTheSurfaceAndDoNotFlickerAtTheBoundary()
        {
            var building = game.World.Buildings[0]; var visual = building.Presentation; var bounds = visual.DetailMesh.bounds;
            Vector3 View(float gap) => building.transform.TransformPoint(new Vector3(bounds.max.x + gap, bounds.center.y, bounds.center.z));
            visual.Tick(View(150)); Assert.That(visual.Detailed, Is.True, "Details must remain beyond the old 100/115m boundary");
            visual.Tick(View(180)); Assert.That(visual.Detailed, Is.True);
            visual.Tick(View(190)); Assert.That(visual.Detailed, Is.False);
            visual.Tick(View(170)); Assert.That(visual.Detailed, Is.False, "Hysteresis must prevent repeated toggling at the same distance");
            visual.Tick(View(150)); Assert.That(visual.Detailed, Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator NearBatchSeparatesCrackedAndCameraOccludingPartsAndRemovesBrokenGeometry()
        {
            var building = game.World.Buildings[0]; var visual = building.Presentation;
            visual.Tick(building.transform.position);
            var wall = building.Blocks.First(x => x.Kind == BuildingPart.Wall);
            int intactVertices = visual.DetailMesh.vertexCount;
            wall.DamageFromBlast(wall.transform.position, .3f, 0, 1); visual.Tick(building.transform.position);
            Assert.That(wall.Detached, Is.False); Assert.That(wall.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(visual.DetailMesh.vertexCount, Is.LessThan(intactVertices));
            var pillar = building.Blocks.First(x => x.Kind == BuildingPart.Pillar);
            int crackedVertices = visual.DetailMesh.vertexCount;
            pillar.ExposeForCamera(true); visual.Tick(building.transform.position);
            Assert.That(pillar.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(visual.DetailMesh.vertexCount, Is.LessThan(crackedVertices));
            pillar.ExposeForCamera(false); visual.Tick(building.transform.position);
            Assert.That(pillar.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(visual.DetailMesh.vertexCount, Is.EqualTo(crackedVertices));
            pillar.Break(pillar.transform.position, 0); visual.Tick(building.transform.position);
            Assert.That(pillar.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(visual.DetailMesh.vertexCount, Is.LessThan(crackedVertices));
            yield return null;
        }
        [UnityTest] public IEnumerator MapUsesLightweightMeshesAndKeepsThemUntilTheCityChanges()
        {
            var map = game.GetComponent<TankTownHUD>().EnableDiagnosticMinimap();
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
        [UnityTest] public IEnumerator LargeDamageQueriesGrowWithoutDroppingTargetsAndClearUsedReferences()
        {
            for (int i = 0; i < 1100; i++)
            {
                var item = game.World.Box("Overlap buffer target", new Vector3(500 + i % 40 * .15f, 15, i / 40 * .15f), Vector3.one * .1f, game.World.ShotMaterial);
                item.AddComponent<DestructibleBlock>().Initialize(game);
            }
            Physics.SyncTransforms();
            var query = new TownDamageQuery(); query.Sphere(new Vector3(503, 15, 2), 10);
            Assert.That(query.Count, Is.EqualTo(1100)); Assert.That(query.Hits.Length, Is.GreaterThan(1024));
            query.Clear(); Assert.That(query.Hits.All(x => x == null), Is.True);
            int before = game.BrokenBlocks;
            Assert.That(game.DamageSphere(new Vector3(503, 15, 2), 10, 0), Is.EqualTo(1100));
            Assert.That(game.BrokenBlocks - before, Is.EqualTo(1100));
            Assert.That(game.DamageSphere(new Vector3(503, 15, 2), 10, 0), Is.Zero);
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
        [UnityTest] public IEnumerator CrowdSharesGeometryAndKeepsRigidBodyAndAnimatedLimbs()
        {
            var people = game.World.Pedestrians;
            Assert.That(people.Count, Is.GreaterThan(10));
            var meshes = people.Select(x => x.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh).Distinct().ToArray();
            Assert.That(meshes.Length, Is.EqualTo(2), "Only hat and hair geometry should be allocated per world");
            foreach (var person in people)
            {
                var renderers = person.GetComponentsInChildren<Renderer>();
                Assert.That(renderers.Length, Is.EqualTo(1));
                Assert.That(renderers[0].sharedMaterials.Length, Is.EqualTo(3));
                Assert.That(person.GetComponent<CapsuleCollider>().radius, Is.EqualTo(game.Settings.pedestrianHitRadius));
            }
            var first = people[0]; first.enabled = false;
            var skin = first.GetComponentInChildren<SkinnedMeshRenderer>();
            var original = new Mesh(); var moved = new Mesh();
            try
            {
                skin.BakeMesh(original);
                skin.bones[3].localRotation *= Quaternion.Euler(70, 0, 0);
                skin.BakeMesh(moved);
                var weights = skin.sharedMesh.boneWeights; var a = original.vertices; var b = moved.vertices;
                int arm = System.Array.FindIndex(weights, x => x.boneIndex0 == 3);
                int body = System.Array.FindIndex(weights, x => x.boneIndex0 == 0);
                Assert.That(Vector3.Distance(a[arm], b[arm]), Is.GreaterThan(.1f), "The sleeve must follow its shoulder joint");
                Assert.That(Vector3.Distance(a[body], b[body]), Is.LessThan(.0001f), "The body must stay rigid");
                Assert.That(a.All(skin.localBounds.Contains), Is.True, "Walking geometry must fit within its culling bounds");
            }
            finally { Object.Destroy(original); Object.Destroy(moved); }
            yield return null;
        }
        [UnityTest] public IEnumerator CarsShareTheirMeshAndKeepIndividualPaintAndCollision()
        {
            var cars = game.World.TrafficCars;
            Assert.That(cars.Count, Is.GreaterThan(10));
            var filters = cars.Select(x => x.transform.Find("Batched traffic car").GetComponent<MeshFilter>()).ToArray();
            Assert.That(filters.Select(x => x.sharedMesh).Distinct().Count(), Is.EqualTo(1));
            Assert.That(filters.Select(x => x.GetComponent<Renderer>().sharedMaterials[0]).Distinct().Count(), Is.GreaterThan(1));
            foreach (var car in cars)
            {
                Assert.That(car.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(2), "No disabled prototype parts should remain");
                Assert.That(car.GetComponent<BoxCollider>().size, Is.EqualTo(new Vector3(1.8f, 2, 3.3f)));
                Assert.That(car.GetComponent<ChainExplosive>(), Is.Not.Null);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator RepeatedMeshCombinesReuseScratchWithoutRetainingRemovedGeometry()
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cube = source.GetComponent<MeshFilter>().sharedMesh;
            var workspace = new TownMeshWorkspace();
            Mesh first = null, second = null;
            var other = game.World.MakeMaterial(Color.magenta);
            try
            {
                workspace.Begin();
                workspace.Add(game.World.ShotMaterial, cube, Matrix4x4.identity);
                workspace.Add(other, cube, Matrix4x4.Translate(Vector3.right * 3));
                first = workspace.Combine("Two materials", out var paints);
                Assert.That(first.subMeshCount, Is.EqualTo(2));
                Assert.That(paints, Is.EqualTo(new[] { game.World.ShotMaterial, other }));
                Assert.That(first.bounds.size.x, Is.EqualTo(4).Within(.001f));
                workspace.Begin(); workspace.Add(other, cube, Matrix4x4.identity);
                second = workspace.Combine("One material", out paints);
                Assert.That(second.subMeshCount, Is.EqualTo(1));
                Assert.That(second.vertexCount, Is.EqualTo(cube.vertexCount));
                Assert.That(second.bounds.size, Is.EqualTo(Vector3.one));
                Assert.That(paints, Is.EqualTo(new[] { other }));
                Assert.That(workspace.ScratchMeshCount, Is.EqualTo(2), "Rebuilding must reuse the native scratch meshes");
            }
            finally { workspace.Dispose(); Object.Destroy(source); Object.Destroy(first); Object.Destroy(second); }
            yield return null;
        }
    }
}
