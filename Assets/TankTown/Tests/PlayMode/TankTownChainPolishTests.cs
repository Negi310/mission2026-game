using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TankTown.Tests
{
    public sealed class TankTownChainPolishTests
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
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1; Time.captureDeltaTime = captureStep; yield return null; }

        [UnityTest]
        public IEnumerator RealCannonHitsTheEnlargedSideOfAWalkingPedestrian()
        {
            var person = game.World.Pedestrians[0]; person.gameObject.SetActive(true);
            var body = game.Tank.GetComponent<Rigidbody>(); body.isKinematic = false;
            game.Tank.Warp(new Pose(person.transform.position - Vector3.forward * 6 + Vector3.right * .8f, Quaternion.identity)); body.isKinematic = true;
            Physics.SyncTransforms();
            Assert.That(person.HitRadius, Is.GreaterThan(.8f));
            game.Input.PulsePad(0); Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Not.Null);
            yield return new WaitForSeconds(.2f);
            Assert.That(person.Knockbacks, Is.EqualTo(1), "A shot offset .8m must hit instead of passing through the old narrow trigger");
            Assert.That(person.Walking, Is.False); Assert.That(person.GetComponent<Collider>().enabled, Is.False);
            Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Null);
            yield return new WaitForSeconds(2.3f);
            Assert.That(person.Walking, Is.True); Assert.That(person.GetComponent<Collider>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator MissilesTrackAndContactAWalkingPedestrianWhenNoCarsAreAvailable()
        {
            var person = new GameObject("Walking missile target").AddComponent<TownPedestrian>(); person.transform.SetParent(game.World.transform);
            var head = game.World.Pedestrians[0].transform.Find("Low poly citizen/Faceted head").GetComponent<MeshFilter>().sharedMesh;
            person.Initialize(game, new[] { new Vector3(0, 0, -60), new Vector3(0, 0, -20) }, 0, 1, game.World.ShotMaterial, game.World.ShotMaterial, game.World.ShotMaterial, head);
            game.World.Pedestrians.Add(person);
            var body = game.Tank.GetComponent<Rigidbody>(); body.isKinematic = false;
            game.Tank.Warp(new Pose(person.transform.position - Vector3.forward * 26, Quaternion.identity)); body.isKinematic = true;
            Physics.SyncTransforms(); game.Items.Receive(ItemKind.Missiles); game.Input.PulsePad(0);
            var missiles = Object.FindObjectsByType<ItemMissile>(FindObjectsSortMode.None);
            Assert.That(missiles.Length, Is.EqualTo(5));
            foreach (var missile in missiles) Assert.That(missile.TrackingTarget, Is.EqualTo(person.GetComponent<TownMovingTarget>()));
            for (int i = 0; i < 160 && game.Items.MissileTrackedStrikes == 0; i++) yield return new WaitForFixedUpdate();
            Assert.That(game.Items.MissileTrackedStrikes, Is.GreaterThan(0), $"strikes={game.Items.MissileStrikes}, knockbacks={person.Knockbacks}");
            Assert.That(person.Knockbacks, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator ChainDisplayCountsUpKeepsTheDeepestBranchPausesExpiresAndHidesAtResults()
        {
            var display = Object.FindFirstObjectByType<TankTownChainDisplay>();
            Vector3 isolated = new Vector3(180, 12, 0);
            game.ChainBlast(isolated, .2f, 1); yield return null;
            Assert.That(display.Visible, Is.True, "The first explosion also needs feedback");
            game.ChainBlast(isolated, .2f, 2); game.ChainBlast(isolated, .2f, 3); game.ChainBlast(isolated, .2f, 1);
            yield return null;
            Assert.That(display.TargetDepth, Is.EqualTo(3)); Assert.That(display.DisplayedDepth, Is.LessThan(3), "The number should animate toward the new depth");
            Assert.That(GameObject.Find("Chain addition").GetComponent<Text>().text, Is.EqualTo("+1"));
            yield return new WaitForSeconds(.25f); Canvas.ForceUpdateCanvases();
            Assert.That(display.DisplayedDepth, Is.EqualTo(3)); Assert.That(display.DisplayedExplosions, Is.EqualTo(4));
            Assert.That(game.LongestChain, Is.EqualTo(3)); Assert.That(game.ChainExplosions, Is.EqualTo(4));
            Assert.That(GameObject.Find("Chain totals").GetComponent<Text>().text, Does.Contain("誘爆合計 4回"));
            foreach (var label in display.GetComponentsInChildren<Text>())
            {
                Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width), label.name);
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height), label.name);
            }
            game.TogglePause(); yield return null; Assert.That(display.Visible, Is.False);
            yield return new WaitForSecondsRealtime(.1f); game.TogglePause(); yield return null; Assert.That(display.Visible, Is.True);
            yield return new WaitForSeconds(2.1f); Assert.That(display.Visible, Is.False);
            game.ChainBlast(isolated, .2f, 1); yield return new WaitForSeconds(.25f);
            Assert.That(display.DisplayedDepth, Is.EqualTo(1)); Assert.That(display.DisplayedExplosions, Is.EqualTo(5));
            game.AdvanceTime(1000); yield return null; Assert.That(display.Visible, Is.False);
        }
    }
}
