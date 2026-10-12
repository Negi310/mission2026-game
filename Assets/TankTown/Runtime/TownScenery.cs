using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public sealed class TownScenery : MonoBehaviour
    {
        private readonly List<Transform> clouds = new List<Transform>();
        private Mesh coastMesh;
        private Material previousSky;
        private Material sky;
        private float cloudWrap;

        public Material Build(TankTownWorld world, TankTownGame game)
        {
            previousSky = RenderSettings.skybox;
            cloudWrap = 330 * game.StageScale;
            sky = new Material(Shader.Find("TankTown/Sky Gradient"));
            sky.SetColor("_Horizon", new Color(.5f, .86f, 1));
            sky.SetColor("_Zenith", new Color(.12f, .56f, 1));
            RenderSettings.skybox = sky;
            var sea = world.MakeMaterial(new Color(.035f, .62f, .82f), ToySurface.Glass);
            sea.SetFloat("_Gloss", .15f);
            var sand = world.MakeMaterial(new Color(1, .81f, .45f), ToySurface.Concrete);
            var rock = world.MakeMaterial(new Color(.76f, .57f, .35f), ToySurface.Concrete);
            var white = world.MakeMaterial(new Color(.97f, .99f, 1));
            var green = world.MakeMaterial(new Color(.15f, .73f, .48f), ToySurface.Organic);
            world.Box("Ocean horizon", new Vector3(0, -3.8f, 0), new Vector3(4000, .12f, 4000), sea, solid: false);
            float edge = world.HalfExtent;
            foreach (bool vertical in new[] { false, true }) foreach (int side in new[] { -1, 1 })
            {
                var origin = vertical ? new Vector2(side < 0 ? -edge : edge - 5, -edge) : new Vector2(-edge, side < 0 ? -edge : edge - 5);
                world.Terrain.Surface("Coastal sand", origin, vertical ? new Vector2(5, edge * 2) : new Vector2(edge * 2, 5), .065f, sand);
                for (float p = -edge + 5; p < edge - 4; p += 8)
                {
                    var point = vertical ? new Vector3(side * (edge - 3.4f), 0, p) : new Vector3(p, 0, side * (edge - 3.4f));
                    var rail = new GameObject("Crushable coastal guardrail"); rail.transform.SetParent(world.transform); rail.transform.position = world.GroundPoint(point, .6f);
                    if (vertical) rail.transform.rotation = Quaternion.Euler(0, 90, 0);
                    rail.AddComponent<BoxCollider>().size = new Vector3(7.7f, 1.2f, .4f);
                    world.Box("Coastal rail", Vector3.up * .25f, new Vector3(7.7f, .36f, .28f), white, rail.transform, false);
                    foreach (int end in new[] { -1, 1 }) world.Box("Rail post", new Vector3(end * 3.4f, -.05f, 0), new Vector3(.24f, 1.1f, .24f), rock, rail.transform, false);
                    var block = rail.AddComponent<DestructibleBlock>(); block.CanCrush = true; block.Kind = BuildingPart.Prop; block.Initialize(game);
                }
            }
            BuildCoast(world, rock, sand);
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2 / 10;
                var position = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (Mathf.Max(240, edge * Mathf.Sqrt(2) + 50) + i % 3 * 32);
                var island = GameObject.CreatePrimitive(PrimitiveType.Sphere); island.name = "Distant island"; island.transform.SetParent(transform);
                island.transform.position = position + Vector3.up * (-6 + i % 3 * 3);
                island.transform.localScale = new Vector3(48 + i % 3 * 16, 26 + i % 3 * 16, 52);
                island.GetComponent<Renderer>().sharedMaterial = green;
                island.GetComponent<Collider>().enabled = false; Destroy(island.GetComponent<Collider>());
            }
            for (int i = 0; i < 16; i++)
            {
                float angle = (i + .3f) * Mathf.PI * 2 / 16;
                var cloud = new GameObject("Soft toy cloud"); cloud.transform.SetParent(transform);
                cloud.transform.position = new Vector3(Mathf.Cos(angle) * (150 + i % 4 * 31) * game.StageScale, 48 + i % 5 * 4, Mathf.Sin(angle) * (150 + i % 4 * 31) * game.StageScale);
                for (int lobe = 0; lobe < 5; lobe++)
                {
                    var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere); puff.transform.SetParent(cloud.transform, false);
                    puff.transform.localPosition = new Vector3((lobe - 2) * 6, lobe % 2 * 2, lobe % 2 * 2);
                    puff.transform.localScale = new Vector3(13, 6 + lobe % 2 * 3, 9);
                    puff.GetComponent<Renderer>().sharedMaterial = white;
                    puff.GetComponent<Collider>().enabled = false; Destroy(puff.GetComponent<Collider>());
                }
                clouds.Add(cloud.transform);
            }
            return sky;
        }

        private void BuildCoast(TankTownWorld world, Material rock, Material sand)
        {
            var vertices = new List<Vector3>(); var cliffs = new List<int>(); var beaches = new List<int>(); float edge = world.HalfExtent;
            const int segments = 68;
            for (int side = 0; side < 4; side++) for (int n = 0; n < segments; n++)
            {
                float a = -edge + edge * 2 * n / segments, b = -edge + edge * 2 * (n + 1) / segments;
                var rotation = Quaternion.Euler(0, side * 90, 0);
                var p = rotation * new Vector3(a, 0, edge); var q = rotation * new Vector3(b, 0, edge);
                int first = vertices.Count;
                vertices.Add(world.GroundPoint(p)); vertices.Add(world.GroundPoint(q));
                float waveA = CoastVariation((float)n / segments, side), waveB = CoastVariation((float)(n + 1) / segments, side);
                vertices.Add(rotation * new Vector3(a + a / edge * 6, -1.3f, edge + 6 + waveA * .25f));
                vertices.Add(rotation * new Vector3(b + b / edge * 6, -1.3f, edge + 6 + waveB * .25f));
                vertices.Add(rotation * new Vector3(a + a / edge * 14, -3.65f, edge + 14 + waveA));
                vertices.Add(rotation * new Vector3(b + b / edge * 14, -3.65f, edge + 14 + waveB));
                cliffs.AddRange(new[] { first, first + 2, first + 1, first + 1, first + 2, first + 3 });
                beaches.AddRange(new[] { first + 2, first + 4, first + 3, first + 3, first + 4, first + 5 });
            }
            coastMesh = new Mesh { name = "Natural island coastline", subMeshCount = 2 }; coastMesh.SetVertices(vertices); coastMesh.SetTriangles(cliffs, 0); coastMesh.SetTriangles(beaches, 1); coastMesh.RecalculateNormals(); coastMesh.RecalculateBounds();
            var coast = new GameObject("Island cliffs", typeof(MeshFilter), typeof(MeshRenderer)); coast.transform.SetParent(transform, false);
            coast.GetComponent<MeshFilter>().sharedMesh = coastMesh; coast.GetComponent<Renderer>().sharedMaterials = new[] { rock, sand };
        }

        private static float CoastVariation(float t, int side)
            => Mathf.Sin(t * Mathf.PI) * (6 + 5 * Mathf.Sin(t * Mathf.PI * 4 + side * 1.3f) + 4 * Mathf.Sin(t * Mathf.PI * 11));

        private void Update()
        {
            foreach (var cloud in clouds)
            {
                cloud.position += Vector3.right * (.45f * Time.deltaTime);
                if (cloud.position.x > cloudWrap) cloud.position += Vector3.left * cloudWrap * 2;
            }
        }

        private void OnDestroy()
        {
            if (RenderSettings.skybox == sky) RenderSettings.skybox = previousSky;
            if (coastMesh != null) Destroy(coastMesh);
        }
    }
}
