using UnityEngine;

namespace TankTown
{
    public sealed class TownTrafficCar : MonoBehaviour
    {
        public float CruiseSpeed { get; private set; }
        public float Speed { get; private set; }
        private TankTownGame game;
        private Rigidbody body;
        private DestructibleBlock block;
        private Vector3[] route;
        private float[] lengths;
        private float total, distance;

        public void Initialize(TankTownGame owner, Vector3[] circuit, float phase, float speed)
        {
            game = owner; route = circuit; CruiseSpeed = speed;
            block = GetComponent<DestructibleBlock>();
            lengths = new float[route.Length];
            for (int i = 0; i < route.Length; i++) { lengths[i] = Vector3.Distance(route[i], route[(i + 1) % route.Length]); total += lengths[i]; }
            distance = total * phase;
            body = gameObject.AddComponent<Rigidbody>(); body.mass = 4;
            body.isKinematic = true; body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Sample(distance, out var point, out var direction);
            body.position = game.World.GroundPoint(point, .72f);
            body.rotation = Quaternion.LookRotation(direction);
        }

        private void FixedUpdate()
        {
            if (!game.IsPlaying || block.Detached) return;
            Sample(distance, out _, out var forward);
            bool stop = false;
            foreach (var signal in game.World.Signals)
                if (signal != null && signal.ShouldStop(body.position, forward)) { stop = true; break; }
            foreach (var car in game.World.TrafficCars)
            {
                if (car == null || car == this || !car.isActiveAndEnabled || car.block.Detached) continue;
                var delta = Vector3.ProjectOnPlane(car.body.position - body.position, Vector3.up);
                float ahead = Vector3.Dot(delta, forward);
                if (ahead > 0 && ahead < 8 && Mathf.Abs(Vector3.Dot(delta, Vector3.Cross(Vector3.up, forward))) < 2) { stop = true; break; }
            }
            Speed = Mathf.MoveTowards(Speed, stop ? 0 : CruiseSpeed, Time.fixedDeltaTime * 12);
            distance = Mathf.Repeat(distance + Speed * Time.fixedDeltaTime, total);
            Sample(distance, out var point, out forward);
            var up = game.World.Terrain.NormalAt(point);
            var heading = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, up).normalized, up);
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, heading, 160 * Time.fixedDeltaTime));
            body.MovePosition(game.World.GroundPoint(point, .72f));
        }

        private void Sample(float travel, out Vector3 point, out Vector3 direction)
        {
            int index = 0;
            while (index < lengths.Length - 1 && travel >= lengths[index]) { travel -= lengths[index]; index++; }
            var next = route[(index + 1) % route.Length];
            direction = (next - route[index]).normalized;
            point = Vector3.Lerp(route[index], next, travel / lengths[index]);
        }
    }
}
