using UnityEngine;

namespace TankTown
{
    [DefaultExecutionOrder(50)]
    public sealed class TankCamera : MonoBehaviour
    {
        public Transform Target { get; set; }
        public Transform AimTarget { get; set; }
        public Vector3 Offset { get; set; } = new Vector3(0, 3.0f, -9.0f);
        private Vector3 velocity, smoothPosition;
        private float kick;
        public void Kick(float amount) => kick = Mathf.Min(.12f, kick + amount);
        private Quaternion ViewRotation => AimTarget != null ? AimTarget.rotation : Target.rotation;
        public void Snap()
        {
            if (Target == null) return;
            var body = Target.GetComponent<Rigidbody>();
            var position = body != null ? body.position : Target.position;
            var rotation = ViewRotation;
            float scale = Target.GetComponent<TankMotor>()?.VehicleScale ?? 1;
            transform.position = position + rotation * Offset * scale;
            transform.rotation = rotation;
            velocity = Vector3.zero;
            smoothPosition = transform.position;
        }
        private void LateUpdate()
        {
            if (Target == null) return;
            // Match the rendered turret every frame, including pitch and roll on slopes.
            // Position smoothing, obstruction avoidance and recoil cannot delay the heading.
            transform.rotation = ViewRotation;
            if (Time.deltaTime <= 0) return;
            float scale = Target.GetComponent<TankMotor>()?.VehicleScale ?? 1;
            var focus = Target.position + Vector3.up * (1.5f * scale);
            var desired = Target.position + transform.rotation * Offset * scale;
            var delta = desired - focus;
            if (delta.sqrMagnitude > .001f && Physics.SphereCast(focus, .2f, delta.normalized, out var hit, delta.magnitude, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore))
                desired = hit.point + hit.normal * .25f;
            smoothPosition = Vector3.SmoothDamp(smoothPosition, desired, ref velocity, .045f);
            kick = Mathf.MoveTowards(kick, 0, Time.deltaTime * .9f);
            transform.position = smoothPosition + Random.insideUnitSphere * kick;
        }
    }
}
