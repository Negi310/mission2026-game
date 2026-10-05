using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    // A single physics-step snapshot avoids every car scanning the entire town.
    internal sealed class TownTrafficGrid
    {
        private readonly struct CarEntry
        {
            internal readonly TownTrafficCar Car;
            internal readonly Vector3 Position;
            internal CarEntry(TownTrafficCar car) { Car = car; Position = car.PhysicsPosition; }
        }
        private readonly TankTownWorld world;
        private readonly float cellSize;
        private readonly Dictionary<Vector2Int, List<CarEntry>> cars = new Dictionary<Vector2Int, List<CarEntry>>();
        private readonly Dictionary<Vector2Int, List<TownTrafficSignal>> signals = new Dictionary<Vector2Int, List<TownTrafficSignal>>();
        private float sampledAt = -1;
        internal TownTrafficGrid(TankTownWorld owner)
        {
            world = owner; cellSize = Mathf.Max(16, world.RoadWidth + 10);
            foreach (var signal in world.Signals)
            {
                var key = Cell(signal.Intersection);
                if (!signals.TryGetValue(key, out var bucket)) signals[key] = bucket = new List<TownTrafficSignal>();
                bucket.Add(signal);
            }
        }
        private Vector2Int Cell(Vector3 point) => new Vector2Int(Mathf.FloorToInt(point.x / cellSize), Mathf.FloorToInt(point.z / cellSize));
        private void Refresh()
        {
            if (sampledAt == Time.fixedTime) return;
            sampledAt = Time.fixedTime;
            foreach (var bucket in cars.Values) bucket.Clear();
            foreach (var car in world.TrafficCars)
            {
                if (car == null || !car.IsDriving) continue;
                var entry = new CarEntry(car); var key = Cell(entry.Position);
                if (!cars.TryGetValue(key, out var bucket)) cars[key] = bucket = new List<CarEntry>(4);
                bucket.Add(entry);
            }
        }
        internal bool ShouldStop(TownTrafficCar self, Vector3 position, Vector3 heading)
        {
            Refresh(); var cell = Cell(position);
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            {
                var key = cell + new Vector2Int(x, z);
                if (signals.TryGetValue(key, out var lights))
                    foreach (var light in lights) if (light != null && light.ShouldStop(position, heading)) return true;
                if (!cars.TryGetValue(key, out var neighbors)) continue;
                foreach (var entry in neighbors)
                {
                    if (entry.Car == self || entry.Car == null || !entry.Car.IsDriving) continue;
                    var delta = entry.Position - position;
                    float ahead = delta.x * heading.x + delta.z * heading.z;
                    if (ahead > 0 && ahead < 8 && Mathf.Abs(delta.x * heading.z - delta.z * heading.x) < 2) return true;
                }
            }
            return false;
        }
    }
}
