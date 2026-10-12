#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;

namespace TankTown
{
    public sealed class TankTownAudioSmoke : MonoBehaviour
    {
        private readonly float[] output = new float[1024];
        private float mixedPeak;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-audio-smoke") >= 0)
                new GameObject("Audio smoke").AddComponent<TankTownAudioSmoke>();
        }

        private void Update()
        {
            AudioListener.GetOutputData(output, 0);
            foreach (float sample in output) mixedPeak = Mathf.Max(mixedPeak, Mathf.Abs(sample));
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return new WaitForSecondsRealtime(2);
            var game = FindFirstObjectByType<TankTownGame>();
            for (int i = 0; i < 4; i++)
            {
                var clip = game.Sound.Clip(i);
                if (clip == null || clip.loadState != AudioDataLoadState.Loaded) { Fail("clip unavailable " + i); yield break; }
                var samples = new float[clip.samples * clip.channels];
                if (!clip.GetData(samples, 0)) { Fail("clip failed to decode " + i); yield break; }
                float peak = 0;
                foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                Debug.Log($"AUDIO CLIP: {clip.name}, seconds={clip.length:0.000}, channels={clip.channels}, peak={peak:0.000}");
                if (peak < .01f) { Fail("silent clip " + i); yield break; }
            }
            game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var car in game.World.TrafficCars) car.gameObject.SetActive(false);
            Vector3 point = Camera.main.transform.position + Camera.main.transform.forward * 12;
            game.Tank.Fire(0); game.Tank.Fire(1);
            yield return new WaitForSecondsRealtime(.4f);
            game.Blast(point, .1f, 0);
            yield return new WaitForSecondsRealtime(.5f);
            game.ChainBlast(point, .1f, 1);
            yield return new WaitForSecondsRealtime(.5f);
            game.ChainBlast(point, .1f, 1, true);
            yield return new WaitForSecondsRealtime(.5f);
            game.Sound.StopAll(); game.Sound.Cannon();
            yield return new WaitForSecondsRealtime(.12f);
            AudioSource playing = null;
            foreach (var source in game.Sound.GetComponentsInChildren<AudioSource>()) if (source.isPlaying) { playing = source; break; }
            if (playing == null) { Fail("no playing cannon source"); yield break; }
            game.TogglePause(); float pausedAt = playing.time;
            yield return new WaitForSecondsRealtime(.25f);
            if (Mathf.Abs(playing.time - pausedAt) > .02f) { Fail("paused sound advanced"); yield break; }
            game.TogglePause();
            yield return new WaitForSecondsRealtime(.15f);
            if (playing.time <= pausedAt) { Fail("paused sound did not resume"); yield break; }
            for (int i = 0; i < 100; i++) game.Sound.Impact(point);
            if (game.Sound.GetComponentsInChildren<AudioSource>().Length != TankTownAudio.VoiceLimit) { Fail("voice pool grew"); yield break; }
            yield return new WaitForSecondsRealtime(.2f);
            game.AdvanceTime(1000);
            foreach (var source in game.Sound.GetComponentsInChildren<AudioSource>()) if (source.isPlaying) { Fail("sound continued at results"); yield break; }
            Debug.Log($"AUDIO SMOKE COMPLETE: mixedPeak={mixedPeak:0.000}, voices={TankTownAudio.VoiceLimit}, cannon={game.Sound.PlayedCount(TownSound.Cannon)}, impact={game.Sound.PlayedCount(TownSound.Impact)}, chain={game.Sound.PlayedCount(TownSound.Chain)}, big={game.Sound.PlayedCount(TownSound.BigExplosion)}, pauseResume=True, resultsStopped=True");
            Application.Quit(mixedPeak > .01f && mixedPeak < .98f && game.Sound.PlayedCount(TownSound.Cannon) == 2 && game.Sound.PlayedCount(TownSound.Impact) >= 1 && game.Sound.PlayedCount(TownSound.Chain) >= 1 && game.Sound.PlayedCount(TownSound.BigExplosion) >= 1 ? 0 : 1);
        }

        private static void Fail(string reason) { Debug.LogError("AUDIO SMOKE FAILED: " + reason); Application.Quit(1); }
    }
}
#endif
