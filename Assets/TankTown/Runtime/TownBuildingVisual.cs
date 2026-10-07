using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    // Physics keeps individual parts. Intact geometry is drawn once per material;
    // cracked parts use their original renderers and detached parts keep their bodies.
    internal sealed class TownBuildingVisual
    {
        private readonly BlockBuilding building;
        private readonly List<(DestructibleBlock block, Renderer[] renderers)> parts = new List<(DestructibleBlock, Renderer[])>();
        private readonly Mesh cube;
        private Mesh mesh;
        private Mesh nearMesh;
        private readonly MeshFilter nearFilter;
        private readonly MeshRenderer nearRenderer;
        private readonly MeshFilter distantFilter, mapFilter;
        private readonly MeshRenderer distant, map;
        private bool detailed = true;
        private int renderedDetached = -1;
        private int nearDetached = -1, nearDamage = -1;
        internal bool Detailed => detailed;
        internal Mesh MapMesh => mesh;
        internal Mesh DetailMesh => nearMesh;
        internal bool NeedsDetailRefresh => detailed && (nearDetached != building.DetachedParts || nearDamage != building.DamageRevision);
        internal TownBuildingVisual(BlockBuilding owner, Mesh box)
        {
            building = owner; cube = box;
            foreach (var block in building.Blocks) parts.Add((block, block.GetComponentsInChildren<Renderer>()));
            var near = new GameObject("Batched building mesh", typeof(MeshFilter), typeof(MeshRenderer)); near.transform.SetParent(building.transform, false);
            nearFilter = near.GetComponent<MeshFilter>(); nearRenderer = near.GetComponent<MeshRenderer>();
            var far = new GameObject("Distant building mesh", typeof(MeshFilter), typeof(MeshRenderer)); far.transform.SetParent(building.transform, false);
            distantFilter = far.GetComponent<MeshFilter>(); distant = far.GetComponent<MeshRenderer>();
            var overhead = new GameObject("Minimap building mesh", typeof(MeshFilter), typeof(MeshRenderer)); overhead.transform.SetParent(building.transform, false);
            overhead.layer = TownPhysics.MapLayer;
            mapFilter = overhead.GetComponent<MeshFilter>(); map = overhead.GetComponent<MeshRenderer>();
            map.shadowCastingMode = ShadowCastingMode.Off; map.receiveShadows = false;
            Rebuild(); RebuildNear(); distant.enabled = false;
            UpdateOriginalRenderers();
        }
        internal void Tick(Vector3 view)
        {
            float distance = (view - building.transform.position).sqrMagnitude;
            bool next = distance < (detailed ? 115 * 115 : 100 * 100);
            if (!next) RefreshMapMesh();
            bool changed = nearDetached != building.DetachedParts || nearDamage != building.DamageRevision;
            if (next && changed) RebuildNear();
            if (next == detailed && (!next || !changed)) return;
            detailed = next;
            UpdateOriginalRenderers();
            nearRenderer.enabled = detailed;
            distant.enabled = !detailed;
        }
        private void UpdateOriginalRenderers()
        {
            foreach (var part in parts)
            {
                if (part.block == null || part.block.Detached) continue;
                foreach (var renderer in part.renderers) if (renderer != null) renderer.enabled = detailed && part.block.RenderSeparately;
            }
        }
        private void RebuildNear()
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            Matrix4x4 local = building.transform.worldToLocalMatrix;
            foreach (var part in parts)
            {
                if (part.block == null || part.block.Detached || part.block.RenderSeparately) continue;
                foreach (var renderer in part.renderers)
                {
                    if (renderer == null || renderer.sharedMaterial == null) continue;
                    var filter = renderer.GetComponent<MeshFilter>(); if (filter == null) continue;
                    if (!groups.TryGetValue(renderer.sharedMaterial, out var list)) groups[renderer.sharedMaterial] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = local * renderer.transform.localToWorldMatrix });
                }
            }
            var next = Combine(groups, "Batched " + building.name, out var materials);
            nearFilter.sharedMesh = next; nearRenderer.sharedMaterials = materials;
            if (nearMesh != null) Object.Destroy(nearMesh);
            nearMesh = next; nearDetached = building.DetachedParts; nearDamage = building.DamageRevision;
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
            var next = Combine(groups, "Lightweight " + building.name, out var materials);
            distantFilter.sharedMesh = mapFilter.sharedMesh = next;
            distant.sharedMaterials = map.sharedMaterials = materials;
            if (mesh != null) Object.Destroy(mesh);
            mesh = next; renderedDetached = building.DetachedParts;
        }
        internal static Mesh Combine(Dictionary<Material, List<CombineInstance>> groups, string label, out Material[] sharedMaterials)
        {
            var combined = new List<CombineInstance>(); var materials = new List<Material>(); var temporary = new List<Mesh>();
            foreach (var group in groups)
            {
                var piece = new Mesh { indexFormat = IndexFormat.UInt32 }; piece.CombineMeshes(group.Value.ToArray(), true, true);
                temporary.Add(piece); combined.Add(new CombineInstance { mesh = piece, transform = Matrix4x4.identity }); materials.Add(group.Key);
            }
            var next = new Mesh { name = label, indexFormat = IndexFormat.UInt32 };
            if (combined.Count > 0)
            {
                next.CombineMeshes(combined.ToArray(), false, true);
                next.RecalculateBounds(); next.UploadMeshData(true);
            }
            foreach (var piece in temporary) Object.Destroy(piece);
            sharedMaterials = materials.ToArray(); return next;
        }
        internal void Dispose() { if (mesh != null) Object.Destroy(mesh); if (nearMesh != null) Object.Destroy(nearMesh); }
    }
}
