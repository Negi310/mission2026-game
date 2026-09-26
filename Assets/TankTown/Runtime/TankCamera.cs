using UnityEngine;

namespace TankTown
{
    public sealed class TankCamera : MonoBehaviour
    {
        public Transform Target { get; set; }
        private Vector3 velocity;
        public void Snap()
        {
            if (Target == null) return;
            transform.position = Target.position - Target.forward * 12 + Vector3.up * 9;
            transform.LookAt(Target.position + Target.forward * 5 + Vector3.up);
            velocity = Vector3.zero;
        }
        private void LateUpdate()
        {
            if (Target == null) return;
            var focus = Target.position + Vector3.up * 1.5f;
            var desired = Target.position - Target.forward * 12 + Vector3.up * 9;
            var delta = desired - focus;
            if (Physics.SphereCast(focus, 0.4f, delta.normalized, out var hit, delta.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore))
                desired = hit.point + hit.normal * 0.6f;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.18f);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Target.position + Target.forward * 5 + Vector3.up - transform.position), 1 - Mathf.Exp(-8 * Time.deltaTime));
        }
    }
}
