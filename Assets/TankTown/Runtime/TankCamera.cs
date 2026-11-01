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
        public TankItems Items { get; set; }
        public Vector3 Offset { get; set; } = new Vector3(0, 3, -9);
        public Vector3 BeamOffset { get; set; } = new Vector3(9, 10, -15);
        public float BeamLookAhead { get; set; } = 40;
        public float BeamShakeAmplitude { get; set; }
        internal float BeamViewBlend => beamView;
        internal Vector3 BeamShakeOffset => beamShakeOffset;
        public float TurnSmoothTime { get; set; } = .3f;
        public float MaxTurnSpeed { get; set; } = 135;
        public float MaxTurnAcceleration { get; set; } = 240;
        public bool Paused { get; set; }
        public float FadeOpacity => Mathf.SmoothStep(0, 1, fadeProgress);
        public bool TransitionCovered => fadeProgress >= 1;

        private Vector3 anchor, anchorVelocity, previousDesired;
        private float yaw, yawVelocity, heightVelocity, scale = 1, scaleVelocity;
        private float fadeProgress, fadeTarget, fadeStartAt;
        private float beamView, beamViewVelocity;
        private float beamShakeLevel, beamShakeTime;
        private Vector3 beamShakeOffset;
        private bool initialized;
        private Collider[] occlusionHits = new Collider[256];
        private readonly HashSet<Renderer> occluded = new HashSet<Renderer>();
        private readonly Dictionary<Renderer, float> visibility = new Dictionary<Renderer, float>();
        private readonly Dictionary<DestructibleBlock, Renderer[]> blockRenderers = new Dictionary<DestructibleBlock, Renderer[]>();
        private readonly List<Renderer> tracked = new List<Renderer>();
        private readonly HashSet<DestructibleBlock> exposed = new HashSet<DestructibleBlock>();
        private readonly List<DestructibleBlock> exposedSnapshot = new List<DestructibleBlock>();
        private readonly HashSet<BlockBuilding> changedBuildings = new HashSet<BlockBuilding>();
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
            beamView = beamViewVelocity = 0;
            ResetBeamShake();
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
            // Blend into a stable off-axis view; changes in the nearest hit never swing the camera.
            beamView = Mathf.SmoothDamp(beamView, Items != null && Items.StopsForward ? 1 : 0,
                ref beamViewVelocity, .4f, float.PositiveInfinity, dt);
            var mount = Quaternion.Euler(0, yaw, 0);
            var viewOffset = Vector3.Lerp(Offset, BeamOffset, beamView);
            var beamDirection = new Vector3(-BeamOffset.x, 1.5f - BeamOffset.y, BeamLookAhead - BeamOffset.z);
            float beamYaw = Mathf.Atan2(beamDirection.x, beamDirection.z) * Mathf.Rad2Deg;
            float beamPitch = -Mathf.Atan2(beamDirection.y, new Vector2(beamDirection.x, beamDirection.z).magnitude) * Mathf.Rad2Deg;
            // Rotation stays independent of the laser's small positional recoil.
            // No roll, FOV change or terrain-driven pitch in either view.
            transform.rotation = mount * Quaternion.Euler(beamPitch * beamView, beamYaw * beamView, 0);
            anchor = Vector3.SmoothDamp(anchor, Target.position, ref anchorVelocity, .12f, float.PositiveInfinity, dt);
            float desiredScale = Target.GetComponent<TankMotor>()?.VehicleScale ?? 1;
            scale = Mathf.SmoothDamp(scale, desiredScale, ref scaleVelocity, .8f, 1.2f, dt);
            var desired = anchor + mount * viewOffset * scale;
            var prediction = desired - previousDesired;
            prediction.y = 0;
            var ahead = desired + Vector3.ClampMagnitude(prediction / dt * .35f, 10);
            desired.y = Mathf.Max(Target.position.y + viewOffset.y * scale, Mathf.Max(GroundFloor(desired), GroundFloor(ahead)));
            float height = Mathf.SmoothDamp(transform.position.y - beamShakeOffset.y, desired.y, ref heightVelocity, .4f, 10, dt);
            // Only terrain can require clearance. Buildings fade instead of pushing/zooming the camera.
            desired.y = Mathf.Max(height, GroundFloor(desired));
            transform.position = desired;
            previousDesired = desired;
            // Stable framing drives both follow smoothing and foreground fading;
            // visual recoil must not feed back into either calculation.
            UpdateOcclusion(dt);
            UpdateBeamShake(dt);
            if (beamShakeOffset.sqrMagnitude > 0)
            {
                var shaken = desired + beamShakeOffset;
                shaken.y = Mathf.Max(shaken.y, GroundFloor(shaken));
                beamShakeOffset = shaken - desired;
                transform.position = shaken;
            }
        }

        private void UpdateBeamShake(float dt)
        {
            bool active = Items != null && Items.StopsForward && BeamShakeAmplitude > 0;
            beamShakeLevel = Mathf.MoveTowards(beamShakeLevel, active ? 1 : 0, dt / (active ? .18f : .24f));
            if (beamShakeLevel <= 0) { ResetBeamShake(); return; }
            const float horizontalHz = 18, detailHz = 23, verticalHz = 21;
            // Keep phase continuous when frame time changes. At lower frame rates,
            // keep the main wave below Nyquist so it cannot vanish between samples.
            float phaseRate = Mathf.Min(1, .45f / (horizontalHz * dt));
            beamShakeTime = Mathf.Repeat(beamShakeTime + dt * phaseRate, 1);
            float phase = beamShakeTime * Mathf.PI * 2;
            var motion = new Vector3(.72f * Mathf.Sin(phase * horizontalHz) + .22f * Mathf.Sin(phase * detailHz),
                .3f * Mathf.Sin(phase * verticalHz), 0);
            float strength = Mathf.Clamp(BeamShakeAmplitude, 0, .4f) * Mathf.SmoothStep(0, 1, beamShakeLevel);
            beamShakeOffset = transform.rotation * motion * strength;
        }

        private void ResetBeamShake()
        {
            beamShakeLevel = beamShakeTime = 0;
            beamShakeOffset = Vector3.zero;
        }

        private void UpdateOcclusion(float dt)
        {
            occluded.Clear();
            changedBuildings.Clear();
            var tankPoint = Target.position + Vector3.up * (.7f * scale);
            // The elevated view can see through a different storey than the line back to the tank.
            // Clear the nearby foreground only, so buildings being burned ahead remain visible.
            var viewPoint = transform.position + transform.forward * Vector3.Distance(transform.position, tankPoint);
            var endpoint = Vector3.Lerp(tankPoint, viewPoint, beamView);
            float radius = Mathf.Lerp(.8f, 5, beamView) * scale;
            int count;
            while (true)
            {
                count = Physics.OverlapCapsuleNonAlloc(transform.position, endpoint, radius, occlusionHits,
                    (TownPhysics.StructureMask | 1 << TownPhysics.DebrisLayer) & ~TownPhysics.GroundMask, QueryTriggerInteraction.Ignore);
                if (count < occlusionHits.Length) break;
                System.Array.Resize(ref occlusionHits, occlusionHits.Length * 2);
            }
            for (int i = 0; i < count; i++)
            {
                Renderer[] renderers;
                var flying = occlusionHits[i].GetComponentInParent<FlyingDebris>();
                if (flying != null) renderers = flying.Renderers;
                else
                {
                    var block = occlusionHits[i].GetComponentInParent<DestructibleBlock>();
                    if (block == null) continue;
                    if (block.Building != null && !block.Detached && exposed.Add(block))
                    {
                        block.ExposeForCamera(true); changedBuildings.Add(block.Building);
                    }
                    if (!blockRenderers.TryGetValue(block, out renderers))
                    {
                        renderers = block.GetComponentsInChildren<Renderer>();
                        blockRenderers.Add(block, renderers);
                    }
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
            exposedSnapshot.Clear(); exposedSnapshot.AddRange(exposed);
            foreach (var block in exposedSnapshot)
            {
                if (block == null) { exposed.Remove(block); continue; }
                bool fading = false;
                if (blockRenderers.TryGetValue(block, out var renderers))
                    foreach (var renderer in renderers) if (renderer != null && visibility.ContainsKey(renderer)) { fading = true; break; }
                if (fading) continue;
                block.ExposeForCamera(false); exposed.Remove(block);
                if (block.Building != null) changedBuildings.Add(block.Building);
            }
            foreach (var building in changedBuildings) building.Presentation?.Tick(transform.position);
        }

        internal void InheritOcclusion(Renderer source, Renderer fragment)
        {
            float value = visibility.TryGetValue(source, out var current) ? current : 1;
            if (value < 1) { SetVisibility(fragment, value); visibility[fragment] = value; }
        }

        private void SetVisibility(Renderer renderer, float value)
        {
            renderer.GetPropertyBlock(properties);
            properties.SetFloat(VisibilityId, value);
            renderer.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
            transform.position -= beamShakeOffset;
            ResetBeamShake();
            foreach (var pair in visibility) if (pair.Key != null) SetVisibility(pair.Key, 1);
            visibility.Clear();
            foreach (var block in exposed) if (block != null) block.ExposeForCamera(false);
            exposed.Clear();
            blockRenderers.Clear();
        }
    }
}
