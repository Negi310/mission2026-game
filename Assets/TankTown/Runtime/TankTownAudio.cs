using System;
using UnityEngine;

namespace TankTown
{
    internal enum TownSound { Cannon, Impact, Chain, BigExplosion, Debris }

    public sealed class TankTownAudio : MonoBehaviour
    {
        // 発射音と大爆発に専用枠を残し、細かい連鎖でかき消さない。
        internal const int VoiceLimit = 12;
        private readonly AudioClip[] clips = new AudioClip[4];
        private readonly AudioSource[] voices = new AudioSource[VoiceLimit];
        private readonly float[] baseVolumes = new float[VoiceLimit];
        private readonly float[] groupVolumes = new float[3];
        private readonly float[] groupGains = new float[3];
        private readonly double[] started = new double[VoiceLimit];
        private readonly double[] nextAllowed = new double[5];
        private readonly int[] played = new int[5];
        private readonly System.Random variation = new System.Random(310);
        private TankTownGame game;
        internal bool Paused { get; private set; }
        internal int PlayedCount(TownSound kind) => played[(int)kind];
        internal AudioClip Clip(int index) => clips[index];

        internal void Initialize(TankTownGame owner)
        {
            game = owner;
            string[] names = { "Cannon", "ExplosionA", "ExplosionB", "BigExplosion" };
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = Resources.Load<AudioClip>("Audio/" + names[i]);
                if (clips[i] == null) Debug.LogError("Tank Town sound is missing: " + names[i]);
            }
            for (int i = 0; i < voices.Length; i++)
            {
                var source = new GameObject("Sound voice " + i).AddComponent<AudioSource>();
                source.transform.SetParent(transform, false);
                source.playOnAwake = false;
                source.loop = false;
                source.dopplerLevel = 0;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 18;
                source.maxDistance = 125;
                voices[i] = source;
            }
        }

        public void Cannon() => Play(TownSound.Cannon, game.Tank.Muzzle.position);
        public void Impact(Vector3 point) => Play(TownSound.Impact, point);
        public void Chain(Vector3 point) => Play(TownSound.Chain, point);
        public void BigExplosion(Vector3 point) => Play(TownSound.BigExplosion, point);
        public void Debris(Vector3 point) => Play(TownSound.Debris, point);

        private void Play(TownSound kind, Vector3 point)
        {
            if (game == null || !game.IsPlaying || Paused) return;
            int key = (int)kind;
            double now = AudioSettings.dspTime;
            if (now < nextAllowed[key]) return;
            int clipIndex = kind == TownSound.Cannon ? 0 : kind == TownSound.Impact ? 1 : kind == TownSound.BigExplosion ? 3 : 2;
            if (clips[clipIndex] == null) return;
            int first = kind == TownSound.Cannon ? 0 : kind == TownSound.BigExplosion ? 2 : 4;
            int end = first < 4 ? first + 2 : voices.Length;
            int selected = first;
            for (int i = first; i < end; i++)
            {
                if (!voices[i].isPlaying) { selected = i; break; }
                if (started[i] < started[selected]) selected = i;
            }
            var voice = voices[selected];
            voice.Stop();
            voice.transform.position = point;
            voice.clip = clips[clipIndex];
            voice.spatialBlend = kind == TownSound.Cannon ? 0 : .8f;
            voice.priority = kind == TownSound.Cannon ? 32 : kind == TownSound.BigExplosion ? 48 : 128;
            voice.pitch = 1 + (float)(variation.NextDouble() - .5) * .06f;
            baseVolumes[selected] = kind == TownSound.Cannon ? .6f : kind == TownSound.BigExplosion ? .6f : kind == TownSound.Debris ? .16f : kind == TownSound.Chain ? .3f : .38f;
            voice.volume = 0;
            voice.Play();
            BalanceVolumes();
            started[selected] = now;
            played[key]++;
            nextAllowed[key] = now + (kind == TownSound.Debris ? .07 : kind == TownSound.Chain ? .035 : 0);
        }

        private void Update() { if (game != null && game.IsPlaying && !Paused) BalanceVolumes(); }

        private void BalanceVolumes()
        {
            Array.Clear(groupVolumes, 0, groupVolumes.Length);
            for (int i = 0; i < voices.Length; i++)
                if (voices[i] != null && voices[i].isPlaying) groupVolumes[Group(i)] += baseVolumes[i];
            // 連鎖音の合計を抑えて発射・大爆発の音量を確保し、全体に余裕を残す。
            float total = 0;
            for (int i = 0; i < groupVolumes.Length; i++)
            {
                float budget = i == 2 ? .5f : .6f;
                groupGains[i] = Mathf.Min(1, budget / Mathf.Max(.0001f, groupVolumes[i]));
                total += groupVolumes[i] * groupGains[i];
            }
            float masterGain = Mathf.Min(1, .9f / Mathf.Max(.0001f, total));
            for (int i = 0; i < voices.Length; i++)
                if (voices[i] != null) voices[i].volume = voices[i].isPlaying ? baseVolumes[i] * groupGains[Group(i)] * masterGain : 0;
        }

        private static int Group(int voice) => voice < 2 ? 0 : voice < 4 ? 1 : 2;

        internal void SetPaused(bool value)
        {
            Paused = value;
            foreach (var voice in voices)
                if (voice != null) { if (value) voice.Pause(); else voice.UnPause(); }
        }

        internal void StopAll()
        {
            foreach (var voice in voices) if (voice != null) voice.Stop();
            Array.Clear(nextAllowed, 0, nextAllowed.Length);
        }
    }
}
