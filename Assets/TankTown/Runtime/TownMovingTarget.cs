using UnityEngine;

namespace TankTown
{
    public sealed class TownMovingTarget : MonoBehaviour
    {
        private TownTrafficCar car;
        private TownPedestrian pedestrian;
        private TankTownGame game;
        private ChainExplosive explosive;
        private Collider explosiveShape;
        public bool IsVehicle => car != null;
        public bool IsExplosive => explosive != null;
        public bool Available => game != null && game.IsPlaying && isActiveAndEnabled &&
            (explosive == null || (!explosive.Primed && !explosive.Detonated)) &&
            (car != null ? car.IsDriving : explosive != null || pedestrian != null && pedestrian.Walking);
        public Vector3 AimPoint => !IsVehicle && explosiveShape != null ? explosiveShape.bounds.center : transform.position + Vector3.up * (IsVehicle ? .7f : 1.2f);
        public Vector3 Velocity => car != null ? car.Motion : pedestrian != null ? pedestrian.Motion : TryGetComponent<Rigidbody>(out var body) ? body.linearVelocity : Vector3.zero;
        public float HitRadius => IsVehicle ? 1.1f : pedestrian != null ? pedestrian.HitRadius : explosiveShape != null ? Mathf.Min(explosiveShape.bounds.extents.x, explosiveShape.bounds.extents.z) : .95f;
        public void Initialize(TankTownGame owner)
        {
            game = owner; car = GetComponent<TownTrafficCar>(); pedestrian = GetComponent<TownPedestrian>();
            explosive = GetComponent<ChainExplosive>(); explosiveShape = explosive != null ? GetComponent<Collider>() : null;
            game.World.MovingTargets.Add(this);
        }
    }
}
