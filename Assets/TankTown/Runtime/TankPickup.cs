using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    // Code-drawn icon silhouettes share one texture/material per type and work in the HUD too.
    public sealed class UpgradeIcons
    {
        private readonly Texture2D[] textures = new Texture2D[4];
        private readonly Material[] materials = new Material[4];
        private readonly Sprite[] sprites = new Sprite[4];
        public Material Material(UpgradeKind kind) => materials[(int)kind];
        public Sprite Sprite(UpgradeKind kind) => sprites[(int)kind];
        public UpgradeIcons()
        {
            for (int kind = 0; kind < 4; kind++)
            {
                var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false) { name = "Upgrade " + (UpgradeKind)kind, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[128 * 128];
                Color color = TankStats.Color((UpgradeKind)kind);
                for (int y = 0; y < 128; y++)
                    for (int x = 0; x < 128; x++)
                    {
                        var p = new Vector2((x + .5f) / 64 - 1, (y + .5f) / 64 - 1);
                        float r = p.magnitude;
                        Color value = Color.clear;
                        if (r < .94f) value = r > .84f ? Color.white : Color.Lerp(color * .72f, color, Mathf.Clamp01(p.y * .4f + .6f));
                        bool symbol = false;
                        if (kind == 0)
                        {
                            // Two upward chevrons.
                            float chevron = .3f - Mathf.Abs(p.x) * .8f;
                            symbol = Mathf.Abs(p.x) < .46f && (Mathf.Abs(p.y - chevron - .17f) < .10f || Mathf.Abs(p.y - chevron + .22f) < .10f);
                        }
                        else if (kind == 1)
                        {
                            float angle = Mathf.Atan2(p.y, p.x);
                            symbol = r < .34f + .18f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 4)), 6);
                        }
                        else if (kind == 2)
                        {
                            for (int bullet = -1; bullet <= 1; bullet++)
                            {
                                float dx = p.x - bullet * .32f;
                                symbol |= Mathf.Abs(dx) < .09f && p.y > -.38f && p.y < .23f;
                                symbol |= new Vector2(dx, p.y - .23f).magnitude < .09f;
                            }
                        }
                        else
                        {
                            symbol = r > .32f && r < .5f && p.x < .24f;
                            symbol |= p.x > .12f && p.x < .54f && p.y > .17f && p.y < .51f && p.y < .65f - p.x * .55f;
                        }
                        if (symbol) value = Color.white;
                        value.a *= Mathf.Clamp01((.94f - r) * 128);
                        pixels[y * 128 + x] = value;
                    }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                textures[kind] = texture;
                materials[kind] = new Material(Shader.Find("TankTown/Billboard")) { mainTexture = texture };
                sprites[kind] = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 128, 128), Vector2.one * .5f, 64, 0, SpriteMeshType.FullRect);
            }
        }
        public void Dispose()
        {
            for (int i = 0; i < 4; i++) { Object.Destroy(sprites[i]); Object.Destroy(materials[i]); Object.Destroy(textures[i]); }
        }
    }

    public sealed class TankPickup : MonoBehaviour
    {
        public UpgradeKind Kind { get; private set; }
        public bool Collected { get; private set; }
        private TankTownGame game;
        private Transform icon;
        private Vector3 rest;
        private float phase;
        public void Initialize(TankTownGame owner, UpgradeKind kind, Material material)
        {
            game = owner; Kind = kind;
            var collider = gameObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 1.9f;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Floating 2D icon";
            quad.GetComponent<Collider>().enabled = false;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = Vector3.up * 1.35f;
            quad.transform.localScale = Vector3.one * 2.2f;
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            icon = quad.transform;
            rest = icon.localPosition;
            phase = transform.position.x * .3f + transform.position.z * .1f;
        }
        private void LateUpdate()
        {
            if (Collected || Camera.main == null) return;
            icon.localPosition = rest + Vector3.up * Mathf.Sin(Time.time * 2.4f + phase) * .28f;
            // Camera-aligned quad is a true billboard; it remains readable from every approach.
            icon.rotation = Camera.main.transform.rotation;
        }
        private void OnTriggerEnter(Collider other)
        {
            var tank = other.GetComponentInParent<TankMotor>();
            if (tank == game.Tank) Collect();
        }
        public bool Collect()
        {
            if (Collected || !game.IsPlaying || game.Tank.Respawning) return false;
            if (!game.Stats.Upgrade(Kind)) return false;
            Collected = true;
            game.Effects.Pickup(transform.position + Vector3.up, TankStats.Color(Kind));
            game.PlayTone(520, .22f, .18f);
            gameObject.SetActive(false);
            return true;
        }

        public void Attract(Vector3 target, float speed)
        {
            if (Collected || !game.IsPlaying || game.Stats.Level(Kind) >= game.Settings.maxUpgradeLevel) return;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude < 2.9f) Collect();
        }
    }
}
