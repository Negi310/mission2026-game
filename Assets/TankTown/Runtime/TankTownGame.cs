using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TankTown
{
    public enum RoundState { Ready, Playing, Paused, Results }

    public sealed class TankTownGame : MonoBehaviour
    {
        public TankTownSettings Settings { get; private set; }
        public TankInput Input { get; private set; }
        public TankStats Stats { get; private set; }
        public TankTownEffects Effects { get; private set; }
        public TankTownAudio Sound { get; private set; }
        public TankMotor Tank { get; private set; }
        public TankItems Items { get; private set; }
        public TankTownWorld World { get; private set; }
        public TankCamera CameraRig { get; private set; }
        public RoundState State { get; private set; }
        public bool IsPlaying => State == RoundState.Playing;
        public float Remaining { get; private set; }
        public int TotalBlocks { get; private set; }
        public int BrokenBlocks { get; private set; }
        public int Crashes { get; private set; }
        public int ChainExplosions { get; private set; }
        public int DebrisExplosions { get; private set; }
        public int LongestChain { get; private set; }
        public int ExplosionGeneration { get; private set; }
        public event Action<ItemKind> SpecialCollected;
        public event Action<int> ChainBurst;
        public float Destruction => TotalBlocks == 0 ? 0 : 100f * BrokenBlocks / TotalBlocks;
        private readonly List<FlyingDebris> debris = new List<FlyingDebris>();
        private System.Random debrisRandom;
        internal int ActiveDebrisCount => debris.Count;
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private AudioSource speaker;
        private bool ownedSettings, oldDebrisIgnore, oldTankIgnore, oldRewardIgnore;
        private UniversalRenderPipelineAsset toyPipeline;
        private RenderPipelineAsset previousPipeline;
        private static bool playAfterReload;
        private float debrisBurstWindow;
        private int debrisBurstsInWindow;

        private void Awake()
        {
            Time.timeScale = 1;
            Settings = Resources.Load<TankTownSettings>("TankTownSettings");
            if (Settings == null) { Settings = ScriptableObject.CreateInstance<TankTownSettings>(); ownedSettings = true; }
            // 消去の乱数を物理演出のUnityEngine.Randomから独立させる。
            debrisRandom = new System.Random(unchecked(Settings.citySeed ^ 0x5D3B17));
            Remaining = Settings.roundSeconds;
            Stats = new TankStats(Settings);
            Effects = gameObject.AddComponent<TankTownEffects>();
            var soundRoot = new GameObject("Sound effects");
            soundRoot.transform.SetParent(transform, false);
            Sound = soundRoot.AddComponent<TankTownAudio>();
            Sound.Initialize(this);
            oldDebrisIgnore = Physics.GetIgnoreLayerCollision(TownPhysics.DebrisLayer, TownPhysics.DebrisLayer);
            oldTankIgnore = Physics.GetIgnoreLayerCollision(TownPhysics.TankLayer, TownPhysics.DebrisLayer);
            oldRewardIgnore = Physics.GetIgnoreLayerCollision(TownPhysics.RewardLayer, TownPhysics.DebrisLayer);
            Physics.IgnoreLayerCollision(TownPhysics.DebrisLayer, TownPhysics.DebrisLayer, true);
            Physics.IgnoreLayerCollision(TownPhysics.TankLayer, TownPhysics.DebrisLayer, false);
            Physics.IgnoreLayerCollision(TownPhysics.RewardLayer, TownPhysics.DebrisLayer, true);
            Input = gameObject.AddComponent<TankInput>();
            Input.settings = Settings;
            var worldRoot = new GameObject("Procedural Town");
            World = worldRoot.AddComponent<TankTownWorld>();
            World.Build(this);
            Tank = World.CreateTank();
            Items = Tank.gameObject.AddComponent<TankItems>();
            Items.Initialize(this);
            Input.PadPressed += HandlePad;
            Input.StartPressed += StartOrRestart;
            Input.PausePressed += TogglePause;
            SetupView();
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.spatialBlend = 0;
            speaker.playOnAwake = false;
            gameObject.AddComponent<TankTownHUD>().Initialize(this);
            CameraRig.Paused = true;
            Time.timeScale = 0;
            if (playAfterReload) { playAfterReload = false; StartOrRestart(); }
        }

        private void HandlePad(int pad)
        {
            if (State == RoundState.Ready) StartOrRestart();
            else Tank.Fire(pad);
        }

        private void SetupView()
        {
            previousPipeline = QualitySettings.renderPipeline;
            var source = previousPipeline != null ? previousPipeline : GraphicsSettings.defaultRenderPipeline;
            if (source is UniversalRenderPipelineAsset universal)
            {
                // A game-owned copy keeps other scenes and the user's URP assets intact.
                toyPipeline = Instantiate(universal);
                toyPipeline.name = "Tank Town toy lighting";
                toyPipeline.shadowDistance = 95;
                toyPipeline.msaaSampleCount = 4;
                QualitySettings.renderPipeline = toyPipeline;
            }
            var camera = Camera.main;
            if (camera == null)
            {
                var item = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                item.tag = "MainCamera";
                camera = item.GetComponent<Camera>();
            }
            camera.fieldOfView = Settings.cameraFieldOfView;
            camera.nearClipPlane = .12f;
            camera.farClipPlane = 500;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(.5f, .86f, 1);
            CameraRig = camera.gameObject.AddComponent<TankCamera>();
            CameraRig.Offset = Settings.cameraOffset;
            CameraRig.Terrain = World.Terrain;
            CameraRig.TurnSmoothTime = Settings.cameraTurnSmoothTime;
            CameraRig.MaxTurnSpeed = Settings.cameraMaxTurnSpeed;
            CameraRig.MaxTurnAcceleration = Settings.cameraMaxTurnAcceleration;
            CameraRig.Target = Tank.transform;
            CameraRig.AimTarget = Tank.Turret;
            CameraRig.Snap();
            RenderSettings.ambientLight = new Color(.75f, .82f, .9f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 160;
            RenderSettings.fogEndDistance = 410;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.75f;
            sun.shadows = LightShadows.Soft;
            sun.color = new Color(1, .96f, .87f);
            sun.shadowStrength = 1;
            sun.shadowBias = .04f;
            sun.shadowNormalBias = .18f;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
        }

        public void StartOrRestart()
        {
            if (State == RoundState.Ready) { State = RoundState.Playing; CameraRig.Paused = false; Time.timeScale = 1; }
            else if (State == RoundState.Paused) TogglePause();
            else if (State == RoundState.Results)
            {
                playAfterReload = true;
                Time.timeScale = 1;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        public void ReturnToTitle()
        {
            if (State != RoundState.Paused && State != RoundState.Results) return;
            playAfterReload = false;
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void TogglePause()
        {
            if (State == RoundState.Playing) { State = RoundState.Paused; CameraRig.Paused = true; Sound.SetPaused(true); speaker.Pause(); Time.timeScale = 0; }
            else if (State == RoundState.Paused) { State = RoundState.Playing; CameraRig.Paused = false; Sound.SetPaused(false); speaker.UnPause(); Time.timeScale = 1; }
        }

        private void OnApplicationFocus(bool focus) { if (!focus && IsPlaying) TogglePause(); }
        private void Update() { if (IsPlaying) AdvanceTime(Time.deltaTime); }
        public void AdvanceTime(float seconds)
        {
            if (!IsPlaying) return;
            Remaining = Mathf.Max(0, Remaining - Mathf.Max(0, seconds));
            if (Remaining <= 0) { State = RoundState.Results; CameraRig.CancelTransition(); CameraRig.Paused = true; Items.FinishRound(); Sound.StopAll(); speaker.Stop(); Time.timeScale = 0; }
        }
        public void RegisterBlock() => TotalBlocks++;
        internal void ReportSpecial(ItemKind kind) => SpecialCollected?.Invoke(kind);
        public void ScoreBlock() { if (IsPlaying) BrokenBlocks = Mathf.Min(TotalBlocks, BrokenBlocks + 1); }
        public void ApplyPenalty() { if (IsPlaying) { Crashes++; AdvanceTime(Settings.respawnPenalty); } }
        public void TrackDebris(GameObject item)
        {
            var fragment = item.GetComponent<FlyingDebris>();
            if (fragment == null || fragment.RegistryIndex >= 0) return;
            while (debris.Count >= Mathf.Max(1, Settings.maxDebris))
            {
                // 古い建物一棟から消さず、残っている破片全体から一様に選ぶ。
                var victim = debris[debrisRandom.Next(debris.Count)];
                UntrackDebris(victim);
                victim.gameObject.SetActive(false);
                Destroy(victim.gameObject);
            }
            fragment.RegistryIndex = debris.Count;
            debris.Add(fragment);
        }

        internal void UntrackDebris(FlyingDebris fragment)
        {
            int index = fragment.RegistryIndex;
            if (index < 0 || index >= debris.Count || !ReferenceEquals(debris[index], fragment)) return;
            int last = debris.Count - 1;
            debris[index] = debris[last];
            debris[index].RegistryIndex = index;
            debris.RemoveAt(last);
            fragment.RegistryIndex = -1;
        }

        internal float NextDebrisLifetime() => Settings.debrisLifetime +
            (float)debrisRandom.NextDouble() * Mathf.Max(0, Settings.debrisLifetimeSpread);

        public void Blast(Vector3 point) => Blast(point, Stats.BlastRadius, Stats.BlastImpulse);

        internal bool TryDebrisBlast(Vector3 point, int generation)
        {
            if (!IsPlaying || generation > Settings.debrisChainLimit) return false;
            // 同時多発する接触は短い時間窓で制限し、大量倒壊でも演出を増やし続けない。
            if (Time.time >= debrisBurstWindow) { debrisBurstWindow = Time.time + .12f; debrisBurstsInWindow = 0; }
            if (debrisBurstsInWindow >= 3) return false;
            int previous = ExplosionGeneration;
            ExplosionGeneration = generation;
            try
            {
                int affected = DamageSphere(point, Settings.debrisBlastRadius, Stats.BlastImpulse * Settings.debrisImpulseMultiplier);
                if (affected == 0) return false;
                debrisBurstsInWindow++; DebrisExplosions++;
                LongestChain = Mathf.Max(LongestChain, generation);
                ChainBurst?.Invoke(generation);
                Effects.DebrisImpact(point, Settings.debrisBlastRadius);
                Sound.Debris(point);
                return true;
            }
            finally { ExplosionGeneration = previous; }
        }

        internal void ChainBlast(Vector3 point, float radius, int generation, bool fuelTank = false)
        {
            if (!IsPlaying) return;
            ChainExplosions++;
            LongestChain = Mathf.Max(LongestChain, generation);
            if (generation > 1) ChainBurst?.Invoke(generation);
            int previous = ExplosionGeneration;
            ExplosionGeneration = generation;
            try
            {
                float impulse = Settings.blastImpulse * (fuelTank ? Settings.fuelImpulseMultiplier : 1.35f);
                DamageSphere(point, radius, impulse);
                if (fuelTank) Effects.FuelImpact(point, radius);
                else Effects.Impact(point, radius);
                if (fuelTank) Sound.BigExplosion(point);
                else Sound.Chain(point);
            }
            finally { ExplosionGeneration = previous; }
        }

        public void Blast(Vector3 point, float radius, float impulse, DestructibleBlock directHit = null)
        {
            if (!IsPlaying) return;
            Damage(Physics.OverlapSphere(point, radius, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore),
                point, impulse, radius * 2, true, directHit, distanceFalloff: true);
            Effects.Impact(point, radius);
            Sound.Impact(point);
        }

        public int DamageSphere(Vector3 point, float radius, float impulse, bool isExplosion = true)
            => Damage(Physics.OverlapSphere(point, radius, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore), point, impulse, radius * 2, isExplosion);

        public int DamageBox(Vector3 point, Vector3 halfSize, Quaternion rotation, float impulse)
            => Damage(Physics.OverlapBox(point, halfSize, rotation, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore), point, impulse, halfSize.magnitude * 2, false);

        public int DamageBeam(Vector3 start, Vector3 end, float radius, float impulse)
        {
            if (!IsPlaying) return 0;
            var buildings = new HashSet<BlockBuilding>();
            int count = 0;
            var segment = end - start;
            foreach (var collider in Physics.OverlapCapsule(start, end, radius, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore))
            {
                var block = collider.GetComponentInParent<DestructibleBlock>();
                if (block == null || block.Detached) continue;
                var origin = start + segment * Mathf.Clamp01(Vector3.Dot(block.transform.position - start, segment) / segment.sqrMagnitude);
                block.Break(origin, impulse, radius * 3); count++;
                if (block.Building != null) buildings.Add(block.Building);
            }
            foreach (var building in buildings) building.ReleaseUnsupported(start, impulse);
            return count;
        }

        private int Damage(Collider[] colliders, Vector3 point, float impulse, float forceRadius, bool isExplosion, DestructibleBlock directHit = null, bool distanceFalloff = false)
        {
            if (!IsPlaying) return 0;
            var buildings = new HashSet<BlockBuilding>();
            var distances = new Dictionary<DestructibleBlock, float>();
            float blastDamage = Mathf.Max(0, impulse) / Mathf.Max(.1f, Settings.blastImpulse);
            // 複合Colliderは最も近い表面までの距離で一度だけ判定する。分離前に距離を確定する。
            foreach (var collider in colliders)
            {
                var block = collider.GetComponentInParent<DestructibleBlock>();
                if (block == null || block.Detached) continue;
                float distance = Vector3.Distance(point, collider.ClosestPoint(point));
                if (!distances.TryGetValue(block, out float previous) || distance < previous) distances[block] = distance;
            }
            if (directHit != null && !directHit.Detached) distances[directHit] = 0;
            int count = 0;
            foreach (var target in distances)
            {
                var block = target.Key;
                if (block == null || block.Detached) continue;
                var explosive = block.GetComponent<ChainExplosive>();
                // A large damage radius must not bypass the tank's smaller ignition range.
                if (isExplosion && explosive != null && !explosive.IgniteFromBlast(point)) continue;
                if (isExplosion && block.Building != null && block != directHit)
                {
                    // 燃料の誘爆・ギガント爆弾の範囲威力は維持し、通常の着弾だけを局所化する。
                    float falloff = distanceFalloff ? 1 - Mathf.Clamp01(target.Value / Mathf.Max(.01f, forceRadius * .5f)) : 1;
                    if (block.DamageFromBlast(point, blastDamage * falloff, impulse * falloff, forceRadius)) count++;
                }
                else { block.Break(point, impulse, forceRadius); count++; }
                if (block.Building != null) buildings.Add(block.Building);
            }
            // 直撃と周囲の損傷をまとめてから支持を調べ、失われた部分だけを倒壊させる。
            foreach (var building in buildings) building.ReleaseUnsupported(point, impulse);
            return count;
        }

        public void PlayTone(float frequency, float duration, float volume)
        {
            // Cached synthesized arcade sounds, including a distinct pickup chime.
            int index = frequency >= 300 ? 3 : frequency > 100 ? 0 : frequency > 70 ? 1 : 2;
            while (clips.Count <= index) clips.Add(null);
            if (clips[index] == null)
            {
                int count = Mathf.CeilToInt(22050 * duration);
                var data = new float[count];
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / count;
                    data[i] = Mathf.Sin(2 * Mathf.PI * frequency * (1 - .5f * t) * i / 22050f) * (1 - t) * (1 - t);
                }
                clips[index] = AudioClip.Create("Arcade impact", count, 1, 22050, false);
                clips[index].SetData(data, 0);
            }
            speaker.PlayOneShot(clips[index], volume);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1;
            Physics.IgnoreLayerCollision(TownPhysics.DebrisLayer, TownPhysics.DebrisLayer, oldDebrisIgnore);
            Physics.IgnoreLayerCollision(TownPhysics.TankLayer, TownPhysics.DebrisLayer, oldTankIgnore);
            Physics.IgnoreLayerCollision(TownPhysics.RewardLayer, TownPhysics.DebrisLayer, oldRewardIgnore);
            foreach (var clip in clips) if (clip != null) Destroy(clip);
            if (ownedSettings) Destroy(Settings);
            if (toyPipeline != null)
            {
                if (QualitySettings.renderPipeline == toyPipeline) QualitySettings.renderPipeline = previousPipeline;
                Destroy(toyPipeline);
            }
        }
    }

}
