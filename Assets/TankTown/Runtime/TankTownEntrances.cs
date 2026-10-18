using UnityEngine;

namespace TankTown
{
    public sealed partial class TankTownWorld
    {
        // A closed, visible doorway belongs to an existing ground-floor wall.
        // The ordinary wall collision and destruction rules remain responsible for it.
        private void AddBuildingEntrance(DestructibleBlock wall, Material accent)
        {
            if (wall.transform.Find("Building entrance") != null) return;
            var building = wall.Building;
            float width = building.Style == 0 ? 3.4f : 2.9f, height = 2.45f;
            float face = CandyTheme ? building.Style == 1 ? .84f : building.Style == 0 ? .48f : .32f : .28f;
            var entrance = new GameObject("Building entrance").transform;
            entrance.SetParent(wall.transform, false);
            Vector3 scale = wall.transform.lossyScale;
            // Put the doorway at the middle of the street-facing facade, in metre units.
            float center = (building.Width - 1.6f) / 4;
            entrance.localPosition = new Vector3(center / scale.x, (building.transform.position.y - wall.transform.position.y) / scale.y, face / scale.z);
            entrance.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
            var trim = CandyTheme ? building.Style == 1 ? candyWafer : candyCream : frame;
            var recess = CandyTheme ? candyChocolate : dark;
            var leaf = CandyTheme ? candyChocolateTile : dark;
            Box("Entrance recess", new Vector3(0, height / 2, -.035f), new Vector3(width, height, .12f), recess, entrance, false);
            foreach (int side in new[] { -1, 1 })
            {
                Box("Entrance jamb", new Vector3(side * (width / 2 + .10f), height / 2, .05f), new Vector3(.24f, height + .08f, .30f), trim, entrance, false);
                Box("Door leaf", new Vector3(side * width / 4, height / 2, .045f), new Vector3(width / 2 - .065f, height - .12f, .12f), leaf, entrance, false);
                Box("Door glass", new Vector3(side * width / 4, height * .65f, .115f), new Vector3(width / 2 - .24f, height * .58f, .04f), glass, entrance, false);
                Box("Door handle", new Vector3(side * .15f, height * .45f, .17f), new Vector3(.06f, .45f, .07f), trim, entrance, false);
            }
            Box("Door center mullion", new Vector3(0, height / 2, .125f), new Vector3(.08f, height - .05f, .08f), trim, entrance, false);
            if (CandyTheme)
            {
                var arch = Box("Candy entrance arch", new Vector3(0, height, .04f), new Vector3(width / 1.25f, .85f, .32f), trim, entrance, false);
                arch.GetComponent<MeshFilter>().sharedMesh = candyGeometry.CaneHook;
                var transom = Box("Rounded entrance transom", new Vector3(0, height - .025f, -.015f), new Vector3(width - .10f, 1.04f, .15f), recess, entrance, false);
                transom.GetComponent<MeshFilter>().sharedMesh = candyGeometry.Sphere;
            }
            else
            {
                Box("Entrance lintel", new Vector3(0, height + .08f, .05f), new Vector3(width + .45f, .30f, .32f), trim, entrance, false);
                Box("Entrance canopy", new Vector3(0, height + .42f, .45f), new Vector3(width + .95f, .16f, 1.4f), accent, entrance, false);
            }
            Box("Entrance threshold", new Vector3(0, .08f, .27f), new Vector3(width + .58f, .16f, .94f), trim, entrance, false);
            Box("Entrance warm light", new Vector3(0, height + .20f, .22f), new Vector3(width * .72f, .07f, .08f), lampGlow, entrance, false);
            Box("Entrance step", new Vector3(0, .04f, .81f), new Vector3(width + .76f, .08f, .42f), trim, entrance, false);
        }
    }
}
