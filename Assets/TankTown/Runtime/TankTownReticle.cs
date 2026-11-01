using UnityEngine;
using UnityEngine.UI;

namespace TankTown
{
    // Canvas geometry stays crisp at every UI scale and needs no texture or text glyph.
    internal sealed class TankTownReticle : Graphic
    {
        private float cooldown, kick, lockAmount;
        private Vector2 marker;
        internal bool HasTarget { get; private set; }

        internal void Tick(float remainingCooldown, bool hasTarget, Vector2 targetOffset)
        {
            if (remainingCooldown > cooldown + .2f) kick = 1;
            cooldown = remainingCooldown;
            kick = Mathf.MoveTowards(kick, 0, Time.deltaTime * 7);
            HasTarget = hasTarget;
            lockAmount = Mathf.MoveTowards(lockAmount, hasTarget ? 1 : 0, Time.deltaTime * 12);
            marker = Vector2.Lerp(marker, targetOffset, 1 - Mathf.Exp(-24 * Time.deltaTime));
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var tint = Color.Lerp(new Color(.82f, .97f, 1), new Color(1, .84f, .22f), lockAmount);
            float radius = 13 + kick * 3, corner = 25 + kick * 5;
            var shadow = new Color(.02f, .065f, .10f, .8f);
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                Arc(mesh, radius, quadrant * 90 + 14, 62, 4.6f, shadow);
                Arc(mesh, radius, quadrant * 90 + 14, 62, 2, tint);
                float x = quadrant % 2 == 0 ? -1 : 1, y = quadrant < 2 ? -1 : 1;
                var p = new Vector2(x * corner, y * corner);
                Stroke(mesh, p, p - new Vector2(x * 9, 0), 5, shadow);
                Stroke(mesh, p, p - new Vector2(0, y * 9), 5, shadow);
                Stroke(mesh, p, p - new Vector2(x * 9, 0), 2.3f, tint);
                Stroke(mesh, p, p - new Vector2(0, y * 9), 2.3f, tint);
            }
            Stroke(mesh, new Vector2(-3, 0), new Vector2(3, 0), 4.5f, shadow);
            Stroke(mesh, new Vector2(0, -3), new Vector2(0, 3), 4.5f, shadow);
            Stroke(mesh, new Vector2(-2, 0), new Vector2(2, 0), 1.6f, Color.white);
            Stroke(mesh, new Vector2(0, -2), new Vector2(0, 2), 1.6f, Color.white);
            if (cooldown > .01f) Arc(mesh, 20 + kick * 2, 90, -360 * (1 - cooldown), 2, new Color(tint.r, tint.g, tint.b, .65f));
            // Small impact diamond: only show a separate marker when the assistance visibly bends the shot.
            if (lockAmount > .01f && marker.sqrMagnitude > 64)
            {
                var markColor = tint; markColor.a = lockAmount;
                for (int side = 0; side < 4; side++)
                {
                    float a = side * Mathf.PI * .5f, b = a + Mathf.PI * .5f;
                    var p = marker + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4.5f;
                    var q = marker + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 4.5f;
                    Stroke(mesh, p, q, 4.2f, new Color(shadow.r, shadow.g, shadow.b, shadow.a * lockAmount));
                    Stroke(mesh, p, q, 1.8f, markColor);
                }
            }
        }

        private static void Arc(VertexHelper mesh, float radius, float start, float sweep, float width, Color tint)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(sweep) / 9));
            for (int i = 0; i < steps; i++)
            {
                float a = (start + sweep * i / steps) * Mathf.Deg2Rad;
                float b = (start + sweep * (i + 1) / steps) * Mathf.Deg2Rad;
                Stroke(mesh, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, tint);
            }
        }

        private static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            int index = mesh.currentVertCount;
            mesh.AddVert(a - normal, tint, Vector2.zero); mesh.AddVert(a + normal, tint, Vector2.zero);
            mesh.AddVert(b + normal, tint, Vector2.zero); mesh.AddVert(b - normal, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
