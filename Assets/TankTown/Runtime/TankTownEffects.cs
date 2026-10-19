using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace TankTown
{
    public sealed class TankTownEffects : MonoBehaviour
    {
        private Material glow, smoke;
        private TankExplosionVfx explosions;
        private readonly List<GameObject> accents = new List<GameObject>(64);
        private bool AccentAvailable()
        {
            if (accents.Count < 64) return true;
            accents.RemoveAll(item => item == null);
            return accents.Count < 64;
        }
        internal int ActiveExplosionCount => explosions.ActiveCount;
        internal int AllocatedExplosionCount => explosions.AllocatedCount;
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
            explosions = new TankExplosionVfx(transform, glow); smoke = explosions.SmokeMaterial;
            RewardAreaLight = new Material(Shader.Find("TankTown/Reward Area"));
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
            Shockwave(position, new Color(1, .66f, .1f), visualRadius * 5.1f, 1.05f);
            Shockwave(position + Vector3.up * .5f, new Color(1, .91f, .38f), visualRadius * 3.6f, .65f, Quaternion.Euler(90, 0, 0));
            if (AccentAvailable())
            {
                var rays = new GameObject("Explosion radial rays", typeof(MeshFilter), typeof(MeshRenderer)); accents.Add(rays);
                rays.transform.SetParent(transform); rays.transform.position = position;
                rays.GetComponent<MeshFilter>().sharedMesh = burst; rays.GetComponent<Renderer>().sharedMaterial = glow;
                rays.AddComponent<ArcadePulse>().Initialize(new Color(1, .94f, .48f), visualRadius * 4.1f, .52f, true);
            }
            Flash(position, new Color(1, .96f, .68f), visualRadius * 2.5f, .28f);
        }
        public void Collapse(Vector3 position, float width)
        {
            Particles(position, new Color(.91f, .83f, .65f), 34, 9, 4.6f, 1.0f, Vector3.up, true);
            Particles(position, new Color(1, .67f, .15f), 35, 17, .5f, .7f, Vector3.up, false, true);
            Shockwave(position, new Color(1, .75f, .3f), width * 1.3f, .6f);
        }
        public void DebrisImpact(Vector3 position, float radius)
        {
            Fireball(position, radius * .85f);
            Particles(position, new Color(1, .66f, .08f), 48, 32, .6f, .7f, Vector3.up, false, true);
            Shockwave(position, new Color(1, .8f, .2f), radius * 3.6f, .8f);
            Flash(position, new Color(1, .96f, .68f), radius * 2.2f, .24f);
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
            Shockwave(position, new Color(1, .75f, .1f), radius * 5.6f, 1.25f);
            Shockwave(position + Vector3.up * 2, new Color(1, .92f, .4f), radius * 4.8f, 1.0f, Quaternion.Euler(90, 0, 0));
            Flash(position, Color.white, radius * 4.0f, .30f);
        }
        public void FuelImpact(Vector3 position, float radius)
        {
            SuperImpact(position, radius * .65f);
        }
        public void CarImpact(Vector3 position, float radius)
        {
            Fireball(position, radius * .95f);
            Shockwave(position, new Color(1, .72f, .12f), radius * 5.1f, 1.05f);
            Shockwave(position + Vector3.up * .5f, new Color(1, .92f, .4f), radius * 3.1f, .65f, Quaternion.Euler(90, 0, 0));
            Flash(position, new Color(1, .96f, .6f), radius * 2.8f, .26f);
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
        private void Fireball(Vector3 position, float radius) => explosions.Burst(position, radius);
        public void Pickup(Vector3 position, Color color)
        {
            Particles(position, color, 26, 5, .55f, .55f, Vector3.up);
            Shockwave(position, color, 3.5f, .5f);
        }
        public void Crash(Vector3 position)
        {
            SuperImpact(position, (settings != null ? settings.crashBlastRadius : 10) * .75f);
            Particles(position, new Color(1, .15f, .55f), 48, 18, 1.2f, .45f, Vector3.up);
        }
        private void Particles(Vector3 position, Color color, int count, float speed, float size, float lifetime, Vector3 forward, bool isSmoke = false, bool streaks = false)
        {
            if (!AccentAvailable()) return;
            var item = new GameObject(isSmoke ? "Explosion smoke" : "Arcade particles");
            accents.Add(item);
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
            explosions.Illuminate(position, color, size, duration);
            if (!AccentAvailable()) return;
            var item = GameObject.CreatePrimitive(PrimitiveType.Quad);
            accents.Add(item);
            item.name = "Impact star flash";
            item.GetComponent<Collider>().enabled = false; Destroy(item.GetComponent<Collider>());
            item.transform.SetParent(transform);
            item.transform.position = position;
            item.GetComponent<Renderer>().sharedMaterial = glow;
            item.AddComponent<ArcadePulse>().Initialize(color, size, duration, true);
        }
        private void Shockwave(Vector3 position, Color color, float size, float duration, Quaternion? rotation = null)
        {
            if (!AccentAvailable()) return;
            var item = new GameObject("Expanding shockwave", typeof(MeshFilter), typeof(MeshRenderer));
            accents.Add(item);
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
            trail.sharedMaterial = glow; trail.time = .075f; trail.startWidth = .26f; trail.endWidth = .015f;
            trail.minVertexDistance = .12f; trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.startColor = new Color(1, .87f, .35f); trail.endColor = new Color(1, .44f, .08f, 0);
        }
        private void OnDestroy()
        {
            explosions?.Dispose();
            Destroy(glow); Destroy(softDot); Destroy(ring); Destroy(burst);
            Destroy(RewardAreaLight);
        }
        private void Update() => explosions?.Tick(Time.deltaTime);
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
