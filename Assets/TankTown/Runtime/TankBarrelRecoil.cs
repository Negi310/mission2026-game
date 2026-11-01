using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    // Weapon feedback moves visible barrel meshes only. The turret heading and
    // logical shot origin stay fixed, so neither the view nor damage queries shake.
    [DefaultExecutionOrder(20)]
    public sealed class TankBarrelRecoil : MonoBehaviour
    {
        private readonly List<(Transform part, Vector3 rest)> parts = new List<(Transform, Vector3)>();
        private TankTownGame game;
        private TankMotor tank;
        private float phase, blend;
        private Vector3 offset;
        internal float Displacement => offset.magnitude;

        internal void Initialize(TankTownGame owner, TankMotor motor, Transform muzzle)
        {
            game = owner; tank = motor;
            foreach (Transform child in transform)
                if (child != muzzle && child.GetComponentInChildren<Renderer>() != null)
                    parts.Add((child, child.localPosition));
        }

        private void LateUpdate()
        {
            if (game == null || tank == null) return;
            if (game.State == RoundState.Paused) return;
            if (!game.IsPlaying || tank.Relocating || game.Items == null || !game.Items.StopsForward)
            { Restore(); return; }
            float dt = Mathf.Min(Time.deltaTime, 1f / 30);
            if (dt <= 0) return;
            phase = Mathf.Repeat(phase + dt, 1);
            blend = Mathf.MoveTowards(blend, 1, dt / .12f);
            offset = Vector3.back * (.08f * Mathf.SmoothStep(0, 1, blend) * (.5f - .5f * Mathf.Cos(phase * Mathf.PI * 2 * 18)));
            foreach (var entry in parts) if (entry.part != null) entry.part.localPosition = entry.rest + offset;
        }

        private void Restore()
        {
            if (offset != Vector3.zero)
                foreach (var entry in parts) if (entry.part != null) entry.part.localPosition = entry.rest;
            offset = Vector3.zero; phase = blend = 0;
        }
        private void OnDisable() => Restore();
    }
}
