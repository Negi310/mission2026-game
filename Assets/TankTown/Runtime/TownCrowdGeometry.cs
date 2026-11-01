using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    // The same rigid body parts and four animated joints, drawn in three material passes.
    // Two shared meshes (hat / hair) replace the separate renderer for every shoe, eye and sleeve.
    internal sealed class TownCrowdGeometry
    {
        private readonly Dictionary<(Mesh head, bool hat), Mesh> meshes = new Dictionary<(Mesh, bool), Mesh>();
        private static readonly Vector3[] JointOrigins = {
            Vector3.zero, new Vector3(-.16f, .68f, 0), new Vector3(.16f, .68f, 0),
            new Vector3(-.39f, 1.42f, 0), new Vector3(.39f, 1.42f, 0)
        };
        internal int MeshCount => meshes.Count;

        internal void Draw(Transform model, Transform[] limbs, int variant, Material shirt, Material skin, Material dark, Mesh head)
        {
            var bones = new Transform[5]; bones[0] = model;
            for (int i = 0; i < limbs.Length; i++)
            {
                limbs[i] = new GameObject(i < 2 ? "Walking leg" : "Walking arm").transform;
                limbs[i].SetParent(model, false); limbs[i].localPosition = JointOrigins[i + 1]; bones[i + 1] = limbs[i];
            }
            bool hat = variant % 3 == 0;
            if (!meshes.TryGetValue((head, hat), out var mesh))
            {
                mesh = Build(head, hat); meshes.Add((head, hat), mesh);
            }
            // Retain the source head reference used by opt-in diagnostics, without another renderer.
            var reference = new GameObject("Faceted head", typeof(MeshFilter)); reference.transform.SetParent(model, false);
            reference.transform.localPosition = Vector3.up * 1.74f; reference.transform.localScale = Vector3.one * .65f;
            reference.GetComponent<MeshFilter>().sharedMesh = head;
            var renderer = model.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.sharedMaterials = new[] { shirt, skin, dark };
            renderer.bones = bones; renderer.rootBone = model; renderer.quality = SkinQuality.Bone1;
            renderer.localBounds = new Bounds(Vector3.up * 1.15f, new Vector3(2.2f, 3.4f, 2));
            renderer.updateWhenOffscreen = false;
        }

        private static Mesh Build(Mesh head, bool hat)
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube); source.SetActive(false);
            var cube = source.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(source);
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>();
            var colors = new List<Color>(); var weights = new List<BoneWeight>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            void Add(Mesh shape, Vector3 center, Vector3 size, int bone, int material)
            {
                var points = shape.vertices; var sourceNormals = shape.normals; var sourceUv = shape.uv; var sourceColors = shape.colors;
                int offset = vertices.Count;
                for (int i = 0; i < points.Length; i++)
                {
                    vertices.Add(Vector3.Scale(points[i], size) + center + JointOrigins[bone]);
                    var n = sourceNormals[i]; normals.Add(new Vector3(n.x / size.x, n.y / size.y, n.z / size.z).normalized);
                    uv.Add(sourceUv.Length == points.Length ? sourceUv[i] : Vector2.zero);
                    colors.Add(sourceColors.Length == points.Length ? sourceColors[i] : Color.white);
                    weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1 });
                }
                foreach (int index in shape.GetTriangles(0)) triangles[material].Add(offset + index);
            }
            Add(cube, new Vector3(0, 1.08f, 0), new Vector3(.62f, .75f, .4f), 0, 0);
            Add(head, new Vector3(0, 1.74f, 0), Vector3.one * .65f, 0, 1);
            Add(cube, new Vector3(0, 2, -.02f), new Vector3(.59f, .17f, .49f), 0, hat ? 0 : 2);
            if (hat) Add(cube, new Vector3(0, 1.98f, .15f), new Vector3(.66f, .06f, .42f), 0, 0);
            for (int side = -1; side <= 1; side += 2)
            {
                Add(cube, new Vector3(side * .115f, 1.77f, .292f), new Vector3(.075f, .09f, .05f), 0, 2);
                int leg = side == -1 ? 1 : 2, arm = leg + 2;
                Add(cube, Vector3.down * .28f, new Vector3(.25f, .6f, .28f), leg, 2);
                Add(cube, new Vector3(0, -.57f, .08f), new Vector3(.29f, .15f, .43f), leg, 2);
                Add(cube, Vector3.down * .27f, new Vector3(.19f, .55f, .23f), arm, 0);
                Add(cube, Vector3.down * .58f, Vector3.one * .2f, arm, 1);
            }
            var bindPoses = new Matrix4x4[JointOrigins.Length];
            for (int i = 0; i < bindPoses.Length; i++) bindPoses[i] = Matrix4x4.Translate(-JointOrigins[i]);
            var mesh = new Mesh { name = hat ? "Shared citizen with hat" : "Shared citizen with hair", indexFormat = IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetColors(colors);
            mesh.subMeshCount = 3;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.boneWeights = weights.ToArray(); mesh.bindposes = bindPoses; mesh.RecalculateBounds();
            return mesh;
        }

        internal void Dispose()
        {
            foreach (var mesh in meshes.Values) if (mesh != null) Object.Destroy(mesh);
            meshes.Clear();
        }
    }
}
