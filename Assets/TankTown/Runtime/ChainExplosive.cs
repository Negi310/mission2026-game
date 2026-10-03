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
        private TankTownGame game;
        private DestructibleBlock block;
        private float fuse;
        private Renderer shell;
        private MaterialPropertyBlock tint;

        public void Initialize(TankTownGame owner, float radius)
        {
            game = owner; Radius = radius;
            block = GetComponent<DestructibleBlock>();
            shell = GetComponentInChildren<Renderer>();
            tint = new MaterialPropertyBlock();
        }

        public void Ignite()
        {
            if (Primed || Detonated || game == null || !game.IsPlaying) return;
            Primed = true; fuse = .18f;
            Generation = game.ExplosionGeneration + 1;
        }

        private void Update()
        {
            if (!Primed || Detonated || !game.IsPlaying) return;
            fuse -= Time.deltaTime;
            if (shell != null)
            {
                tint.SetColor("_BaseColor", Color.Lerp(new Color(1, .08f, .025f), new Color(1, .95f, .1f), Mathf.PingPong(Time.time * 7, 1)));
                shell.SetPropertyBlock(tint);
            }
            if (fuse <= 0) Detonate();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (game == null || !game.IsPlaying || Primed || Detonated) return;
            if (collision.relativeVelocity.sqrMagnitude < 16) return;
            // Falling building parts and a vehicle can also set off a tank.
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
            game.ChainBlast(point, Radius, Generation);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
