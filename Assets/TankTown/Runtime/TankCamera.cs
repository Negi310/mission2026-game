using UnityEngine;

namespace TankTown
{
    public sealed class TankCamera : MonoBehaviour
    {
        public Transform Target { get; set; }
        public Vector3 Offset { get; set; } = new Vector3(0, 2.4f, -2.2f);
        private Vector3 velocity, smoothPosition;
        private float kick;
        public void Kick(float amount) => kick = Mathf.Min(.12f, kick + amount);
        private static Vector3 Aim(Vector3 position, Vector3 forward) => position + forward * 70 + Vector3.up * .72f;
        public void Snap()
        {
            if (Target == null) return;
            var body = Target.GetComponent<Rigidbody>();
            var position = body != null ? body.position : Target.position;
            var rotation = body != null ? body.rotation : Target.rotation;
            var forward = rotation * Vector3.forward;
            transform.position = position + rotation * Offset;
            transform.LookAt(Aim(position, forward));
            velocity = Vector3.zero;
            smoothPosition = transform.position;
        }
        private void LateUpdate()
        {
            if (Target == null || Time.deltaTime <= 0) return;
            var focus = Target.position + Vector3.up * 1.5f;
            var desired = Target.position + Target.rotation * Offset;
            var delta = desired - focus;
            if (delta.sqrMagnitude > .001f && Physics.SphereCast(focus, .2f, delta.normalized, out var hit, delta.magnitude, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore))
                desired = hit.point + hit.normal * .25f;
            smoothPosition = Vector3.SmoothDamp(smoothPosition, desired, ref velocity, .045f);
            kick = Mathf.MoveTowards(kick, 0, Time.deltaTime * .9f);
            transform.position = smoothPosition + Random.insideUnitSphere * kick;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Aim(Target.position, Target.forward) - transform.position), 1 - Mathf.Exp(-24 * Time.deltaTime));
        }
    }
}
