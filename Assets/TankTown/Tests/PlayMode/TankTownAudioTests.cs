using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownAudioTests
    {
        private TankTownGame game;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
            game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var car in game.World.TrafficCars) car.gameObject.SetActive(false);
            foreach (var pickup in game.World.ItemPickups) pickup.gameObject.SetActive(false);
        }

        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1; yield return null; }

        [UnityTest]
        public IEnumerator AllFourProvidedClipsDecodeBeforeTheFirstShot()
        {
            for (int i = 0; i < 4; i++)
            {
                var clip = game.Sound.Clip(i);
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
                Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
                Assert.That(clip.channels, Is.EqualTo(1));
                var samples = new float[clip.samples * clip.channels];
                Assert.That(clip.GetData(samples, 0), Is.True);
                Assert.That(samples.Any(x => Mathf.Abs(x) > .01f), Is.True, clip.name + " must contain decoded sound");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealFiringAndExplosionRoutesUseTheProvidedSounds()
        {
            game.Tank.Fire(0); game.Tank.Fire(1);
            Assert.That(game.Sound.PlayedCount(TownSound.Cannon), Is.EqualTo(1), "A rejected cooldown shot must remain silent");
            Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Not.Null);
            Vector3 empty = new Vector3(0, 90, 0);
            game.Blast(empty, .1f, 0);
            game.ChainBlast(empty, .1f, 1);
            game.ChainBlast(empty, .1f, 1, true);
            Assert.That(game.Sound.PlayedCount(TownSound.Impact), Is.EqualTo(1));
            Assert.That(game.Sound.PlayedCount(TownSound.Chain), Is.EqualTo(1));
            Assert.That(game.Sound.PlayedCount(TownSound.BigExplosion), Is.EqualTo(1));
            game.Tank.Explode();
            Assert.That(game.Sound.PlayedCount(TownSound.BigExplosion), Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExplosionStormKeepsAFixedPoolAndPreservesCannonAndBigVoices()
        {
            game.Sound.Cannon(); game.Sound.BigExplosion(Vector3.zero);
            for (int i = 0; i < 120; i++) game.Sound.Impact(Vector3.forward * i);
            var sources = game.Sound.GetComponentsInChildren<AudioSource>();
            Assert.That(sources.Length, Is.EqualTo(TankTownAudio.VoiceLimit));
            Assert.That(sources.Sum(x => x.volume), Is.LessThanOrEqualTo(.901f), "Simultaneous peaks must leave output headroom");
            Assert.That(sources.Any(x => x.clip == game.Sound.Clip(0)), Is.True, "Small explosions cannot steal cannon voices");
            Assert.That(sources.Any(x => x.clip == game.Sound.Clip(3)), Is.True, "Small explosions cannot steal big explosion voices");
            game.Sound.Chain(Vector3.zero);
            int accepted = game.Sound.PlayedCount(TownSound.Chain);
            for (int i = 0; i < 100; i++) game.Sound.Chain(Vector3.zero);
            Assert.That(game.Sound.PlayedCount(TownSound.Chain), Is.EqualTo(accepted), "Same-instant chain sounds must be limited");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PausedAndFinishedRoundsRejectNewSoundPlayback()
        {
            game.Sound.Cannon(); game.TogglePause();
            game.Tank.Fire(0); game.Sound.Impact(Vector3.zero);
            Assert.That(game.Sound.Paused, Is.True);
            Assert.That(game.Sound.PlayedCount(TownSound.Cannon), Is.EqualTo(1));
            Assert.That(game.Sound.PlayedCount(TownSound.Impact), Is.Zero);
            yield return new WaitForSecondsRealtime(.1f);
            game.TogglePause(); game.Sound.Impact(Vector3.zero);
            Assert.That(game.Sound.PlayedCount(TownSound.Impact), Is.EqualTo(1));
            game.AdvanceTime(1000); game.Sound.Cannon(); game.Sound.BigExplosion(Vector3.zero);
            Assert.That(game.State, Is.EqualTo(RoundState.Results));
            Assert.That(game.Sound.GetComponentsInChildren<AudioSource>().Any(x => x.isPlaying), Is.False);
            Assert.That(game.Sound.PlayedCount(TownSound.Cannon), Is.EqualTo(1));
            Assert.That(game.Sound.PlayedCount(TownSound.BigExplosion), Is.Zero);
        }
    }
}
