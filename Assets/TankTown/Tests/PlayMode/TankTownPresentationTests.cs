using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEngine.UI;

namespace TankTown.Tests
{
    public sealed class TankTownPresentationTests
    {
        private TankTownGame game;
        private float captureStep;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            captureStep = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
            game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1; Time.captureDeltaTime = captureStep; yield return null; }

        [UnityTest]
        public IEnumerator CompactHudLeavesTheDrivingViewOpenAndTracksActualUpgrades()
        {
            game.Stats.Upgrade(UpgradeKind.Power); game.Stats.Upgrade(UpgradeKind.Power);
            yield return null; Canvas.ForceUpdateCanvases();
            var root = (RectTransform)GameObject.Find("Gameplay HUD").transform;
            Rect screen = Bounds(root);
            Rect drivingView = new Rect(screen.x + screen.width * .34f, screen.y + screen.height * .25f, screen.width * .32f, screen.height * .5f);
            foreach (string name in new[] { "Destruction", "Clock", "Upgrades", "Special items", "Controls", "Live ranking", "Minimap" })
            {
                Rect panel = Bounds((RectTransform)GameObject.Find(name).transform);
                Assert.That(screen.Contains(panel.min) && screen.Contains(panel.max), Is.True, name + " must fit on screen");
                Assert.That(panel.Overlaps(drivingView), Is.False, name + " must leave the road and aim clear");
            }
            var chip = GameObject.Find("Upgrade chip 1");
            Assert.That(chip.GetComponentsInChildren<Text>().Any(x => x.text == "Lv.2"), Is.True);
            Assert.That(chip.GetComponentsInChildren<Image>().Any(x => x.sprite == game.World.Icons.Sprite(UpgradeKind.Power)), Is.True);
        }

        [UnityTest]
        public IEnumerator CrashHidesActionTextWhileStillApplyingThePenalty()
        {
            float remaining = game.Remaining;
            game.Tank.Explode();
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(game.Tank.Respawning, Is.True);
            Assert.That(game.Remaining, Is.LessThanOrEqualTo(remaining - game.Settings.respawnPenalty));
            var texts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var heading = texts.Single(x => x.name == "Action heading");
            var caption = texts.Single(x => x.name == "Action caption");
            foreach (var text in new[] { heading, caption })
            {
                Assert.That(text.text, Is.Empty);
                Assert.That(text.gameObject.activeInHierarchy, Is.False, "The crash must leave the view free of action text");
            }
        }

        [UnityTest]
        public IEnumerator SpecialCardFollowsHeldWeaponAndActiveEffects()
        {
            Assert.That(game.Items.Receive(ItemKind.Missiles), Is.True); yield return null;
            var card = GameObject.Find("Special items");
            Assert.That(card.GetComponentsInChildren<Text>().Any(x => x.text == "追尾ミサイル"), Is.True);
            Assert.That(card.GetComponentsInChildren<Image>().Any(x => x.sprite == game.World.SpecialIcons.Sprite(ItemKind.Missiles)), Is.True);
            Assert.That(game.Items.MissilesActive, Is.True); Assert.That(game.Items.Held, Is.EqualTo(ItemKind.None)); yield return null;
            Assert.That(card.GetComponentsInChildren<Text>().Any(x => x.text == "発動中！"), Is.True);
            Assert.That(game.Items.Receive(ItemKind.Magnet), Is.True);
            Assert.That(game.Items.Receive(ItemKind.HighSpeed), Is.True);
            Assert.That(game.Items.Receive(ItemKind.Bulldozer), Is.True);
            yield return null; Canvas.ForceUpdateCanvases();
            var active = GameObject.Find("Active item timers");
            var timers = active.GetComponentInChildren<Text>();
            foreach (string name in new[] { "追尾ミサイル", "マグネット", "ブルドーザー", "ハイスピード" })
                Assert.That(timers.text, Does.Contain(name));
            Assert.That(timers.preferredHeight, Is.LessThanOrEqualTo(timers.rectTransform.rect.height), "All four effect timers must fit without clipping");
            Rect panelBounds = Bounds((RectTransform)active.transform), textBounds = Bounds(timers.rectTransform);
            Assert.That(panelBounds.Contains(textBounds.min) && panelBounds.Contains(textBounds.max), Is.True, "Timer text must stay inside its card");
        }

        [UnityTest]
        public IEnumerator ModelPolishKeepsTheTankAndBuildingPhysicsDimensions()
        {
            Assert.That(game.Tank.HullCollider.size, Is.EqualTo(new Vector3(2.6f, 1.6f, 3.4f)));
            Assert.That(game.Tank.Visual.GetComponentsInChildren<Collider>(true).Length, Is.Zero);
            var pillar = game.World.Buildings[0].Blocks.First(x => x.Foundation);
            var collider = pillar.GetComponent<BoxCollider>();
            Assert.That(collider.size, Is.EqualTo(Vector3.one));
            Assert.That(pillar.GetComponent<MeshFilter>().sharedMesh.bounds.size, Is.EqualTo(Vector3.one).Using(Vector3ComparerWithEqualsOperator.Instance));
            pillar.Break(pillar.transform.position, 0); yield return null;
            Assert.That(pillar.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(pillar.GetComponent<BoxCollider>(), Is.SameAs(collider));
        }

        [UnityTest]
        public IEnumerator DebrisFadesWithoutOverwritingCameraVisibilityAndPausesWithTheRound()
        {
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            game.CameraRig.enabled = false;
            var pillar = game.World.Buildings[0].Blocks.First(x => x.Foundation);
            var renderer = pillar.GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_CameraVisibility", .4f); renderer.SetPropertyBlock(properties);
            pillar.Break(pillar.transform.position, 0);
            int score = game.BrokenBlocks;
            yield return new WaitForSeconds(pillar.GetComponent<DebrisFade>().Lifetime - .22f);
            Assert.That(pillar != null, Is.True, "A piece must remain through its visible physical collapse");
            renderer.GetPropertyBlock(properties);
            float visibility = properties.GetFloat("_DebrisVisibility");
            Assert.That(visibility, Is.InRange(.05f, .95f), "The piece must fade before it is removed");
            Assert.That(properties.GetFloat("_CameraVisibility"), Is.EqualTo(.4f).Within(.001f));
            game.TogglePause(); yield return new WaitForSecondsRealtime(.12f);
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_DebrisVisibility"), Is.EqualTo(visibility).Within(.001f));
            Assert.That(pillar != null, Is.True);
            game.TogglePause(); yield return new WaitForSeconds(.5f);
            Assert.That(pillar == null, Is.True, "Expired debris must stop obstructing the road");
            Assert.That(game.BrokenBlocks, Is.EqualTo(score), "Cleanup must preserve destruction score");
        }

        [UnityTest]
        public IEnumerator NormalAndGiantExplosionVisualsClearQuickly()
        {
            foreach (bool giant in new[] { false, true })
            {
                Vector3 point = game.Tank.transform.position + Vector3.forward * 12;
                if (giant) game.Effects.SuperImpact(point, game.Settings.gigantBombRadius);
                else game.Effects.Impact(point, game.Stats.BlastRadius);
                Assert.That(game.Effects.GetComponentInChildren<FireballBurst>(), Is.Not.Null);
                Assert.That(game.Effects.GetComponentsInChildren<ParticleSystem>().Length, Is.GreaterThan(0));
                yield return new WaitForSeconds(1.2f);
                Assert.That(game.Effects.GetComponentInChildren<FireballBurst>() == null, Is.True);
                Assert.That(game.Effects.GetComponentsInChildren<ParticleSystem>().Any(x => x.IsAlive()), Is.False, "Fire, sparks and smoke must clear for the next target");
                Assert.That(game.Effects.GetComponentsInChildren<ArcadePulse>().Length, Is.Zero);
            }
        }

        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
