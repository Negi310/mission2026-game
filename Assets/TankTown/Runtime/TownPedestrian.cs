using UnityEngine;

namespace TankTown
{
    public sealed class TownPedestrian : MonoBehaviour
    {
        public bool Walking => isActiveAndEnabled && !thrown;
        public Vector3 Motion { get; private set; }
        public int Knockbacks { get; private set; }
        public float HitRadius => hitbox.radius;
        public Vector3 ClosestPoint(Vector3 point) => hitbox.ClosestPoint(point);
        private CapsuleCollider hitbox;
        private TankTownGame game;
        private Vector3[] route;
        private float distance, perimeter, speed, phase, flightAge;
        private Vector3 flightVelocity;
        private bool thrown;
        private Transform model;
        private readonly Transform[] limbs = new Transform[4];

        public void Initialize(TankTownGame owner, Vector3[] circuit, float start, int variant, Material shirt, Material skin, Material dark, Mesh head)
        {
            game = owner; route = circuit; phase = variant * 1.17f; speed = 1.3f + variant % 5 * .16f;
            for (int i = 0; i < route.Length; i++) perimeter += Vector3.Distance(route[i], route[(i + 1) % route.Length]);
            distance = perimeter * start;
            gameObject.layer = TownPhysics.PedestrianLayer;
            hitbox = gameObject.AddComponent<CapsuleCollider>(); hitbox.isTrigger = true;
            hitbox.radius = game.Settings.pedestrianHitRadius; hitbox.height = 2.8f; hitbox.center = Vector3.up * 1.4f;
            model = new GameObject("Low poly citizen").transform; model.SetParent(transform, false);
            model.localScale = Vector3.one * (.95f + variant % 3 * .08f);
            game.World.DrawCitizen(model, limbs, variant, shirt, skin, dark, head);
            SnapToWalk(); gameObject.AddComponent<TownMovingTarget>().Initialize(game);
        }
        private void Sample(out Vector3 point, out Vector3 forward)
        {
            float travel = Mathf.Repeat(distance, perimeter); int i = 0;
            while (i < route.Length - 1 && travel > Vector3.Distance(route[i], route[(i + 1) % route.Length]))
            { travel -= Vector3.Distance(route[i], route[(i + 1) % route.Length]); i++; }
            forward = (route[(i + 1) % route.Length] - route[i]).normalized;
            point = route[i] + forward * travel;
        }
        private void SnapToWalk()
        {
            Sample(out var point, out var forward); transform.SetPositionAndRotation(game.World.GroundPoint(point, .04f), Quaternion.LookRotation(forward));
            model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity;
            foreach (var limb in limbs) limb.localRotation = Quaternion.identity;
            hitbox.enabled = true;
        }
        private void Update()
        {
            if (!game.IsPlaying) return;
            float dt = Time.deltaTime;
            if (thrown)
            {
                flightAge += dt; flightVelocity += Physics.gravity * dt; transform.position += flightVelocity * dt;
                model.localRotation = Quaternion.Euler(flightAge * 220, 0, flightAge * 160);
                if (flightAge > 2.2f || (flightAge > .2f && transform.position.y <= game.World.Terrain.HeightAt(transform.position) + .04f))
                { thrown = false; distance += 8; SnapToWalk(); }
                return;
            }
            distance += speed * dt; Sample(out var point, out var forward); Motion = forward * speed;
            transform.position = game.World.GroundPoint(point, .04f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(forward), 220 * dt);
            if ((transform.position - game.Tank.transform.position).sqrMagnitude > 90 * 90) return;
            float swing = Mathf.Sin(Time.time * speed * 5 + phase);
            model.localPosition = Vector3.up * Mathf.Abs(swing) * .045f;
            for (int i = 0; i < limbs.Length; i++) limbs[i].localRotation = Quaternion.Euler(swing * (i % 2 == 0 ? 1 : -1) * (i < 2 ? 27 : -23), 0, 0);
        }
        public void KnockAside(Vector3 origin, float impulse)
        {
            if (!game.IsPlaying || thrown) return;
            thrown = true; flightAge = 0; Knockbacks++; Motion = Vector3.zero;
            hitbox.enabled = false;
            var away = Vector3.ProjectOnPlane(transform.position - origin, Vector3.up).normalized;
            if (away.sqrMagnitude < .01f) away = transform.right;
            flightVelocity = away * Mathf.Clamp(6 + impulse * .28f, 9, 18) + Vector3.up * 8;
            game.Effects.PedestrianBonk(transform.position + Vector3.up * 1.6f);
        }
        private void OnTriggerEnter(Collider other) => ReactToTraffic(other);
        private void OnTriggerStay(Collider other) => ReactToTraffic(other);
        private void ReactToTraffic(Collider other)
        {
            if (!game.IsPlaying || thrown) return;
            var tank = other.GetComponentInParent<TankMotor>();
            if (tank != null && !tank.Relocating && tank.ActualSpeed > 1) KnockAside(tank.transform.position, 12);
            var car = other.GetComponentInParent<TownTrafficCar>();
            if (car != null && car.Speed > 4) KnockAside(car.transform.position, 8);
        }
    }
}
