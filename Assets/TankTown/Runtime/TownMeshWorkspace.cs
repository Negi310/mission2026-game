using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    // One workspace per world: rebuilding a damaged building reuses the material groups,
    // exact-length CombineMeshes input arrays and intermediate native meshes.
    internal sealed class TownMeshWorkspace
    {
        private readonly Dictionary<Material, List<CombineInstance>> groups = new Dictionary<Material, List<CombineInstance>>();
        private readonly List<Material> active = new List<Material>();
        private readonly Dictionary<int, CombineInstance[]> arrays = new Dictionary<int, CombineInstance[]>();
        private readonly Dictionary<int, CombineInstance[]> finalArrays = new Dictionary<int, CombineInstance[]>();
        private readonly List<Mesh> scratch = new List<Mesh>();
        internal int ScratchMeshCount => scratch.Count;
        internal void Begin()
        {
            foreach (var material in active) groups[material].Clear();
            active.Clear();
        }
        internal void Add(Material material, Mesh mesh, Matrix4x4 transform)
        {
            if (!groups.TryGetValue(material, out var list)) groups.Add(material, list = new List<CombineInstance>());
            if (list.Count == 0) active.Add(material);
            list.Add(new CombineInstance { mesh = mesh, transform = transform });
        }
        private CombineInstance[] Array(int count)
        {
            if (!arrays.TryGetValue(count, out var array)) arrays.Add(count, array = new CombineInstance[count]);
            return array;
        }
        internal Mesh Combine(string label, out Material[] materials)
        {
            // The final input must be distinct from the per-material input, including
            // when their lengths match. Otherwise the next group overwrites its entries.
            if (!finalArrays.TryGetValue(active.Count, out var combined))
                finalArrays.Add(active.Count, combined = new CombineInstance[active.Count]);
            materials = active.ToArray();
            for (int i = 0; i < active.Count; i++)
            {
                if (scratch.Count <= i) scratch.Add(new Mesh { name = "Building combine scratch", indexFormat = IndexFormat.UInt32 });
                var list = groups[active[i]]; var input = Array(list.Count); list.CopyTo(input);
                var piece = scratch[i]; piece.Clear(); piece.CombineMeshes(input, true, true);
                System.Array.Clear(input, 0, input.Length);
                combined[i] = new CombineInstance { mesh = piece, transform = Matrix4x4.identity };
            }
            var result = new Mesh { name = label, indexFormat = IndexFormat.UInt32 };
            if (combined.Length > 0)
            {
                result.CombineMeshes(combined, false, true);
                result.UploadMeshData(true);
            }
            System.Array.Clear(combined, 0, combined.Length);
            return result;
        }
        internal void Dispose()
        {
            foreach (var mesh in scratch) if (mesh != null) Object.Destroy(mesh);
            scratch.Clear(); groups.Clear(); active.Clear(); arrays.Clear(); finalArrays.Clear();
        }
    }
}
