using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    // Reuse the field quad, after tank interpolation and camera movement have finished.
    [DefaultExecutionOrder(90)]
    public sealed class TankPickupDisplay : MonoBehaviour
    {
        private sealed class Entry
        {
            public TankPickup Pickup;
            public Vector3 Origin;
            public float InitialSize, Age, X, XVelocity, Row, RowVelocity;
        }
        private readonly List<Entry> entries = new List<Entry>();
        private TankTownGame game;
        internal int Count => entries.Count;
        internal void Initialize(TankTownGame owner) => game = owner;
        internal void Show(TankPickup pickup)
        {
            entries.Add(new Entry { Pickup = pickup, Origin = pickup.Icon.position, InitialSize = pickup.Icon.localScale.x });
        }
        private void LateUpdate()
        {
            if (game == null) return;
            if (game.State == RoundState.Results || game.Tank.Respawning) { Clear(); return; }
            if (!game.IsPlaying || Time.deltaTime <= 0) return;
            float duration = Mathf.Max(.5f, game.Settings.pickupDisplayDuration);
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i]; entry.Age += Time.deltaTime;
                if (entry.Pickup == null || !entry.Pickup.isActiveAndEnabled || entry.Age >= duration)
                {
                    if (entry.Pickup != null) entry.Pickup.gameObject.SetActive(false);
                    entries.RemoveAt(i);
                }
            }
            var view = game.CameraRig.transform;
            float scale = game.Tank.VehicleScale;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i]; int row = i / 4, columns = Mathf.Min(4, entries.Count - row * 4);
                float desiredX = (i % 4 - (columns - 1) * .5f) * 1.35f;
                entry.X = Mathf.SmoothDamp(entry.X, desiredX, ref entry.XVelocity, .09f);
                entry.Row = Mathf.SmoothDamp(entry.Row, row, ref entry.RowVelocity, .09f);
                var target = game.Tank.transform.position + Vector3.up * (2.8f * scale + entry.Row * 1.25f) + view.right * entry.X;
                float flight = Mathf.Clamp01(entry.Age / Mathf.Clamp(game.Settings.pickupFlyTime, .1f, duration * .6f));
                float blend = Mathf.SmoothStep(0, 1, flight);
                var position = Vector3.Lerp(entry.Origin, target, blend) + Vector3.up * (Mathf.Sin(flight * Mathf.PI) * .55f);
                float opacity = Mathf.Clamp01((duration - entry.Age) / .28f);
                entry.Pickup.Display(position, view.rotation, Mathf.Lerp(entry.InitialSize, 1.15f, blend), opacity);
            }
        }
        internal void Clear()
        {
            foreach (var entry in entries) if (entry.Pickup != null) entry.Pickup.gameObject.SetActive(false);
            entries.Clear();
        }
        private void OnDisable() => Clear();
    }
}
