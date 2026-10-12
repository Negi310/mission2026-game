using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    [Serializable]
    public sealed class TownScoreRecord
    {
        [SerializeField] private string id;
        [SerializeField] private int score;
        [SerializeField] private int seed;
        [SerializeField] private string playedAt;
        public string Id => id;
        public int Score => score;
        public int Seed => seed;
        public string PlayedAt => playedAt;
        public float Destruction => score / 1000f;
        internal TownScoreRecord(int points, int citySeed)
        {
            id = Guid.NewGuid().ToString("N"); score = points; seed = citySeed;
            playedAt = DateTime.UtcNow.ToString("O");
        }
    }

    public sealed class TankTownRanking
    {
        public const int Capacity = 10;
        internal const string StorageKey = "TankTown.Ranking.v1";
        [Serializable] private sealed class SaveData
        {
            public int version = 1;
            public List<TownScoreRecord> records = new List<TownScoreRecord>();
        }
        private readonly List<TownScoreRecord> records = new List<TownScoreRecord>();
        private readonly string key;
        private readonly bool persist;
        public IReadOnlyList<TownScoreRecord> Records { get; }
        public int BestScore => records.Count == 0 ? 0 : records[0].Score;

        internal TankTownRanking(bool saveRecords, int legacyBest = 0, string storageKey = StorageKey)
        {
            persist = saveRecords; key = storageKey; Records = records.AsReadOnly();
            if (persist && PlayerPrefs.HasKey(key))
            {
                try
                {
                    var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(key));
                    if (data != null && data.version == 1 && data.records != null)
                    {
                        var ids = new HashSet<string>();
                        foreach (var record in data.records)
                            if (record != null && !string.IsNullOrEmpty(record.Id) && record.Score > 0 && record.Score <= 100000 && ids.Add(record.Id)) records.Add(record);
                        // Stable insertion sort: older entries win ties, including after a reload.
                        for (int i = 1; i < records.Count; i++)
                        {
                            var value = records[i]; int j = i - 1;
                            while (j >= 0 && records[j].Score < value.Score) { records[j + 1] = records[j]; j--; }
                            records[j + 1] = value;
                        }
                        Trim();
                    }
                }
                catch (ArgumentException) { /* Recover from an incomplete local save using the old best. */ }
            }
            legacyBest = Mathf.Clamp(legacyBest, 0, 100000);
            if (legacyBest > BestScore) { records.Insert(0, new TownScoreRecord(legacyBest, 0)); Trim(); Save(); }
        }

        // A tied new score follows existing entries. 11 means outside the saved top 10.
        public int PredictRank(int score)
        {
            if (score <= 0) return 0;
            int position = 1;
            foreach (var record in records) if (record.Score >= score) position++;
            return position;
        }

        internal TownScoreRecord Submit(int score, int seed, out int position)
        {
            score = Mathf.Clamp(score, 0, 100000); position = PredictRank(score);
            if (position == 0 || position > Capacity) return null;
            var record = new TownScoreRecord(score, seed);
            records.Insert(position - 1, record); Trim(); Save(); return record;
        }
        private void Trim() { if (records.Count > Capacity) records.RemoveRange(Capacity, records.Count - Capacity); }
        private void Save()
        {
            if (!persist) return;
            PlayerPrefs.SetString(key, JsonUtility.ToJson(new SaveData { records = records })); PlayerPrefs.Save();
        }
    }
}
