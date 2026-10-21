using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownTankModelTests
    {
        private TankTownGame game;
        private TownStage previousStage;
        private bool previousSkip;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousStage = TankTownGame.SelectedStage; previousSkip = TankTownGame.SkipIntroForTests;
            TankTownGame.SelectedStage = TownStage.Town; TankTownGame.SkipIntroForTests = true;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart();
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TankTownGame.SelectedStage = previousStage; TankTownGame.SkipIntroForTests = previousSkip;
            Time.timeScale = 1; yield return null;
        }

        [UnityTest]
        public IEnumerator ImportedModelKeepsPivotsSizeAndToyMaterials()
        {
            var visual = game.Tank.Visual;
            Assert.That(visual.name, Is.EqualTo("Tank visual"));
            Assert.That(visual.Find("Hull").GetComponent<MeshRenderer>().sharedMaterials.Length, Is.GreaterThan(1), "The Blender model must be used, not the box tank");
            Assert.That(visual.Find("Tracks"), Is.Not.Null);
            Assert.That(game.Tank.Turret, Is.SameAs(visual.Find("Turret pivot")));
            Assert.That(Vector3.Distance(game.Tank.Turret.localPosition, new Vector3(0, .68f, -.15f)), Is.LessThan(.001f));
            var cannon = game.Tank.Muzzle.parent;
            Assert.That(cannon.name, Is.EqualTo("Fixed cannon pivot"));
            Assert.That(Vector3.Distance(cannon.localPosition, new Vector3(0, .04f, .55f)), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(game.Tank.Muzzle.localPosition, new Vector3(0, 0, 2.8f)), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(cannon.localRotation, Quaternion.Euler(-game.Settings.cannonElevation, 0, 0)), Is.LessThan(.01f));

            // Item effects (shield, blade) also live under the visual; only check the model's own parts.
            var parts = new[] { "Hull", "Tracks", "Turret", "Fixed cannon" };
            var renderers = visual.GetComponentsInChildren<MeshRenderer>(true).Where(x => parts.Contains(x.name)).ToArray();
            Assert.That(renderers.Select(x => x.name), Is.EquivalentTo(parts));
            Assert.That(renderers.SelectMany(x => x.sharedMaterials).All(x => x != null && x.shader.name == "TankTown/Saturated Toon"), Is.True,
                "Imported placeholder materials must all be replaced by the toy materials");
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, "The visual must not add colliders to the tank");
            Assert.That(visual.GetComponentsInChildren<Animator>(true), Is.Empty, "The static model must be imported without an animator");

            // Size and ground line in the visual's own space (meters, Y up, Z forward).
            var lo = Vector3.one * float.MaxValue; var hi = Vector3.one * float.MinValue;
            foreach (var renderer in renderers)
            {
                var b = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = visual.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p);
                }
            }
            Assert.That(lo.y, Is.EqualTo(-.8f).Within(.02f), "Tracks must touch the ground line of the 1.6m hull collider");
            Assert.That(hi.x - lo.x, Is.InRange(3f, 3.4f));
            Assert.That(-lo.z, Is.InRange(1.8f, 2.2f));
            Assert.That(hi.z, Is.InRange(2.9f, 3.2f), "The muzzle must stay in front of the shot origin pivot");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BulldozerPaintsOnlyTheBodyAndRestoresIt()
        {
            var hull = game.Tank.Visual.Find("Hull").GetComponent<Renderer>();
            var tracks = game.Tank.Visual.Find("Tracks").GetComponent<Renderer>();
            var original = hull.sharedMaterials; var originalTracks = tracks.sharedMaterials;
            Assert.That(original, Has.Member(game.World.TankPaint));

            game.Settings.bulldozerSeconds = 4;
            game.Items.Receive(ItemKind.Bulldozer); yield return null;
            var painted = hull.sharedMaterials;
            Assert.That(painted, Has.No.Member(game.World.TankPaint), "Every body paint slot must turn yellow");
            for (int i = 0; i < original.Length; i++)
                if (original[i] != game.World.TankPaint) Assert.That(painted[i], Is.SameAs(original[i]), "Details keep their own materials");
            Assert.That(painted.Distinct().Count(), Is.GreaterThan(1));
            Assert.That(tracks.sharedMaterials, Is.EqualTo(originalTracks));

            game.Items.FinishRound(); yield return null;
            Assert.That(hull.sharedMaterials, Is.EqualTo(original));
            Assert.That(game.Tank.VehicleScale, Is.EqualTo(1));
        }
    }
}
