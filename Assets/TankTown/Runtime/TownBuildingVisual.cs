using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    // Physics keeps individual parts. Only distant renderers use a shared low-detail mesh.
    internal sealed class TownBuildingVisual
    {
        private readonly BlockBuilding building;
        private readonly List<(DestructibleBlock block, Renderer[] renderers)> parts = new List<(DestructibleBlock, Renderer[])>();
        private readonly Mesh cube;
        private Mesh mesh;
        private readonly MeshFilter distantFilter, mapFilter;
        private readonly MeshRenderer distant, map;
        private bool detailed = true;
        private int renderedDetached = -1;
        internal bool Detailed => detailed;
        internal Mesh MapMesh => mesh;
        internal TownBuildingVisual(BlockBuilding owner, Mesh box)
        {
            building = owner; cube = box;
            foreach (var block in building.Blocks) parts.Add((block, block.GetComponentsInChildren<Renderer>()));
            var far = new GameObject("Distant building mesh", typeof(MeshFilter), typeof(MeshRenderer)); far.transform.SetParent(building.transform, false);
            distantFilter = far.GetComponent<MeshFilter>(); distant = far.GetComponent<MeshRenderer>();
            var overhead = new GameObject("Minimap building mesh", typeof(MeshFilter), typeof(MeshRenderer)); overhead.transform.SetParent(building.transform, false);
            overhead.layer = TownPhysics.MapLayer;
            mapFilter = overhead.GetComponent<MeshFilter>(); map = overhead.GetComponent<MeshRenderer>();
            map.shadowCastingMode = ShadowCastingMode.Off; map.receiveShadows = false;
            Rebuild(); distant.enabled = false;
        }
        internal void Tick(Vector3 view)
        {
            float distance = (view - building.transform.position).sqrMagnitude;
            bool next = distance < (detailed ? 115 * 115 : 100 * 100);
            if (!next) RefreshMapMesh();
            if (next == detailed) return;
            detailed = next;
            foreach (var part in parts)
            {
                if (part.block == null || part.block.Detached) continue;
                foreach (var renderer in part.renderers) if (renderer != null) renderer.enabled = detailed;
            }
            distant.enabled = !detailed;
        }
        internal void RefreshMapMesh() { if (renderedDetached != building.DetachedParts) Rebuild(); }
        private void Rebuild()
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            Matrix4x4 local = building.transform.worldToLocalMatrix;
            foreach (var part in parts)
            {
                if (part.block == null || part.block.Detached) continue;
                foreach (var renderer in part.renderers)
                {
                    if (renderer == null || renderer.sharedMaterial == null) continue;
                    var material = renderer.sharedMaterial;
                    if (!groups.TryGetValue(material, out var list)) groups[material] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = cube, transform = local * renderer.transform.localToWorldMatrix });
                }
            }
            var combined = new List<CombineInstance>(); var materials = new List<Material>(); var temporary = new List<Mesh>();
            foreach (var group in groups)
            {
                var piece = new Mesh { indexFormat = IndexFormat.UInt32 }; piece.CombineMeshes(group.Value.ToArray(), true, true);
                temporary.Add(piece); combined.Add(new CombineInstance { mesh = piece, transform = Matrix4x4.identity }); materials.Add(group.Key);
            }
            var next = new Mesh { name = "Lightweight " + building.name, indexFormat = IndexFormat.UInt32 };
            if (combined.Count > 0)
            {
                next.CombineMeshes(combined.ToArray(), false, true);
                next.RecalculateBounds(); next.UploadMeshData(true);
            }
            distantFilter.sharedMesh = mapFilter.sharedMesh = next;
            distant.sharedMaterials = map.sharedMaterials = materials.ToArray();
            foreach (var piece in temporary) Object.Destroy(piece);
            if (mesh != null) Object.Destroy(mesh);
            mesh = next; renderedDetached = building.DetachedParts;
        }
        internal void Dispose() { if (mesh != null) Object.Destroy(mesh); }
    }
}
