using UnityEngine;

namespace TankTown
{
    // UI and the overview camera keep moving while the game world is frozen.
    internal sealed class TankTownRoundFlow
    {
        internal const float CoverSeconds = .10f;
        internal const float RevealSeconds = .12f;
        internal const float BeatSeconds = .22f;
        internal const float GoSeconds = .40f;
        internal const float IntroSeconds = CoverSeconds + RevealSeconds + BeatSeconds * 3;
        private readonly TankTownGame game;
        private readonly Camera camera;
        private readonly float overviewHeight, overviewFocusHeight;
        private readonly int playCullingMask;
        private float elapsed, goElapsed = GoSeconds, orbit = -36;
        private bool fromReload, cameraChanged, reloading, stageCoverShown;
        public float CoverOpacity { get; private set; }
        public string Countdown { get; private set; } = "";
        public float BeatProgress { get; private set; }

        public TankTownRoundFlow(TankTownGame owner, Camera view)
        {
            game = owner; camera = view;
            playCullingMask = camera.cullingMask;
            float highestRoof = 0;
            foreach (var building in game.World.Buildings)
                highestRoof = Mathf.Max(highestRoof, building.transform.position.y + building.Floors * game.Settings.floorHeight + 3);
            if (game.TitlePreview != null) { highestRoof = game.TitlePreview.HighestRoof; camera.cullingMask = (1 << TownTitlePreview.Layer) | (1 << 5); }
            // Orbit just above the tallest roof, at a constant height clear of all buildings.
            overviewHeight = Mathf.Max(38, highestRoof + 9);
            overviewFocusHeight = Mathf.Max(8, highestRoof * .35f);
            game.CameraRig.enabled = false;
            game.SetOverviewShadows(true);
            MoveOverview(0);
        }

        public void BeginStart(bool reload)
        {
            elapsed = 0; fromReload = reload; cameraChanged = false; stageCoverShown = false;
            CoverOpacity = reload ? 1 : 0;
            Countdown = ""; game.CameraRig.Paused = true;
        }

        public void BeginReplay() { elapsed = 0; reloading = false; Countdown = ""; }

        public void UsePlayCamera()
        {
            if (cameraChanged) return;
            cameraChanged = true;
            if (game.TitlePreview != null) game.TitlePreview.Root.SetActive(false);
            camera.cullingMask = playCullingMask;
            camera.fieldOfView = game.Settings.cameraFieldOfView;
            camera.farClipPlane = game.PlayCameraFarClip;
            game.SetOverviewShadows(false);
            game.CameraRig.CancelTransition();
            game.CameraRig.Snap();
            game.CameraRig.enabled = true;
        }

        public void Tick(float dt)
        {
            if (game.State == RoundState.Restarting)
            {
                elapsed += dt;
                CoverOpacity = Mathf.SmoothStep(0, 1, elapsed / CoverSeconds);
                if (elapsed >= CoverSeconds && !reloading) { reloading = true; game.ReloadForReplay(); }
                return;
            }
            if (game.State == RoundState.Starting)
            {
                // A loading hitch must not skip an entire number in the countdown.
                elapsed += Mathf.Min(dt, .08f);
                float coveredAt = fromReload ? 0 : CoverSeconds;
                if (elapsed < coveredAt)
                {
                    CoverOpacity = Mathf.SmoothStep(0, 1, elapsed / CoverSeconds);
                    return;
                }
                if (!cameraChanged)
                {
                    // Render one fully covered frame before revealing the new viewpoint.
                    CoverOpacity = 1;
                    if (game.Stage != game.BuiltStage && !stageCoverShown) { stageCoverShown = true; elapsed = coveredAt; return; }
                    if (!game.PrepareSelectedStage()) return;
                    UsePlayCamera(); CoverOpacity = 1; elapsed = coveredAt; return;
                }
                CoverOpacity = 1 - Mathf.SmoothStep(0, 1, (elapsed - coveredAt) / RevealSeconds);
                float countdownTime = elapsed - coveredAt - RevealSeconds;
                if (countdownTime < 0) return;
                if (countdownTime >= BeatSeconds * 3)
                {
                    game.FinishIntro(); goElapsed = 0;
                    Countdown = "GO!"; BeatProgress = 0;
                    game.PlayTone(660, .12f, .17f);
                    return;
                }
                int beat = Mathf.Min(2, Mathf.FloorToInt(countdownTime / BeatSeconds));
                string next = (3 - beat).ToString();
                if (next != Countdown) game.PlayTone(330, .07f, .11f);
                Countdown = next;
                BeatProgress = (countdownTime / BeatSeconds) % 1;
                return;
            }
            CoverOpacity = 0;
            if (game.IsPlaying && goElapsed < GoSeconds)
            {
                goElapsed += dt;
                BeatProgress = goElapsed / GoSeconds;
                Countdown = goElapsed < GoSeconds ? "GO!" : "";
            }
            else Countdown = "";
        }

        public void MoveOverview(float dt)
        {
            if (game.State != RoundState.Ready) return;
            // Constant, slow orbit: no sway, roll, zoom or randomized direction changes.
            orbit += Mathf.Min(dt, .05f) * 3.5f;
            float radians = orbit * Mathf.Deg2Rad;
            float extent = game.TitlePreview?.HalfExtent ?? game.World.HalfExtent;
            Vector3 position = new Vector3(Mathf.Sin(radians) * extent * 1.22f, overviewHeight, -Mathf.Cos(radians) * extent * 1.22f);
            Vector3 focus = new Vector3(0, overviewFocusHeight, 0);
            Quaternion rotation = Quaternion.LookRotation(focus - position);
            // Leave the left portion available for the title while revealing the town on the right.
            focus -= rotation * Vector3.right * extent * .32f;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position));
            camera.fieldOfView = 54; camera.farClipPlane = 1000;
        }
    }
}
