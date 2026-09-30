using UnityEngine;

namespace TankTown
{
    public sealed class TankCamera : MonoBehaviour
    {
        public Transform Target { get; set; }
        private Vector3 velocity, smoothPosition;
        private float kick;
        public void Kick(float amount) => kick = Mathf.Min(.22f, kick + amount);
        public void Snap()
        {
            if (Target == null) return;
            var body = Target.GetComponent<Rigidbody>();
            var position = body != null ? body.position : Target.position;
            var forward = body != null ? body.rotation * Vector3.forward : Target.forward;
            transform.position = position - forward * 12 + Vector3.up * 9;
            transform.LookAt(position + forward * 5 + Vector3.up);
            velocity = Vector3.zero;
            smoothPosition = transform.position;
        }
        private void LateUpdate()
        {
            if (Target == null || Time.deltaTime <= 0) return;
            var focus = Target.position + Vector3.up * 1.5f;
            var desired = Target.position - Target.forward * 12 + Vector3.up * 9;
            var delta = desired - focus;
            if (Physics.SphereCast(focus, 0.4f, delta.normalized, out var hit, delta.magnitude, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore))
                desired = hit.point + hit.normal * 0.6f;
            smoothPosition = Vector3.SmoothDamp(smoothPosition, desired, ref velocity, 0.18f);
            kick = Mathf.MoveTowards(kick, 0, Time.deltaTime * .9f);
            transform.position = smoothPosition + Random.insideUnitSphere * kick;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Target.position + Target.forward * 5 + Vector3.up - transform.position), 1 - Mathf.Exp(-8 * Time.deltaTime));
        }
    }
}
