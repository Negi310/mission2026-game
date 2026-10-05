using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TankTown.Tests
{
    public sealed class TankTownRankingMinimapTests
    {
        private string testKey;
        [SetUp] public void SetUp() => testKey = "TankTown.Tests.Ranking." + Guid.NewGuid().ToString("N");
        [TearDown] public void TearDown() { PlayerPrefs.DeleteKey(testKey); PlayerPrefs.DeleteKey(testKey + ".BigCity"); PlayerPrefs.Save(); Time.timeScale = 1; }

        [UnityTest]
        public IEnumerator StageRankingsRemainSeparateWhenReturningToTheOriginalTown()
        {
            string previous = TankTownGame.RankingStorageOverrideForTests;
            TownStage previousStage = TankTownGame.SelectedStage;
            try
            {
                TankTownGame.RankingStorageOverrideForTests = testKey; TankTownGame.SelectedStage = TownStage.Town;
                SceneManager.LoadScene("TankTown"); yield return null;
                var game = UnityEngine.Object.FindFirstObjectByType<TankTownGame>();
                game.Ranking.Submit(30000, 2026, out _);
                game.SelectStage(TownStage.BigCity); yield return null; yield return null;
                game = UnityEngine.Object.FindFirstObjectByType<TankTownGame>();
                Assert.That(game.Ranking.Records, Is.Empty);
                game.Ranking.Submit(18000, 2026, out _);
                var map = game.GetComponent<TankTownHUD>().Minimap;
                Assert.That(map.MapHalfExtent, Is.EqualTo(game.World.HalfExtent + 3));
                game.SelectStage(TownStage.Town); yield return null; yield return null;
                game = UnityEngine.Object.FindFirstObjectByType<TankTownGame>();
                Assert.That(game.Ranking.Records.Select(x => x.Score), Is.EqualTo(new[] { 30000 }));
                Assert.That(new TankTownRanking(true, 0, testKey + ".BigCity").Records.Select(x => x.Score), Is.EqualTo(new[] { 18000 }));
            }
            finally { TankTownGame.RankingStorageOverrideForTests = previous; TankTownGame.SelectedStage = previousStage; }
        }

        [Test]
        public void TopTenSortsPersistsAndRetainsOlderTies()
        {
            var board = new TankTownRanking(true, 0, testKey);
            var first = board.Submit(30000, 123, out int rank);
            Assert.That(rank, Is.EqualTo(1));
            var tie = board.Submit(30000, 456, out rank); Assert.That(rank, Is.EqualTo(2));
            board.Submit(50000, 1, out rank); Assert.That(rank, Is.EqualTo(1));
            var loaded = new TankTownRanking(true, 0, testKey);
            Assert.That(loaded.Records.Select(x => x.Score), Is.EqualTo(new[] { 50000, 30000, 30000 }));
            Assert.That(loaded.Records[1].Id, Is.EqualTo(first.Id));
            Assert.That(loaded.Records[2].Id, Is.EqualTo(tie.Id));
            Assert.That(loaded.Records[2].Seed, Is.EqualTo(456));
            Assert.That(DateTime.TryParse(loaded.Records[2].PlayedAt, out _), Is.True);
            for (int i = 0; i < 12; i++) loaded.Submit(60000 + i * 100, i, out rank);
            Assert.That(loaded.Records.Count, Is.EqualTo(10));
            Assert.That(loaded.Records.Select(x => x.Score), Is.Ordered.Descending);
            Assert.That(loaded.Submit(10, 9, out rank), Is.Null); Assert.That(rank, Is.EqualTo(11));
            Assert.That(new TankTownRanking(true, 0, testKey).Records.Count, Is.EqualTo(10));
        }

        [Test]
        public void EmptyZeroAndDisabledPersistenceDoNotCreateFakeRecords()
        {
            var board = new TankTownRanking(false, 0, testKey);
            Assert.That(board.PredictRank(0), Is.Zero);
            Assert.That(board.Submit(0, 1, out int place), Is.Null); Assert.That(place, Is.Zero);
            board.Submit(12345, 1, out place); Assert.That(place, Is.EqualTo(1));
            Assert.That(board.Records[0].Destruction, Is.EqualTo(12.345f).Within(.001));
            Assert.That(PlayerPrefs.HasKey(testKey), Is.False);
        }

        [Test]
        public void LegacyBestSurvivesMigrationAndCorruptSave()
        {
            PlayerPrefs.SetString(testKey, "this is an incomplete save");
            var board = new TankTownRanking(true, 43000, testKey);
            Assert.That(board.BestScore, Is.EqualTo(43000));
            board.Submit(34000, 100, out _);
            var loaded = new TankTownRanking(true, 43000, testKey);
            Assert.That(loaded.Records.Count, Is.EqualTo(2), "Migrating twice must not duplicate the old best");
            Assert.That(loaded.BestScore, Is.EqualTo(43000));
        }

        [TestCase(0, 1, 0)] [TestCase(1, 0, -90)] [TestCase(0, -1, -180)] [TestCase(-1, 0, 90)]
        public void PlayerArrowUsesBodyHeadingWithNorthAtTheTop(float x, float z, float expected)
            => Assert.That(Mathf.DeltaAngle(TankTownMinimap.Heading(new Vector3(x, 0, z)), expected), Is.EqualTo(0).Within(.001));

        private IEnumerator LoadPlaying()
        {
            SceneManager.LoadScene("TankTown"); yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<TankTownGame>();
            game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AFinishedRoundRecordsExactlyOnceAndLiveRankTracksScore()
        {
            yield return LoadPlaying(); var game = UnityEngine.Object.FindFirstObjectByType<TankTownGame>();
            game.Ranking.Submit(20000, 1, out _); game.Ranking.Submit(50000, 2, out _);
            int count = Mathf.RoundToInt(game.TotalBlocks * .35f);
            for (int i = 0; i < count; i++) game.ScoreBlock();
            yield return null;
            Assert.That(game.CurrentRank, Is.EqualTo(2));
            Assert.That(GameObject.Find("Live ranking").GetComponentsInChildren<Text>().Any(x => x.text == "2位"), Is.True);
            game.AdvanceTime(1000); yield return null;
            Assert.That(game.FinalRank, Is.EqualTo(2)); Assert.That(game.RoundRecord.Score, Is.EqualTo(game.RoundScore));
            Assert.That(game.Ranking.Records.Count, Is.EqualTo(3));
            game.AdvanceTime(1000); game.ScoreBlock(); yield return null;
            Assert.That(game.Ranking.Records.Count, Is.EqualTo(3));
            var board = GameObject.Find("Local leaderboard"); Assert.That(board, Is.Not.Null);
            Assert.That(board.GetComponentsInChildren<Text>().Any(x => x.text == $"{game.RoundScore:N0} 点"), Is.True);
            foreach (var text in GameObject.Find("Result score card").GetComponentsInChildren<Text>())
            {
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1), text.name);
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name);
            }
        }

        [UnityTest]
        public IEnumerator OrthographicMapMatchesWorldCoordinatesAndDoesNotTakeOverMainCamera()
        {
            yield return LoadPlaying(); var game = UnityEngine.Object.FindFirstObjectByType<TankTownGame>();
            var map = game.GetComponent<TankTownHUD>().Minimap;
            Assert.That(Camera.main, Is.SameAs(game.CameraRig.GetComponent<Camera>()));
            Assert.That(map.View.enabled, Is.False); Assert.That(map.View.orthographic, Is.True);
            Assert.That(map.View.transform.forward.y, Is.EqualTo(-1).Within(.001));
            Assert.That(map.View.transform.up.z, Is.EqualTo(1).Within(.001));
            Assert.That(map.View.GetComponent<AudioListener>(), Is.Null);
            Assert.That(map.View.cullingMask & (1 << 5), Is.Zero, "The map must never recursively render the HUD");
            Assert.That(map.MapPoint(Vector3.zero), Is.EqualTo(Vector2.zero));
            Assert.That(map.MapPoint(new Vector3(map.MapHalfExtent, 80, map.MapHalfExtent)), Is.EqualTo(new Vector2(110, 110)));
            var body = game.Tank.GetComponent<Rigidbody>(); body.position = new Vector3(60, 20, -30); body.rotation = Quaternion.Euler(0, 90, 0);
            game.Tank.transform.SetPositionAndRotation(body.position, body.rotation); Physics.SyncTransforms(); map.Tick();
            Assert.That(Vector2.Distance(map.Marker.anchoredPosition, map.MapPoint(body.position)), Is.LessThan(.001));
            Assert.That(Mathf.DeltaAngle(map.Marker.localEulerAngles.z, -90), Is.EqualTo(0).Within(.001));
            game.Tank.Turret.localRotation = Quaternion.Euler(0, 25, 0); map.Tick();
            Assert.That(Mathf.DeltaAngle(map.Marker.localEulerAngles.z, -90), Is.EqualTo(0).Within(.001), "The marker follows the hull, not the independently aimed turret");
            game.TogglePause(); int renders = map.RenderCount; yield return new WaitForSecondsRealtime(.35f);
            Assert.That(map.RenderCount, Is.EqualTo(renders), "A paused map must not keep rendering");
        }

        [UnityTest]
        public IEnumerator ReplayReleasesOldMapCameraAndTexture()
        {
            yield return LoadPlaying(); var game = UnityEngine.Object.FindFirstObjectByType<TankTownGame>();
            var oldMap = game.GetComponent<TankTownHUD>().Minimap; var oldCamera = oldMap.View; var oldTexture = oldMap.Texture;
            game.AdvanceTime(1000); game.StartOrRestart(); yield return null; yield return null;
            Assert.That(oldCamera == null, Is.True);
            Assert.That(oldTexture == null, Is.True);
            Assert.That(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(x => x.name == "Minimap camera"), Is.EqualTo(1));
        }
    }
}
