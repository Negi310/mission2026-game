using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    public sealed class TankTownEffects : MonoBehaviour
    {
        private Material glow, smoke;
        private readonly Material[] fireColors = new Material[3];
        private Texture2D softDot;
        private Mesh ring, burst;
        private TankTownSettings settings;
        public Material Glow => glow;
        private void Awake()
        {
            settings = GetComponent<TankTownGame>()?.Settings;
            softDot = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float r = new Vector2((x + .5f) / 32 - 1, (y + .5f) / 32 - 1).magnitude;
                pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - r), 1.4f));
            }
            softDot.SetPixels(pixels); softDot.Apply(false, true);
            glow = new Material(Shader.Find("TankTown/Arcade Glow")) { mainTexture = softDot };
            smoke = new Material(Shader.Find("TankTown/Arcade Smoke")) { mainTexture = softDot };
            for (int i = 0; i < fireColors.Length; i++)
            {
                fireColors[i] = new Material(Shader.Find("TankTown/Explosion Fire"));
                fireColors[i].SetColor("_BaseColor", i == 0 ? new Color(1, .21f, .015f) : i == 1 ? new Color(1, .56f, .025f) : new Color(1, .88f, .13f));
            }
            ring = BuildRing();
            burst = BuildBurst();
        }
        private static Mesh BuildBurst()
        {
            const int count = 16;
            var vertices = new Vector3[count * 3]; var triangles = new int[count * 3];
            var colors = new Color[count * 3]; var uv = new Vector2[count * 3];
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                var d = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                var side = new Vector3(-d.y, d.x, 0) * .045f;
                vertices[i * 3] = d * .08f - side; vertices[i * 3 + 1] = d * .08f + side;
                vertices[i * 3 + 2] = d * (1 + i % 3 * .13f);
                for (int n = 0; n < 3; n++) { triangles[i * 3 + n] = i * 3 + n; uv[i * 3 + n] = Vector2.one * .5f; colors[i * 3 + n] = new Color(1, 1, 1, n == 2 ? 0 : .8f); }
            }
            var mesh = new Mesh { name = "Explosion burst rays", vertices = vertices, triangles = triangles, colors = colors, uv = uv };
            mesh.RecalculateBounds(); return mesh;
        }
        private static Mesh BuildRing()
        {
            const int count = 64;
            var vertices = new Vector3[count * 2]; var triangles = new int[count * 6];
            var colors = new Color[count * 2]; var uv = new Vector2[count * 2];
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count;
                var direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                vertices[i * 2] = direction * .82f; vertices[i * 2 + 1] = direction;
                colors[i * 2] = colors[i * 2 + 1] = Color.white;
                uv[i * 2] = uv[i * 2 + 1] = new Vector2(.5f, .5f);
                int n = (i + 1) % count * 2, b = i * 6;
                triangles[b] = i * 2; triangles[b + 1] = n; triangles[b + 2] = i * 2 + 1;
                triangles[b + 3] = i * 2 + 1; triangles[b + 4] = n; triangles[b + 5] = n + 1;
            }
            var mesh = new Mesh { name = "Arcade shockwave", vertices = vertices, colors = colors, uv = uv, triangles = triangles };
            mesh.RecalculateBounds(); return mesh;
        }
        public void Muzzle(Vector3 position, Vector3 forward)
        {
            Particles(position, new Color(1, .87f, .1f), 40, 11, 1.1f, .25f, forward);
            Flash(position, new Color(1, .9f, .35f), 3.4f, .15f);
        }
        public void Impact(Vector3 position, float radius)
        {
            float visualRadius = Mathf.Clamp(radius * (settings != null ? settings.explosionVisualScale : 1.55f), 4, 16);
            Fireball(position, visualRadius);
            Particles(position, new Color(1, .36f, .025f), 180, 29, 2, 1, Vector3.up);
            Particles(position, new Color(1, .82f, .08f), 120, 38, .48f, .9f, Vector3.up, false, true);
            Particles(position + Vector3.up, new Color(.48f, .33f, .26f), 46, 8, 5.2f, 1.8f, Vector3.up, true);
            Shockwave(position, new Color(1, .66f, .1f), visualRadius * 2.7f, .6f);
            Shockwave(position + Vector3.up * 1.2f, new Color(1, .9f, .3f), visualRadius * 1.9f, .45f, Quaternion.Euler(90, 0, 0));
            var rays = new GameObject("Explosion radial rays", typeof(MeshFilter), typeof(MeshRenderer));
            rays.transform.SetParent(transform); rays.transform.position = position;
            rays.GetComponent<MeshFilter>().sharedMesh = burst; rays.GetComponent<Renderer>().sharedMaterial = glow;
            rays.AddComponent<ArcadePulse>().Initialize(new Color(1, .94f, .48f), visualRadius * 2.1f, .3f, true);
            Flash(position, new Color(1, .91f, .45f), visualRadius * 2.2f, .13f);
        }
        public void Collapse(Vector3 position, float width)
        {
            Particles(position, new Color(.65f, .57f, .44f), 42, 8, 4, 1.6f, Vector3.up, true);
            Particles(position, new Color(1, .67f, .15f), 35, 13, .4f, .65f, Vector3.up, false, true);
            Shockwave(position, new Color(1, .75f, .3f), width, .7f);
        }
        private void Fireball(Vector3 position, float radius)
        {
            var root = new GameObject("Large explosion fireball");
            root.transform.SetParent(transform);
            root.transform.position = position;
            for (int i = 0; i < 10; i++)
            {
                var lobe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lobe.GetComponent<Collider>().enabled = false;
                Destroy(lobe.GetComponent<Collider>());
                lobe.transform.SetParent(root.transform, false);
                var offset = i == 0 ? Vector3.zero : Random.insideUnitSphere * .3f;
                offset.y = Mathf.Abs(offset.y) + .22f;
                lobe.transform.localPosition = offset * radius;
                lobe.transform.localScale = Vector3.one * radius * (i == 0 ? .78f : .55f);
                var renderer = lobe.GetComponent<Renderer>();
                renderer.sharedMaterial = fireColors[i == 0 ? 2 : i % 3];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            root.AddComponent<FireballBurst>();
        }
        public void Pickup(Vector3 position, Color color)
        {
            Particles(position, color, 26, 5, .55f, .55f, Vector3.up);
            Shockwave(position, color, 3.5f, .5f);
        }
        public void Crash(Vector3 position)
        {
            Impact(position, 7);
            Particles(position, new Color(1, .15f, .55f), 28, 8, .9f, .6f, Vector3.up);
        }
        private void Particles(Vector3 position, Color color, int count, float speed, float size, float lifetime, Vector3 forward, bool isSmoke = false, bool streaks = false)
        {
            var item = new GameObject(isSmoke ? "Explosion smoke" : "Arcade particles");
            item.transform.SetParent(transform);
            item.transform.position = position;
            item.transform.rotation = Quaternion.LookRotation(forward);
            var particles = item.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.duration = .1f; main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .5f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .45f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .35f, size);
            main.startColor = Color.white; main.gravityModifier = isSmoke ? -.08f : .7f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count;
            var emission = particles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = isSmoke ? 2.6f : .8f;
            var fading = particles.colorOverLifetime; fading.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(color, .25f), new GradientColorKey(color, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.9f, .35f), new GradientAlphaKey(0, 1) });
            if (isSmoke)
                gradient.SetKeys(new[] { new GradientColorKey(new Color(1, .54f, .12f), 0), new GradientColorKey(color, .25f), new GradientColorKey(new Color(.3f, .34f, .4f), 1) }, new[] { new GradientAlphaKey(.55f, 0), new GradientAlphaKey(.35f, .35f), new GradientAlphaKey(0, 1) });
            fading.color = gradient;
            var shrink = particles.sizeOverLifetime; shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1, isSmoke ? AnimationCurve.Linear(0, .5f, 1, 1.6f) : AnimationCurve.Linear(0, 1, 1, .1f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = isSmoke ? smoke : glow;
            if (streaks) { renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 3.8f; renderer.velocityScale = .15f; }
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            particles.Play(); Destroy(item, lifetime + .4f);
        }
        private void Flash(Vector3 position, Color color, float size, float duration)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Quad);
            item.name = "Impact star flash";
            item.GetComponent<Collider>().enabled = false; Destroy(item.GetComponent<Collider>());
            item.transform.SetParent(transform);
            item.transform.position = position;
            item.GetComponent<Renderer>().sharedMaterial = glow;
            item.AddComponent<ArcadePulse>().Initialize(color, size, duration, true);
        }
        private void Shockwave(Vector3 position, Color color, float size, float duration, Quaternion? rotation = null)
        {
            var item = new GameObject("Expanding shockwave", typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(transform);
            item.transform.position = position + Vector3.up * .1f;
            item.transform.rotation = rotation ?? Quaternion.identity;
            item.GetComponent<MeshFilter>().sharedMesh = ring;
            item.GetComponent<Renderer>().sharedMaterial = glow;
            item.AddComponent<ArcadePulse>().Initialize(color, size, duration, false);
        }
        public void Trail(GameObject shell)
        {
            var trail = shell.AddComponent<TrailRenderer>();
            trail.sharedMaterial = glow; trail.time = .18f; trail.startWidth = .65f; trail.endWidth = .02f;
            trail.minVertexDistance = .12f; trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.startColor = new Color(1, .8f, .15f); trail.endColor = new Color(1, .16f, .55f, 0);
        }
        private void OnDestroy()
        {
            Destroy(glow); Destroy(smoke); Destroy(softDot); Destroy(ring); Destroy(burst);
            foreach (var material in fireColors) Destroy(material);
        }
    }

    public sealed class FireballBurst : MonoBehaviour
    {
        private float age;
        private void Awake() { transform.localScale = Vector3.zero; }
        private void Update()
        {
            age += Time.deltaTime;
            float size = age < .09f ? Mathf.Lerp(.2f, 1, age / .09f) : Mathf.Pow(Mathf.Clamp01(1 - (age - .09f) / .46f), .65f);
            transform.localScale = Vector3.one * size;
            if (age >= .55f) Destroy(gameObject);
        }
    }

    public sealed class ArcadePulse : MonoBehaviour
    {
        private Color color;
        private float size, duration, age;
        private bool billboard;
        private Renderer target;
        private MaterialPropertyBlock properties;
        public void Initialize(Color tint, float scale, float life, bool faceCamera)
        {
            properties = new MaterialPropertyBlock();
            color = tint; size = scale; duration = life; billboard = faceCamera;
            target = GetComponent<Renderer>();
            target.shadowCastingMode = ShadowCastingMode.Off;
            transform.localScale = Vector3.zero;
        }
        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            transform.localScale = Vector3.one * size * (billboard ? Mathf.Sin(t * Mathf.PI) : Mathf.Lerp(.12f, 1, t));
            if (billboard && Camera.main != null) transform.rotation = Camera.main.transform.rotation;
            properties.SetColor("_Color", new Color(color.r, color.g, color.b, 1 - t));
            target.SetPropertyBlock(properties);
            if (age >= duration) Destroy(gameObject);
        }
    }
}
