using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TankTown
{
    public enum RoundState { Ready, Playing, Paused, Results, Starting, Restarting }

    public sealed class TankTownGame : MonoBehaviour
    {
        public TankTownSettings Settings { get; private set; }
        public int CitySeed { get; private set; }
        internal static TownStage SelectedStage = TownStage.CandyTown;
        public TownStage Stage { get; private set; }
        internal TownStage BuiltStage { get; private set; }
        internal TownTitlePreview TitlePreview { get; private set; }
        public int StageScale => Stage == TownStage.Town ? 1 : 2;
        public string StageLabel => Stage == TownStage.AmusementPark ? "遊園地" : Stage == TownStage.CandyTown ? "お菓子の街" : Stage == TownStage.BigCity ? "大きな街" : "いつもの街";
        internal string StageStorageSuffix => Stage == TownStage.AmusementPark ? ".AmusementPark" : Stage == TownStage.CandyTown ? ".CandyTown" : Stage == TownStage.BigCity ? ".BigCity" : "";
        internal float PlayCameraFarClip => Mathf.Max(500, World.HalfExtent * 3.2f);
        internal static int? GenerationSeedOverride;
        internal static bool SkipIntroForTests;
        internal static bool DisableRecordSavingForTests;
        internal static string RankingStorageOverrideForTests;
        internal TankTownRoundFlow Presentation { get; private set; }
        public bool HowToOpen { get; private set; }
        public TankInput Input { get; private set; }
        public TankStats Stats { get; private set; }
        internal TankPickupDisplay PickupDisplay { get; private set; }
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
        internal float ExplosionPowerMultiplier { get; private set; } = 1;
        public Vector3 LastBeamImpact { get; private set; }
        public event Action<ItemKind> SpecialCollected;
        public event Action<int> ChainBurst;
        public float Destruction => TotalBlocks == 0 ? 0 : 100f * BrokenBlocks / TotalBlocks;
        // Generated towns have different part counts; equal destruction earns equal points.
        public int RoundScore => Mathf.RoundToInt(Mathf.Clamp(Destruction, 0, 100) * 1000);
        public int BestScore { get; private set; }
        public bool NewBest { get; private set; }
        public TankTownRanking Ranking { get; private set; }
        public int CurrentRank => Ranking.PredictRank(RoundScore);
        public int FinalRank { get; private set; }
        public TownScoreRecord RoundRecord { get; private set; }
        private const string BestScoreKey = "TankTown.BestScore.v1";
        private readonly List<FlyingDebris> debris = new List<FlyingDebris>();
        private readonly Stack<TownDamageQuery> damageQueries = new Stack<TownDamageQuery>();
        private TownDamageQuery RentDamageQuery() => damageQueries.Count > 0 ? damageQueries.Pop() : new TownDamageQuery();
        private void ReturnDamageQuery(TownDamageQuery query) { query.Clear(); damageQueries.Push(query); }
        private System.Random debrisRandom;
        internal int ActiveDebrisCount => debris.Count;
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private AudioSource speaker;
        private bool ownedSettings, oldDebrisIgnore, oldTankIgnore, oldRewardIgnore, oldPedestrianDebrisIgnore, oldPedestrianIgnore;
        private UniversalRenderPipelineAsset toyPipeline;
        private RenderPipelineAsset previousPipeline;
        private VolumeProfile lightingProfile;
        private static bool playAfterReload;
        private static int? seedAfterStageSelection;
        private float debrisBurstWindow;
        private int debrisBurstsInWindow;
        private float previousFixedStep, previousMaximumStep;
        private double clockAt;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartFullscreen()
        {
            if (Application.isBatchMode) return;
            // Override an older windowed player preference once, at startup.
            var desktop = Screen.currentResolution;
            Screen.SetResolution(desktop.width, desktop.height, FullScreenMode.FullScreenWindow);
        }
#endif

        private void Awake()
        {
            Time.timeScale = 1;
            previousFixedStep = Time.fixedDeltaTime; previousMaximumStep = Time.maximumDeltaTime;
            var sourceSettings = Resources.Load<TankTownSettings>("TankTownSettings");
            Settings = sourceSettings != null ? Instantiate(sourceSettings) : ScriptableObject.CreateInstance<TankTownSettings>();
            ownedSettings = true;
            Stage = SelectedStage == TownStage.BigCity || SelectedStage == TownStage.CandyTown || SelectedStage == TownStage.AmusementPark ? SelectedStage : TownStage.Town;
            BuiltStage = Stage;
            if (Stage != TownStage.Town)
            {
                Time.fixedDeltaTime = Mathf.Max(previousFixedStep, 1f / 30);
                Time.maximumDeltaTime = Mathf.Min(previousMaximumStep, .1f);
                Settings.cityHalfBlocks *= 2;
                Settings.cityParks *= 4;
                Settings.megaBeamLength *= 2;
            }
            CitySeed = ResolveCitySeed();
            Debug.Log($"TOWN GENERATED: stage={Stage}, seed={CitySeed}, seconds={Settings.roundSeconds}");
            // 消去の乱数を物理演出のUnityEngine.Randomから独立させる。
            debrisRandom = new System.Random(unchecked(CitySeed ^ 0x5D3B17));
            Remaining = Settings.roundSeconds;
            LoadStageRanking();
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
            oldPedestrianDebrisIgnore = Physics.GetIgnoreLayerCollision(TownPhysics.PedestrianLayer, TownPhysics.DebrisLayer);
            oldPedestrianIgnore = Physics.GetIgnoreLayerCollision(TownPhysics.PedestrianLayer, TownPhysics.PedestrianLayer);
            Physics.IgnoreLayerCollision(TownPhysics.PedestrianLayer, TownPhysics.DebrisLayer, true);
            Physics.IgnoreLayerCollision(TownPhysics.PedestrianLayer, TownPhysics.PedestrianLayer, true);
            Input = gameObject.AddComponent<TankInput>();
            Input.settings = Settings;
            var worldRoot = new GameObject("Procedural Town");
            World = worldRoot.AddComponent<TankTownWorld>();
            World.Build(this);
            Tank = World.CreateTank();
            Items = Tank.gameObject.AddComponent<TankItems>();
            Items.Initialize(this);
            PickupDisplay = Tank.gameObject.AddComponent<TankPickupDisplay>();
            PickupDisplay.Initialize(this);
            Input.PadPressed += HandlePad;
            Input.StartPressed += StartOrRestart;
            Input.PausePressed += TogglePause;
            Input.HelpPressed += ToggleHowTo;
            Input.StagePressed += SelectStage;
            SetupView();
            if (!playAfterReload) TitlePreview = new TownTitlePreview(this);
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.spatialBlend = 0;
            speaker.playOnAwake = false;
            Presentation = new TankTownRoundFlow(this, CameraRig.GetComponent<Camera>());
            gameObject.AddComponent<TankTownHUD>().Initialize(this);
            CameraRig.Paused = true;
            Time.timeScale = 0;
            if (playAfterReload)
            {
                playAfterReload = false;
                State = RoundState.Starting;
                Presentation.BeginStart(true);
                if (SkipIntroForTests) FinishIntro();
            }
        }

        private int ResolveCitySeed()
        {
            if (seedAfterStageSelection.HasValue) { int seed = seedAfterStageSelection.Value; seedAfterStageSelection = null; return seed; }
            if (GenerationSeedOverride.HasValue) return GenerationSeedOverride.Value;
            foreach (var argument in Environment.GetCommandLineArgs())
                if (argument.StartsWith("--tank-town-seed=", StringComparison.Ordinal) && int.TryParse(argument.Substring(17), out int seed)) return seed;
            return Settings.randomizeCitySeed ? BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0) : Settings.citySeed;
        }

        private void HandlePad(int pad)
        {
            if (State == RoundState.Ready || State == RoundState.Results || State == RoundState.Paused) StartOrRestart();
            else if (IsPlaying) Tank.Fire(pad);
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
            camera.farClipPlane = PlayCameraFarClip;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.cullingMask &= ~((1 << TownPhysics.MapLayer) | (1 << TownTitlePreview.Layer));
            camera.backgroundColor = new Color(.5f, .86f, 1);
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthTexture = true;
            var lighting = new GameObject("Toy light bloom", typeof(Volume)); lighting.transform.SetParent(transform, false);
            lightingProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = lightingProfile.Add<Bloom>(true);
            bloom.threshold.Override(1.2f); bloom.intensity.Override(.28f); bloom.scatter.Override(.55f); bloom.clamp.Override(6);
            lightingProfile.Add<Tonemapping>(true).mode.Override(TonemappingMode.None);
            lightingProfile.Add<ColorAdjustments>(true);
            lightingProfile.Add<WhiteBalance>(true);
            var volume = lighting.GetComponent<Volume>(); volume.isGlobal = true; volume.priority = 20; volume.sharedProfile = lightingProfile;
            CameraRig = camera.gameObject.AddComponent<TankCamera>();
            CameraRig.Offset = Settings.cameraOffset;
            CameraRig.Items = Items;
            CameraRig.BeamOffset = Settings.megaBeamCameraOffset;
            CameraRig.BeamLookAhead = Settings.megaBeamCameraLookAhead;
            CameraRig.BeamShakeAmplitude = Settings.megaBeamCameraShake;
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
            sun.shadowBias = .025f;
            sun.shadowNormalBias = .09f;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
        }

        public void StartOrRestart()
        {
            if (State == RoundState.Ready)
            {
                HowToOpen = false;
                State = RoundState.Starting;
                Presentation.BeginStart(false);
                if (SkipIntroForTests && PrepareSelectedStage()) FinishIntro();
            }
            else if (State == RoundState.Paused) TogglePause();
            else if (State == RoundState.Results)
            {
                State = RoundState.Restarting;
                if (SkipIntroForTests) ReloadForReplay();
                else Presentation.BeginReplay();
            }
        }

        public void SelectStage(TownStage stage)
        {
            if (State != RoundState.Ready || stage == Stage || (stage != TownStage.Town && stage != TownStage.BigCity && stage != TownStage.CandyTown && stage != TownStage.AmusementPark)) return;
            SelectedStage = stage;
            Stage = stage;
            LoadStageRanking();
        }

        private void LoadStageRanking()
        {
            BestScore = DisableRecordSavingForTests ? 0 : Mathf.Clamp(PlayerPrefs.GetInt(BestScoreKey + StageStorageSuffix, 0), 0, 100000);
            Ranking = new TankTownRanking(!DisableRecordSavingForTests || !string.IsNullOrEmpty(RankingStorageOverrideForTests), BestScore,
                (RankingStorageOverrideForTests ?? TankTownRanking.StorageKey) + StageStorageSuffix);
            BestScore = Mathf.Max(BestScore, Ranking.BestScore);
        }

        internal bool PrepareSelectedStage()
        {
            if (Stage != BuiltStage)
            {
                // Generate the chosen play field only after Play, under the transition cover.
                seedAfterStageSelection = CitySeed;
                ReloadForReplay(); return false;
            }
            if (TitlePreview != null) TitlePreview.Root.SetActive(false);
            return true;
        }

        internal void FinishIntro()
        {
            Presentation.UsePlayCamera();
            State = RoundState.Playing;
            clockAt = Time.realtimeSinceStartupAsDouble;
            CameraRig.Paused = false;
            Time.timeScale = 1;
        }

        internal void ReloadForReplay()
        {
            playAfterReload = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        internal void SetOverviewShadows(bool overview)
        {
            float extent = TitlePreview?.HalfExtent ?? World.HalfExtent;
            if (toyPipeline != null) toyPipeline.shadowDistance = overview ? Mathf.Max(430, extent * 2.4f) : 95;
            RenderSettings.fogStartDistance = overview ? Mathf.Max(430, extent * 3.2f) : 160;
            RenderSettings.fogEndDistance = overview ? Mathf.Max(700, extent * 5.2f) : 410;
        }

        public void ToggleHowTo() { if (State == RoundState.Ready) HowToOpen = !HowToOpen; }

        public void ReturnToTitle()
        {
            if (State != RoundState.Paused && State != RoundState.Results) return;
            playAfterReload = false;
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void TogglePause()
        {
            if (State == RoundState.Ready && HowToOpen) { HowToOpen = false; return; }
            if (State == RoundState.Playing) { State = RoundState.Paused; CameraRig.Paused = true; Sound.SetPaused(true); speaker.Pause(); Time.timeScale = 0; }
            else if (State == RoundState.Paused) { State = RoundState.Playing; clockAt = Time.realtimeSinceStartupAsDouble; CameraRig.Paused = false; Sound.SetPaused(false); speaker.UnPause(); Time.timeScale = 1; }
        }

        private void OnApplicationFocus(bool focus) { if (!focus && IsPlaying) TogglePause(); }
        private void Update()
        {
            Presentation.Tick(Time.unscaledDeltaTime);
            if (IsPlaying)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                AdvanceTime((float)(now - clockAt)); clockAt = now;
            }
        }
        private void LateUpdate()
        {
            Presentation.MoveOverview(Time.unscaledDeltaTime);
            World.UpdatePresentation(CameraRig.transform.position);
            if (IsPlaying) foreach (var fragment in debris) if (fragment.isActiveAndEnabled) fragment.Fade?.Tick();
        }
        private void FixedUpdate()
        {
            if (IsPlaying) foreach (var fragment in debris) if (fragment.isActiveAndEnabled) fragment.TickPhysics();
        }
        public void AdvanceTime(float seconds)
        {
            if (!IsPlaying) return;
            Remaining = Mathf.Max(0, Remaining - Mathf.Max(0, seconds));
            if (Remaining <= 0)
            {
                State = RoundState.Results; CameraRig.CancelTransition(); CameraRig.Paused = true;
                PickupDisplay.Clear(); Items.FinishRound(); Sound.StopAll(); speaker.Stop(); Time.timeScale = 0;
                NewBest = RoundScore > BestScore;
                BestScore = Mathf.Max(BestScore, RoundScore);
                RoundRecord = Ranking.Submit(RoundScore, CitySeed, out int finalRank);
                FinalRank = finalRank;
                if (NewBest && !DisableRecordSavingForTests) { PlayerPrefs.SetInt(BestScoreKey + StageStorageSuffix, BestScore); PlayerPrefs.Save(); }
            }
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
                Effects.DebrisImpact(point, Settings.debrisBlastRadius * Settings.chainExplosionVisualScale);
                Sound.Debris(point);
                return true;
            }
            finally { ExplosionGeneration = previous; }
        }

        internal void ChainBlast(Vector3 point, float radius, int generation, bool fuelTank = false, bool vehicle = false, float powerMultiplier = 1)
        {
            if (!IsPlaying) return;
            ChainExplosions++;
            LongestChain = Mathf.Max(LongestChain, generation);
            ChainBurst?.Invoke(generation);
            int previous = ExplosionGeneration;
            float previousPower = ExplosionPowerMultiplier;
            ExplosionGeneration = generation;
            ExplosionPowerMultiplier = Mathf.Max(previousPower, powerMultiplier);
            try
            {
                float impulse = Settings.blastImpulse * (fuelTank ? Settings.fuelImpulseMultiplier : vehicle ? Settings.carExplosionImpulseMultiplier : 1.35f) * ExplosionPowerMultiplier;
                float effectiveRadius = radius * Mathf.Sqrt(ExplosionPowerMultiplier);
                DamageSphere(point, effectiveRadius, impulse);
                float visualRadius = effectiveRadius * Settings.chainExplosionVisualScale;
                if (fuelTank) Effects.FuelImpact(point, visualRadius);
                else if (vehicle) Effects.CarImpact(point, visualRadius);
                else Effects.Impact(point, visualRadius);
                if (fuelTank) Sound.BigExplosion(point);
                else Sound.Chain(point);
            }
            finally { ExplosionGeneration = previous; ExplosionPowerMultiplier = previousPower; }
        }

        internal void MissileBlast(Vector3 point)
        {
            float previousPower = ExplosionPowerMultiplier;
            ExplosionPowerMultiplier = Mathf.Max(previousPower, Settings.missileChainPowerMultiplier);
            try { Blast(point, Stats.BlastRadius * 1.15f, Stats.BlastImpulse * 1.35f); }
            finally { ExplosionPowerMultiplier = previousPower; }
        }

        public void Blast(Vector3 point, float radius, float impulse, DestructibleBlock directHit = null)
        {
            if (!IsPlaying) return;
            var query = RentDamageQuery();
            try { query.Sphere(point, radius); Damage(query, point, impulse, radius * 2, true, directHit, distanceFalloff: true); }
            finally { ReturnDamageQuery(query); }
            Effects.Impact(point, radius);
            Sound.Impact(point);
        }

        public int DamageSphere(Vector3 point, float radius, float impulse, bool isExplosion = true)
        {
            if (!IsPlaying) return 0;
            var query = RentDamageQuery();
            try { query.Sphere(point, radius); return Damage(query, point, impulse, radius * 2, isExplosion); }
            finally { ReturnDamageQuery(query); }
        }

        public int DamageBox(Vector3 point, Vector3 halfSize, Quaternion rotation, float impulse)
        {
            if (!IsPlaying) return 0;
            var query = RentDamageQuery();
            try { query.Box(point, halfSize, rotation); return Damage(query, point, impulse, halfSize.magnitude * 2, false); }
            finally { ReturnDamageQuery(query); }
        }

        public int DamageBeam(Vector3 start, Vector3 end, float radius, float impulse)
        {
            if (!IsPlaying) return 0;
            var query = RentDamageQuery();
            try
            {
            query.Capsule(start, end, radius);
            var buildings = query.Buildings;
            int count = 0;
            var segment = end - start;
            float nearest = float.PositiveInfinity; LastBeamImpact = end;
            for (int i = 0; i < query.Count; i++)
            {
                var collider = query.Hits[i];
                var block = collider.GetComponentInParent<DestructibleBlock>();
                if (block == null || block.Detached) continue;
                var origin = start + segment * Mathf.Clamp01(Vector3.Dot(block.transform.position - start, segment) / segment.sqrMagnitude);
                float distance = (origin - start).sqrMagnitude;
                if (distance < nearest) { nearest = distance; LastBeamImpact = origin; }
                block.Break(origin, impulse, radius * 3); count++;
                if (block.Building != null) buildings.Add(block.Building);
            }
            foreach (var building in buildings) building.ReleaseUnsupported(start, impulse);
            foreach (var person in World.Pedestrians)
            {
                if (person == null || !person.Walking) continue;
                var point = person.transform.position + Vector3.up;
                var origin = start + segment * Mathf.Clamp01(Vector3.Dot(point - start, segment) / Mathf.Max(.01f, segment.sqrMagnitude));
                if ((person.ClosestPoint(origin) - origin).sqrMagnitude <= radius * radius) person.KnockAside(origin, impulse);
            }
            return count;
            }
            finally { ReturnDamageQuery(query); }
        }

        private int Damage(TownDamageQuery query, Vector3 point, float impulse, float forceRadius, bool isExplosion, DestructibleBlock directHit = null, bool distanceFalloff = false)
        {
            if (!IsPlaying) return 0;
            var buildings = query.Buildings;
            var distances = query.Distances;
            float blastDamage = Mathf.Max(0, impulse) / Mathf.Max(.1f, Settings.blastImpulse);
            float breakRadius = Mathf.Min(forceRadius * .5f, Settings.blastBreakRadius * forceRadius * .5f / Mathf.Max(.1f, Settings.blastRadius));
            // 複合Colliderは最も近い表面までの距離で一度だけ判定する。分離前に距離を確定する。
            for (int i = 0; i < query.Count; i++)
            {
                var collider = query.Hits[i];
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
                bool closeBlast = distanceFalloff && blastDamage > 0 && breakRadius > 0 && target.Value <= breakRadius;
                if (isExplosion && block.Building != null && block != directHit && !closeBlast)
                {
                    // 燃料の誘爆・ギガント爆弾の範囲威力は維持し、通常の着弾だけを局所化する。
                    float falloff = distanceFalloff ? 1 - Mathf.Clamp01(target.Value / Mathf.Max(.01f, forceRadius * .5f)) : 1;
                    float damage = blastDamage * falloff * (distanceFalloff ? Settings.blastDamageMultiplier : 1);
                    if (block.DamageFromBlast(point, damage, impulse * falloff, forceRadius)) count++;
                }
                else { block.Break(point, impulse, forceRadius); count++; }
                if (block.Building != null) buildings.Add(block.Building);
            }
            // 直撃と周囲の損傷をまとめてから支持を調べ、失われた部分だけを倒壊させる。
            foreach (var building in buildings) building.ReleaseUnsupported(point, impulse);
            float pedestrianRadius = forceRadius * .5f;
            foreach (var person in World.Pedestrians)
                if (person != null && person.Walking && (person.ClosestPoint(point) - point).sqrMagnitude <= pedestrianRadius * pedestrianRadius)
                    person.KnockAside(point, impulse);
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
            Time.fixedDeltaTime = previousFixedStep; Time.maximumDeltaTime = previousMaximumStep;
            Physics.IgnoreLayerCollision(TownPhysics.DebrisLayer, TownPhysics.DebrisLayer, oldDebrisIgnore);
            Physics.IgnoreLayerCollision(TownPhysics.TankLayer, TownPhysics.DebrisLayer, oldTankIgnore);
            Physics.IgnoreLayerCollision(TownPhysics.RewardLayer, TownPhysics.DebrisLayer, oldRewardIgnore);
            Physics.IgnoreLayerCollision(TownPhysics.PedestrianLayer, TownPhysics.DebrisLayer, oldPedestrianDebrisIgnore);
            Physics.IgnoreLayerCollision(TownPhysics.PedestrianLayer, TownPhysics.PedestrianLayer, oldPedestrianIgnore);
            foreach (var clip in clips) if (clip != null) Destroy(clip);
            TitlePreview?.Dispose();
            if (ownedSettings) Destroy(Settings);
            if (lightingProfile != null)
            {
                foreach (var component in lightingProfile.components) if (component != null) Destroy(component);
                Destroy(lightingProfile);
            }
            if (toyPipeline != null)
            {
                if (QualitySettings.renderPipeline == toyPipeline) QualitySettings.renderPipeline = previousPipeline;
                Destroy(toyPipeline);
            }
        }
    }

}
