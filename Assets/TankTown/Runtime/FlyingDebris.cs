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
        internal DebrisFade Fade { get; set; }
        private TankTownGame game;
        private Rigidbody body;
        private float lowSpeedSince = -1;

        public void Initialize(TankTownGame owner, DestructibleBlock source)
        {
            game = owner; Source = source; Generation = game.ExplosionGeneration;
            body = GetComponent<Rigidbody>();
            Renderers = GetComponentsInChildren<Renderer>();
        }

        internal void TickPhysics()
        {
            if (body.IsSleeping()) return;
            // 大爆発や強化射撃でも高速衝突を解決できる範囲に収める。
            float speed = body.linearVelocity.sqrMagnitude;
            if (speed > 3600)
                body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, 60);
            if (speed > 144)
            {
                lowSpeedSince = -1;
                if (body.collisionDetectionMode != CollisionDetectionMode.ContinuousSpeculative) body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
            else if (speed < 64)
            {
                if (lowSpeedSince < 0) lowSpeedSince = Time.time;
                if (Time.time - lowSpeedSince > .15f && body.collisionDetectionMode != CollisionDetectionMode.Discrete) body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
            else lowSpeedSince = -1;
        }

        private void OnDestroy()
        {
            if (game != null) game.UntrackDebris(this);
        }

        private void OnCollisionEnter(Collision hit)
        {
            if (ImpactSpent || !game.IsPlaying || Generation >= game.Settings.debrisChainLimit) return;
            // Most debris contacts are slow bounces. Avoid a hierarchy lookup and
            // contact-point iteration unless the relative speed can trigger a blast.
            if (hit.relativeVelocity.sqrMagnitude < game.Settings.debrisImpactSpeed * game.Settings.debrisImpactSpeed) return;
            var target = hit.collider.GetComponentInParent<DestructibleBlock>();
            if (target == null || target.Detached) return;
            float closingSpeed = 0; Vector3 point = Vector3.zero; int contacts = 0;
            for (int i = 0; i < hit.contactCount; i++)
            {
                var contact = hit.GetContact(i);
                float speed = Mathf.Abs(Vector3.Dot(hit.relativeVelocity, contact.normal));
                if (speed > closingSpeed + .01f) { closingSpeed = speed; point = contact.point; contacts = 1; }
                else if (Mathf.Abs(speed - closingSpeed) <= .01f) { point += contact.point; contacts++; }
            }
            // 速い横滑り、床・戦車への接触、ゆっくりした落下では炸裂しない。
            if (closingSpeed < game.Settings.debrisImpactSpeed) return;
            // A contact face can contain four corners; use its centre rather than an arbitrary corner.
            point /= Mathf.Max(1, contacts);
            if (game.TryDebrisBlast(point, Generation + 1)) ImpactSpent = true;
        }
    }
}
