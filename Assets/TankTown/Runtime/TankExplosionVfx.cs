using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    // Reuses bounded particle bursts and four short-lived local lights. No particle physics.
    internal sealed class TankExplosionVfx
    {
        internal const int Capacity = 16;
        internal const float BurstLifetime = 3.8f;
        private readonly Transform parent;
        private readonly List<FireballBurst> bursts = new List<FireballBurst>(Capacity);
        private readonly Material flame, smoke, sparks;
        private readonly Texture2D flameTexture, smokeTexture;
        private readonly Vector4[] positions = new Vector4[4], colors = new Vector4[4];
        private readonly float[] time = new float[4], duration = new float[4];
        private readonly Color[] tints = new Color[4];
        private static TankExplosionVfx lightOwner;
        private int nextBurst;
        internal int ActiveCount { get { int n = 0; foreach (var burst in bursts) if (burst.gameObject.activeSelf) n++; return n; } }
        internal int AllocatedCount => bursts.Count;
        internal Material SmokeMaterial => smoke;

        internal TankExplosionVfx(Transform owner, Material sparkMaterial)
        {
            parent = owner; sparks = sparkMaterial;
            flameTexture = CloudTexture(true); smokeTexture = CloudTexture(false);
            flame = new Material(Shader.Find("TankTown/Volume Particles")) { name = "Layered flame particles", mainTexture = flameTexture };
            flame.SetFloat("_Fire", 1); flame.SetFloat("_Emission", 2.05f);
            smoke = new Material(Shader.Find("TankTown/Volume Particles")) { name = "Lit rolling smoke", mainTexture = smokeTexture };
            smoke.SetFloat("_Fire", 0); smoke.SetFloat("_Emission", 1);
            lightOwner = this; Shader.SetGlobalInt("_ToyFlashCount", 0);
        }

        private static Texture2D CloudTexture(bool fire)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = fire ? "Turbulent flame density" : "Soft volumetric smoke density", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float px = (x + .5f) / size * 2 - 1, py = (y + .5f) / size * 2 - 1;
                float n = Mathf.PerlinNoise(px * 3.1f + 7.8f, py * 3.1f + 4.3f) * .6f + Mathf.PerlinNoise(px * 7.7f + 3.2f, py * 7.7f + 6.7f) * .28f + Mathf.PerlinNoise(px * 16 + 1.4f, py * 16 + 3.5f) * .12f;
                float radial = Mathf.Max(0, 1 - px * px - py * py);
                float density = Mathf.SmoothStep(0, 1, Mathf.Clamp01(radial * 2.8f - (1 - n) * .9f));
                float shade = Mathf.Clamp01(.64f + py * .22f - px * .18f + (n - .5f) * .55f);
                pixels[y * size + x] = new Color(shade, n, radial, density * (fire ? 1 : .9f));
            }
            texture.SetPixels(pixels); texture.Apply(true, true); return texture;
        }

        internal void Burst(Vector3 point, float radius)
        {
            FireballBurst target = null;
            foreach (var burst in bursts) if (!burst.gameObject.activeSelf) { target = burst; break; }
            if (target == null && bursts.Count < Capacity)
            {
                var root = new GameObject("Layered explosion VFX"); root.transform.SetParent(parent, false); root.SetActive(false);
                target = root.AddComponent<FireballBurst>(); target.Build(flame, smoke, sparks); bursts.Add(target);
            }
            if (target == null) target = bursts[nextBurst++ % bursts.Count];
            target.Emit(point, Mathf.Clamp(radius * 2.8f, 2.8f, 86));
        }

        internal void Illuminate(Vector3 point, Color tint, float range, float life)
        {
            int slot = 0;
            for (int i = 1; i < time.Length; i++) if (time[i] < time[slot]) slot = i;
            positions[slot] = new Vector4(point.x, point.y + 1, point.z, Mathf.Pow(Mathf.Clamp(range * 3.8f, 10, 125), 2));
            tints[slot] = tint; time[slot] = duration[slot] = Mathf.Clamp(life * 2.0f, .24f, 1.5f);
        }

        internal void Tick(float dt)
        {
            if (lightOwner != this) return;
            int count = 0;
            for (int i = 0; i < time.Length; i++)
            {
                time[i] = Mathf.Max(0, time[i] - dt);
                if (time[i] <= 0) continue;
                colors[count] = (Vector4)(tints[i] * (2.8f * Mathf.Pow(time[i] / duration[i], 2)));
                // Compact the upload without modifying the persistent light slots.
                uploadPositions[count] = positions[i]; count++;
            }
            Shader.SetGlobalVectorArray("_ToyFlashPositions", uploadPositions);
            Shader.SetGlobalVectorArray("_ToyFlashColors", colors);
            Shader.SetGlobalInt("_ToyFlashCount", count);
        }
        private readonly Vector4[] uploadPositions = new Vector4[4];

        internal void Dispose()
        {
            if (lightOwner == this) { Shader.SetGlobalInt("_ToyFlashCount", 0); lightOwner = null; }
            Object.Destroy(flame); Object.Destroy(smoke); Object.Destroy(flameTexture); Object.Destroy(smokeTexture);
        }
    }

    public sealed class FireballBurst : MonoBehaviour
    {
        private ParticleSystem fire, smoke, sparks;
        private float age;
        internal void Build(Material fireMaterial, Material smokeMaterial, Material sparkMaterial)
        {
            fire = Create("Rolling fire and flame", fireMaterial, 32);
            smoke = Create("Rolling explosion smoke", smokeMaterial, 18);
            sparks = Create("Hot explosion sparks", sparkMaterial, 36);
            var renderer = sparks.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 3.2f; renderer.velocityScale = .10f;
        }

        private ParticleSystem Create(string label, Material material, int count)
        {
            var root = new GameObject(label); root.transform.SetParent(transform, false);
            var ps = root.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = false; main.duration = .22f;
            main.maxParticles = count; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            var emission = ps.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)(count * .7f)), new ParticleSystem.Burst(.14f, (short)(count * .3f)) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            return ps;
        }

        internal void Emit(Vector3 point, float radius)
        {
            gameObject.SetActive(true); transform.position = point; age = 0;
            Configure(fire, radius, .85f, 1.85f, .42f, .86f, .72f, 1.32f);
            Configure(smoke, radius, 1.45f, 2.85f, .45f, .90f, .20f, .42f);
            Configure(sparks, radius, .8f, 2.1f, .025f, .065f, 2.8f, 4.6f);
            var fireMain = fire.main; fireMain.gravityModifier = -.12f;
            var smokeMain = smoke.main; smokeMain.gravityModifier = -.28f; smokeMain.startDelay = .45f;
            var sparkMain = sparks.main; sparkMain.gravityModifier = 1.4f;
            AddRoll(fire, radius * .14f, .7f); AddRoll(smoke, radius * .10f, .35f);
            SetGradient(fire, new[] { new GradientColorKey(new Color(1, .97f, .65f), 0), new GradientColorKey(new Color(1, .75f, .08f), .42f), new GradientColorKey(new Color(1, .22f, .015f), .8f), new GradientColorKey(new Color(.45f, .06f, .01f), 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.95f, .62f), new GradientAlphaKey(0, 1) });
            SetGradient(smoke, new[] { new GradientColorKey(new Color(.46f, .32f, .24f), 0), new GradientColorKey(new Color(.55f, .53f, .51f), .35f), new GradientColorKey(new Color(.76f, .75f, .72f), 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.42f, .16f), new GradientAlphaKey(.20f, .55f), new GradientAlphaKey(0, 1) });
            SetGradient(sparks, new[] { new GradientColorKey(new Color(1, .97f, .6f), 0), new GradientColorKey(new Color(1, .3f, .02f), 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            var fireSize = fire.sizeOverLifetime; fireSize.enabled = true; fireSize.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .7f), new Keyframe(.22f, 1.3f), new Keyframe(.68f, 1.2f), new Keyframe(1, .7f)));
            var smokeSize = smoke.sizeOverLifetime; smokeSize.enabled = true; smokeSize.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .65f, 1, 2.25f));
            foreach (var ps in new[] { fire, smoke, sparks }) ps.Play(false);
        }

        private static void Configure(ParticleSystem ps, float radius, float minLife, float maxLife, float minSize, float maxSize, float minSpeed, float maxSpeed)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * minSize, radius * maxSize);
            main.startSpeed = new ParticleSystem.MinMaxCurve(Mathf.Min(45, radius * minSpeed), Mathf.Min(60, radius * maxSpeed));
            main.startColor = Color.white;
            var shape = ps.shape; shape.radius = radius * .16f;
            var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.limit = radius * maxSpeed; limit.dampen = .18f;
        }

        private static void SetGradient(ParticleSystem ps, GradientColorKey[] colors, GradientAlphaKey[] alpha)
        { var gradient = new Gradient(); gradient.SetKeys(colors, alpha); var module = ps.colorOverLifetime; module.enabled = true; module.color = gradient; }

        private static void AddRoll(ParticleSystem ps, float strength, float scroll)
        {
            var noise = ps.noise; noise.enabled = true; noise.strength = strength; noise.frequency = .5f;
            noise.scrollSpeed = scroll; noise.octaveCount = 1; noise.quality = ParticleSystemNoiseQuality.Low;
        }

        private void Update() { age += Time.deltaTime; if (age >= TankExplosionVfx.BurstLifetime) gameObject.SetActive(false); }
        private void OnDisable()
        {
            if (fire != null) fire.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (smoke != null) smoke.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (sparks != null) sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
