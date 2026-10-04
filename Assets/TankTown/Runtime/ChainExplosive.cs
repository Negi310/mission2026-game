using UnityEngine;

namespace TankTown
{
    // Fuses defer propagation, so a city-wide chain never recursively calls Blast.
    public sealed class ChainExplosive : MonoBehaviour
    {
        public bool Primed { get; private set; }
        public bool Detonated { get; private set; }
        public float Radius { get; private set; }
        public int Generation { get; private set; }
        public bool IsFuelTank { get; private set; }
        public bool IsVehicle { get; private set; }
        public float PowerMultiplier { get; private set; } = 1;
        private TankTownGame game;
        private DestructibleBlock block;
        private float fuse;
        private Renderer shell;
        private MaterialPropertyBlock tint;
        private Collider tankCollider;

        public void Initialize(TankTownGame owner, float radius, bool fuelTank = false, bool vehicle = false)
        {
            game = owner; Radius = radius; IsFuelTank = fuelTank; IsVehicle = vehicle;
            block = GetComponent<DestructibleBlock>();
            tankCollider = GetComponent<Collider>();
            shell = GetComponentInChildren<Renderer>();
            tint = new MaterialPropertyBlock();
            if (!vehicle) gameObject.AddComponent<TownMovingTarget>().Initialize(game);
        }

        public void Ignite()
        {
            if (!IsFuelTank) Prime();
        }

        public bool IgniteFromBlast(Vector3 point)
        {
            if (game == null || !game.IsPlaying || Detonated) return false;
            if (IsFuelTank)
            {
                Vector3 nearest = tankCollider != null ? tankCollider.ClosestPoint(point) : transform.position;
                float distance = game.Settings.fuelIgnitionDistance;
                if ((point - nearest).sqrMagnitude > distance * distance) return false;
            }
            Prime();
            return Primed;
        }

        private void Prime()
        {
            if (Detonated || game == null || !game.IsPlaying) return;
            PowerMultiplier = Mathf.Max(PowerMultiplier, game.ExplosionPowerMultiplier);
            if (Primed) return;
            Primed = true; fuse = .18f;
            Generation = game.ExplosionGeneration + 1;
        }

        private void Update()
        {
            if (!Primed || Detonated || !game.IsPlaying) return;
            fuse -= Time.deltaTime;
            if (shell != null)
            {
                shell.GetPropertyBlock(tint);
                tint.SetColor("_BaseColor", Color.Lerp(new Color(1, .08f, .025f), new Color(1, .95f, .1f), Mathf.PingPong(Time.time * 7, 1)));
                shell.SetPropertyBlock(tint);
            }
            if (fuse <= 0) Detonate();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (game == null || !game.IsPlaying || Primed || Detonated || IsFuelTank) return;
            if (collision.relativeVelocity.sqrMagnitude < 16) return;
            // Cars and barrels may ignite from collisions; fuel needs a nearby blast.
            if (collision.rigidbody == null) return;
            block.Break(collision.GetContact(0).point, 0);
        }

        private void Detonate()
        {
            if (Detonated || !game.IsPlaying) return;
            Detonated = true;
            var point = transform.position;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            game.ChainBlast(point, Radius, Generation, IsFuelTank, IsVehicle, PowerMultiplier);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
