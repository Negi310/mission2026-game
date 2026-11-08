using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public enum TownStage { Town, BigCity, CandyTown, AmusementPark }

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
            // A large attraction spans SpanX × SpanZ blocks from (X, Z); the other blocks it takes are Covered.
            public readonly int SpanX, SpanZ;
            public readonly bool Covered;
            public Plot(int x, int z, int style, int floors, Vector3 center, float width, bool park, ItemKind reward, int spanX = 1, int spanZ = 1, bool covered = false)
            {
                X = x; Z = z; Style = style; Floors = floors; Center = center; Width = width; Park = park; Reward = reward;
                SpanX = spanX; SpanZ = spanZ; Covered = covered;
            }
        }
        public int Seed { get; }
        public IReadOnlyList<Plot> Plots => plots;
        // Blocks merged into one lot for a large attraction; the streets between them become plaza.
        public IReadOnlyList<RectInt> Footprints => footprints;
        // Traffic circuits that would cross a merged lot are left out.
        public bool RingClosed(int ring) => closedRings.Contains(ring);
        private readonly List<Plot> plots = new List<Plot>();
        private readonly List<RectInt> footprints = new List<RectInt>();
        private readonly HashSet<int> closedRings = new HashSet<int>();
        private readonly float[,] heights;
        private readonly int half;

        // Amusement styles: 0 wheel, 1 coaster, 2 carousel, 3 drop tower, 4 ticket booth, 5 snack booth, 6 gate,
        // 7 giant coaster (2×2 blocks), 8 giant wheel (2×1), 9 circus tent (2×1), 10 pirate ship, 11 teacups.
        internal const int GiantCoaster = 7, GiantWheel = 8, CircusTent = 9, PirateShip = 10, Teacups = 11;
        private static readonly int[] amusementSingles = { 0, 1, 2, 3, PirateShip, Teacups, 4, 5, 4, 5, 4, 5 };

        public TownLayout(TankTownSettings settings, int seed, float roadWidth, TownStage stage = TownStage.Town)
        {
            Seed = seed; half = settings.cityHalfBlocks;
            int count = half * half * 4;
            var footprintOf = new int[count];
            for (int i = 0; i < count; i++) footprintOf[i] = -1;
            if (stage == TownStage.AmusementPark) ChooseFootprints(seed, footprintOf);
            int coveredCount = 0;
            foreach (var footprint in footprints) coveredCount += footprint.width * footprint.height - 1;
            var parks = new bool[count];
            var parkRandom = new System.Random(unchecked(seed ^ 0x6F218));
            // Keep the entry corner occupied, so the first avenue always has a landmark.
            int parkCount = 0;
            int desiredParks = Mathf.Clamp(settings.cityParks, 0, count - 6);
            if (stage == TownStage.AmusementPark)
            {
                int available = 0;
                for (int i = 1; i < count; i++)
                {
                    int x = i / (half * 2) - half, z = i % (half * 2) - half;
                    if (!EntryReserved(x, z) && footprintOf[i] < 0) available++;
                }
                desiredParks = Mathf.Min(desiredParks, available);
            }
            while (parkCount < desiredParks)
            {
                int index = parkRandom.Next(1, count);
                int x = index / (half * 2) - half, z = index % (half * 2) - half;
                if (stage == TownStage.AmusementPark && (EntryReserved(x, z) || footprintOf[index] >= 0)) continue;
                if (!parks[index]) { parks[index] = true; parkCount++; }
            }
            var rewards = new ItemKind[count - parkCount - coveredCount];
            for (int i = 0; i < rewards.Length; i++) rewards[i] = (ItemKind)(i % 6);
            var rewardRandom = new System.Random(unchecked(seed ^ 0x1B023));
            for (int i = rewards.Length - 1; i > 0; i--) { int j = rewardRandom.Next(i + 1); var swap = rewards[i]; rewards[i] = rewards[j]; rewards[j] = swap; }
            var shapeRandom = new System.Random(unchecked(seed ^ 42137));
            // Candy has six shared silhouettes; shape and reward color are independent.
            int[] candyStyles = null;
            if (stage == TownStage.CandyTown)
            {
                var kinds = new[] { 0, 1, 2, 4, 5, 6 }; // Style 3 is reserved for the physical doughnut gate.
                candyStyles = new int[rewards.Length];
                for (int i = 0; i < candyStyles.Length; i++) candyStyles[i] = kinds[i % kinds.Length];
                var candyRandom = new System.Random(unchecked(seed ^ 0x6C412));
                for (int i = candyStyles.Length - 1; i > 0; i--) { int j = candyRandom.Next(i + 1); int swap = candyStyles[i]; candyStyles[i] = candyStyles[j]; candyStyles[j] = swap; }
            }
            float padWidth = Mathf.Max(10, settings.blockSpacing - roadWidth - 4);
            float baseWidth = Mathf.Clamp(settings.buildingWidth, 10, padWidth);
            int plotIndex = 0, rewardIndex = 0, singleIndex = 0;
            for (int x = -half; x < half; x++) for (int z = -half; z < half; z++, plotIndex++)
            {
                int style = candyStyles != null && !parks[plotIndex] ? candyStyles[rewardIndex] : shapeRandom.Next(3);
                int spanX = 1, spanZ = 1;
                bool covered = false;
                if (stage == TownStage.AmusementPark)
                {
                    int owner = footprintOf[plotIndex];
                    if (owner >= 0)
                    {
                        var footprint = footprints[owner];
                        covered = footprint.x != x || footprint.y != z;
                        if (!covered) { spanX = footprint.width; spanZ = footprint.height; }
                        // The four-block lots hold the long coasters; the two-block lots alternate wheel and tent.
                        style = footprint.width * footprint.height == 4 ? GiantCoaster : owner % 2 == 0 ? GiantWheel : CircusTent;
                    }
                    // Small booths keep the park open between six readable ride silhouettes.
                    else if (!parks[plotIndex]) style = amusementSingles[singleIndex++ % amusementSingles.Length];
                    if (x == -1 && z == -half) style = 2;
                    if (x == 0 && z == -half) style = 5;
                    if (x == -1 && z == -half + 1) style = 0;
                    if (x == 0 && z == -half + 1) style = 1;
                    if (x == 0 && z == -half + 2) style = 3;
                }
                int floors = style == 0 ? shapeRandom.Next(6, 9) : style == 1 ? shapeRandom.Next(4, 7) : style == 4 || style == 5 ? shapeRandom.Next(3, 5) : shapeRandom.Next(2, 4);
                float width = Mathf.Max(10, baseWidth * (style == 0 ? .72f : style == 2 ? .88f : style >= 4 ? .92f : 1));
                if (stage == TownStage.AmusementPark) { floors = 1; width = baseWidth + (Mathf.Max(spanX, spanZ) - 1) * settings.blockSpacing; }
                // Keep the entire footprint on its level pad, including gable overhang.
                float movement = Mathf.Max(0, (padWidth - width - .5f) * .5f);
                var center = new Vector3((x + .5f) * settings.blockSpacing, 0, (z + .5f) * settings.blockSpacing);
                center += new Vector3((float)(shapeRandom.NextDouble() * 2 - 1) * movement, 0, (float)(shapeRandom.NextDouble() * 2 - 1) * movement);
                // A merged lot is centred on its blocks, over the street it replaces.
                if (spanX * spanZ > 1) center = new Vector3((x + spanX * .5f) * settings.blockSpacing, 0, (z + spanZ * .5f) * settings.blockSpacing);
                bool park = parks[plotIndex];
                plots.Add(new Plot(x, z, style, floors, center, width, park, park || covered ? ItemKind.None : rewards[rewardIndex++], spanX, spanZ, covered));
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
            // A merged lot is one level pad, including the streets it replaces. Lots never share a junction.
            foreach (var footprint in footprints)
            {
                float sum = 0; int corners = 0;
                for (int x = footprint.xMin; x <= footprint.xMax; x++) for (int z = footprint.yMin; z <= footprint.yMax; z++) { sum += JunctionHeight(x, z); corners++; }
                for (int x = footprint.xMin; x <= footprint.xMax; x++) for (int z = footprint.yMin; z <= footprint.yMax; z++) heights[x + half + 1, z + half + 1] = sum / corners;
            }
        }
        public float JunctionHeight(int x, int z)
            => heights[Mathf.Clamp(x + half + 1, 0, heights.GetLength(0) - 1), Mathf.Clamp(z + half + 1, 0, heights.GetLength(1) - 1)];

        private bool EntryReserved(int x, int z) => (x == -1 || x == 0) && z < -half + 3;

        // The merged lot holding block (x, z), or -1.
        public int FootprintAt(int x, int z)
        {
            for (int i = 0; i < footprints.Count; i++) if (footprints[i].Contains(new Vector2Int(x, z))) return i;
            return -1;
        }

        // Whether the street on a north-south (or east-west) line, from junction `segment` to the next one, lies inside a merged lot.
        public bool StreetRemoved(bool northSouth, int line, int segment)
        {
            foreach (var footprint in footprints)
            {
                int across = northSouth ? footprint.x : footprint.y, along = northSouth ? footprint.y : footprint.x;
                int acrossSize = northSouth ? footprint.width : footprint.height, alongSize = northSouth ? footprint.height : footprint.width;
                if (line > across && line < across + acrossSize && segment >= along && segment < along + alongSize) return true;
            }
            return false;
        }

        // A junction with merged-lot blocks on all four sides.
        public bool JunctionInside(int x, int z)
        {
            foreach (var footprint in footprints)
                if (x > footprint.xMin && x < footprint.xMax && z > footprint.yMin && z < footprint.yMax) return true;
            return false;
        }

        // The streets one traffic circuit drives along, as (north-south, line, segment), matching TankTownWorld.CreateTraffic.
        public static IEnumerable<(bool northSouth, int line, int segment)> RingStreets(int ring, int half)
        {
            int right = ring == 1 ? 0 : ring, left = -ring, north = ring, south = ring == 1 ? -half : -ring;
            for (int z = south; z < north; z++) { yield return (true, left, z); yield return (true, right, z); }
            for (int x = left; x < right; x++) { yield return (false, south, x); yield return (false, north, x); }
        }

        private void ChooseFootprints(int seed, int[] footprintOf)
        {
            // Big lots may only cut odd outer circuits, so the entry circuits and every second ring keep their cars.
            var closable = new HashSet<int>();
            for (int ring = 3; ring < half; ring += 2) closable.Add(ring);
            var ringOf = new Dictionary<(bool, int, int), int>();
            for (int ring = 1; ring <= half; ring++)
                foreach (var street in RingStreets(ring, half)) ringOf[street] = ring;
            var candidates = new List<RectInt>();
            for (int x = -half; x < half; x++) for (int z = -half; z < half; z++)
            {
                if (x + 1 < half && z + 1 < half) candidates.Add(new RectInt(x, z, 2, 2));
                if (x + 1 < half) candidates.Add(new RectInt(x, z, 2, 1));
                if (z + 1 < half) candidates.Add(new RectInt(x, z, 1, 2));
            }
            var random = new System.Random(unchecked(seed ^ 0x2A7F41));
            for (int i = candidates.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var swap = candidates[i]; candidates[i] = candidates[j]; candidates[j] = swap; }
            int blocks = half * half * 4;
            foreach (bool large in new[] { true, false })
            {
                int placed = 0, want = large ? blocks / 36 : blocks / 18;
                foreach (var candidate in candidates)
                {
                    if (placed >= want) break;
                    if ((candidate.width * candidate.height == 4) != large || !Fits(candidate, footprintOf)) continue;
                    var cut = new HashSet<int>(); bool allowed = true;
                    for (int line = candidate.xMin + 1; line < candidate.xMax; line++) for (int z = candidate.yMin; z < candidate.yMax; z++)
                        allowed &= Cut(ringOf, closable, cut, (true, line, z));
                    for (int line = candidate.yMin + 1; line < candidate.yMax; line++) for (int x = candidate.xMin; x < candidate.xMax; x++)
                        allowed &= Cut(ringOf, closable, cut, (false, line, x));
                    if (!allowed) continue;
                    closedRings.UnionWith(cut);
                    for (int x = candidate.xMin; x < candidate.xMax; x++) for (int z = candidate.yMin; z < candidate.yMax; z++)
                        footprintOf[(x + half) * half * 2 + z + half] = footprints.Count;
                    footprints.Add(candidate); placed++;
                }
            }
        }

        private static bool Cut(Dictionary<(bool, int, int), int> ringOf, HashSet<int> closable, HashSet<int> cut, (bool, int, int) street)
        {
            if (!ringOf.TryGetValue(street, out int ring)) return true;
            cut.Add(ring); return closable.Contains(ring);
        }

        private bool Fits(RectInt candidate, int[] footprintOf)
        {
            // The first plot and the entry landmarks stay small.
            for (int x = candidate.xMin; x < candidate.xMax; x++) for (int z = candidate.yMin; z < candidate.yMax; z++)
                if (EntryReserved(x, z) || (x == -half && z == -half) || footprintOf[(x + half) * half * 2 + z + half] >= 0) return false;
            // Lots keep at least one block between them, so each levels its own junctions.
            foreach (var other in footprints)
                if (candidate.xMin <= other.xMax && other.xMin <= candidate.xMax && candidate.yMin <= other.yMax && other.yMin <= candidate.yMax) return false;
            return true;
        }
    }
}
