using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownVisualPolishTests
    {
        private TankTownGame game;
        private TownStage previousStage;
        private bool previousSkip;
        private float previousStep;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousStage = TankTownGame.SelectedStage; previousSkip = TankTownGame.SkipIntroForTests;
            previousStep = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            TankTownGame.SelectedStage = TownStage.Town; TankTownGame.SkipIntroForTests = true;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart();
            game.Input.useHardware = true; game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var item in game.World.ItemPickups) item.gameObject.SetActive(false);
            foreach (var pickup in game.World.Pickups) pickup.gameObject.SetActive(false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TankTownGame.SelectedStage = previousStage; TankTownGame.SkipIntroForTests = previousSkip;
            Time.timeScale = 1; Time.captureDeltaTime = previousStep; yield return null;
        }

        [UnityTest]
        public IEnumerator ExpiryBlinksAllVehiclePartsAndSeparateEffectsWithoutChangingAbilities()
        {
            game.Settings.magnetSeconds = game.Settings.bulldozerSeconds = game.Settings.highSpeedSeconds = 4;
            game.Items.Receive(ItemKind.Magnet); game.Items.Receive(ItemKind.Bulldozer); game.Items.Receive(ItemKind.HighSpeed);
            var ring = game.Tank.Visual.Find("Magnetic field ring").gameObject;
            var shield = game.Tank.Visual.Find("High speed energy shield").gameObject;
            var renderers = game.Tank.Visual.GetComponentsInChildren<MeshRenderer>(true).Where(x => x.sharedMaterial.HasProperty("_ItemFlash")).ToArray();
            Assert.That(renderers.Any(x => x.name == "Track" || x.name.Contains("Track")), Is.True, "Tracks must be included in the warning");
            var properties = new MaterialPropertyBlock(); bool visible = false, hidden = false, flashed = false, normal = false;
            yield return new WaitForSeconds(1.15f);
            float end = Time.time + .7f;
            while (Time.time < end)
            {
                Assert.That(game.Items.Invincible, Is.True);
                Assert.That(game.Tank.VehicleScale, Is.EqualTo(game.Settings.bulldozerScale));
                visible |= ring.activeSelf && shield.activeSelf; hidden |= !ring.activeSelf && !shield.activeSelf;
                renderers[0].GetPropertyBlock(properties); float first = properties.GetFloat("_ItemFlash");
                foreach (var renderer in renderers)
                {
                    renderer.GetPropertyBlock(properties); Assert.That(properties.GetFloat("_ItemFlash"), Is.EqualTo(first), renderer.name);
                }
                flashed |= first > .5f; normal |= first == 0;
                yield return null;
            }
            Assert.That(visible && hidden && flashed && normal, Is.True, "All three warnings must visibly cycle while their abilities stay active");
            float remaining = game.Items.Remaining(ItemKind.Bulldozer); bool pausedRing = ring.activeSelf;
            renderers[0].GetPropertyBlock(properties); float pausedFlash = properties.GetFloat("_ItemFlash");
            game.TogglePause(); yield return new WaitForSecondsRealtime(.2f);
            renderers[0].GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_ItemFlash"), Is.EqualTo(pausedFlash)); Assert.That(ring.activeSelf, Is.EqualTo(pausedRing));
            Assert.That(game.Items.Remaining(ItemKind.Bulldozer), Is.EqualTo(remaining));
            game.TogglePause(); game.Items.Receive(ItemKind.Magnet); game.Items.Receive(ItemKind.Bulldozer); game.Items.Receive(ItemKind.HighSpeed);
            yield return null;
            Assert.That(ring.activeSelf && shield.activeSelf, Is.True, "Reacquiring an effect must immediately stop its expiry warning");
            foreach (var renderer in renderers) { renderer.GetPropertyBlock(properties); Assert.That(properties.GetFloat("_ItemFlash"), Is.Zero); }
            game.Items.FinishRound(); yield return null;
            Assert.That(ring.activeSelf || shield.activeSelf, Is.False); Assert.That(game.Tank.VehicleScale, Is.EqualTo(1));
            foreach (var renderer in renderers) { renderer.GetPropertyBlock(properties); Assert.That(properties.GetFloat("_ItemFlash"), Is.Zero); }
            Assert.That(game.Tank.Visual.GetComponentsInChildren<TrailRenderer>(true).Any(x => x.emitting), Is.False);
        }

        [UnityTest]
        public IEnumerator ExplosionsReuseBoundedParticlesAndReleaseFlashLightingAfterTheyClear()
        {
            Vector3 point = game.Tank.transform.position + Vector3.forward * 14;
            for (int i = 0; i < 40; i++) game.Effects.Impact(point + Vector3.right * (i % 4), 8);
            yield return null;
            Assert.That(game.Effects.AllocatedExplosionCount, Is.EqualTo(TankExplosionVfx.Capacity));
            Assert.That(game.Effects.ActiveExplosionCount, Is.EqualTo(TankExplosionVfx.Capacity));
            Assert.That(game.Effects.GetComponentsInChildren<ArcadePulse>().Length, Is.LessThanOrEqualTo(64));
            Assert.That(Shader.GetGlobalInt("_ToyFlashCount"), Is.InRange(1, 4));
            Assert.That(game.Effects.GetComponentsInChildren<Collider>().Length, Is.Zero);
            Assert.That(game.Effects.GetComponentsInChildren<ParticleSystemRenderer>().All(x => x.sharedMaterial.shader.isSupported), Is.True);
            game.TogglePause(); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(game.Effects.ActiveExplosionCount, Is.EqualTo(TankExplosionVfx.Capacity));
            game.TogglePause(); yield return new WaitForSeconds(2.45f);
            Assert.That(game.Effects.ActiveExplosionCount, Is.EqualTo(TankExplosionVfx.Capacity), "The larger explosion must retain its aftermath beyond the old cutoff");
            Assert.That(game.Effects.GetComponentsInChildren<ParticleSystem>().Any(x => x.IsAlive()), Is.True);
            yield return new WaitForSeconds(TankExplosionVfx.BurstLifetime - 2.45f + .4f);
            Assert.That(game.Effects.ActiveExplosionCount, Is.Zero); Assert.That(Shader.GetGlobalInt("_ToyFlashCount"), Is.Zero);
            Assert.That(game.Effects.GetComponentsInChildren<ParticleSystem>(true).Any(x => x.IsAlive()), Is.False);
            game.Effects.SuperImpact(point, 24); yield return null;
            Assert.That(game.Effects.AllocatedExplosionCount, Is.EqualTo(TankExplosionVfx.Capacity));
            Assert.That(game.Effects.ActiveExplosionCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FlashFadesWithoutShrinkingAndOnlySmokeRemainsAtTheEnd()
        {
            game.Effects.Impact(game.Tank.transform.position + Vector3.forward * 14, 8);
            yield return new WaitForSeconds(.34f);
            var flash = game.Effects.GetComponentsInChildren<ArcadePulse>().Single(x => x.name == "Impact star flash");
            float size = flash.transform.localScale.x;
            var properties = new MaterialPropertyBlock(); var renderer = flash.GetComponent<Renderer>();
            renderer.GetPropertyBlock(properties); float opacity = properties.GetColor("_Color").a;
            yield return new WaitForSeconds(.24f);
            Assert.That(flash, Is.Not.Null);
            Assert.That(flash.transform.localScale.x, Is.GreaterThanOrEqualTo(size * .99f), "The flash must fade at its expanded size rather than snap shut");
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetColor("_Color").a, Is.InRange(0.001f, opacity * .5f));
            yield return new WaitForSeconds(1.82f);
            Assert.That(game.Effects.GetComponentsInChildren<ArcadePulse>(), Is.Empty);
            var alive = game.Effects.GetComponentsInChildren<ParticleSystem>().Where(x => x.IsAlive()).ToArray();
            Assert.That(alive, Is.Not.Empty);
            Assert.That(alive.All(x => x.name == "Rolling explosion smoke"), Is.True, "Light, flame and sparks must finish before the smoke tail");
        }

        [UnityTest]
        public IEnumerator FullPoolFadesLiveParticlesBeforeMovingThemToANewImpact()
        {
            Vector3 first = game.Tank.transform.position + Vector3.forward * 14, next = first + Vector3.right * 120;
            for (int i = 0; i < TankExplosionVfx.Capacity; i++) game.Effects.Impact(first, 8);
            yield return new WaitForSeconds(.3f);
            var bursts = game.Effects.GetComponentsInChildren<FireballBurst>();
            for (int i = 0; i < TankExplosionVfx.Capacity; i++) game.Effects.Impact(next, 8);
            Assert.That(bursts.All(x => (x.transform.position - first).sqrMagnitude < .01f), Is.True, "Pool pressure must not instantly teleport a visible effect");
            yield return null;
            var renderer = bursts[0].GetComponentInChildren<ParticleSystemRenderer>();
            var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
            float fading = properties.GetFloat("_BurstOpacity"); Assert.That(fading, Is.InRange(.001f, .999f));
            game.TogglePause(); yield return new WaitForSecondsRealtime(.1f); renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_BurstOpacity"), Is.EqualTo(fading));
            game.TogglePause(); yield return new WaitForSeconds(.25f);
            Assert.That(bursts.All(x => (x.transform.position - next).sqrMagnitude < .01f), Is.True);
            Assert.That(game.Effects.AllocatedExplosionCount, Is.EqualTo(TankExplosionVfx.Capacity));
            renderer.GetPropertyBlock(properties); Assert.That(properties.GetFloat("_BurstOpacity"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TreeGeometryIsSharedAndWindowDetailsFollowTheirBrokenWall()
        {
            var crowns = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(x => x.name == "Tree crown").ToArray();
            Assert.That(crowns.Length, Is.GreaterThan(3));
            Assert.That(crowns.Select(x => x.sharedMesh).Distinct().Count(), Is.LessThanOrEqualTo(3));
            Assert.That(crowns.All(x => x.sharedMesh.colors.Length == x.sharedMesh.vertexCount), Is.True);
            Assert.That(crowns.All(x => x.GetComponent<SphereCollider>() != null), Is.True);
            var wall = game.World.Buildings.SelectMany(x => x.Blocks).First(x => x.transform.Find("Deep window frame") != null);
            var frame = wall.transform.Find("Deep window frame"); var pane = wall.transform.Find("Window");
            Assert.That(frame.position.z != pane.position.z || frame.position.x != pane.position.x, Is.True);
            int score = game.BrokenBlocks; wall.Break(wall.transform.position, 0); yield return null;
            Assert.That(wall.DebrisBodies.Contains(frame.GetComponentInParent<Rigidbody>()), Is.True, "Window trim must follow one of the physical wall fragments");
            Assert.That(frame.gameObject.layer, Is.EqualTo(TownPhysics.DebrisLayer));
            Assert.That(frame.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(game.BrokenBlocks, Is.GreaterThan(score));
        }
    }
}
