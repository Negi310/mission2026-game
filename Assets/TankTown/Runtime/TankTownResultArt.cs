using UnityEngine;
using UnityEngine.UI;

namespace TankTown
{
    internal enum ResultArtKind { Panel, Details, Emblem, Action, Party, Confetti, TitleDecor, TitleAction }

    // Bright toy-like vector artwork. Decorations never intercept result controls.
    public sealed class TankTownResultArt : MaskableGraphic
    {
        internal static readonly Color Ink = new Color(.16f, .08f, .36f);
        internal static readonly Color Pink = new Color(1, .16f, .48f);
        internal static readonly Color Yellow = new Color(1, .82f, .08f);
        internal static readonly Color Aqua = new Color(.06f, .8f, .95f);
        internal static readonly Color Purple = new Color(.53f, .29f, .91f);
        private static readonly Color[] Palette = { Pink, Yellow, Aqua, Purple, new Color(.36f, .88f, .30f), new Color(1, .49f, .13f) };
        private ResultArtKind kind;
        private Color accent;
        private float progress, sweep = -1;
        private Rect bounds;

        internal void Initialize(ResultArtKind type, Color highlight)
        { kind = type; accent = highlight; raycastTarget = false; SetVerticesDirty(); }
        internal void SetAccent(Color value)
        { if (accent == value) return; accent = value; SetVerticesDirty(); }
        internal void SetMotion(float value, float time = -1)
        {
            if (Mathf.Approximately(progress, value) && Mathf.Approximately(sweep, time)) return;
            progress = value; sweep = time; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); bounds = rectTransform.rect;
            if (kind == ResultArtKind.Emblem) DrawEmblem(vh);
            else if (kind == ResultArtKind.Action || kind == ResultArtKind.TitleAction) DrawAction(vh);
            else if (kind == ResultArtKind.Party || kind == ResultArtKind.Confetti) DrawParty(vh);
            else if (kind == ResultArtKind.TitleDecor) DrawTitleDecor(vh);
            else DrawPanel(vh);
        }

        private void DrawTitleDecor(VertexHelper vh)
        {
            float time = Mathf.Max(0, sweep);
            for (int i = 0; i < 9; i++)
            {
                float x = i < 3 ? 32 + i * 288 : i < 6 ? 706 + (i % 3) * 17 : 54 + (i % 3) * 277;
                float y = i < 3 ? 113 : i < 6 ? 255 + i % 3 * 190 : 820;
                Star(vh, x, y + Mathf.Sin(time * 1.4f + i) * 3, 13 + i % 3 * 5, 6 + i % 3 * 2, i * 31, Palette[i % Palette.Length]);
            }
            Line(vh, new Vector2(703, 167), new Vector2(712, 144), 7, Pink);
            Line(vh, new Vector2(729, 185), new Vector2(748, 178), 7, Aqua);
            Circle(vh, 715, 770, 7, Yellow);
        }

        private void DrawPanel(VertexHelper vh)
        {
            float w = bounds.width, h = bounds.height;
            Rounded(vh, 0, 12, w, h - 12, 48, Ink, Ink);
            Rounded(vh, 0, 0, w, h - 12, 48, Purple, Pink);
            Rounded(vh, 8, 8, w - 16, h - 31, 40, new Color(1, .99f, .94f), new Color(.97f, .94f, 1));
            Rounded(vh, 31, 23, 768, 120, 30, Pink, new Color(.94f, .09f, .51f));
            Star(vh, 832, 66, 29, 13, -12, Yellow);
            Star(vh, w - 48, 146, 18, 8, 12, Aqua);
            for (int i = 0; i < 7; i++) Circle(vh, 890 + i * 50, 34, 7, Palette[i % Palette.Length]);
            if (kind == ResultArtKind.Details)
            {
                Rounded(vh, 34, 170, 818, 190, 30, new Color(1, .94f, .71f), new Color(1, .97f, .84f));
                return;
            }
            Rounded(vh, 33, 172, 851, 329, 36, new Color(1, .85f, .24f), new Color(1, .92f, .57f));
            Rounded(vh, 43, 181, 831, 310, 29, new Color(1, .99f, .89f), new Color(1, .96f, .77f));
            for (int i = 0; i < 20; i++)
            {
                var color = Palette[(i / 3) % Palette.Length];
                Rounded(vh, 63 + i * 39, 456, 30, 13, 6, new Color(.87f, .81f, .72f), new Color(.87f, .81f, .72f));
                if (progress * 20 > i) Rounded(vh, 63 + i * 39, 456, 30, 13, 6, color, color);
            }
            // Confetti surrounds the score, leaving its cream reading area clear.
            Star(vh, 885, 537, 24, 11, 14, Pink);
            Star(vh, 37, 541, 17, 7, -14, Aqua);
            Star(vh, w - 54, h - 58, 23, 10, 17, Yellow);
            Line(vh, new Vector2(918, 531), new Vector2(932, 509), 8, Purple);
            Line(vh, new Vector2(870, 168), new Vector2(881, 146), 7, Aqua);
        }

        private void DrawEmblem(VertexHelper vh)
        {
            float cx = bounds.width * .5f, cy = bounds.height * .5f, r = Mathf.Min(cx, cy);
            Ribbon(vh, cx - 93, cy + 81, 66, 91, -13, Pink);
            Ribbon(vh, cx + 28, cy + 81, 66, 91, 13, Aqua);
            Starburst(vh, cx, cy + 4, r - 3, r - 20, 20, Ink);
            Starburst(vh, cx, cy - 3, r - 4, r - 20, 20, Yellow);
            Circle(vh, cx, cy - 3, r - 30, new Color(1, .58f, .06f));
            Circle(vh, cx, cy - 7, r - 37, new Color(1, .9f, .25f));
            Circle(vh, cx, cy - 11, r - 47, new Color(1, .95f, .55f));
            Star(vh, cx, cy - 83, 26, 12, -90, Pink);
            Star(vh, cx - 55, cy - 68, 13, 6, -90, Purple);
            Star(vh, cx + 55, cy - 68, 13, 6, -90, Aqua);
            Circle(vh, cx - 87, cy + 27, 5, new Color(1, 1, 1, .85f));
            Circle(vh, cx + 89, cy - 24, 7, new Color(1, 1, 1, .85f));
        }

        private void DrawAction(VertexHelper vh)
        {
            float w = bounds.width, h = bounds.height;
            if (kind == ResultArtKind.TitleAction)
            {
                var shadow = new Color(Ink.r, Ink.g, Ink.b, .15f);
                Rounded(vh, 0, 3, w, h - 3, 22, shadow, shadow);
                Rounded(vh, 0, 0, w, h - 5, 22, Color.Lerp(accent, Color.white, .65f), Color.Lerp(accent, Ink, .12f));
                Rounded(vh, 1.5f, 1.5f, w - 3, h - 8, 20.5f, Color.Lerp(accent, Color.white, .13f), accent);
            }
            else
            {
                Rounded(vh, 0, 7, w, h - 7, 22, Ink, Ink);
                Rounded(vh, 0, 0, w, h - 7, 22, Ink, Ink);
                Rounded(vh, 4, 4, w - 8, h - 15, 18, Color.Lerp(accent, Color.white, .13f), accent);
            }
            Line(vh, new Vector2(24, 12), new Vector2(w - 24, 12), 3, new Color(1, 1, 1, .44f));
            Line(vh, new Vector2(w - 28, h / 2 - 8), new Vector2(w - 20, h / 2), 4, Color.white);
            Line(vh, new Vector2(w - 20, h / 2), new Vector2(w - 28, h / 2 + 8), 4, Color.white);
        }

        private void DrawParty(VertexHelper vh)
        {
            float w = bounds.width, h = bounds.height;
            var center = new Vector2(w * .5f, h * .40f);
            // A fixed sunburst adds energy without rotating the entire background.
            for (int i = 0; kind == ResultArtKind.Party && i < 24; i++)
            {
                float a = i * Mathf.PI * 2 / 24;
                Triangle(vh, center, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1200,
                    center + new Vector2(Mathf.Cos(a + .12f), Mathf.Sin(a + .12f)) * 1200,
                    new Color(1, 1, 1, .10f));
            }
            float time = Mathf.Max(0, sweep);
            if (kind == ResultArtKind.Confetti && sweep < 0) return;
            // Finite confetti, then gently bobbing border stars. No rapid flashes.
            if (kind == ResultArtKind.Confetti && time < 4.5f)
            {
                for (int i = 0; i < 76; i++)
                {
                    float delay = (i % 13) * .035f, t = time - delay;
                    if (t < 0) continue;
                    float startX = i % 2 == 0 ? 25 : w - 25, direction = i % 2 == 0 ? 1 : -1;
                    float x = startX + direction * (90 + (i * 37 % 480)) * t + Mathf.Sin(i + t * 3) * 15;
                    float y = h - 80 - (350 + i * 53 % 480) * t + 185 * t * t;
                    // Keep the score, captions, medal digits and clickable controls clear.
                    if ((x > 100 && x < 955 && y > 245 && y < 510) ||
                        (x > 100 && x < 875 && y > 80 && y < 215) ||
                        (x > 130 && x < 900 && y > 565 && y < 640) ||
                        (x > 1040 && x < 1290 && y > 320 && y < 520) ||
                        (x > 100 && x < 1350 && y > 640 && y < 855)) continue;
                    var color = Palette[i % Palette.Length]; color.a = 1 - Mathf.Clamp01((t - 3.0f) / .8f);
                    if (y < -35 || y > h + 35 || x < -35 || x > w + 35 || color.a <= 0) continue;
                    float angle = i * 41 + t * (55 + i % 5 * 18);
                    if (i % 4 == 0) Star(vh, x, y, 13, 6, angle, color);
                    else Chip(vh, x, y, 9 + i % 5, 5, angle, color);
                }
            }
            for (int i = 0; kind == ResultArtKind.Party && i < 12; i++)
            {
                float x = i < 6 ? 22 + i % 3 * 17 : w - 22 - i % 3 * 17;
                float y = 90 + i % 6 * 140 + Mathf.Sin(time * 1.6f + i) * 5;
                Star(vh, x, y, 13 + i % 3 * 4, 6 + i % 3, i * 27, Palette[i % Palette.Length]);
            }
        }

        private Vector3 Point(float x, float y) => new Vector3(bounds.xMin + x, bounds.yMax - y);
        private void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int n = vh.currentVertCount;
            vh.AddVert(Point(a.x, a.y), tint, Vector2.zero); vh.AddVert(Point(b.x, b.y), tint, Vector2.zero);
            vh.AddVert(Point(c.x, c.y), tint, Vector2.zero); vh.AddTriangle(n, n + 1, n + 2);
        }
        private void Rounded(VertexHelper vh, float x, float y, float w, float h, float radius, Color top, Color bottom)
        {
            radius = Mathf.Min(radius, Mathf.Min(w, h) * .5f);
            int n = vh.currentVertCount;
            vh.AddVert(Point(x + w * .5f, y + h * .5f), Color.Lerp(top, bottom, .5f), Vector2.zero);
            const int segments = 8;
            for (int corner = 0; corner < 4; corner++)
            {
                float cx = corner < 2 ? x + w - radius : x + radius;
                float cy = corner == 0 || corner == 3 ? y + radius : y + h - radius;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (-90 + corner * 90 + i * 90f / segments) * Mathf.Deg2Rad;
                    float px = cx + Mathf.Cos(angle) * radius, py = cy + Mathf.Sin(angle) * radius;
                    vh.AddVert(Point(px, py), Color.Lerp(top, bottom, (py - y) / h), Vector2.zero);
                }
            }
            int count = 4 * (segments + 1);
            for (int i = 0; i < count; i++) vh.AddTriangle(n, n + 1 + i, n + 1 + (i + 1) % count);
        }
        private void Circle(VertexHelper vh, float x, float y, float radius, Color tint)
            => Starburst(vh, x, y, radius, radius, 32, tint);
        private void Star(VertexHelper vh, float x, float y, float outer, float inner, float angle, Color tint)
            => Starburst(vh, x, y, outer, inner, 5, tint, angle);
        private void Starburst(VertexHelper vh, float x, float y, float outer, float inner, int points, Color tint, float angle = -90)
        {
            int n = vh.currentVertCount; vh.AddVert(Point(x, y), tint, Vector2.zero);
            for (int i = 0; i < points * 2; i++)
            {
                float a = (angle + i * 180f / points) * Mathf.Deg2Rad, r = i % 2 == 0 ? outer : inner;
                vh.AddVert(Point(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r), tint, Vector2.zero);
            }
            for (int i = 0; i < points * 2; i++) vh.AddTriangle(n, n + 1 + i, n + 1 + (i + 1) % (points * 2));
        }
        private void Ribbon(VertexHelper vh, float x, float y, float w, float h, float angle, Color tint)
        {
            Chip(vh, x + w * .5f, y + h * .5f, w * .5f, h * .5f, angle, Ink);
            Chip(vh, x + w * .5f, y + h * .5f - 5, w * .5f - 4, h * .5f - 3, angle, tint);
        }
        private void Chip(VertexHelper vh, float x, float y, float rx, float ry, float angle, Color tint)
        {
            var right = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * rx;
            var down = new Vector2(-Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad)) * ry;
            int n = vh.currentVertCount; var c = new Vector2(x, y);
            var p = c - right - down; vh.AddVert(Point(p.x, p.y), tint, Vector2.zero);
            p = c + right - down; vh.AddVert(Point(p.x, p.y), tint, Vector2.zero);
            p = c + right + down; vh.AddVert(Point(p.x, p.y), tint, Vector2.zero);
            p = c - right + down; vh.AddVert(Point(p.x, p.y), tint, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
        private void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var delta = b - a;
            Chip(vh, (a.x + b.x) * .5f, (a.y + b.y) * .5f, delta.magnitude * .5f, width * .5f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, tint);
        }
    }
}
