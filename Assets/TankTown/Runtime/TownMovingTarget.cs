using UnityEngine;

namespace TankTown
{
    public sealed class TownMovingTarget : MonoBehaviour
    {
        private TownTrafficCar car;
        private TownPedestrian pedestrian;
        private TankTownGame game;
        public bool IsVehicle => car != null;
        public bool Available => game != null && game.IsPlaying && isActiveAndEnabled &&
            (car != null ? car.IsDriving : pedestrian != null && pedestrian.Walking);
        public Vector3 AimPoint => transform.position + Vector3.up * (IsVehicle ? .7f : 1.2f);
        public Vector3 Velocity => car != null ? car.Motion : pedestrian != null ? pedestrian.Motion : Vector3.zero;
        public float HitRadius => IsVehicle ? 1.1f : pedestrian != null ? pedestrian.HitRadius : .95f;
        public void Initialize(TankTownGame owner)
        {
            game = owner; car = GetComponent<TownTrafficCar>(); pedestrian = GetComponent<TownPedestrian>();
            game.World.MovingTargets.Add(this);
        }
    }
}
