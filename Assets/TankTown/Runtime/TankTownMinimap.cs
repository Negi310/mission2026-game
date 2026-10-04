using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TankTown
{
    // North stays at the top; moving the tank never rotates or jolts the map.
    internal sealed class TankTownMinimap : System.IDisposable
    {
        internal const int TextureSize = 384;
        private readonly TankTownGame game;
        private readonly RectTransform panel, viewport, marker;
        private readonly Transform hud;
        private RenderTexture texture;
        private float renderAt;
        internal Camera View { get; }
        internal RectTransform Marker => marker;
        internal int RenderCount { get; private set; }
        internal float MapHalfExtent { get; }
        internal RenderTexture Texture => texture;

        internal TankTownMinimap(TankTownGame owner, Transform parent, Font font, ToyHudStyle style)
        {
            game = owner; hud = parent; MapHalfExtent = game.World.HalfExtent + 3;
            var menu = new TankTownMenuLayout("Minimap layout helper", parent, font, style, Color.clear);
            // Reuse the UI factory without adding a second full-screen layout.
            var root = menu.Panel(parent, "Minimap", Vector2.zero, new Vector2(244, 286), TankTownMenuLayout.Ink);
            panel = (RectTransform)root.transform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = new Vector2(-28, -127);
            style.Card(root, new Color(.83f, .93f, 1));
            menu.Label(panel, "Minimap heading", "STREET MAP", new Vector2(12, -7), new Vector2(185, 26), 17, Color.white);
            menu.Label(panel, "Minimap north", "N ↑", new Vector2(193, -7), new Vector2(43, 26), 16, TankTownMenuLayout.Lime);
            var picture = new GameObject("Minimap town view", typeof(RectTransform), typeof(RawImage)); picture.transform.SetParent(panel, false);
            viewport = (RectTransform)picture.transform;
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0, 1);
            viewport.anchoredPosition = new Vector2(12, -38); viewport.sizeDelta = new Vector2(220, 220);
            picture.GetComponent<RawImage>().raycastTarget = false;
            menu.Label(panel, "Minimap legend", "▲ いまいる場所・車体の向き", new Vector2(12, -262), new Vector2(220, 22), 13, TankTownMenuLayout.Lime);
            var arrow = new GameObject("Minimap player", typeof(RectTransform), typeof(TownMapArrow)); arrow.transform.SetParent(viewport, false);
            marker = (RectTransform)arrow.transform;
            marker.anchorMin = marker.anchorMax = marker.pivot = Vector2.one * .5f;
            marker.sizeDelta = new Vector2(18, 23);
            arrow.GetComponent<TownMapArrow>().raycastTarget = false;
            Object.Destroy(menu.Root);

            var cameraRoot = new GameObject("Minimap camera", typeof(Camera)); cameraRoot.transform.SetParent(game.transform, false);
            View = cameraRoot.GetComponent<Camera>(); View.enabled = false;
            float roof = 0;
            foreach (var building in game.World.Buildings) roof = Mathf.Max(roof, building.transform.position.y + building.Floors * game.Settings.floorHeight + 3);
            View.transform.SetPositionAndRotation(new Vector3(0, roof + 20, 0), Quaternion.Euler(90, 0, 0));
            View.orthographic = true; View.orthographicSize = MapHalfExtent; View.aspect = 1;
            View.nearClipPlane = .1f; View.farClipPlane = roof + 40;
            View.clearFlags = CameraClearFlags.SolidColor; View.backgroundColor = new Color(.21f, .67f, .84f);
            View.cullingMask = ~((1 << 5) | (1 << TownPhysics.DebrisLayer) | (1 << TownPhysics.RewardLayer) | (1 << TownPhysics.PedestrianLayer));
            View.allowHDR = false; View.allowMSAA = false;
            var data = View.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false; data.renderShadows = false;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                texture = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32) { name = "Live town minimap", antiAliasing = 1 };
                texture.Create(); picture.GetComponent<RawImage>().texture = texture;
            }
        }

        internal Vector2 MapPoint(Vector3 world) => new Vector2(world.x, world.z) / (MapHalfExtent * 2) * viewport.rect.width;
        internal static float Heading(Vector3 forward) => -Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        internal void Tick()
        {
            if (!panel.gameObject.activeInHierarchy) return;
            float scale = Mathf.Clamp((((RectTransform)hud).rect.height - 285 - 142) / 286, .65f, 1);
            panel.localScale = Vector3.one * scale;
            Vector2 point = MapPoint(game.Tank.transform.position);
            float margin = viewport.rect.width * .5f - 12;
            marker.anchoredPosition = new Vector2(Mathf.Clamp(point.x, -margin, margin), Mathf.Clamp(point.y, -margin, margin));
            marker.localRotation = Quaternion.Euler(0, 0, Heading(game.Tank.transform.forward));
            if (Time.unscaledTime >= renderAt && game.State != RoundState.Paused)
            {
                renderAt = Time.unscaledTime + .25f; RenderNow();
            }
        }
        internal void RenderNow()
        {
            if (texture == null || !texture.IsCreated()) return;
            var request = new RenderPipeline.StandardRequest { destination = texture };
            if (RenderPipeline.SupportsRenderRequest(View, request)) { RenderPipeline.SubmitRenderRequest(View, request); RenderCount++; }
        }
        public void Dispose()
        {
            if (texture != null) { texture.Release(); Object.Destroy(texture); texture = null; }
            if (View != null) Object.Destroy(View.gameObject);
        }
    }

    public sealed class TownMapArrow : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); AddArrow(mesh, 1, TankTownMenuLayout.Ink); AddArrow(mesh, .70f, TankTownMenuLayout.Lime);
        }
        private void AddArrow(VertexHelper mesh, float scale, Color tint)
        {
            int start = mesh.currentVertCount;
            var size = rectTransform.rect.size * scale;
            mesh.AddVert(new Vector3(0, size.y * .5f), tint, Vector2.zero);
            mesh.AddVert(new Vector3(-size.x * .5f, -size.y * .5f), tint, Vector2.zero);
            mesh.AddVert(new Vector3(0, -size.y * .24f), tint, Vector2.zero);
            mesh.AddVert(new Vector3(size.x * .5f, -size.y * .5f), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
