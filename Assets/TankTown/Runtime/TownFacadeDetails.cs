using UnityEngine;

namespace TankTown
{
    public sealed partial class TankTownWorld
    {
        private void CityWindow(DestructibleBlock wall, int side)
        {
            Vector3 size = wall.transform.localScale;
            var point = new Vector3(side * size.x * .24f, size.y * .03f, size.z / 2 + .035f);
            var pane = new Vector3(size.x * .32f, size.y * .55f, .07f);
            CandyDetail(wall.transform, "Window", point, pane, glass);
            var trim = new Vector3(pane.x + .24f, pane.y + .24f, .24f);
            CandyDetail(wall.transform, "Deep window frame", point + Vector3.forward * .075f, trim, frame, geometry.WindowFrame(trim));
        }

        private void CityRoofEdge(DestructibleBlock slab)
        {
            Vector3 size = slab.transform.localScale;
            int x = slab.Column == 0 || slab.Column == 3 ? -1 : 1;
            int z = slab.Column < 2 ? -1 : 1;
            CandyDetail(slab.transform, "Roof parapet", new Vector3(x * (size.x / 2 - .10f), .29f, 0), new Vector3(.20f, .48f, size.z), cream);
            CandyDetail(slab.transform, "Roof parapet", new Vector3(0, .29f, z * (size.z / 2 - .10f)), new Vector3(size.x, .48f, .20f), cream);
        }

        private void DetailedTree(GameObject item, Material color)
        {
            var branches = new GameObject("Tree trunk and branches", typeof(MeshFilter), typeof(MeshRenderer)); branches.transform.SetParent(item.transform, false);
            branches.GetComponent<MeshFilter>().sharedMesh = geometry.TreeTrunk(); branches.GetComponent<MeshRenderer>().sharedMaterial = wood;
            var crown = new GameObject("Tree crown", typeof(MeshFilter), typeof(MeshRenderer), typeof(SphereCollider)); crown.transform.SetParent(item.transform, false);
            crown.transform.localPosition = Vector3.up * 3.05f; crown.transform.localScale = new Vector3(3.25f, 3.05f, 3.25f);
            int variant = Mathf.Abs(Mathf.RoundToInt(item.transform.position.x * 3 + item.transform.position.z * 7)) % 3;
            crown.GetComponent<MeshFilter>().sharedMesh = geometry.TreeCanopy(variant); crown.GetComponent<MeshRenderer>().sharedMaterial = color;
            crown.GetComponent<SphereCollider>().radius = .5f;
        }
    }
}
