using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public enum TownStage { Town, BigCity }

    // Streets stay connected; only occupied plots and their content are shuffled.
    public sealed class TownLayout
    {
        public readonly struct Plot
        {
            public readonly int X, Z, Style, Floors;
            public readonly Vector3 Center;
            public readonly float Width;
            public readonly bool Park;
            public readonly ItemKind Reward;
            public Plot(int x, int z, int style, int floors, Vector3 center, float width, bool park, ItemKind reward)
            { X = x; Z = z; Style = style; Floors = floors; Center = center; Width = width; Park = park; Reward = reward; }
        }
        public int Seed { get; }
        public IReadOnlyList<Plot> Plots => plots;
        private readonly List<Plot> plots = new List<Plot>();
        private readonly float[,] heights;
        private readonly int half;

        public TownLayout(TankTownSettings settings, int seed, float roadWidth)
        {
            Seed = seed; half = settings.cityHalfBlocks;
            int count = half * half * 4;
            var parks = new bool[count];
            var parkRandom = new System.Random(unchecked(seed ^ 0x6F218));
            // Keep the entry corner occupied, so the first avenue always has a landmark.
            int parkCount = 0;
            int desiredParks = Mathf.Clamp(settings.cityParks, 0, count - 6);
            while (parkCount < desiredParks) { int index = parkRandom.Next(1, count); if (!parks[index]) { parks[index] = true; parkCount++; } }
            var rewards = new ItemKind[count - parkCount];
            for (int i = 0; i < rewards.Length; i++) rewards[i] = (ItemKind)(i % 6);
            var rewardRandom = new System.Random(unchecked(seed ^ 0x1B023));
            for (int i = rewards.Length - 1; i > 0; i--) { int j = rewardRandom.Next(i + 1); var swap = rewards[i]; rewards[i] = rewards[j]; rewards[j] = swap; }
            var shapeRandom = new System.Random(unchecked(seed ^ 42137));
            float padWidth = Mathf.Max(10, settings.blockSpacing - roadWidth - 4);
            float baseWidth = Mathf.Clamp(settings.buildingWidth, 10, padWidth);
            int plotIndex = 0, rewardIndex = 0;
            for (int x = -half; x < half; x++) for (int z = -half; z < half; z++, plotIndex++)
            {
                int style = shapeRandom.Next(3);
                int floors = style == 0 ? shapeRandom.Next(6, 9) : style == 1 ? shapeRandom.Next(4, 7) : shapeRandom.Next(2, 4);
                float width = Mathf.Max(10, baseWidth * (style == 0 ? .72f : style == 2 ? .88f : 1));
                // Keep the entire footprint on its level pad, including gable overhang.
                float movement = Mathf.Max(0, (padWidth - width - .5f) * .5f);
                var center = new Vector3((x + .5f) * settings.blockSpacing, 0, (z + .5f) * settings.blockSpacing);
                center += new Vector3((float)(shapeRandom.NextDouble() * 2 - 1) * movement, 0, (float)(shapeRandom.NextDouble() * 2 - 1) * movement);
                plots.Add(new Plot(x, z, style, floors, center, width, parks[plotIndex], parks[plotIndex] ? ItemKind.None : rewards[rewardIndex++]));
            }
            var terrainRandom = new System.Random(unchecked(seed ^ 0x33D217));
            float phaseX = (float)terrainRandom.NextDouble() * Mathf.PI * 2;
            float phaseZ = (float)terrainRandom.NextDouble() * Mathf.PI * 2;
            float phaseHill = (float)terrainRandom.NextDouble() * Mathf.PI * 2;
            heights = new float[half * 2 + 3, half * 2 + 3];
            for (int x = -half - 1; x <= half + 1; x++) for (int z = -half - 1; z <= half + 1; z++)
            {
                float h = 1.4f + .85f * Mathf.Sin(x * .55f + phaseX) + .85f * Mathf.Sin(z * .55f + phaseZ)
                    + .35f * Mathf.Cos((x + z) * .4f + phaseHill);
                heights[x + half + 1, z + half + 1] = Mathf.Clamp(h, 0, 2.8f) * settings.terraceHeight;
            }
        }
        public float JunctionHeight(int x, int z)
            => heights[Mathf.Clamp(x + half + 1, 0, heights.GetLength(0) - 1), Mathf.Clamp(z + half + 1, 0, heights.GetLength(1) - 1)];
    }
}
