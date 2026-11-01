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
        private readonly TownMeshWorkspace workspace;
        private readonly List<(DestructibleBlock block, Mesh near, Mesh far, Material material, Matrix4x4 matrix)> sources = new List<(DestructibleBlock, Mesh, Mesh, Material, Matrix4x4)>();
        private Mesh mesh;
        private Mesh nearMesh;
        private readonly Bounds detailBounds;
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
        internal TownBuildingVisual(BlockBuilding owner, Mesh box, TownMeshWorkspace sharedWorkspace, System.Func<Mesh, Mesh> candySilhouette = null)
        {
            building = owner; workspace = sharedWorkspace;
            Matrix4x4 local = building.transform.worldToLocalMatrix;
            foreach (var block in building.Blocks)
            {
                var renderers = block.GetComponentsInChildren<Renderer>(); parts.Add((block, renderers));
                foreach (var renderer in renderers)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || renderer.sharedMaterial == null) continue;
                    var farShape = candySilhouette?.Invoke(filter.sharedMesh);
                    // Attached geometry never moves relative to its building. Once it
                    // detaches, it is excluded and its own renderer handles the motion.
                    sources.Add((block, filter.sharedMesh, farShape != null ? farShape : box,
                        renderer.sharedMaterial, local * renderer.transform.localToWorldMatrix));
                }
            }
            var near = new GameObject("Batched building mesh", typeof(MeshFilter), typeof(MeshRenderer)); near.transform.SetParent(building.transform, false);
            nearFilter = near.GetComponent<MeshFilter>(); nearRenderer = near.GetComponent<MeshRenderer>();
            var far = new GameObject("Distant building mesh", typeof(MeshFilter), typeof(MeshRenderer)); far.transform.SetParent(building.transform, false);
            distantFilter = far.GetComponent<MeshFilter>(); distant = far.GetComponent<MeshRenderer>();
            var overhead = new GameObject("Minimap building mesh", typeof(MeshFilter), typeof(MeshRenderer)); overhead.transform.SetParent(building.transform, false);
            overhead.layer = TownPhysics.MapLayer;
            mapFilter = overhead.GetComponent<MeshFilter>(); map = overhead.GetComponent<MeshRenderer>();
            map.shadowCastingMode = ShadowCastingMode.Off; map.receiveShadows = false;
            Rebuild(); RebuildNear(); distant.enabled = false;
            detailBounds = nearMesh.bounds;
            UpdateOriginalRenderers();
        }
        internal void Tick(Vector3 view)
        {
            float distance = detailBounds.SqrDistance(building.transform.InverseTransformPoint(view));
            bool next = distance < (detailed ? 185 * 185 : 160 * 160);
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
            workspace.Begin();
            foreach (var part in sources)
                if (part.block != null && !part.block.Detached && !part.block.RenderSeparately)
                    workspace.Add(part.material, part.near, part.matrix);
            var next = workspace.Combine("Batched " + building.name, out var materials);
            nearFilter.sharedMesh = next; nearRenderer.sharedMaterials = materials;
            if (nearMesh != null) Object.Destroy(nearMesh);
            nearMesh = next; nearDetached = building.DetachedParts; nearDamage = building.DamageRevision;
        }
        internal void RefreshMapMesh() { if (renderedDetached != building.DetachedParts) Rebuild(); }
        private void Rebuild()
        {
            workspace.Begin();
            foreach (var part in sources)
                if (part.block != null && !part.block.Detached) workspace.Add(part.material, part.far, part.matrix);
            var next = workspace.Combine("Lightweight " + building.name, out var materials);
            distantFilter.sharedMesh = mapFilter.sharedMesh = next;
            distant.sharedMaterials = map.sharedMaterials = materials;
            if (mesh != null) Object.Destroy(mesh);
            mesh = next; renderedDetached = building.DetachedParts;
        }
        internal void Dispose() { if (mesh != null) Object.Destroy(mesh); if (nearMesh != null) Object.Destroy(nearMesh); }
    }
}
