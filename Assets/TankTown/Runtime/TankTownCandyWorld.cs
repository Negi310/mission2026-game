using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public sealed partial class TankTownWorld
    {
        internal bool CandyTheme => game.BuiltStage == TownStage.CandyTown;
        private CandyGeometry candyGeometry;
        private Material[] candyLollipops;
        private Material candyCream, candySponge, candyWafer, candyBiscuit, candyChocolate, candyChocolateTile, candyStrawberry, candyLeaf, candyPink;
        private Material candyDough, candyStripe, candyJam, candyLollipopPink, candyLollipopRed;
        private Material candyPastelPink, candyAqua, candyMint, candyAquaGlass, candyMintGlass;
        private void ApplyCandyTexture(Material material, string name, float tiling, float gloss, float relief)
        {
            var texture = Resources.Load<Texture2D>("CandyTextures/" + name);
            if (texture == null) { Debug.LogError("Missing candy texture: " + name); return; }
            material.name = "Candy " + name;
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", Vector2.one * tiling);
            material.SetFloat("_TextureRelief", relief);
            material.SetFloat("_Gloss", gloss);
            material.SetFloat("_HighlightPower", gloss > .3f ? 48 : 24);
            material.SetFloat("_Coat", name == "Sponge" || name == "Wafer" || name == "Biscuit" ? .015f : name == "Cream" ? .08f : name == "Chocolate" ? .48f : .68f);
            material.SetFloat("_TextureContrast", name == "Cream" ? .55f : name == "Glaze" ? .65f : .92f);
            material.SetFloat("_SurfaceDetail", 0); material.SetFloat("_Brushed", 0);
            material.EnableKeyword("_CANDY_TEXTURE");
        }
        private Material CandyMaterial(string name, float tiling, float gloss, float relief, Color? tint = null)
        {
            var material = MakeMaterial(tint ?? Color.white);
            ApplyCandyTexture(material, name, tiling, gloss, relief); return material;
        }
        private void InitializeCandyAccents(Material[] colors)
        {
            candyLollipops = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                ApplyCandyTexture(colors[i], "Glaze", .35f, .38f, .02f);
                candyLollipops[i] = CandyMaterial("CandySwirl", 1, .42f, .02f, ItemIcons.Color((ItemKind)i));
            }
        }
        private void InitializeCandyPalette()
        {
            candyGeometry = new CandyGeometry();
            candyCream = CandyMaterial("Cream", .35f, .18f, .025f);
            candySponge = CandyMaterial("Sponge", .16f, .045f, .055f);
            candyWafer = CandyMaterial("Wafer", .25f, .08f, .10f);
            candyBiscuit = CandyMaterial("Biscuit", .18f, .09f, .04f);
            candyChocolate = CandyMaterial("Chocolate", .25f, .32f, .018f, new Color(.68f, .68f, .68f));
            candyChocolateTile = CandyMaterial("Chocolate", .25f, .52f, .018f);
            candyStrawberry = CandyMaterial("Strawberry", 1, .36f, .045f);
            candyLeaf = CandyMaterial("Glaze", 1, .14f, .015f, new Color(.13f, .77f, .25f));
            candyPink = CandyMaterial("Glaze", 1, .40f, .02f, ItemIcons.Color(ItemKind.Magnet));
            candyDough = CandyMaterial("Dough", 1, .12f, .045f);
            candyStripe = CandyMaterial("CandyStripe", 1, .32f, .015f);
            candyJam = CandyMaterial("Glaze", 1, .40f, .02f, new Color(1, .07f, .16f));
            candyLollipopPink = CandyMaterial("CandySwirl", 1, .42f, .02f, ItemIcons.Color(ItemKind.Magnet));
            candyLollipopRed = CandyMaterial("CandySwirl", 1, .42f, .02f, new Color(1, .07f, .16f));
            candyPastelPink = CandyMaterial("Glaze", .35f, .38f, .02f, new Color(1, .34f, .57f));
            candyAqua = CandyMaterial("Glaze", .35f, .36f, .02f, new Color(.10f, .82f, 1));
            candyMint = CandyMaterial("Glaze", .35f, .32f, .02f, new Color(.45f, .97f, .80f));
            candyAquaGlass = MakeMaterial(new Color(.035f, .48f, .70f), ToySurface.Glass);
            candyMintGlass = MakeMaterial(new Color(.10f, .58f, .52f), ToySurface.Glass);
            if (game.Settings.importedCityModels)
            {
                // Blender candy decorations (scripts/blender/models/candy_kit.py) on the existing candy materials.
                candyKit = TownModelKit.Load(new Dictionary<string, Material>
                {
                    ["CandyCream"] = candyCream, ["CandyWafer"] = candyWafer, ["CandyChocolate"] = candyChocolate, ["CandyJam"] = candyJam,
                    ["CandyLeaf"] = candyLeaf, ["CandyPaint"] = candyPink, ["CandyFrame"] = candyCream, ["CandyGlass"] = candyAquaGlass,
                    ["CandyPastelPink"] = candyPastelPink, ["CandyAqua"] = candyAqua, ["CandyYellow"] = ShotMaterial
                }, "Models/CandyKit");
                candyKit?.RoundInDistance("Wrapped candy", "Cupcake topper", "Ice cream topper", "Candy dome topper");
            }
            road.SetColor("_BaseColor", new Color(.18f, .09f, .10f));
            grass.SetColor("_BaseColor", new Color(.48f, .93f, .71f));
            cream.SetColor("_BaseColor", new Color(1, .88f, .59f));
        }
        private Material CandyWallMaterial(int style) => style == 0 ? candySponge : style == 1 ? candyChocolate
            : style == 4 ? candyPastelPink : style == 5 ? candyAqua : style == 6 ? candyWafer : candyBiscuit;
        private static string CandyHouseName(int style) => style == 0 ? "Shortcake tower" : style == 1 ? "Chocolate house"
            : style == 4 ? "Pink candy shop" : style == 5 ? "Aqua ice cream shop" : style == 6 ? "Cupcake cafe" : "Biscuit house";

        private readonly Dictionary<string, Material> candyPaints = new Dictionary<string, Material>();
        // Kit colours per house: CandyPaint is the reward colour, CandyFrame the trim and CandyGlass the windows.
        private Dictionary<string, Material> CandyPaints(Material reward, Material trim = null, Material windows = null)
        {
            candyPaints.Clear(); candyPaints["CandyPaint"] = reward;
            if (trim != null) candyPaints["CandyFrame"] = trim;
            if (windows != null) candyPaints["CandyGlass"] = windows;
            return candyPaints;
        }
        private bool CandyKitHas(string piece) => candyKit != null && candyKit.Has(piece);
        // Reward-coloured icing with drips, stretched from the kit's 9 x .32 x .48 band. Ground floors with an
        // awning use half-height drips so they stay clear of it.
        private void CandyDrips(Transform parent, Vector3 point, Vector3 size, Material color, Quaternion rotation)
            => candyKit.Attach(parent, "Icing drip edge", point, new Vector3(size.x / 9, size.y / .32f, size.z / .48f), rotation, CandyPaints(color));

        // Decorations retain metre-sized geometry even when their structural parent is a thin scaled panel.
        private GameObject CandyDetail(Transform parent, string label, Vector3 point, Vector3 size, Material material, Mesh shape = null)
        {
            var item = Box(label, Vector3.zero, size, material, solid: false);
            item.transform.SetParent(parent, false);
            Vector3 scale = parent.lossyScale;
            item.transform.localPosition = new Vector3(point.x / scale.x, point.y / scale.y, point.z / scale.z);
            item.transform.localScale = new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z);
            if (shape != null) item.GetComponent<MeshFilter>().sharedMesh = shape;
            return item;
        }
        private void DecorateCandyPanel(DestructibleBlock wall, int style, Material color)
        {
            float w = wall.transform.localScale.x, h = wall.transform.localScale.y;
            if (style == 0)
            {
                // Two structural floors form one large cake tier. The next tier is narrower.
                bool tierTop = wall.Level % 2 == 1 || wall.Level == wall.Building.Floors - 1;
                if (!tierTop) return;
                CandyDetail(wall.transform, "Cream layer", new Vector3(0, h / 2 - .20f, .38f), new Vector3(w + .30f, 1.10f, 2.25f), candyCream, candyGeometry.CreamRibbon);
                CandyDetail(wall.transform, "Reward-colored icing", new Vector3(0, h / 2 + .23f, .55f), new Vector3(w + .24f, .16f, 1.5f), color);
                CandyDetail(wall.transform, "Strawberry filling", new Vector3(0, h / 2 - .85f, .36f), new Vector3(w, .16f, .14f), candyJam);
                if (wall.Level < wall.Building.Floors - 1)
                {
                    int count = w >= 7 ? 3 : 2;
                    float diameter = Mathf.Min(2.2f, w / count * .82f), berryHeight = diameter * 1.12f;
                    for (int i = 0; i < count; i++)
                        CandyDetail(wall.transform, "Tier strawberry", new Vector3(((i + .5f) / count - .5f) * w, h / 2 + .35f + berryHeight * .02f, .40f),
                            new Vector3(diameter, berryHeight, diameter), candyStrawberry, candyGeometry.TierStrawberry);
                }
            }
            else if (style == 1)
            {
                wall.GetComponent<MeshFilter>().sharedMesh = FragmentBoxMesh(wall.transform.localScale, true);
                int count = Mathf.Clamp(Mathf.RoundToInt(w / 2.5f), 2, 3);
                var size = new Vector3(w / count - .15f, h - .28f, .95f);
                for (int i = 0; i < count; i++)
                {
                    var cell = CandyDetail(wall.transform, "Chocolate square", new Vector3(((i + .5f) / count - .5f) * w, -.03f, .15f), size, candyChocolateTile, candyGeometry.ChocolateBar(size));
                    var insetSize = new Vector3(size.x - .66f, size.y - .66f, .025f);
                    CandyDetail(cell.transform, "Chocolate inset", new Vector3(0, 0, size.z * .29f + .015f), insetSize, candyChocolate, geometry.RoundedBox(insetSize));
                }
                if (CandyKitHas("Icing drip edge")) CandyDrips(wall.transform, new Vector3(0, h / 2 - .04f, .17f), new Vector3(w + .12f, .18f, 1.1f), color, Quaternion.identity);
                else CandyDetail(wall.transform, "Reward-colored icing", new Vector3(0, h / 2 - .04f, .17f), new Vector3(w + .12f, .18f, 1.1f), color);
            }
            else if (style >= 4)
            {
                DecoratePastelShop(wall, style, color);
            }
            else if (CandyKitHas("Candy biscuit window"))
            {
                float face = wall.transform.localScale.z / 2;
                CandyDrips(wall.transform, new Vector3(0, h / 2 - .12f, .20f), new Vector3(w + .10f, wall.Level == 0 ? .16f : .32f, .48f), color, Quaternion.identity);
                // Kit window: 3.0 x 1.7 arched chocolate pane with piped cream and a reward-coloured sugar cross.
                candyKit.Attach(wall.transform, "Candy biscuit window", new Vector3(0, -.18f, face), new Vector3(w * .36f / 3, h * .56f / 1.7f, 1), CandyPaints(color));
                foreach (int side in new[] { -1, 1 })
                    CandyDetail(wall.transform, "Wafer edge", new Vector3(side * w * .44f, -.1f, .19f), new Vector3(.18f, h * .75f, .15f), candyCream);
                if (wall.Level == 0)
                    candyKit.Attach(wall.transform, "Candy awning", new Vector3(0, h * .20f + .08f, face), new Vector3(w * .68f / 6, 1, 1), CandyPaints(color, candyCream));
            }
            else
            {
                CandyDetail(wall.transform, "Reward-colored icing", new Vector3(0, h / 2 - .12f, .20f), new Vector3(w + .10f, .32f, .48f), color);
                CandyDetail(wall.transform, "Biscuit window", new Vector3(0, -.18f, .19f), new Vector3(w * .36f, h * .56f, .16f), candyChocolate);
                var windowFrame = new Vector3(w * .36f + .24f, h * .56f + .24f, .34f);
                CandyDetail(wall.transform, "Biscuit window frame", new Vector3(0, -.18f, .29f), windowFrame, candyCream, geometry.WindowFrame(windowFrame));
                foreach (int side in new[] { -1, 1 })
                    CandyDetail(wall.transform, "Wafer edge", new Vector3(side * w * .44f, -.1f, .19f), new Vector3(.18f, h * .75f, .15f), candyCream);
                CandyDetail(wall.transform, "Sugar window cross", new Vector3(0, -.18f, .3f), new Vector3(.13f, h * .56f, .11f), color);
                CandyDetail(wall.transform, "Sugar window cross", new Vector3(0, -.18f, .3f), new Vector3(w * .36f, .13f, .11f), color);
                if (wall.Level == 0)
                {
                    float canopyWidth = w * .68f, stripeWidth = canopyWidth / 7;
                    for (int stripe = 0; stripe < 7; stripe++)
                        CandyDetail(wall.transform, "Shop awning stripe", new Vector3((stripe - 3) * stripeWidth, h * .20f, 1.1f),
                            new Vector3(stripeWidth + .015f, .16f, 1.8f), stripe % 2 == 0 ? color : candyCream);
                    CandyDetail(wall.transform, "Shop awning fringe", new Vector3(0, h * .20f - .14f, 1.9f), new Vector3(canopyWidth, .40f, .22f), color, candyGeometry.CreamRibbon);
                }
            }
        }
        private void DecoratePastelShop(DestructibleBlock wall, int style, Material rewardColor)
        {
            float w = wall.transform.localScale.x, h = wall.transform.localScale.y;
            var frameColor = style == 5 ? candyPastelPink : candyCream;
            var windowColor = style == 4 ? candyAquaGlass : candyMintGlass;
            float windowWidth = w * .66f, windowHeight = h * .63f;
            if (CandyKitHas("Candy shop window"))
            {
                float face = wall.transform.localScale.z / 2;
                // Kit window: 6.0 x 1.86 rounded glass ("Pastel candy window") in a thick piped sugar frame.
                candyKit.Attach(wall.transform, "Candy shop window", new Vector3(0, -.05f, face), new Vector3(windowWidth / 6, windowHeight / 1.86f, 1), CandyPaints(rewardColor, frameColor, windowColor));
                CandyDrips(wall.transform, new Vector3(0, h / 2 - .08f, .27f), new Vector3(w + .12f, wall.Level == 0 ? .175f : .35f, .65f), rewardColor, Quaternion.identity);
                if (style == 6)
                    CandyDetail(wall.transform, "Cupcake wrapper trim", new Vector3(0, -h / 2 + .14f, .25f), new Vector3(w + .1f, .28f, .40f), candyPastelPink);
                if (wall.Level == 0)
                    candyKit.Attach(wall.transform, "Candy awning", new Vector3(0, h * .25f - .04f, face), new Vector3(w * .86f / 6, 1, 1), CandyPaints(rewardColor, frameColor));
                return;
            }
            // Pastry texture, generous shop windows and sugar frames, with no extra physics bodies.
            CandyDetail(wall.transform, "Pastel candy window", new Vector3(0, -.05f, .24f), new Vector3(windowWidth, windowHeight, .10f), windowColor);
            foreach (int side in new[] { -1, 1 })
                CandyDetail(wall.transform, "Sugar window jamb", new Vector3(side * (windowWidth / 2 + .08f), -.05f, .40f), new Vector3(.20f, windowHeight + .35f, .36f), frameColor);
            foreach (int side in new[] { -1, 1 })
                CandyDetail(wall.transform, "Sugar window sill", new Vector3(0, -.05f + side * (windowHeight / 2 + .08f), .40f), new Vector3(windowWidth + .35f, .20f, .36f), frameColor);
            CandyDetail(wall.transform, "Candy window mullion", new Vector3(0, -.05f, .43f), new Vector3(.16f, windowHeight, .22f), frameColor);
            CandyDetail(wall.transform, "Reward-colored icing", new Vector3(0, h / 2 - .08f, .27f), new Vector3(w + .12f, .35f, .65f), rewardColor);
            if (style == 6)
                CandyDetail(wall.transform, "Cupcake wrapper trim", new Vector3(0, -h / 2 + .14f, .25f), new Vector3(w + .1f, .28f, .40f), candyPastelPink);
            if (wall.Level != 0) return;
            float canopyWidth = w * .86f, stripeWidth = canopyWidth / 5;
            for (int stripe = 0; stripe < 5; stripe++)
                CandyDetail(wall.transform, "Pastel shop awning", new Vector3((stripe - 2) * stripeWidth, h * .25f, 1), new Vector3(stripeWidth + .015f, .20f, 1.65f), stripe % 2 == 0 ? rewardColor : candyCream);
            CandyDetail(wall.transform, "Pastel awning fringe", new Vector3(0, h * .25f - .16f, 1.74f), new Vector3(canopyWidth, .48f, .24f), frameColor, candyGeometry.CreamRibbon);
        }
        private void AddCandyRewardSign(DestructibleBlock wall, ItemKind reward)
        {
            float center = (wall.Building.Width - 1.6f) / 4;
            // Fixed shop palettes still communicate the independently randomized reward.
            CandyDetail(wall.transform, "Candy reward sign", new Vector3(center, wall.transform.localScale.y / 2 + .8f, 1),
                new Vector3(2.2f, 2.2f, 1), SpecialIcons.Material(reward), candyGeometry.RewardSign);
        }
        private void DecorateBiscuitRoof(DestructibleBlock roof, Material color)
        {
            Vector3 size = roof.transform.localScale;
            if (CandyKitHas("Icing drip edge"))
            {
                // Dripping icing along the eave, the lower outer edge of each tilted roof half.
                float side = Mathf.Sign(roof.transform.localPosition.z);
                int count = Mathf.Max(1, Mathf.RoundToInt(size.x / 9)); float span = size.x / count;
                for (int i = 0; i < count; i++)
                    CandyDrips(roof.transform, new Vector3((i + .5f) * span - size.x / 2, .02f, side * (size.z / 2 - .1f)), new Vector3(span, .28f, .36f), color,
                        side > 0 ? Quaternion.identity : Quaternion.Euler(0, 180, 0));
                return;
            }
            CandyDetail(roof.transform, "Icing roof edge", new Vector3(0, .20f, -size.z * .45f), new Vector3(size.x, .28f, .36f), color);
        }
        private void DecorateCandyRoof(DestructibleBlock roof, int style, Material color, float width)
        {
            if (style >= 4) { DecoratePastelRoof(roof, style, color, width); return; }
            foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            {
                Vector3 point = new Vector3(x * width * .20f, style == 0 ? roof.transform.localScale.y / 2 + .12f : 1.6f, z * width * .20f);
                if (style == 0)
                {
                    var berry = CandyDetail(roof.transform, "Roof strawberry", point, new Vector3(3.2f, 3.4f, 3.2f), candyStrawberry, candyGeometry.Strawberry);
                    CandyDetail(berry.transform, "Strawberry leaf", new Vector3(0, 1.65f, 0), new Vector3(1.8f, .22f, .7f), candyLeaf);
                    CandyDetail(berry.transform, "Strawberry leaf", new Vector3(0, 1.65f, 0), new Vector3(.7f, .22f, 1.8f), candyLeaf);
                }
                else if (CandyKitHas("Wrapped candy"))
                    candyKit.Attach(roof.transform, "Wrapped candy", new Vector3(point.x, roof.transform.localScale.y / 2 + .75f, point.z), Vector3.one * 1.7f,
                        Quaternion.Euler(0, x * z > 0 ? 45 : -45, 0), CandyPaints(color));
                else CandyDetail(roof.transform, "Roof candy", point - Vector3.up * .8f, new Vector3(2, 1.2f, 2), color, candyGeometry.Sphere);
            }
        }
        private void DecoratePastelRoof(DestructibleBlock roof, int style, Material rewardColor, float width)
        {
            float surface = roof.transform.localScale.y / 2;
            // Kit toppers are modelled for the 18.4 m pastel shop roof; the ice cream keeps its fixed size.
            var spread = new Vector3(width / 18.4f, 1, width / 18.4f);
            if (style == 6 && CandyKitHas("Cupcake topper"))
            {
                candyKit.Attach(roof.transform, "Cupcake topper", new Vector3(0, surface, 0), spread, CandyPaints(rewardColor));
                return;
            }
            if (style == 6)
            {
                CandyDetail(roof.transform, "Cupcake icing cap", new Vector3(0, surface + .40f, 0), new Vector3(width * 1.04f, 2.5f, width * 1.04f), candyPastelPink, candyGeometry.Sphere);
                CandyDetail(roof.transform, "Cupcake cream lower", new Vector3(0, surface + 1.55f, 0), new Vector3(width * .88f, 2.8f, width * .88f), candyCream, candyGeometry.Sphere);
                CandyDetail(roof.transform, "Cupcake cream upper", new Vector3(.25f, surface + 2.95f, 0), new Vector3(width * .65f, 2.6f, width * .65f), candyCream, candyGeometry.Sphere);
                CandyDetail(roof.transform, "Cupcake cream peak", new Vector3(.5f, surface + 4.10f, 0), new Vector3(width * .38f, 2.3f, width * .38f), candyCream, candyGeometry.Sphere);
                CandyDetail(roof.transform, "Cafe cherry", new Vector3(.55f, surface + 5.25f, 0), new Vector3(2.4f, 2.4f, 2.4f), candyJam, candyGeometry.Sphere);
                return;
            }
            CandyDetail(roof.transform, "Pastel roof icing", new Vector3(0, surface, 0), new Vector3(width * .82f, 1.2f, width * .82f), style == 4 ? candyPastelPink : candyAqua, candyGeometry.Sphere);
            if (style == 5 && CandyKitHas("Ice cream topper"))
                candyKit.Attach(roof.transform, "Ice cream topper", new Vector3(0, surface, 0), Vector3.one, CandyPaints(rewardColor));
            else if (style == 4 && CandyKitHas("Candy dome topper"))
                candyKit.Attach(roof.transform, "Candy dome topper", new Vector3(0, surface, 0), spread, CandyPaints(rewardColor));
            else if (style == 5)
            {
                CandyDetail(roof.transform, "Ice cream wafer cone", new Vector3(0, surface + 2.6f, 0), new Vector3(4.6f, 5.2f, 4.6f), candyWafer, candyGeometry.Cone);
                CandyDetail(roof.transform, "Ice cream scoop", new Vector3(0, surface + 5.35f, 0), new Vector3(6.1f, 3.8f, 6.1f), candyCream, candyGeometry.Sphere);
                CandyDetail(roof.transform, "Ice cream swirl", new Vector3(.25f, surface + 7.1f, 0), new Vector3(4.1f, 2.6f, 4.1f), candyPastelPink, candyGeometry.Sphere);
                CandyDetail(roof.transform, "Ice cream peak", new Vector3(.45f, surface + 8.2f, 0), new Vector3(2.3f, 1.9f, 2.3f), candyCream, candyGeometry.Sphere);
            }
            else
            {
                CandyDetail(roof.transform, "Candy dome", new Vector3(0, surface + 1.4f, 0), new Vector3(width * .55f, 3.2f, width * .55f), candyCream, candyGeometry.Sphere);
                CandyDetail(roof.transform, "Candy shop cherry", new Vector3(0, surface + 3.05f, 0), new Vector3(2.5f, 2.5f, 2.5f), candyJam, candyGeometry.Sphere);
            }
            foreach (int side in new[] { -1, 1 })
            {
                var point = new Vector3(side * width * .30f, surface + 1.8f, -width * .27f);
                CandyDetail(roof.transform, "Shop candy cane", point, new Vector3(.50f, 3.6f, .50f), candyStripe, candyGeometry.SugarStick);
                CandyDetail(roof.transform, "Shop candy hook", point + new Vector3(.52f, 1.72f, 0), new Vector3(1.1f, 1.1f, .50f), candyStripe, candyGeometry.CaneHook);
                CandyDetail(roof.transform, "Reward sugar drop", new Vector3(side * width * .30f, surface + .35f, width * .25f), new Vector3(2, 1.4f, 2), rewardColor, candyGeometry.Sphere);
            }
        }
        private void CreateCandyProp(Vector3 point, string label, Material color, bool sign)
        {
            var item = new GameObject(sign ? "Candy cane " + label : "Lollipop tree"); item.transform.SetParent(transform, false);
            item.transform.position = GroundPoint(point);
            var post = item.AddComponent<BoxCollider>(); post.center = Vector3.up * 1.5f; post.size = new Vector3(.4f, 3, .4f);
            AddBlock(item, true);
            CandyDetail(item.transform, "Sugar stick", post.center, post.size, sign ? candyStripe : candyCream, candyGeometry.SugarStick);
            if (!sign)
            {
                color = color == candyPink ? candyLollipopPink : color == candyStrawberry ? candyLollipopRed : candyLollipops[Mathf.Abs(Mathf.RoundToInt(point.x) + Mathf.RoundToInt(point.z) * 2) % candyLollipops.Length];
                var lollipop = CandyDetail(item.transform, "Lollipop crown", Vector3.up * 3.4f, new Vector3(3.3f, 3.3f, 1.1f), color, candyGeometry.Sphere);
                var sphere = lollipop.AddComponent<SphereCollider>(); sphere.radius = .5f;
            }
            else if (label == "Street lamp")
            {
                CandyDetail(item.transform, "Candy cane hook", new Vector3(.55f, 3.0f, 0), new Vector3(1.1f, 1.1f, .4f), candyStripe, candyGeometry.CaneHook);
                CandyDetail(item.transform, "Candy lamp", new Vector3(1.1f, 2.8f, 0), new Vector3(.9f, .8f, .9f), lampGlow, candyGeometry.Sphere);
            }
            else Box("Biscuit sign", new Vector3(0, 2.8f, 0), new Vector3(1.9f, 1.0f, .3f), candyBiscuit, item.transform, false);
        }
        private void CreateCandyPark(Vector3 center)
        {
            ParkCount++;
            Terrain.Surface("Candy garden plaza", new Vector2(center.x - BuildingWidth / 2, center.z - BuildingWidth / 2), Vector2.one * BuildingWidth, .04f, grass);
            var muffin = Box("Crushable cupcake", center + Vector3.up * 1.1f, new Vector3(5, 2.2f, 5), candyWafer);
            AddBlock(muffin, true);
            CandyDetail(muffin.transform, "Cupcake frosting", new Vector3(0, 1.5f, 0), new Vector3(6.2f, 1.7f, 6.2f), candyPink, candyGeometry.Sphere);
            CandyDetail(muffin.transform, "Whipped cream lower", new Vector3(0, 2.3f, 0), new Vector3(5.5f, 1.5f, 5.5f), candyCream, candyGeometry.Sphere);
            CandyDetail(muffin.transform, "Whipped cream upper", new Vector3(.15f, 3.0f, 0), new Vector3(3.9f, 1.4f, 3.9f), candyCream, candyGeometry.Sphere);
            CandyDetail(muffin.transform, "Whipped cream peak", new Vector3(.3f, 3.6f, 0), new Vector3(2.5f, 1.3f, 2.5f), candyCream, candyGeometry.Sphere);
            CandyDetail(muffin.transform, "Cupcake cherry", new Vector3(.35f, 4.35f, 0), new Vector3(1.6f, 1.6f, 1.6f), candyJam, candyGeometry.Sphere);
            for (int i = 0; i < 4; i++)
            {
                var direction = Quaternion.Euler(0, i * 90, 0) * Vector3.forward;
                var bench = Box("Crushable biscuit bench", GroundPoint(center + direction * 7, .45f), new Vector3(2.6f, .9f, .8f), candyChocolateTile);
                bench.transform.rotation = Quaternion.Euler(0, i * 90, 0); AddBlock(bench, true);
                CreateCandyProp(center + Quaternion.Euler(0, i * 90 + 45, 0) * Vector3.forward * 8, "Tree", i % 2 == 0 ? candyPink : candyStrawberry, false);
            }
        }
        private void CreateDoughnutGate()
        {
            var root = new GameObject("Doughnut gate"); root.transform.SetParent(transform, false);
            root.transform.position = GroundPoint(SpawnPosition + Vector3.forward * 58);
            var building = root.AddComponent<BlockBuilding>(); building.Initialize(game);
            building.Width = 28; building.Floors = 1; building.Style = 3;
            building.RewardKind = ItemKind.Magnet; building.FacadeColor = ItemIcons.Color(ItemKind.Magnet); Buildings.Add(building);
            var feet = new DestructibleBlock[4];
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -12 : 12, z = i % 2 == 0 ? -1 : 1;
                float floor = Terrain.HeightAt(root.transform.position + new Vector3(x, 0, z)) - root.transform.position.y;
                feet[i] = Part("Doughnut support wafer", new Vector3(x, (floor + 6) / 2, z), new Vector3(1.7f, Mathf.Max(2, 6 - floor), 1.7f), candyWafer, BuildingPart.Pillar, 0, i, building);
                feet[i].Foundation = true;
            }
            var slices = new DestructibleBlock[12];
            for (int i = 0; i < slices.Length; i++)
            {
                float angle = -82.5f + i * 15, radians = angle * Mathf.Deg2Rad;
                var slice = Part("Doughnut slice", new Vector3(Mathf.Sin(radians) * 12, 6 + Mathf.Cos(radians) * 12, 0), new Vector3(3.8f, 4.2f, 4.2f), candyDough, BuildingPart.Prop, 0, -1, building);
                slice.PreserveShape = true;
                slice.transform.localRotation = Quaternion.Euler(0, 0, -angle);
                slice.GetComponent<MeshFilter>().sharedMesh = candyGeometry.DoughnutSlice;
                CandyDetail(slice.transform, "Pink doughnut icing", new Vector3(0, 0, -1.3f), new Vector3(3.85f, 4.3f, 1.7f), candyPink, candyGeometry.DoughnutSlice);
                CandyDetail(slice.transform, "Doughnut sugar sprinkle", new Vector3(.3f, .45f, -2.2f), new Vector3(.28f, .8f, .20f), i % 2 == 0 ? candyCream : ShotMaterial);
                slices[i] = slice;
                if (i > 0) BlockBuilding.Connect(slices[i - 1], slice);
            }
            BlockBuilding.Connect(feet[0], slices[0]); BlockBuilding.Connect(feet[1], slices[0]);
            BlockBuilding.Connect(feet[2], slices[11]); BlockBuilding.Connect(feet[3], slices[11]);
            CreateBuildingReward(building);
        }
    }
}
