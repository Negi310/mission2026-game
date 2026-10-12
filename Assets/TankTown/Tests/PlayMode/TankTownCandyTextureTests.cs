using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownCandyTextureTests
    {
        [UnityTearDown]
        public IEnumerator TearDown() { TankTownGame.SelectedStage = TownStage.Town; Time.timeScale = 1; yield return null; }

        [UnityTest]
        public IEnumerator BatchedCandyUsesAllTexturesAndSplitSlabsRetainDetailDensity()
        {
            TankTownGame.SelectedStage = TownStage.CandyTown;
            SceneManager.LoadScene("TankTown"); yield return null;
            var game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true; yield return null;
            var textures = game.World.GetComponentsInChildren<Renderer>().SelectMany(x => x.sharedMaterials)
                .Where(x => x != null && x.IsKeywordEnabled("_CANDY_TEXTURE")).Select(x => (Texture2D)x.GetTexture("_BaseMap")).Distinct().ToArray();
            CollectionAssert.AreEquivalent(new[] { "Sponge", "Wafer", "Chocolate", "Cream", "Biscuit", "Glaze", "Strawberry", "CandyStripe", "CandySwirl", "Dough" }, textures.Select(x => x.name).ToArray());
            Assert.That(textures.All(x => x.width <= 1024 && x.mipmapCount > 1 && !x.isReadable), Is.True, "Shared candy maps need bounded memory and filtered distance detail");
            foreach (var building in game.World.Buildings)
                Assert.That(building.Presentation.DetailMesh.HasVertexAttribute(VertexAttribute.TexCoord0), Is.True, "Material batching must retain the texture coordinates");
            var slab = game.World.Buildings.First(x => x.Style == 0).Blocks.First(x => x.Kind == BuildingPart.Slab);
            var material = slab.GetComponent<Renderer>().sharedMaterial; var texture = material.GetTexture("_BaseMap");
            slab.Break(slab.transform.position, 0);
            Assert.That(slab.DebrisBodies.Count, Is.GreaterThan(1));
            Mesh shared = null;
            foreach (var fragment in slab.DebrisBodies)
            {
                var filter = fragment.GetComponent<MeshFilter>(); var mesh = filter.sharedMesh;
                Assert.That(fragment.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
                Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(texture));
                if (shared != null) Assert.That(mesh, Is.SameAs(shared), "Equal-sized fragments must share geometry"); shared = mesh;
                float maxDimension = Mathf.Max(fragment.transform.localScale.x, fragment.transform.localScale.y, fragment.transform.localScale.z);
                float uvExtent = mesh.uv.Max(x => Mathf.Max(x.x, x.y));
                Assert.That(uvExtent, Is.EqualTo(maxDimension).Within(.001f), "Splitting must not squeeze the full slab's texture onto every small fragment");
            }
            TankTownGame.SelectedStage = TownStage.Town; SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); Assert.That(game.BuiltStage, Is.EqualTo(TownStage.Town));
            Assert.That(game.World.GetComponentsInChildren<Renderer>().SelectMany(x => x.sharedMaterials).Any(x => x != null && x.IsKeywordEnabled("_CANDY_TEXTURE")), Is.False);
        }
        [UnityTest]
        public IEnumerator TierFruitAndEmbossedChocolateFollowTheirBrokenWalls()
        {
            TankTownGame.SelectedStage = TownStage.CandyTown; SceneManager.LoadScene("TankTown"); yield return null;
            var game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            var cake = game.World.Buildings.First(x => x.Style == 0);
            var wall = cake.Blocks.First(x => x.Kind == BuildingPart.Wall && x.Level == 1);
            var berries = wall.GetComponentsInChildren<Transform>().Where(x => x.name == "Tier strawberry").ToArray();
            Assert.That(berries.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(berries.All(x => x.position.y < cake.transform.position.y + cake.Floors * game.Settings.floorHeight), Is.True, "Fruit must decorate lower cake tiers too");
            float creamTop = wall.transform.position.y + wall.transform.lossyScale.y / 2 + .35f;
            foreach (var berry in berries)
            {
                var bounds = berry.GetComponent<Renderer>().bounds;
                Assert.That((creamTop - bounds.min.y) / bounds.size.y, Is.InRange(.44f, .54f), "About half the tier berry must be inside the icing");
            }
            var roof = cake.Blocks.First(x => x.name == "Candy roof topper");
            float roofTop = roof.transform.position.y + roof.transform.lossyScale.y / 2;
            foreach (var berry in roof.GetComponentsInChildren<Transform>().Where(x => x.name == "Roof strawberry"))
            {
                var bounds = berry.GetComponent<Renderer>().bounds;
                Assert.That((roofTop - bounds.min.y) / bounds.size.y, Is.InRange(.44f, .54f), "Roof fruit must be seated in the cream, too");
            }
            wall.Break(wall.transform.position, 0);
            foreach (var berry in berries) Assert.That(wall.DebrisBodies, Does.Contain(berry.GetComponentInParent<Rigidbody>()), "Tier fruit must fly with its wall fragments");
            var chocolate = game.World.Buildings.First(x => x.Style == 1).Blocks.First(x => x.Kind == BuildingPart.Wall);
            var cells = chocolate.GetComponentsInChildren<Transform>().Where(x => x.name == "Chocolate square").ToArray();
            Assert.That(cells.Length, Is.InRange(2, 3));
            Assert.That(cells.All(x => x.GetComponent<MeshFilter>().sharedMesh.name.StartsWith("Embossed chocolate") && x.lossyScale.z > .8f), Is.True);
            Assert.That(cells.Select(x => x.GetComponent<MeshFilter>().sharedMesh).Distinct().Count(), Is.EqualTo(1), "Equal chocolate cells must reuse their shape");
            chocolate.Break(chocolate.transform.position, 0);
            foreach (var cell in cells) Assert.That(chocolate.DebrisBodies, Does.Contain(cell.GetComponentInParent<Rigidbody>()));
            foreach (var fragment in chocolate.DebrisBodies)
                Assert.That(fragment.GetComponent<MeshFilter>().sharedMesh.bounds.size.z, Is.LessThan(.3f), "The backing must not cover the embossed chocolate after splitting");
            yield return null;
        }
        [UnityTest]
        public IEnumerator CandySilhouettesHaveCompleteUvsAndOutwardSmoothNormals()
        {
            var geometry = new CandyGeometry();
            try
            {
                Assert.That(geometry.DistantSilhouette(geometry.Strawberry), Is.SameAs(geometry.DistantStrawberry));
                Assert.That(geometry.DistantSilhouette(geometry.Sphere), Is.SameAs(geometry.DistantSphere));
                Assert.That(geometry.DistantStrawberry.triangles.Length, Is.LessThan(geometry.Strawberry.triangles.Length / 2));
                foreach (var mesh in new[] { geometry.Strawberry, geometry.TierStrawberry, geometry.Sphere, geometry.SugarStick, geometry.CaneHook, geometry.CreamRibbon, geometry.DoughnutSlice, geometry.DistantStrawberry, geometry.DistantSphere, geometry.Cone, geometry.DistantCone, geometry.RewardSign, geometry.ChocolateBar(new Vector3(3, 3.2f, .95f)) })
                {
                    Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount), mesh.name);
                    var points = mesh.vertices; var normals = mesh.normals; var triangles = mesh.triangles;
                    Assert.That(normals.All(x => Mathf.Abs(x.magnitude - 1) < .001f), Is.True, mesh.name);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                        var face = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                        if (face.sqrMagnitude < .00000001f) continue;
                        Assert.That(Vector3.Dot(face.normalized, (normals[a] + normals[b] + normals[c]).normalized), Is.GreaterThan(0), mesh.name + " triangle " + i / 3);
                    }
                }
            }
            finally { geometry.Dispose(); }
            yield return null;
        }
    }
}
