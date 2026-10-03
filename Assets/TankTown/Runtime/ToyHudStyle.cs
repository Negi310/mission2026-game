using UnityEngine;
using UnityEngine.UI;

namespace TankTown
{
    internal sealed class ToyHudStyle
    {
        private readonly Texture2D texture;
        public Sprite Rounded { get; }
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
        }
        public void Card(GameObject target, Color border)
        {
            var image = target.GetComponent<Image>(); image.sprite = Rounded; image.type = Image.Type.Sliced;
            var shadow = target.AddComponent<Shadow>(); shadow.effectColor = new Color(.02f, .07f, .12f, .38f); shadow.effectDistance = new Vector2(0, -5);
            var outline = target.AddComponent<Outline>(); outline.effectColor = border; outline.effectDistance = new Vector2(2, -2);
        }
        public void Dispose() { Object.Destroy(Rounded); Object.Destroy(texture); }
    }
}
