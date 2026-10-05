using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    public enum ItemKind { None = -1, MegaBeam, Missiles, Magnet, Bulldozer, GigantBomb, HighSpeed }

    public sealed class ItemPickup : MonoBehaviour
    {
        public ItemKind Kind { get; private set; }
        public bool Collected { get; private set; }
        public BlockBuilding SourceBuilding { get; private set; }
        public DestructibleBlock SupportingFloor { get; private set; }
        public Rigidbody DropBody { get; private set; }
        public bool Released { get; private set; }
        public bool Available => SourceBuilding == null || (Released && SourceBuilding.DestroyedFraction >= game.Settings.buildingRewardThreshold);
        private TankTownGame game;
        private Transform icon;
        private SphereCollider solid;
        private TrailRenderer dropTrail;
        private bool unlockShown;
        private bool attracted;
        private float attractUntil;
        public void Initialize(TankTownGame owner, ItemKind kind, Material material, BlockBuilding building = null, DestructibleBlock floor = null)
        {
            game = owner; Kind = kind; SourceBuilding = building; SupportingFloor = floor;
            gameObject.layer = TownPhysics.RewardLayer;
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true; trigger.radius = 2.2f;
            if (building != null)
            {
                solid = gameObject.AddComponent<SphereCollider>(); solid.radius = .65f;
                DropBody = gameObject.AddComponent<Rigidbody>();
                DropBody.mass = .5f; DropBody.isKinematic = true;
                DropBody.interpolation = RigidbodyInterpolation.Interpolate;
                DropBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                DropBody.constraints = RigidbodyConstraints.FreezeRotation;
            }
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Special item billboard";
            quad.layer = TownPhysics.RewardLayer;
            quad.GetComponent<Collider>().enabled = false; Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            quad.transform.localScale = Vector3.one * 3.6f;
            quad.GetComponent<Renderer>().sharedMaterial = material;
            quad.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            quad.GetComponent<Renderer>().receiveShadows = false;
            icon = quad.transform;
        }
        private void Start()
        {
            // Keep the pickup trigger working, while its physical body passes through the tank.
            if (solid != null && game.Tank != null) Physics.IgnoreCollision(solid, game.Tank.HullCollider, true);
        }
        private void FixedUpdate()
        {
            if (attracted && Time.time > attractUntil) StopAttracting();
            if (dropTrail != null) dropTrail.emitting = !Collected && game.IsPlaying && DropBody.linearVelocity.sqrMagnitude > 1;
            if (SourceBuilding == null || Released || !game.IsPlaying) return;
            if (SupportingFloor != null && !SupportingFloor.Detached && SourceBuilding.DestroyedFraction < game.Settings.buildingRewardThreshold) return;
            Released = true;
            DropBody.isKinematic = false;
            DropBody.WakeUp();
            dropTrail = gameObject.AddComponent<TrailRenderer>();
            dropTrail.sharedMaterial = game.Effects.Glow; dropTrail.time = .4f;
            dropTrail.startWidth = .35f; dropTrail.endWidth = 0;
            dropTrail.startColor = ItemIcons.Color(Kind); dropTrail.endColor = new Color(1, 1, 1, 0);
            dropTrail.shadowCastingMode = ShadowCastingMode.Off;
        }
        private void LateUpdate()
        {
            if (Collected || Camera.main == null) return;
            icon.localPosition = Vector3.up * ((SourceBuilding == null ? 2 : 1.2f) + Mathf.Sin(Time.time * 2.5f + transform.position.x) * .2f);
            icon.rotation = Camera.main.transform.rotation;
            if (!unlockShown && SourceBuilding != null && Available)
            {
                unlockShown = true;
                game.Effects.Pickup(transform.position, ItemIcons.Color(Kind));
            }
        }
        private void OnTriggerEnter(Collider other) => Touch(other);
        private void OnTriggerStay(Collider other) => Touch(other);
        private void Touch(Collider other)
        {
            if (other.GetComponentInParent<TankMotor>() == game.Tank) Collect();
        }
        public bool Collect()
        {
            if (Collected || !Available || !game.IsPlaying || game.Tank.Relocating || !game.Items.Receive(Kind)) return false;
            Collected = true;
            game.Effects.Pickup(transform.position + Vector3.up, ItemIcons.Color(Kind));
            game.PlayTone(620, .2f, .2f);
            game.ReportSpecial(Kind);
            gameObject.SetActive(false);
            return true;
        }
        public void Attract(Vector3 target, float speed)
        {
            if (Collected || !Available || !game.IsPlaying || game.Tank.Respawning || game.Tank.Relocating) return;
            // Float freed rewards through the rubble instead of fighting their fall velocity.
            if (DropBody != null && !attracted)
            {
                DropBody.linearVelocity = Vector3.zero; DropBody.angularVelocity = Vector3.zero;
                DropBody.isKinematic = true; solid.enabled = false; attracted = true;
            }
            attractUntil = Time.time + .08f;
            Vector3 point = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if (DropBody != null) DropBody.position = point;
            transform.position = point;
            if ((point - target).sqrMagnitude < 2.9f) Collect();
        }
        private void StopAttracting()
        {
            if (!attracted) return;
            attracted = false; solid.enabled = true;
            if (Released) { DropBody.isKinematic = false; DropBody.WakeUp(); }
        }
        private void OnDisable() => StopAttracting();
    }

    public sealed class ItemIcons
    {
        private readonly Texture2D[] textures = new Texture2D[6];
        private readonly Material[] materials = new Material[6];
        private readonly Sprite[] sprites = new Sprite[6];
        public Material Material(ItemKind kind) => materials[(int)kind];
        public Sprite Sprite(ItemKind kind) => sprites[(int)kind];
        public static bool Automatic(ItemKind kind) => kind == ItemKind.Missiles || kind == ItemKind.Magnet || kind == ItemKind.Bulldozer || kind == ItemKind.HighSpeed;
        public static string Label(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.MegaBeam: return "メガビーム";
                case ItemKind.Missiles: return "追尾ミサイル";
                case ItemKind.Magnet: return "マグネット";
                case ItemKind.Bulldozer: return "ブルドーザー";
                case ItemKind.GigantBomb: return "ギガント爆弾";
                case ItemKind.HighSpeed: return "ハイスピード";
                default: return "アイテムをひろおう";
            }
        }
        public static Color Color(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.MegaBeam: return new Color(.1f, .9f, 1);
                case ItemKind.Missiles: return new Color(1, .4f, .1f);
                case ItemKind.Magnet: return new Color(1, .17f, .65f);
                case ItemKind.Bulldozer: return new Color(1, .8f, .03f);
                case ItemKind.GigantBomb: return new Color(1, .18f, .3f);
                default: return new Color(.25f, .5f, 1);
            }
        }
        public ItemIcons()
        {
            for (int kind = 0; kind < 6; kind++)
            {
                var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false) { name = Label((ItemKind)kind), wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[128 * 128];
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                {
                    var p = new Vector2((x + .5f) / 64 - 1, (y + .5f) / 64 - 1);
                    float hex = Mathf.Max(Mathf.Abs(p.y) / .86f, (Mathf.Abs(p.x) + Mathf.Abs(p.y) * .55f) / .96f);
                    var color = hex < 1 ? hex > .88f ? UnityEngine.Color.white : Color((ItemKind)kind) * (.8f + p.y * .16f) : UnityEngine.Color.clear;
                    bool symbol = false;
                    if (kind == 0)
                    {
                        symbol = Mathf.Abs(p.x) < .12f && p.y > -.45f && p.y < .46f;
                        symbol |= Mathf.Abs(p.y + .42f) < .09f && Mathf.Abs(p.x) < .39f;
                        symbol |= p.y > .1f && Mathf.Abs(p.x) < .44f && Mathf.Abs(p.y - .43f + Mathf.Abs(p.x) * .5f) < .055f;
                    }
                    else if (kind == 1)
                    {
                        for (int m = -2; m <= 2; m++)
                        {
                            float dx = p.x - m * .23f, top = .43f - Mathf.Abs(m) * .09f;
                            symbol |= Mathf.Abs(dx) < .075f && p.y > -.36f && p.y < top;
                            symbol |= Mathf.Abs(dx) < .1f && p.y >= top && p.y < top + .14f && Mathf.Abs(dx) < (top + .14f - p.y) * .7f;
                        }
                    }
                    else if (kind == 2)
                    {
                        float r = new Vector2(p.x, p.y + .02f).magnitude;
                        symbol = r > .27f && r < .53f && p.y < .02f;
                        symbol |= Mathf.Abs(p.x) > .27f && Mathf.Abs(p.x) < .53f && p.y >= -.02f && p.y < .45f;
                    }
                    else if (kind == 3)
                    {
                        symbol = p.x > -.55f && p.x < .24f && p.y > -.18f && p.y < .17f;
                        symbol |= p.x > -.25f && p.x < .1f && p.y > .13f && p.y < .42f;
                        symbol |= p.x > .34f && p.x < .55f && p.y > -.38f && p.y < .31f;
                        symbol |= new Vector2(p.x + .36f, p.y + .32f).magnitude < .14f || new Vector2(p.x - .08f, p.y + .32f).magnitude < .14f;
                    }
                    else if (kind == 4)
                    {
                        symbol = new Vector2(p.x, p.y + .12f).magnitude < .42f;
                        symbol |= Mathf.Abs(p.x - .1f) < .065f && p.y > .25f && p.y < .46f;
                        symbol |= new Vector2(p.x - .21f, p.y - .48f).magnitude < .12f;
                    }
                    else
                    {
                        for (int n = -1; n <= 1; n++)
                            symbol |= Mathf.Abs(p.y) < .43f && Mathf.Abs(p.x - n * .28f - .2f + Mathf.Abs(p.y) * .7f) < .07f;
                    }
                    if (symbol && hex < .88f) color = UnityEngine.Color.white;
                    color.a = hex < 1 ? Mathf.Clamp01((1 - hex) * 80) : 0;
                    pixels[y * 128 + x] = color;
                }
                texture.SetPixels(pixels); texture.Apply(false, true); textures[kind] = texture;
                materials[kind] = new Material(Shader.Find("TankTown/Billboard")) { mainTexture = texture };
                sprites[kind] = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 128, 128), Vector2.one * .5f, 64, 0, SpriteMeshType.FullRect);
            }
        }
        public void Dispose()
        {
            for (int i = 0; i < 6; i++) { Object.Destroy(sprites[i]); Object.Destroy(materials[i]); Object.Destroy(textures[i]); }
        }
    }
}
