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
            float iconSize = game.Settings.pickupIconSize;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i]; int row = i / 4, columns = Mathf.Min(4, entries.Count - row * 4);
                float column = i % 4 - (columns - 1) * .5f;
                float desiredX = column * game.Settings.pickupIconSpacing;
                entry.X = Mathf.SmoothDamp(entry.X, desiredX, ref entry.XVelocity, .045f);
                entry.Row = Mathf.SmoothDamp(entry.Row, row, ref entry.RowVelocity, .045f);
                float stagger = columns > 1 ? Mathf.Sin((i % 4) * 2.3f) * .10f : 0;
                var target = game.Tank.transform.position + Vector3.up * (game.Settings.pickupIconHeight * scale + entry.Row * iconSize * .7f + stagger) + view.right * entry.X;
                float flyTime = Mathf.Clamp(game.Settings.pickupFlyTime, .1f, duration * .6f);
                float flight = Mathf.Clamp01(entry.Age / flyTime);
                float blend = 1 - Mathf.Pow(1 - flight, 3);
                var position = Vector3.Lerp(entry.Origin, target, blend) + Vector3.up * (Mathf.Sin(flight * Mathf.PI) * .25f);
                float arrival = Mathf.Clamp01((entry.Age - flyTime) / .12f);
                float pop = 1 + Mathf.Sin(arrival * Mathf.PI) * .15f;
                var rotation = view.rotation * Quaternion.Euler(0, 0, -column * 7 * blend);
                float opacity = Mathf.Clamp01((duration - entry.Age) / .28f);
                entry.Pickup.Display(position, rotation, Mathf.Lerp(entry.InitialSize, iconSize, blend) * pop, opacity, 20 + i);
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
