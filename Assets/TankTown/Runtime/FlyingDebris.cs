using UnityEngine;

namespace TankTown
{
    // 分裂した破片にも元の部材とは独立した物理衝突と連鎖世代を持たせる。
    public sealed class FlyingDebris : MonoBehaviour
    {
        public DestructibleBlock Source { get; private set; }
        public int Generation { get; private set; }
        public bool ImpactSpent { get; private set; }
        public Renderer[] Renderers { get; private set; }
        internal int RegistryIndex { get; set; } = -1;
        private TankTownGame game;
        private Rigidbody body;

        public void Initialize(TankTownGame owner, DestructibleBlock source)
        {
            game = owner; Source = source; Generation = game.ExplosionGeneration;
            body = GetComponent<Rigidbody>();
            Renderers = GetComponentsInChildren<Renderer>();
        }

        private void FixedUpdate()
        {
            // 大爆発や強化射撃でも高速衝突を解決できる範囲に収める。
            if (game.IsPlaying && body.linearVelocity.sqrMagnitude > 3600)
                body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, 60);
        }

        private void OnDestroy()
        {
            if (game != null) game.UntrackDebris(this);
        }

        private void OnCollisionEnter(Collision hit)
        {
            if (ImpactSpent || !game.IsPlaying || Generation >= game.Settings.debrisChainLimit) return;
            var target = hit.collider.GetComponentInParent<DestructibleBlock>();
            if (target == null || target.Detached) return;
            float closingSpeed = 0; Vector3 point = Vector3.zero;
            for (int i = 0; i < hit.contactCount; i++)
            {
                var contact = hit.GetContact(i);
                float speed = Mathf.Abs(Vector3.Dot(hit.relativeVelocity, contact.normal));
                if (speed > closingSpeed) { closingSpeed = speed; point = contact.point; }
            }
            // 速い横滑り、床・戦車への接触、ゆっくりした落下では炸裂しない。
            if (closingSpeed < game.Settings.debrisImpactSpeed) return;
            if (game.TryDebrisBlast(point, Generation + 1)) ImpactSpent = true;
        }
    }
}
