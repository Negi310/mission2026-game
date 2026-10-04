using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownDebrisTests
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
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1; Time.captureDeltaTime = captureStep; yield return null; }

        private DestructibleBlock Block(string name, Vector3 position, Vector3 size, BuildingPart kind = BuildingPart.Prop)
        {
            var item = game.World.Box(name, position, size, game.World.ShotMaterial);
            var block = item.AddComponent<DestructibleBlock>(); block.Kind = kind; block.Initialize(game);
            return block;
        }

        [UnityTest]
        public IEnumerator FloorFragmentsPreserveGeometryMaterialDecorationAndOneScore()
        {
            var floor = Block("Fracture floor", new Vector3(180, 14, 0), new Vector3(9, .24f, 6), BuildingPart.Slab);
            floor.transform.rotation = Quaternion.Euler(0, 25, 0);
            var decoration = game.World.Box("Floor decoration", new Vector3(.3f, .6f, .2f), new Vector3(.2f, .1f, .2f), game.World.ShotMaterial, floor.transform, false);
            Vector3 decorationPosition = decoration.transform.position;
            var initialBounds = floor.GetComponent<Renderer>().bounds;
            var initialMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            int total = game.TotalBlocks, broken = game.BrokenBlocks;
            floor.Break(floor.transform.position, 0);
            var pieces = floor.DebrisBodies.ToArray();
            Assert.That(pieces.Length, Is.GreaterThan(1));
            float volume = 0; Bounds union = pieces[0].GetComponent<Renderer>().bounds;
            foreach (var piece in pieces)
            {
                piece.useGravity = false;
                var size = piece.transform.lossyScale; volume += size.x * size.y * size.z;
                Assert.That(Mathf.Max(size.x, size.z), Is.LessThanOrEqualTo(game.Settings.fragmentSize + .001f));
                Assert.That(piece.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(initialMesh));
                Assert.That(piece.GetComponent<Renderer>().sharedMaterial, Is.SameAs(game.World.ShotMaterial));
                Assert.That(piece.gameObject.layer, Is.EqualTo(TownPhysics.DebrisLayer));
                union.Encapsulate(piece.GetComponent<Renderer>().bounds);
            }
            Assert.That(volume, Is.EqualTo(9 * .24f * 6).Within(.001f));
            Assert.That(Vector3.Distance(union.center, initialBounds.center), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(union.size, initialBounds.size), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(decoration.transform.position, decorationPosition), Is.LessThan(.001f));
            Assert.That(decoration.GetComponentInParent<FlyingDebris>(), Is.Not.Null);
            floor.Break(floor.transform.position, 100);
            Assert.That(game.TotalBlocks, Is.EqualTo(total)); Assert.That(game.BrokenBlocks, Is.EqualTo(broken + 1));
            game.TogglePause(); yield return new WaitForSecondsRealtime(.1f);
            Assert.That(pieces.All(x => x != null), Is.True);
            game.TogglePause(); yield return new WaitForSeconds(game.Settings.debrisLifetime + .1f);
            Assert.That(pieces.All(x => x == null), Is.True, "Each independent piece must be cleaned up");
            Assert.That(game.BrokenBlocks, Is.EqualTo(broken + 1));
        }

        [UnityTest]
        public IEnumerator StrongerExplosionActuallyLaunchesDebrisFaster()
        {
            var old = Block("Previous impulse", new Vector3(180, 12, 0), Vector3.one);
            var current = Block("Stronger impulse", new Vector3(180, 12, 15), Vector3.one);
            old.Break(old.transform.position - Vector3.right * 1.5f, 20);
            current.Break(current.transform.position - Vector3.right * 1.5f, game.Stats.BlastImpulse);
            var oldBody = old.GetComponent<Rigidbody>(); var newBody = current.GetComponent<Rigidbody>();
            oldBody.useGravity = newBody.useGravity = false;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(newBody.linearVelocity.magnitude, Is.GreaterThan(oldBody.linearVelocity.magnitude * 1.4f));
            Assert.That(newBody.linearVelocity.magnitude, Is.GreaterThan(30));
        }

        [UnityTest]
        public IEnumerator FastDebrisExplodesAtActualContactAndItsNewDebrisContinuesTheChain()
        {
            var source = Block("Incoming fragment", new Vector3(180, 12, 0), Vector3.one * .8f);
            var target = Block("First impact target", new Vector3(185, 12, 0), Vector3.one);
            var neighbour = Block("Local blast neighbour", new Vector3(187, 12, 0), Vector3.one);
            var distant = Block("Next impact target", new Vector3(199, 12, 0), Vector3.one);
            source.Break(source.transform.position, 0);
            var body = source.GetComponent<Rigidbody>(); body.useGravity = false; body.linearVelocity = Vector3.right * 40;
            Physics.SyncTransforms();
            for (int i = 0; i < 20 && !target.Detached; i++) yield return new WaitForFixedUpdate();
            Assert.That(target.Detached, Is.True, "The moving Rigidbody must hit the intact target");
            Assert.That(neighbour.Detached, Is.True, "The impact must create localized blast damage");
            Assert.That(distant.Detached, Is.False, "The contact must not explode the whole area");
            Assert.That(game.DebrisExplosions, Is.EqualTo(1));
            Assert.That(source.GetComponent<FlyingDebris>().ImpactSpent, Is.True);
            var next = target.GetComponent<Rigidbody>();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(next.linearVelocity.magnitude, Is.GreaterThan(12), "Newly broken pieces must be propelled again");
            Assert.That(next.GetComponent<FlyingDebris>().Generation, Is.EqualTo(1));
            // 次の接触だけ向きを固定し、二段目も実際の物理衝突で確認する。
            next.useGravity = false; next.position = distant.transform.position - Vector3.right * 4;
            next.rotation = Quaternion.identity; next.angularVelocity = Vector3.zero; next.linearVelocity = Vector3.right * 38;
            Physics.SyncTransforms();
            for (int i = 0; i < 20 && !distant.Detached; i++) yield return new WaitForFixedUpdate();
            Assert.That(distant.Detached, Is.True);
            Assert.That(game.DebrisExplosions, Is.EqualTo(2));
            Assert.That(distant.GetComponent<FlyingDebris>().Generation, Is.EqualTo(2));
            Assert.That(game.LongestChain, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator DebrisContactBlastOnlyIgnitesFuelNearItsActualImpactPoint()
        {
            var source = Block("Fuel contact fragment", new Vector3(180, 12, 0), Vector3.one * .8f);
            var target = Block("Fuel contact target", new Vector3(185, 12, 0), Vector3.one);
            var near = Fuel(new Vector3(184.5f, 12, -3));
            // 衝突点から表面まで3.25。局所爆風3.5には入るが燃料の点火距離3の外側。
            var far = Fuel(new Vector3(184.5f, 12, 4.65f));
            source.Break(source.transform.position, 0);
            var body = source.GetComponent<Rigidbody>(); body.useGravity = false; body.linearVelocity = Vector3.right * 40;
            Physics.SyncTransforms();
            for (int i = 0; i < 20 && !target.Detached; i++) yield return new WaitForFixedUpdate();
            Assert.That(target.Detached, Is.True);
            Assert.That(game.DebrisExplosions, Is.EqualTo(1));
            Assert.That(near.Primed, Is.True, "A real contact explosion close to fuel must ignite it");
            Assert.That(far.Primed, Is.False, "A fuel tank at the edge of the contact blast must stay safe");
            Assert.That(far.GetComponent<DestructibleBlock>().Detached, Is.False);
        }

        private ChainExplosive Fuel(Vector3 position)
        {
            var block = Block("Contact fuel", position, new Vector3(2.2f, 2.2f, 2.8f));
            var fuel = block.gameObject.AddComponent<ChainExplosive>(); fuel.Initialize(game, game.Settings.fuelExplosionRadius, true);
            return fuel;
        }

        [UnityTest]
        public IEnumerator SlowAndTangentialContactsDoNotExplode()
        {
            var slow = Block("Slow fragment", new Vector3(180, 12, 0), Vector3.one * .8f);
            var target = Block("Slow contact target", new Vector3(182, 12, 0), Vector3.one);
            var sliding = Block("Sliding fragment", new Vector3(184.4f, 12, 17), Vector3.one * .8f);
            var wall = Block("Glancing contact wall", new Vector3(185, 12, 25), new Vector3(.3f, 3, 20), BuildingPart.Wall);
            foreach (var fragment in new[] { slow, sliding }) { fragment.Break(fragment.transform.position, 0); fragment.GetComponent<Rigidbody>().useGravity = false; }
            var slowBody = slow.GetComponent<Rigidbody>(); slowBody.linearVelocity = Vector3.right * 6;
            var slidingBody = sliding.GetComponent<Rigidbody>(); slidingBody.constraints = RigidbodyConstraints.FreezeRotation;
            slidingBody.linearVelocity = new Vector3(1, 0, 30);
            Physics.SyncTransforms();
            for (int i = 0; i < 24; i++) yield return new WaitForFixedUpdate();
            Assert.That(slowBody.position.x, Is.LessThan(181.2f), "Slow debris must physically stop at the target");
            Assert.That(slidingBody.position.x, Is.LessThan(184.6f), "The high tangential speed must still produce contact");
            Assert.That(target.Detached, Is.False); Assert.That(wall.Detached, Is.False);
            Assert.That(game.DebrisExplosions, Is.Zero);
        }

        [UnityTest]
        public IEnumerator GroundContactDoesNotExplodeAndFragmentedDebrisCannotSteerTank()
        {
            var ground = game.World.Box("Debris ground fixture", new Vector3(180, 7, 10), new Vector3(5, .3f, 5), game.World.ShotMaterial);
            ground.layer = TownPhysics.GroundLayer;
            var falling = Block("Fast ground debris", new Vector3(180, 10, 10), Vector3.one * .8f);
            falling.Break(falling.transform.position, 0);
            var fallingBody = falling.GetComponent<Rigidbody>(); fallingBody.useGravity = false; fallingBody.linearVelocity = Vector3.down * 40;
            Physics.SyncTransforms();
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            Assert.That(fallingBody.position.y, Is.GreaterThan(7.4f)); Assert.That(game.DebrisExplosions, Is.Zero);
            var floor = Block("Tank debris floor", new Vector3(180, 14, 25), new Vector3(6, .24f, 6), BuildingPart.Slab);
            floor.Break(floor.transform.position, 0);
            var piece = floor.DebrisBodies.First(x => x.GetComponent<DestructibleBlock>() == null);
            foreach (var other in floor.DebrisBodies) if (other != piece) other.gameObject.SetActive(false);
            var tankBody = game.Tank.GetComponent<Rigidbody>(); tankBody.isKinematic = false; game.Tank.enabled = true;
            Vector3 start = tankBody.position;
            piece.useGravity = false; piece.position = start + new Vector3(1.65f, 0, 4); piece.rotation = Quaternion.identity;
            piece.linearVelocity = Vector3.back * 30; Physics.SyncTransforms();
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(piece.linearVelocity.z, Is.GreaterThan(0));
            Assert.That(tankBody.position.z - start.z, Is.GreaterThan(.3f));
            Assert.That(Quaternion.Angle(tankBody.rotation, Quaternion.identity), Is.LessThan(.2f));
            Assert.That(game.Crashes, Is.Zero); Assert.That(game.DebrisExplosions, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PhysicsChainStopsAtGenerationLimitAndPauseStopsMovingDebris()
        {
            var source = Block("Chain seed", new Vector3(180, 12, 0), Vector3.one * .8f);
            source.Break(source.transform.position, 0);
            var next = source.GetComponent<Rigidbody>();
            for (int generation = 1; generation <= game.Settings.debrisChainLimit + 1; generation++)
            {
                var target = Block("Generation " + generation, new Vector3(180 + generation * 18, 12, 0), Vector3.one);
                next.useGravity = false; next.position = target.transform.position - Vector3.right * 4;
                next.rotation = Quaternion.identity; next.angularVelocity = Vector3.zero; next.linearVelocity = Vector3.right * 36;
                Physics.SyncTransforms();
                for (int i = 0; i < 16 && !target.Detached; i++) yield return new WaitForFixedUpdate();
                if (generation <= game.Settings.debrisChainLimit)
                {
                    Assert.That(target.Detached, Is.True, "Actual contact must propagate generation " + generation); next = target.GetComponent<Rigidbody>();
                    Assert.That(next.GetComponent<FlyingDebris>().Generation, Is.EqualTo(generation));
                    // 衝突中に予約された爆発の力を物理ステップで消化してから次の接触を配置する。
                    yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                }
                else Assert.That(target.Detached, Is.False, "A finite chain must not keep producing explosions");
            }
            Assert.That(game.DebrisExplosions, Is.EqualTo(game.Settings.debrisChainLimit));
            game.TogglePause(); Vector3 position = next.position; int explosions = game.DebrisExplosions;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(next.position, Is.EqualTo(position)); Assert.That(game.DebrisExplosions, Is.EqualTo(explosions));
        }

        [UnityTest]
        public IEnumerator SimultaneousFragmentContactsRespectBurstAndDebrisBudgets()
        {
            for (int i = 0; i < 8; i++)
            {
                var source = Block("Budget fragment " + i, new Vector3(180, 12, i * 12), Vector3.one * .8f);
                Block("Budget target " + i, new Vector3(184, 12, i * 12), Vector3.one);
                source.Break(source.transform.position, 0);
                var body = source.GetComponent<Rigidbody>(); body.useGravity = false; body.linearVelocity = Vector3.right * 36;
            }
            Physics.SyncTransforms();
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            Assert.That(game.DebrisExplosions, Is.InRange(1, 3), "A simultaneous impact burst must be bounded");
            foreach (var building in game.World.Buildings.GetRange(0, 5))
            {
                foreach (var support in building.Blocks.FindAll(x => x != null && x.Foundation && x.Column < 2)) support.Break(support.transform.position, 0);
                building.ReleaseUnsupported(building.transform.position, 0);
            }
            yield return null;
            Assert.That(Object.FindObjectsByType<FlyingDebris>(FindObjectsSortMode.None).Length, Is.LessThanOrEqualTo(game.Settings.maxDebris));
            Assert.That(game.World.Buildings.Take(5).All(x => x.DestroyedFraction >= game.Settings.buildingRewardThreshold), Is.True);
            Assert.That(game.BrokenBlocks, Is.LessThanOrEqualTo(game.TotalBlocks));
        }
    }
}
