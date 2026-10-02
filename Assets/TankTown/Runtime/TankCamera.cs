using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    [DefaultExecutionOrder(50)]
    public sealed class TankCamera : MonoBehaviour
    {
        public Transform Target { get; set; }
        public Transform AimTarget { get; set; }
        public TownTerrain Terrain { get; set; }
        public Vector3 Offset { get; set; } = new Vector3(0, 3, -9);
        public float TurnSmoothTime { get; set; } = .3f;
        public float MaxTurnSpeed { get; set; } = 135;
        public float MaxTurnAcceleration { get; set; } = 240;
        public bool Paused { get; set; }
        public float FadeOpacity => Mathf.SmoothStep(0, 1, fadeProgress);
        public bool TransitionCovered => fadeProgress >= 1;

        private Vector3 anchor, anchorVelocity, previousDesired;
        private float yaw, yawVelocity, heightVelocity, scale = 1, scaleVelocity;
        private float fadeProgress, fadeTarget, fadeStartAt;
        private bool initialized;
        private readonly Collider[] occlusionHits = new Collider[256];
        private readonly HashSet<Renderer> occluded = new HashSet<Renderer>();
        private readonly Dictionary<Renderer, float> visibility = new Dictionary<Renderer, float>();
        private readonly Dictionary<DestructibleBlock, Renderer[]> blockRenderers = new Dictionary<DestructibleBlock, Renderer[]>();
        private readonly List<Renderer> tracked = new List<Renderer>();
        private MaterialPropertyBlock properties;
        private static readonly int VisibilityId = Shader.PropertyToID("_CameraVisibility");

        private void Awake() => properties = new MaterialPropertyBlock();

        private float AimYaw
        {
            get
            {
                var direction = (AimTarget != null ? AimTarget : Target).forward;
                return direction.x * direction.x + direction.z * direction.z > .0001f
                    ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg : yaw;
            }
        }

        public void FadeOut(float delay = 0) { fadeTarget = 1; fadeStartAt = Time.time + Mathf.Max(0, delay); }
        public void FadeIn() { fadeTarget = 0; fadeStartAt = Time.time + .06f; }
        public void CancelTransition() { fadeTarget = fadeProgress = 0; }

        public void Snap()
        {
            if (Target == null) return;
            var body = Target.GetComponent<Rigidbody>();
            anchor = body != null ? body.position : Target.position;
            scale = Target.GetComponent<TankMotor>()?.VehicleScale ?? 1;
            yaw = AimYaw;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            var position = anchor + transform.rotation * Offset * scale;
            position.y = Mathf.Max(position.y, GroundFloor(position));
            transform.position = previousDesired = position;
            anchorVelocity = Vector3.zero;
            yawVelocity = heightVelocity = scaleVelocity = 0;
            initialized = true;
        }

        private float GroundFloor(Vector3 position) => Terrain != null ? Terrain.HeightAt(position) + 1.5f : float.NegativeInfinity;

        private void LateUpdate()
        {
            if (Target == null || Paused || Time.deltaTime <= 0) return;
            if (!initialized) Snap();
            if (Time.time >= fadeStartAt)
                fadeProgress = Mathf.MoveTowards(fadeProgress, fadeTarget, Time.deltaTime / (fadeTarget > 0 ? .2f : .35f));

            // A stalled frame must not produce a large catch-up rotation or zoom.
            float dt = Mathf.Min(Time.deltaTime, 1f / 30);
            float remaining = dt, desiredYaw = AimYaw;
            float frequency = 2 / Mathf.Max(.1f, TurnSmoothTime);
            while (remaining > .00001f)
            {
                float step = Mathf.Min(remaining, 1f / 120);
                float error = Mathf.DeltaAngle(yaw, desiredYaw);
                float acceleration = Mathf.Clamp(frequency * frequency * error - 2 * frequency * yawVelocity, -MaxTurnAcceleration, MaxTurnAcceleration);
                float nextVelocity = Mathf.Clamp(yawVelocity + acceleration * step, -MaxTurnSpeed, MaxTurnSpeed);
                yaw = Mathf.Repeat(yaw + (yawVelocity + nextVelocity) * .5f * step, 360);
                yawVelocity = nextVelocity;
                remaining -= step;
            }
            // Keep the horizon level; terrain slope, body lean and recoil do not rotate the view.
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            anchor = Vector3.SmoothDamp(anchor, Target.position, ref anchorVelocity, .12f, float.PositiveInfinity, dt);
            float desiredScale = Target.GetComponent<TankMotor>()?.VehicleScale ?? 1;
            scale = Mathf.SmoothDamp(scale, desiredScale, ref scaleVelocity, .8f, 1.2f, dt);
            var desired = anchor + transform.rotation * Offset * scale;
            var prediction = desired - previousDesired;
            prediction.y = 0;
            var ahead = desired + Vector3.ClampMagnitude(prediction / dt * .35f, 10);
            desired.y = Mathf.Max(Target.position.y + Offset.y * scale, Mathf.Max(GroundFloor(desired), GroundFloor(ahead)));
            float height = Mathf.SmoothDamp(transform.position.y, desired.y, ref heightVelocity, .4f, 10, dt);
            // Only terrain can require clearance. Buildings fade instead of pushing/zooming the camera.
            desired.y = Mathf.Max(height, GroundFloor(desired));
            transform.position = desired;
            previousDesired = desired;
            UpdateOcclusion(dt);
        }

        private void UpdateOcclusion(float dt)
        {
            occluded.Clear();
            int count = Physics.OverlapCapsuleNonAlloc(transform.position, Target.position + Vector3.up * (.7f * scale), .8f * scale,
                occlusionHits, TownPhysics.StructureMask & ~TownPhysics.GroundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var block = occlusionHits[i].GetComponentInParent<DestructibleBlock>();
                if (block == null) continue;
                if (!blockRenderers.TryGetValue(block, out var renderers))
                {
                    renderers = block.GetComponentsInChildren<Renderer>();
                    blockRenderers.Add(block, renderers);
                }
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    if (renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(VisibilityId)) continue;
                    occluded.Add(renderer);
                    if (!visibility.ContainsKey(renderer)) visibility.Add(renderer, 1);
                }
            }
            tracked.Clear(); tracked.AddRange(visibility.Keys);
            foreach (var renderer in tracked)
            {
                if (renderer == null) { visibility.Remove(renderer); continue; }
                float value = Mathf.MoveTowards(visibility[renderer], occluded.Contains(renderer) ? 0 : 1, dt * 4);
                SetVisibility(renderer, value);
                if (value >= 1) visibility.Remove(renderer); else visibility[renderer] = value;
            }
        }

        private void SetVisibility(Renderer renderer, float value)
        {
            renderer.GetPropertyBlock(properties);
            properties.SetFloat(VisibilityId, value);
            renderer.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
            foreach (var pair in visibility) if (pair.Key != null) SetVisibility(pair.Key, 1);
            visibility.Clear();
            blockRenderers.Clear();
        }
    }
}
