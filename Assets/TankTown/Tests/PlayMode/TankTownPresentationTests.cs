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
            yield return new WaitForSeconds(.36f); Canvas.ForceUpdateCanvases();
            var root = (RectTransform)GameObject.Find("Gameplay HUD").transform;
            Rect screen = Bounds(root);
            Rect drivingView = new Rect(screen.x + screen.width * .34f, screen.y + screen.height * .25f, screen.width * .32f, screen.height * .5f);
            foreach (string name in new[] { "Points", "Clock", "Upgrades" })
            {
                Rect panel = Bounds((RectTransform)GameObject.Find(name).transform);
                Assert.That(screen.Contains(panel.min) && screen.Contains(panel.max), Is.True, name + " must fit on screen");
                Assert.That(panel.Overlaps(drivingView), Is.False, name + " must leave the road and aim clear");
            }
            var chip = GameObject.Find("Upgrade chip 1");
            Assert.That(chip.GetComponentsInChildren<Text>().Any(x => x.name == "Level digit" && x.text == "2"), Is.True);
            Assert.That(GameObject.Find("Controls"), Is.Null);
            Assert.That(GameObject.Find("Minimap camera"), Is.Null);
            Assert.That(game.GetComponent<TankTownHUD>().Minimap, Is.Null);
            Assert.That(GameObject.Find("Clock").GetComponent<Image>(), Is.Null);
            Assert.That(root.GetComponentsInChildren<Text>().Any(x => System.Text.RegularExpressions.Regex.IsMatch(x.text, @"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]")), Is.False);
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
            Assert.That(texts.Any(x => (x.name == "Action heading" || x.name == "Action caption") && x.gameObject.activeInHierarchy), Is.False);
        }

        [UnityTest]
        public IEnumerator SpecialCardSeparatesHeldWeaponsFromWideAutomaticEffects()
        {
            Assert.That(game.Items.Receive(ItemKind.Missiles), Is.True); yield return null;
            var hud = game.GetComponent<TankTownHUD>();
            var card = hud.SpecialsView.HeldRoot.gameObject;
            Assert.That(card.activeSelf, Is.False);
            Assert.That(card.GetComponentsInChildren<Image>().Any(x => x.sprite == game.World.SpecialIcons.Sprite(ItemKind.Missiles)), Is.False);
            Assert.That(game.Items.MissilesActive, Is.True); Assert.That(game.Items.Held, Is.EqualTo(ItemKind.None)); yield return null;
            Assert.That(game.Items.Receive(ItemKind.Magnet), Is.True);
            Assert.That(game.Items.Receive(ItemKind.HighSpeed), Is.True);
            Assert.That(game.Items.Receive(ItemKind.Bulldozer), Is.True);
            Assert.That(game.Items.Receive(ItemKind.MegaBeam), Is.True);
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(card.activeSelf, Is.True);
            Assert.That(card.GetComponentsInChildren<Image>().Any(x => x.sprite == game.World.SpecialIcons.Sprite(ItemKind.MegaBeam)), Is.True);
            Assert.That(GameObject.Find("Held special hint").GetComponent<Text>().text, Is.EqualTo("SPACE"));
            Assert.That(hud.SpecialsView.HeldRoot.anchorMin, Is.EqualTo(new Vector2(.5f, 0)));
            Assert.That(hud.SpecialsView.AutomaticRoot.anchorMin, Is.EqualTo(new Vector2(1, 0)));
            foreach (var kind in new[] { ItemKind.Missiles, ItemKind.Magnet, ItemKind.Bulldozer, ItemKind.HighSpeed })
            {
                var effect = GameObject.Find("Effect " + kind);
                Assert.That(effect.transform.parent, Is.SameAs(hud.SpecialsView.AutomaticRoot));
                Assert.That(((RectTransform)effect.transform).rect.width, Is.GreaterThan(70));
                Assert.That(effect.GetComponentsInChildren<Image>().Any(x => x.sprite == game.World.SpecialIcons.Sprite(kind)), Is.True);
                foreach (var text in effect.GetComponentsInChildren<Text>())
                    if (text.text.Length > 0) Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name + ": " + text.text);
            }
            Assert.That(game.Items.UseHeld(), Is.True); Assert.That(game.Items.Receive(ItemKind.GigantBomb), Is.True);
            yield return null;
            Assert.That(GameObject.Find("Effect MegaBeam").transform.parent, Is.SameAs(hud.SpecialsView.WeaponRoot));
            Assert.That(GameObject.Find("Special use prompt").GetComponent<Image>().color, Is.Not.EqualTo(TankTownMenuLayout.Lime));
            Rect held = Bounds(hud.SpecialsView.HeldRoot), automatic = Bounds(hud.SpecialsView.AutomaticRoot), weapon = Bounds(hud.SpecialsView.WeaponRoot);
            Assert.That(held.Overlaps(automatic) || held.Overlaps(weapon) || automatic.Overlaps(weapon), Is.False);
        }

        [UnityTest]
        public IEnumerator UpgradeNumbersQueueEveryGainAndFreezeWhilePaused()
        {
            foreach (var kind in new[] { UpgradeKind.Speed, UpgradeKind.Power, UpgradeKind.Rapid, UpgradeKind.Handling })
                for (int i = 0; i < 3; i++) Assert.That(game.Stats.Upgrade(kind), Is.True);
            var hud = game.GetComponent<TankTownHUD>(); yield return null;
            foreach (var kind in new[] { UpgradeKind.Speed, UpgradeKind.Power, UpgradeKind.Rapid, UpgradeKind.Handling })
            {
                Assert.That(game.Stats.Level(kind), Is.EqualTo(3), "Actual upgrades apply immediately");
                Assert.That(hud.UpgradesView.Counting(kind), Is.True);
                Assert.That(GameObject.Find("Upgrade chip " + (int)kind).GetComponentsInChildren<Text>().Any(x => x.text == "+3"), Is.True);
            }
            game.TogglePause(); yield return null;
            var digit = GameObject.Find("Upgrade chip 0").GetComponentsInChildren<Text>().Single(x => x.name == "Level digit");
            Vector2 position = digit.rectTransform.anchoredPosition; string value = digit.text;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(digit.text, Is.EqualTo(value)); Assert.That(digit.rectTransform.anchoredPosition, Is.EqualTo(position));
            game.TogglePause(); yield return new WaitForSeconds(.55f);
            foreach (var kind in new[] { UpgradeKind.Speed, UpgradeKind.Power, UpgradeKind.Rapid, UpgradeKind.Handling })
            { Assert.That(hud.UpgradesView.DisplayedLevel(kind), Is.EqualTo(3)); Assert.That(hud.UpgradesView.Counting(kind), Is.False); }
        }

        [UnityTest]
        public IEnumerator EffectResetShowsOnlyTimeAddedAndBombShowsFlightState()
        {
            Assert.That(game.Items.Receive(ItemKind.Magnet), Is.True);
            yield return new WaitForSeconds(.4f);
            float before = game.Items.Remaining(ItemKind.Magnet);
            Assert.That(game.Items.Receive(ItemKind.Magnet), Is.True); yield return null;
            var magnet = GameObject.Find("Effect Magnet");
            string expected = $"+{game.Items.Duration(ItemKind.Magnet) - before:0.0}s";
            Assert.That(magnet.GetComponentsInChildren<Text>().Single(x => x.name == "Effect extension").text, Is.EqualTo(expected));
            var ring = magnet.GetComponentsInChildren<Image>().Single(x => x.name == "Effect remaining ring");
            Assert.That(ring.fillAmount, Is.GreaterThan(.99f));
            Assert.That(game.Items.Receive(ItemKind.GigantBomb) && game.Items.UseHeld(), Is.True); yield return null;
            var bomb = GameObject.Find("Effect GigantBomb");
            Assert.That(bomb.GetComponentsInChildren<Text>().Single(x => x.name == "Effect time").text, Is.EqualTo("↗"));
            Assert.That(bomb.GetComponentsInChildren<Image>().Any(x => x.name == "Effect remaining ring"), Is.False);
            game.Items.FinishRound(); yield return null;
            Assert.That(GameObject.Find("Effect Magnet"), Is.Null); Assert.That(GameObject.Find("Effect GigantBomb"), Is.Null);
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
