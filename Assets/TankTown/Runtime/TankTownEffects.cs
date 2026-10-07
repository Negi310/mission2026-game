using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    public sealed class TankTownEffects : MonoBehaviour
    {
        private Material glow, smoke;
        private readonly Material[] fireColors = new Material[4];
        private Texture2D softDot;
        private Mesh ring, burst;
        private TankTownSettings settings;
        public Material Glow => glow;
        internal Material RewardAreaLight { get; private set; }
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
            RewardAreaLight = new Material(Shader.Find("TankTown/Reward Area"));
            for (int i = 0; i < fireColors.Length; i++)
            {
                fireColors[i] = new Material(Shader.Find("TankTown/Explosion Fire"));
                fireColors[i].SetColor("_BaseColor", i == 0 ? new Color(1, .23f, .025f) : i == 1 ? new Color(1, .59f, .045f) : i == 2 ? new Color(1, .87f, .16f) : new Color(1, .99f, .78f));
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
            float visualRadius = Mathf.Clamp(radius * (settings != null ? settings.explosionVisualScale : 1.55f) * .55f, 2.5f, 8);
            Fireball(position, visualRadius);
            Particles(position, new Color(1, .52f, .045f), 95, 25, 1.1f, .45f, Vector3.up);
            Particles(position, new Color(1, .82f, .08f), 120, 38, .48f, .45f, Vector3.up, false, true);
            Particles(position + Vector3.up, new Color(.94f, .76f, .47f), 28, 6, 3.9f, .65f, Vector3.up, true);
            Shockwave(position, new Color(1, .66f, .1f), visualRadius * 2.7f, .35f);
            Shockwave(position + Vector3.up * 1.2f, new Color(1, .9f, .3f), visualRadius * 1.9f, .28f, Quaternion.Euler(90, 0, 0));
            var rays = new GameObject("Explosion radial rays", typeof(MeshFilter), typeof(MeshRenderer));
            rays.transform.SetParent(transform); rays.transform.position = position;
            rays.GetComponent<MeshFilter>().sharedMesh = burst; rays.GetComponent<Renderer>().sharedMaterial = glow;
            rays.AddComponent<ArcadePulse>().Initialize(new Color(1, .94f, .48f), visualRadius * 2.1f, .2f, true);
            Flash(position, new Color(1, .96f, .68f), visualRadius * 1.25f, .12f);
        }
        public void Collapse(Vector3 position, float width)
        {
            Particles(position, new Color(.91f, .83f, .65f), 34, 7, 3.2f, .5f, Vector3.up, true);
            Particles(position, new Color(1, .67f, .15f), 35, 13, .4f, .4f, Vector3.up, false, true);
            Shockwave(position, new Color(1, .75f, .3f), width, .35f);
        }
        public void DebrisImpact(Vector3 position, float radius)
        {
            Fireball(position, radius * .85f);
            Particles(position, new Color(1, .66f, .08f), 48, 28, .45f, .35f, Vector3.up, false, true);
            Shockwave(position, new Color(1, .8f, .2f), radius * 2, .25f);
            Flash(position, new Color(1, .96f, .68f), radius * 1.1f, .1f);
        }
        public void EnergyImpact(Vector3 position, Color color)
        {
            Particles(position, color, 36, 14, .8f, .5f, Vector3.up, false, true);
            Shockwave(position, color, 7, .4f);
            Flash(position, color, 4, .18f);
        }
        public void SuperImpact(Vector3 position, float radius)
        {
            Fireball(position, radius * 1.25f);
            Particles(position, new Color(1, .35f, .015f), 320, 45, 4, .65f, Vector3.up);
            Particles(position + Vector3.up * 3, new Color(.96f, .76f, .42f), 58, 15, 9, .9f, Vector3.up, true);
            Particles(position, new Color(1, .9f, .15f), 160, 55, .9f, .6f, Vector3.up, false, true);
            Shockwave(position, new Color(1, .75f, .1f), radius * 3.2f, .5f);
            Shockwave(position + Vector3.up * 2, new Color(1, .92f, .4f), radius * 2.8f, .38f, Quaternion.Euler(90, 0, 0));
            Flash(position, Color.white, radius * 2.3f, .12f);
        }
        public void FuelImpact(Vector3 position, float radius)
        {
            SuperImpact(position, radius * .65f);
        }
        public void CarImpact(Vector3 position, float radius)
        {
            Fireball(position, radius * .95f);
            Fireball(position + Vector3.up * 3, radius * .6f);
            Particles(position, new Color(1, .45f, .035f), 150, 34, 1.8f, .48f, Vector3.up);
            Particles(position, new Color(1, .9f, .15f), 130, 44, .55f, .38f, Vector3.up, false, true);
            Particles(position + Vector3.up * 2, new Color(.94f, .76f, .48f), 35, 9, 5, .65f, Vector3.up, true);
            Shockwave(position, new Color(1, .72f, .12f), radius * 2.8f, .38f);
            Shockwave(position + Vector3.up * 2, new Color(1, .96f, .5f), radius * 2.2f, .3f, Quaternion.Euler(90, 0, 0));
            Flash(position, new Color(1, .96f, .6f), radius * 1.4f, .1f);
        }
        public void PedestrianBonk(Vector3 position)
        {
            Particles(position, new Color(1, .92f, .18f), 10, 6, .24f, .35f, Vector3.up, false, true);
            Shockwave(position, new Color(1, .9f, .3f), 1.8f, .22f);
        }
        public void BeamPulse(Vector3 start, Vector3 forward, float radius, Vector3 impact, bool hit)
        {
            Particles(start + forward * 1.5f, new Color(.2f, .95f, 1), 24, 12, .4f, .24f, forward, false, true);
            Shockwave(start + forward * 2, new Color(.12f, .85f, 1), radius * 1.1f, .22f, Quaternion.FromToRotation(Vector3.up, forward));
            if (!hit) return;
            Particles(impact, new Color(.8f, .98f, 1), 65, 30, .55f, .3f, Vector3.up, false, true);
            Particles(impact, new Color(1, .18f, .7f), 30, 20, .45f, .25f, Vector3.up, false, true);
            Shockwave(impact, new Color(.1f, .9f, 1), radius * 2.5f, .3f);
            Flash(impact, new Color(.65f, .95f, 1), radius * .85f, .12f);
        }
        private void Fireball(Vector3 position, float radius)
        {
            var root = new GameObject("Large explosion fireball");
            root.transform.SetParent(transform);
            root.transform.position = position;
            if (Camera.main != null) root.transform.rotation = Quaternion.LookRotation((Camera.main.transform.position - position).normalized, Vector3.up);
            // Outer orange petals, inner yellow petals, and a visible cream core.
            // The shape opens toward the current view without moving the camera.
            for (int i = 0; i < 19; i++)
            {
                var lobe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lobe.GetComponent<Collider>().enabled = false;
                Destroy(lobe.GetComponent<Collider>());
                lobe.transform.SetParent(root.transform, false);
                bool outer = i < 9, core = i >= 16;
                float angle = (outer ? i / 9f : (i - 9) / 7f) * Mathf.PI * 2 + Mathf.Sin(i * 1.7f) * .16f;
                float spread = outer ? .29f + Mathf.Sin(i * 2.21f) * .045f : .16f;
                var offset = core ? i == 16 ? new Vector3(-.03f, .20f, .38f) : i == 17 ? new Vector3(.1f, .27f, .37f) : new Vector3(-.09f, .34f, .35f)
                    : new Vector3(Mathf.Cos(angle) * spread, .24f + Mathf.Sin(angle) * spread * .8f, (outer ? -.035f : .19f) + Mathf.Sin(i * 1.2f) * .025f);
                lobe.transform.localPosition = offset * radius;
                lobe.transform.localScale = Vector3.one * radius * (core ? i == 16 ? .4f : i == 17 ? .28f : .24f : outer ? .42f + i % 3 * .085f : .34f + i % 2 * .055f);
                var renderer = lobe.GetComponent<Renderer>();
                renderer.sharedMaterial = fireColors[core ? 3 : outer ? i % 4 == 0 ? 1 : 0 : i % 2 == 0 ? 2 : 1];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            root.AddComponent<FireballBurst>().Initialize();
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
                gradient.SetKeys(new[] { new GradientColorKey(new Color(1, .76f, .42f), 0), new GradientColorKey(color, .25f), new GradientColorKey(new Color(.95f, .88f, .72f), 1) }, new[] { new GradientAlphaKey(.36f, 0), new GradientAlphaKey(.23f, .35f), new GradientAlphaKey(0, 1) });
            fading.color = gradient;
            var shrink = particles.sizeOverLifetime; shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1, isSmoke ? AnimationCurve.Linear(0, .5f, 1, 1.6f) : AnimationCurve.Linear(0, 1, 1, .1f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = isSmoke ? smoke : glow;
            if (streaks) { renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 3.8f; renderer.velocityScale = .15f; }
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            particles.Play(); Destroy(item, lifetime + .15f);
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
            Destroy(RewardAreaLight);
            foreach (var material in fireColors) Destroy(material);
        }
    }

    public sealed class FireballBurst : MonoBehaviour
    {
        private float age;
        private Renderer[] lobes;
        private Color[] colors;
        private MaterialPropertyBlock properties;
        private void Awake() { properties = new MaterialPropertyBlock(); transform.localScale = Vector3.zero; }
        public void Initialize()
        {
            lobes = GetComponentsInChildren<Renderer>();
            colors = new Color[lobes.Length];
            for (int i = 0; i < lobes.Length; i++) colors[i] = lobes[i].sharedMaterial.GetColor("_BaseColor");
        }
        private void Update()
        {
            age += Time.deltaTime;
            float size = age < .1f ? Mathf.SmoothStep(.12f, 1, age / .1f) : 1 + Mathf.Clamp01((age - .1f) / .5f) * .16f;
            transform.localScale = Vector3.one * size;
            float opacity = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, .6f, age));
            if (lobes != null) for (int i = 0; i < lobes.Length; i++)
            {
                properties.SetColor("_BaseColor", new Color(colors[i].r, colors[i].g, colors[i].b, opacity));
                lobes[i].SetPropertyBlock(properties);
            }
            if (age >= .6f) Destroy(gameObject);
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
