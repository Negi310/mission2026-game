using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    public sealed class TankItems : MonoBehaviour
    {
        public ItemKind Held { get; private set; } = ItemKind.None;
        public ItemKind Weapon { get; private set; } = ItemKind.None;
        internal event System.Action<ItemKind, float> Received;
        public bool MissilesActive => missileTime > 0;
        public bool StopsForward => Weapon == ItemKind.MegaBeam;
        public bool Invincible => bulldozer > 0 || highSpeed > 0;
        public float SpeedMultiplier => (highSpeed > 0 ? game.Settings.highSpeedMultiplier : 1) * (bulldozer > 0 ? game.Settings.bulldozerSpeedMultiplier : 1);
        public int MissileStrikes { get; private set; }
        public int MissileTrackedStrikes { get; private set; }
        internal float MagnetPullSpeed => Mathf.Max(game.Settings.magnetPullSpeed,
            Mathf.Max(game.Stats.Speed * SpeedMultiplier, game.Tank.ActualSpeed) * 3);
        internal Material MissileWhite { get; private set; }
        internal Material MissileOrange { get; private set; }
        internal int MissilesTracking(TownMovingTarget target)
        {
            int count = 0; foreach (var missile in missiles) if (missile != null && missile.TrackingTarget == target) count++; return count;
        }
        public int BeamHits { get; private set; }
        public ItemKind Active => Weapon != ItemKind.None ? Weapon : MissilesActive ? ItemKind.Missiles : bulldozer > 0 ? ItemKind.Bulldozer : highSpeed > 0 ? ItemKind.HighSpeed : magnet > 0 ? ItemKind.Magnet : ItemKind.None;
        public float Remaining(ItemKind kind) => kind == ItemKind.Missiles ? missileTime : kind == Weapon ? weaponTime : kind == ItemKind.Magnet ? magnet : kind == ItemKind.Bulldozer ? bulldozer : kind == ItemKind.HighSpeed ? highSpeed : 0;
        public string ActiveSummary
        {
            get
            {
                var lines = new List<string>(4);
                if (Weapon != ItemKind.None) lines.Add($"{ItemIcons.Label(Weapon)}  {weaponTime:0.0}秒");
                if (MissilesActive) lines.Add($"追尾ミサイル  {missileTime:0.0}秒");
                if (magnet > 0) lines.Add($"マグネット  {magnet:0.0}秒");
                if (bulldozer > 0) lines.Add($"ブルドーザー  {bulldozer:0.0}秒");
                if (highSpeed > 0) lines.Add($"ハイスピード  {highSpeed:0.0}秒");
                return string.Join("\n", lines);
            }
        }
        private TankTownGame game;
        private float weaponTime, missileTime, magnet, bulldozer, highSpeed, nextBeamHit, nextRam, nextRamEffect;
        private GameObject beamRoot, bladeRoot, magnetRing, shield;
        private readonly List<ItemMissile> missiles = new List<ItemMissile>();
        private LineRenderer[] beam, beamRings;
        private TrailRenderer[] dashTrails;
        private GigantBomb bomb;
        private Material energy, yellow;
        private Renderer hull;
        private Material[] hullPaint, bulldozerPaint;
        private MeshRenderer[] vehicleRenderers;
        private MaterialPropertyBlock expiryProperties;
        private float vehicleFlash = -1;

        public void Initialize(TankTownGame owner)
        {
            game = owner;
            expiryProperties = new MaterialPropertyBlock();
            MissileWhite = game.World.MakeMaterial(new Color(1, .93f, .72f));
            MissileOrange = game.World.MakeMaterial(new Color(1, .3f, .04f));
            energy = new Material(Shader.Find("TankTown/Item Energy"));
            energy.SetColor("_Color", Color.white);
            yellow = game.World.MakeMaterial(new Color(1, .72f, .025f));
            hull = game.Tank.Visual.Find("Hull").GetComponent<Renderer>(); hullPaint = hull.sharedMaterials;
            // Only the body paint turns yellow; lights, reflectors and dark details keep their materials.
            bulldozerPaint = new Material[hullPaint.Length];
            for (int i = 0; i < hullPaint.Length; i++) bulldozerPaint[i] = hullPaint[i] == game.World.TankPaint ? yellow : hullPaint[i];
            bladeRoot = new GameObject("Bulldozer blade and cabin");
            bladeRoot.transform.SetParent(game.Tank.Visual, false);
            game.World.Box("Wide bulldozer blade", new Vector3(0, -.03f, 1.8f), new Vector3(3.6f, 1.55f, .5f), yellow, bladeRoot.transform, false);
            game.World.Box("Bulldozer cabin", new Vector3(0, .7f, -1), new Vector3(1.5f, 1.2f, 1.2f), yellow, bladeRoot.transform, false);
            for (int i = -2; i <= 2; i++)
                game.World.Box("Blade tooth", new Vector3(i * .65f, -.78f, 2.08f), new Vector3(.3f, .24f, .6f), yellow, bladeRoot.transform, false);
            bladeRoot.SetActive(false);
            magnetRing = new GameObject("Magnetic field ring");
            magnetRing.transform.SetParent(game.Tank.Visual, false);
            var ring = magnetRing.AddComponent<LineRenderer>();
            SetupLine(ring, .18f, ItemIcons.Color(ItemKind.Magnet));
            ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 64;
            for (int i = 0; i < 64; i++) ring.SetPosition(i, new Vector3(Mathf.Cos(i * Mathf.PI / 32) * 2.4f, -.45f, Mathf.Sin(i * Mathf.PI / 32) * 2.4f));
            magnetRing.SetActive(false);
            shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shield.name = "High speed energy shield";
            shield.GetComponent<Collider>().enabled = false; Destroy(shield.GetComponent<Collider>());
            shield.transform.SetParent(game.Tank.Visual, false);
            shield.transform.localScale = new Vector3(3.4f, 2.15f, 4.4f);
            shield.GetComponent<Renderer>().sharedMaterial = energy;
            var properties = new MaterialPropertyBlock(); properties.SetColor("_Color", new Color(.12f, .5f, 1, .2f));
            shield.GetComponent<Renderer>().SetPropertyBlock(properties);
            shield.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            shield.SetActive(false);
            dashTrails = new TrailRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var root = new GameObject("High speed trail"); root.transform.SetParent(game.Tank.Visual, false);
                root.transform.localPosition = new Vector3(i == 0 ? -1.4f : 1.4f, 0, -1.5f);
                var trail = root.AddComponent<TrailRenderer>(); trail.sharedMaterial = energy;
                trail.time = .45f; trail.startWidth = .8f; trail.endWidth = 0; trail.minVertexDistance = .1f;
                trail.startColor = new Color(.15f, .65f, 1, .8f); trail.endColor = new Color(.05f, .9f, 1, 0);
                trail.emitting = false; trail.shadowCastingMode = ShadowCastingMode.Off; dashTrails[i] = trail;
            }
            vehicleRenderers = game.Tank.Visual.GetComponentsInChildren<MeshRenderer>(true);
        }

        public bool Receive(ItemKind kind)
        {
            if (!game.IsPlaying || game.Tank.Respawning || (int)kind < 0 || (int)kind >= 6) return false;
            float previous = Remaining(kind);
            if (!ItemIcons.Automatic(kind))
            {
                if (Held != ItemKind.None) return false;
                Held = kind; Received?.Invoke(kind, 0); return true;
            }
            if (kind == ItemKind.Missiles)
            {
                missileTime = Duration(kind);
                if (missiles.Count == 0)
                    for (int i = 0; i < 5; i++)
                    {
                        var root = new GameObject("Homing missile " + (i + 1)); root.transform.SetParent(game.World.transform);
                        var missile = root.AddComponent<ItemMissile>(); missile.Initialize(game, this, i); missiles.Add(missile);
                    }
            }
            if (kind == ItemKind.Magnet) { magnet = Duration(kind); magnetRing.SetActive(true); }
            if (kind == ItemKind.Bulldozer)
            {
                bulldozer = Duration(kind); bladeRoot.SetActive(true); hull.sharedMaterials = bulldozerPaint;
                game.Tank.SetVehicleScale(game.Settings.bulldozerScale, true);
            }
            if (kind == ItemKind.HighSpeed)
            {
                highSpeed = Duration(kind); shield.SetActive(true);
                foreach (var trail in dashTrails) { trail.Clear(); trail.emitting = true; }
            }
            UpdateExpiryVisuals();
            Received?.Invoke(kind, previous);
            return true;
        }

        public bool UseHeld()
        {
            if (!game.IsPlaying || game.Tank.Respawning || Held == ItemKind.None || Weapon != ItemKind.None) return false;
            Weapon = Held; Held = ItemKind.None; weaponTime = Duration(Weapon);
            game.PlayTone(440, .25f, .2f);
            if (Weapon == ItemKind.MegaBeam)
            {
                game.Tank.SetBeamElevation(true);
                beamRoot = new GameObject("Mega beam"); beamRoot.transform.SetParent(game.Effects.transform, false);
                beam = new LineRenderer[5];
                float[] widths = { 2.7f, 1.65f, .3f, .055f, .055f };
                Color[] tints = { new Color(.05f, .9f, 1, .12f), new Color(.08f, .8f, 1, .4f), Color.white, new Color(1, .3f, .8f, .85f), new Color(.65f, .98f, 1, .9f) };
                for (int i = 0; i < beam.Length; i++)
                {
                    var layer = new GameObject("Mega beam layer"); layer.transform.SetParent(beamRoot.transform, false);
                    beam[i] = layer.AddComponent<LineRenderer>();
                    SetupLine(beam[i], game.Settings.megaBeamRadius * widths[i], tints[i]);
                    if (i < 2)
                    {
                        var halo = new MaterialPropertyBlock(); halo.SetFloat("_SoftEdge", 1); beam[i].SetPropertyBlock(halo);
                    }
                    if (i >= 3) beam[i].positionCount = 22;
                }
                beamRings = new LineRenderer[2];
                for (int i = 0; i < 2; i++)
                {
                    var ring = new GameObject("Beam muzzle energy ring"); ring.transform.SetParent(beamRoot.transform, false);
                    beamRings[i] = ring.AddComponent<LineRenderer>(); SetupLine(beamRings[i], .28f, i == 0 ? new Color(.1f, .95f, 1) : Color.white);
                    beamRings[i].positionCount = 48; beamRings[i].loop = true;
                }
                nextBeamHit = 0; UpdateBeam();
            }
            if (Weapon == ItemKind.GigantBomb)
            {
                var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                root.name = "Gigant bomb"; root.transform.SetParent(game.World.transform);
                root.transform.localScale = Vector3.one * 8;
                root.GetComponent<Renderer>().sharedMaterial = game.World.MakeMaterial(new Color(.12f, .15f, .22f));
                root.transform.position = game.Tank.Muzzle.position + game.Tank.Muzzle.forward * 4 + Vector3.up * 6;
                bomb = root.AddComponent<GigantBomb>(); bomb.Initialize(game, this);
            }
            return true;
        }

        public float Duration(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.MegaBeam: return game.Settings.megaBeamSeconds;
                case ItemKind.Missiles: return game.Settings.missileSeconds;
                case ItemKind.Magnet: return game.Settings.magnetSeconds;
                case ItemKind.Bulldozer: return game.Settings.bulldozerSeconds;
                case ItemKind.GigantBomb: return game.Settings.gigantBombFuse;
                case ItemKind.HighSpeed: return game.Settings.highSpeedSeconds;
                default: return 0;
            }
        }
        private void SetupLine(LineRenderer line, float width, Color tint)
        {
            line.sharedMaterial = energy; line.positionCount = 2; line.useWorldSpace = true;
            line.startWidth = width; line.endWidth = width * .8f; line.startColor = line.endColor = tint;
            line.numCapVertices = 6; line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
        }
        private void Update()
        {
            if (!game.IsPlaying) return;
            float dt = Time.deltaTime;
            if (Weapon != ItemKind.None)
            {
                weaponTime = Mathf.Max(0, weaponTime - dt);
                if (StopsForward) UpdateBeam();
                if (weaponTime <= 0) EndWeapon(true);
            }
            if (MissilesActive)
            {
                missileTime = Mathf.Max(0, missileTime - dt);
                if (missileTime <= 0) EndMissiles();
            }
            bool wasBulldozer = bulldozer > 0, wasHighSpeed = highSpeed > 0;
            magnet = Mathf.Max(0, magnet - dt); bulldozer = Mathf.Max(0, bulldozer - dt); highSpeed = Mathf.Max(0, highSpeed - dt);
            if (wasBulldozer && bulldozer <= 0) ResetBulldozer();
            if (wasHighSpeed && highSpeed <= 0) foreach (var trail in dashTrails) trail.Clear();
            UpdateExpiryVisuals();
            if (magnet > 0 && !game.Tank.Respawning)
            {
                var target = game.Tank.GetComponent<Rigidbody>().position + Vector3.up * .6f;
                float speed = MagnetPullSpeed;
                foreach (var pickup in game.World.Pickups)
                    if (pickup != null && pickup.isActiveAndEnabled && !pickup.Collected && (pickup.transform.position - target).sqrMagnitude < game.Settings.magnetRadius * game.Settings.magnetRadius)
                        pickup.Attract(target, speed);
                foreach (var pickup in game.World.ItemPickups)
                    if (pickup != null && pickup.isActiveAndEnabled && !pickup.Collected && pickup.Available && (pickup.transform.position - target).sqrMagnitude < game.Settings.magnetRadius * game.Settings.magnetRadius)
                        pickup.Attract(target, speed);
                magnetRing.transform.localRotation = Quaternion.Euler(0, Time.time * 90, 0);
            }
        }
        private static bool WarningOn(float remaining)
            => remaining > 3 || Mathf.Repeat((3 - remaining) * 2.4f, 1) < .6f;

        private void UpdateExpiryVisuals()
        {
            bool speedVisible = WarningOn(highSpeed);
            magnetRing.SetActive(magnet > 0 && WarningOn(magnet));
            shield.SetActive(highSpeed > 0 && speedVisible);
            foreach (var trail in dashTrails)
            {
                trail.enabled = highSpeed <= 0 || speedVisible;
                trail.emitting = highSpeed > 0 && !StopsForward && !game.Tank.Respawning;
            }
            SetVehicleFlash(bulldozer > 0 && bulldozer <= 3 && !WarningOn(bulldozer) ? 1 : 0);
        }

        private void SetVehicleFlash(float value)
        {
            if (vehicleFlash == value || vehicleRenderers == null) return;
            vehicleFlash = value;
            foreach (var renderer in vehicleRenderers)
            {
                if (renderer == null || renderer.gameObject == shield) continue;
                renderer.GetPropertyBlock(expiryProperties);
                expiryProperties.SetFloat("_ItemFlash", value);
                renderer.SetPropertyBlock(expiryProperties);
            }
        }
        private void UpdateBeam()
        {
            var start = game.Tank.Muzzle.position;
            var end = start + game.Tank.Muzzle.forward * game.Settings.megaBeamLength;
            var right = game.Tank.Muzzle.right; var up = game.Tank.Muzzle.up;
            for (int i = 0; i < 3; i++) { beam[i].SetPosition(0, start); beam[i].SetPosition(1, end); beam[i].widthMultiplier = 1 + Mathf.Sin(Time.time * 18 + i) * .035f; }
            for (int i = 3; i < beam.Length; i++) for (int p = 0; p < beam[i].positionCount; p++)
            {
                float t = p / (float)(beam[i].positionCount - 1), angle = t * 28 - Time.time * 9 + i * Mathf.PI;
                float radius = game.Settings.megaBeamRadius * .32f * Mathf.Sin(t * Mathf.PI);
                beam[i].SetPosition(p, Vector3.Lerp(start, end, t) + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius);
            }
            for (int i = 0; i < beamRings.Length; i++) for (int p = 0; p < beamRings[i].positionCount; p++)
            {
                float angle = p * Mathf.PI * 2 / beamRings[i].positionCount;
                float radius = game.Settings.megaBeamRadius * (.3f + i * .1f) * (1 + Mathf.Sin(Time.time * 9 + i) * .06f);
                beamRings[i].SetPosition(p, start + game.Tank.Muzzle.forward * (1.3f + i * 2) + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius);
            }
            if (Time.time < nextBeamHit) return;
            nextBeamHit = Time.time + .12f;
            int hits = game.DamageBeam(start, end, game.Settings.megaBeamRadius, game.Stats.BlastImpulse * 1.8f);
            BeamHits += hits;
            game.Effects.BeamPulse(start, game.Tank.Muzzle.forward, game.Settings.megaBeamRadius, game.LastBeamImpact, hits > 0);
        }
        private void FixedUpdate()
        {
            if (!game.IsPlaying || game.Tank.Respawning || !Invincible || StopsForward || Time.time < nextRam) return;
            nextRam = Time.time + .08f;
            var body = game.Tank.GetComponent<Rigidbody>();
            var half = game.Tank.HullCollider.size * .5f;
            var center = body.position + body.rotation * Vector3.forward * (half.z + .6f);
            int hits = game.DamageBox(center, new Vector3(half.x + 1.8f, half.y + 1.5f, 2.5f), body.rotation, game.Stats.BlastImpulse * 2.4f);
            RamEffect(center, hits);
        }
        public void Ram(Vector3 point)
        {
            int hits = game.DamageSphere(point, bulldozer > 0 ? 8 : 6, game.Stats.BlastImpulse * 2.4f, false);
            RamEffect(point, hits);
        }
        private void RamEffect(Vector3 point, int hits)
        {
            if (hits <= 0 || Time.time < nextRamEffect) return;
            nextRamEffect = Time.time + .22f;
            game.Effects.EnergyImpact(point, bulldozer > 0 ? ItemIcons.Color(ItemKind.Bulldozer) : ItemIcons.Color(ItemKind.HighSpeed));
        }
        public void CountMissileStrike(bool trackedHit = false) { MissileStrikes++; if (trackedHit) MissileTrackedStrikes++; }
        internal void BombDetonated(GigantBomb finished)
        {
            if (bomb != finished || Weapon != ItemKind.GigantBomb) return;
            bomb = null; Weapon = ItemKind.None; weaponTime = 0;
        }
        private void EndWeapon(bool detonate)
        {
            if (Weapon == ItemKind.MegaBeam) game.Tank.SetBeamElevation(false);
            Weapon = ItemKind.None; weaponTime = 0;
            if (beamRoot != null) Destroy(beamRoot); beamRoot = null; beam = beamRings = null;
            if (bomb != null) { if (detonate) bomb.Detonate(); else Destroy(bomb.gameObject); }
            bomb = null;
        }
        private void EndMissiles()
        {
            missileTime = 0;
            foreach (var missile in missiles) if (missile != null) Destroy(missile.gameObject);
            missiles.Clear();
        }
        private void ResetBulldozer()
        {
            bulldozer = 0; bladeRoot.SetActive(false); hull.sharedMaterials = hullPaint; game.Tank.SetVehicleScale(1, false);
            SetVehicleFlash(0);
        }
        public void OnTankCrash()
        {
            if (StopsForward) EndWeapon(false);
            ClearPersonalEffects();
        }
        private void ClearPersonalEffects()
        {
            magnet = highSpeed = 0; magnetRing.SetActive(false); shield.SetActive(false);
            if (bulldozer > 0) ResetBulldozer();
            foreach (var trail in dashTrails) { trail.enabled = true; trail.emitting = false; trail.Clear(); }
            SetVehicleFlash(0);
        }
        public void FinishRound() { EndWeapon(false); EndMissiles(); ClearPersonalEffects(); Held = ItemKind.None; }
        private void OnDestroy() { if (energy != null) Destroy(energy); }
    }
}
