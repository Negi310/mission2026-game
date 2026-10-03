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
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
            game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1; yield return null; }

        [UnityTest]
        public IEnumerator CompactHudLeavesTheDrivingViewOpenAndTracksActualUpgrades()
        {
            game.Stats.Upgrade(UpgradeKind.Power); game.Stats.Upgrade(UpgradeKind.Power);
            yield return null; Canvas.ForceUpdateCanvases();
            var root = (RectTransform)GameObject.Find("Gameplay HUD").transform;
            Rect screen = Bounds(root);
            Rect drivingView = new Rect(screen.x + screen.width * .34f, screen.y + screen.height * .25f, screen.width * .32f, screen.height * .5f);
            foreach (string name in new[] { "Destruction", "Clock", "Upgrades", "Special items", "Controls" })
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
        public IEnumerator SpecialCardFollowsHeldWeaponAndActiveEffects()
        {
            Assert.That(game.Items.Receive(ItemKind.Missiles), Is.True); yield return null;
            var card = GameObject.Find("Special items");
            Assert.That(card.GetComponentsInChildren<Text>().Any(x => x.text == "ミサイル"), Is.True);
            Assert.That(card.GetComponentsInChildren<Image>().Any(x => x.sprite == game.World.SpecialIcons.Sprite(ItemKind.Missiles)), Is.True);
            Assert.That(game.Items.UseHeld(), Is.True); yield return null;
            Assert.That(card.GetComponentsInChildren<Text>().Any(x => x.text == "発動中！"), Is.True);
            Assert.That(game.Items.Receive(ItemKind.Magnet), Is.True);
            Assert.That(game.Items.Receive(ItemKind.HighSpeed), Is.True);
            Assert.That(game.Items.Receive(ItemKind.Bulldozer), Is.True);
            yield return null; Canvas.ForceUpdateCanvases();
            var active = GameObject.Find("Active item timers");
            var timers = active.GetComponentInChildren<Text>();
            foreach (string name in new[] { "ミサイル", "マグネット", "ブルドーザー", "ハイスピード" })
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

        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
