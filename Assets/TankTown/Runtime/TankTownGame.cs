using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TankTown
{
    public enum RoundState { Ready, Playing, Paused, Results }

    public sealed class TankTownGame : MonoBehaviour
    {
        public TankTownSettings Settings { get; private set; }
        public TankInput Input { get; private set; }
        public TankMotor Tank { get; private set; }
        public TankTownWorld World { get; private set; }
        public TankCamera CameraRig { get; private set; }
        public RoundState State { get; private set; }
        public bool IsPlaying => State == RoundState.Playing;
        public float Remaining { get; private set; }
        public int TotalBlocks { get; private set; }
        public int BrokenBlocks { get; private set; }
        public int Crashes { get; private set; }
        public float Destruction => TotalBlocks == 0 ? 0 : 100f * BrokenBlocks / TotalBlocks;
        private readonly Queue<GameObject> debris = new Queue<GameObject>();
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private AudioSource speaker;
        private bool ownedSettings, oldIgnore;

        private void Awake()
        {
            Time.timeScale = 1;
            Settings = Resources.Load<TankTownSettings>("TankTownSettings");
            if (Settings == null) { Settings = ScriptableObject.CreateInstance<TankTownSettings>(); ownedSettings = true; }
            Remaining = Settings.roundSeconds;
            oldIgnore = Physics.GetIgnoreLayerCollision(2, 2);
            Physics.IgnoreLayerCollision(2, 2, true);
            Input = gameObject.AddComponent<TankInput>();
            Input.settings = Settings;
            var worldRoot = new GameObject("Procedural Town");
            World = worldRoot.AddComponent<TankTownWorld>();
            World.Build(this);
            Tank = World.CreateTank();
            Input.PadPressed += Tank.Fire;
            Input.StartPressed += StartOrRestart;
            Input.PausePressed += TogglePause;
            SetupView();
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.spatialBlend = 0;
            gameObject.AddComponent<TankTownHUD>().Initialize(this);
        }

        private void SetupView()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var item = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                item.tag = "MainCamera";
                camera = item.GetComponent<Camera>();
            }
            camera.fieldOfView = 58;
            camera.farClipPlane = 220;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.64f, .84f, .89f);
            CameraRig = camera.gameObject.AddComponent<TankCamera>();
            CameraRig.Target = Tank.transform;
            CameraRig.Snap();
            RenderSettings.ambientLight = new Color(.75f, .82f, .9f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 85;
            RenderSettings.fogEndDistance = 185;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
        }

        public void StartOrRestart()
        {
            if (State == RoundState.Ready) State = RoundState.Playing;
            else if (State == RoundState.Paused) TogglePause();
            else if (State == RoundState.Results)
            {
                Time.timeScale = 1;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        public void TogglePause()
        {
            if (State == RoundState.Playing) { State = RoundState.Paused; Time.timeScale = 0; }
            else if (State == RoundState.Paused) { State = RoundState.Playing; Time.timeScale = 1; }
        }

        private void OnApplicationFocus(bool focus) { if (!focus && IsPlaying) TogglePause(); }
        private void Update() { if (IsPlaying) AdvanceTime(Time.deltaTime); }
        public void AdvanceTime(float seconds)
        {
            if (!IsPlaying) return;
            Remaining = Mathf.Max(0, Remaining - Mathf.Max(0, seconds));
            if (Remaining <= 0) { State = RoundState.Results; Time.timeScale = 0; }
        }
        public void RegisterBlock() => TotalBlocks++;
        public void ScoreBlock() { if (IsPlaying) BrokenBlocks = Mathf.Min(TotalBlocks, BrokenBlocks + 1); }
        public void ApplyPenalty() { if (IsPlaying) { Crashes++; AdvanceTime(Settings.respawnPenalty); } }
        public void TrackDebris(GameObject item)
        {
            debris.Enqueue(item);
            while (debris.Count > Mathf.Max(1, Settings.maxDebris))
            {
                var old = debris.Dequeue();
                if (old != null) Destroy(old);
            }
        }

        public void Blast(Vector3 point)
        {
            if (!IsPlaying) return;
            var buildings = new HashSet<BlockBuilding>();
            foreach (var collider in Physics.OverlapSphere(point, Settings.blastRadius, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                var block = collider.GetComponent<DestructibleBlock>();
                if (block == null || block.Detached) continue;
                block.Break(point, Settings.blastImpulse);
                if (block.Building != null) buildings.Add(block.Building);
            }
            foreach (var building in buildings) building.ReleaseUnsupported(point, Settings.blastImpulse);
            Burst(point, new Color(1, .57f, .16f), 2);
            PlayTone(85, .16f, .2f);
        }

        public void Burst(Vector3 position, Color color, float size)
        {
            var effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effect.name = "Impact flash";
            Destroy(effect.GetComponent<Collider>());
            effect.transform.position = position;
            var renderer = effect.GetComponent<Renderer>();
            renderer.sharedMaterial = World.ShotMaterial;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
            effect.AddComponent<ImpactFlash>().Size = size;
        }

        public void PlayTone(float frequency, float duration, float volume)
        {
            // Three cached synthesized arcade sounds; no external asset dependency.
            int index = frequency > 100 ? 0 : frequency > 70 ? 1 : 2;
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
            Physics.IgnoreLayerCollision(2, 2, oldIgnore);
            foreach (var clip in clips) if (clip != null) Destroy(clip);
            if (ownedSettings) Destroy(Settings);
        }
    }

    public sealed class ImpactFlash : MonoBehaviour
    {
        public float Size = 1;
        private float age;
        private void Update()
        {
            age += Time.deltaTime;
            transform.localScale = Vector3.one * Size * Mathf.Sin(Mathf.Clamp01(age / .24f) * Mathf.PI);
            if (age > .24f) Destroy(gameObject);
        }
    }
}
