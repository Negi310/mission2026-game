using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    public sealed class TankTownEffects : MonoBehaviour
    {
        private Material glow;
        private Texture2D softDot;
        private Mesh ring;
        public Material Glow => glow;
        private void Awake()
        {
            softDot = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float r = new Vector2((x + .5f) / 32 - 1, (y + .5f) / 32 - 1).magnitude;
                pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - r), 1.4f));
            }
            softDot.SetPixels(pixels); softDot.Apply(false, true);
            glow = new Material(Shader.Find("TankTown/Arcade Glow")) { mainTexture = softDot };
            ring = BuildRing();
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
            Particles(position, new Color(1, .87f, .1f), 22, 7, .7f, .22f, forward);
            Flash(position, new Color(1, .9f, .35f), 2.4f, .13f);
        }
        public void Impact(Vector3 position, float radius)
        {
            Particles(position, new Color(1, .35f, .06f), 55, 13, 1.1f, .7f, Vector3.up);
            Particles(position, new Color(1, .88f, .16f), 32, 18, .36f, .45f, Vector3.up);
            Shockwave(position, new Color(1, .64f, .1f), radius * 2.1f, .45f);
            Flash(position, new Color(1, .9f, .5f), radius * 1.8f, .20f);
        }
        public void Pickup(Vector3 position, Color color)
        {
            Particles(position, color, 26, 5, .55f, .55f, Vector3.up);
            Shockwave(position, color, 3.5f, .5f);
        }
        public void Crash(Vector3 position)
        {
            Impact(position, 4);
            Particles(position, new Color(1, .15f, .55f), 28, 8, .9f, .6f, Vector3.up);
        }
        private void Particles(Vector3 position, Color color, int count, float speed, float size, float lifetime, Vector3 forward)
        {
            var item = new GameObject("Arcade particles");
            item.transform.position = position;
            item.transform.rotation = Quaternion.LookRotation(forward);
            var particles = item.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.duration = .1f; main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .5f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .45f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .35f, size);
            main.startColor = color; main.gravityModifier = .7f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count;
            var emission = particles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .35f;
            var fading = particles.colorOverLifetime; fading.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(color, .25f), new GradientColorKey(color, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.9f, .35f), new GradientAlphaKey(0, 1) });
            fading.color = gradient;
            var shrink = particles.sizeOverLifetime; shrink.enabled = true; shrink.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, .1f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glow; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            particles.Play(); Destroy(item, lifetime + .4f);
        }
        private void Flash(Vector3 position, Color color, float size, float duration)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Quad);
            item.name = "Impact star flash";
            item.GetComponent<Collider>().enabled = false; Destroy(item.GetComponent<Collider>());
            item.transform.position = position;
            item.GetComponent<Renderer>().sharedMaterial = glow;
            item.AddComponent<ArcadePulse>().Initialize(color, size, duration, true);
        }
        private void Shockwave(Vector3 position, Color color, float size, float duration)
        {
            var item = new GameObject("Expanding shockwave", typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.position = position + Vector3.up * .1f;
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
        private void OnDestroy() { Destroy(glow); Destroy(softDot); Destroy(ring); }
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
