using UnityEngine;

namespace TankTown
{
    public sealed class DestructibleBlock : MonoBehaviour
    {
        public BlockBuilding Building { get; set; }
        public bool CanCrush { get; set; }
        public bool Detached { get; private set; }
        public int Column { get; set; }
        public int Level { get; set; }
        private TankTownGame game;

        public void Initialize(TankTownGame owner)
        {
            game = owner;
            game.RegisterBlock();
        }

        public void Break(Vector3 origin, float impulse)
        {
            if (Detached || game == null || !game.IsPlaying) return;
            Detached = true;
            game.ScoreBlock();
            // Debris collides with the ground but never traps the player or future shots.
            gameObject.layer = 2;
            var body = gameObject.AddComponent<Rigidbody>();
            body.mass = 0.7f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.AddExplosionForce(impulse, origin, game.Settings.blastRadius * 2, 1.5f, ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * impulse * 0.3f, ForceMode.Impulse);
            game.TrackDebris(gameObject);
            Destroy(gameObject, game.Settings.debrisLifetime);
        }
    }
}
