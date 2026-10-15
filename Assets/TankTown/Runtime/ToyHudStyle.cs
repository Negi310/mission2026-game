using UnityEngine;
using UnityEngine.UI;

namespace TankTown
{
    internal sealed class ToyHudStyle
    {
        private readonly Texture2D texture;
        private readonly Texture2D ringTexture;
        private readonly Texture2D headerTexture, headerEdgeTexture, actionTexture;
        public Sprite Rounded { get; }
        public Sprite Ring { get; }
        private Sprite HeaderFill { get; }
        private Sprite HeaderEdge { get; }
        private Sprite ActionFill { get; }
        public ToyHudStyle()
        {
            const int size = 64;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Toy HUD rounded card", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Vector2 q = new Vector2(Mathf.Abs(x + .5f - 32) - 14, Mathf.Abs(y + .5f - 32) - 14);
                float distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - 17;
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - distance));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            Rounded = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * 20);
            ringTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HUD duration ring", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float radius = new Vector2(x + .5f - 32, y + .5f - 32).magnitude;
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(31 - radius) * Mathf.Clamp01(radius - 27));
            }
            ringTexture.SetPixels(pixels); ringTexture.Apply(false, true);
            Ring = Sprite.Create(ringTexture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            headerTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HUD header shading", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            headerEdgeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HUD header fine edge", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var edges = new Color[size * size];
            var action = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Vector2 q = new Vector2(Mathf.Abs(x + .5f - 32) - 14, Mathf.Abs(y + .5f - 32) - 14);
                float distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - 17;
                float mask = Mathf.Clamp01(.5f - distance), height = y / 63f;
                var color = Color.Lerp(new Color(.06f, .12f, .17f, .76f), new Color(.16f, .28f, .36f, .64f), height);
                color.a *= mask; pixels[y * size + x] = color;
                float light = Mathf.Lerp(.87f, 1, height);
                action[y * size + x] = new Color(light, light, light, mask);
                edges[y * size + x] = new Color(.75f, .89f, .96f, (mask - Mathf.Clamp01(-.65f - distance)) * Mathf.Lerp(.12f, .32f, height));
            }
            headerTexture.SetPixels(pixels); headerTexture.Apply(false, true);
            headerEdgeTexture.SetPixels(edges); headerEdgeTexture.Apply(false, true);
            HeaderFill = Sprite.Create(headerTexture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * 20);
            HeaderEdge = Sprite.Create(headerEdgeTexture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * 20);
            actionTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Title action shading", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            actionTexture.SetPixels(action); actionTexture.Apply(false, true);
            ActionFill = Sprite.Create(actionTexture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * 20);
        }
        public void TitleControl(GameObject target, Color tint, bool primary = false)
        {
            var image = target.GetComponent<Image>(); image.sprite = ActionFill;
            image.type = Image.Type.Sliced; image.color = tint;
            if (tint.a > .5f)
            {
                var shadow = target.AddComponent<Shadow>(); shadow.effectColor = new Color(.01f, .025f, .055f, .23f); shadow.effectDistance = new Vector2(0, -3);
            }
            var edge = new GameObject("Title fine edge", typeof(RectTransform), typeof(Image)); edge.transform.SetParent(target.transform, false);
            var rect = (RectTransform)edge.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var line = edge.GetComponent<Image>(); line.sprite = HeaderEdge; line.type = Image.Type.Sliced; line.raycastTarget = false;
            line.color = new Color(1, 1, 1, primary ? 1 : .75f);
        }
        public void Card(GameObject target, Color border)
        {
            var image = target.GetComponent<Image>(); image.sprite = Rounded; image.type = Image.Type.Sliced;
            var shadow = target.AddComponent<Shadow>(); shadow.effectColor = new Color(.02f, .07f, .12f, .38f); shadow.effectDistance = new Vector2(0, -5);
            var outline = target.AddComponent<Outline>(); outline.effectColor = border; outline.effectDistance = new Vector2(2, -2);
        }
        public Image Header(GameObject target)
        {
            var image = target.AddComponent<Image>(); image.sprite = HeaderFill; image.type = Image.Type.Sliced;
            image.color = Color.white; image.raycastTarget = false;
            var shadow = target.AddComponent<Shadow>(); shadow.effectColor = new Color(.015f, .035f, .06f, .3f);
            shadow.effectDistance = new Vector2(0, -3);
            var edge = new GameObject("Header fine edge", typeof(RectTransform), typeof(Image)); edge.transform.SetParent(target.transform, false);
            var edgeRect = (RectTransform)edge.transform; edgeRect.anchorMin = Vector2.zero; edgeRect.anchorMax = Vector2.one;
            edgeRect.offsetMin = edgeRect.offsetMax = Vector2.zero;
            var border = edge.GetComponent<Image>(); border.sprite = HeaderEdge; border.type = Image.Type.Sliced; border.raycastTarget = false;
            var shine = new GameObject("Header top highlight", typeof(RectTransform), typeof(Image)); shine.transform.SetParent(target.transform, false);
            var shineRect = (RectTransform)shine.transform; shineRect.anchorMin = new Vector2(0, 1); shineRect.anchorMax = Vector2.one;
            shineRect.pivot = new Vector2(.5f, 1); shineRect.offsetMin = new Vector2(18, -5); shineRect.offsetMax = new Vector2(-18, -4);
            shine.GetComponent<Image>().color = new Color(.8f, .92f, 1, .1f); shine.GetComponent<Image>().raycastTarget = false;
            return image;
        }
        public void Dispose()
        {
            Object.Destroy(Rounded); Object.Destroy(texture); Object.Destroy(Ring); Object.Destroy(ringTexture);
            Object.Destroy(HeaderFill); Object.Destroy(headerTexture); Object.Destroy(HeaderEdge); Object.Destroy(headerEdgeTexture);
            Object.Destroy(ActionFill); Object.Destroy(actionTexture);
        }
    }
}
