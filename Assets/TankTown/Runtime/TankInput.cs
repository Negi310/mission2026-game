using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TankTown
{
    // Hardware adapters call these methods on Unity's main thread.
    public sealed class TankInput : MonoBehaviour
    {
        public event Action<int> PadPressed;
        public event Action StartPressed;
        public event Action PausePressed;
        public event Action HelpPressed;
        public event Action<TownStage> StagePressed;
        public float Steering { get; private set; }
        public int MicrobitPresses { get; private set; }
        public bool useHardware;
        public TankTownSettings settings;
        private float wheel;
        private readonly bool[] held = new bool[2];

        internal bool MenuConfirmHeld
        {
            get
            {
                var keys = Keyboard.current;
                var pad = Gamepad.current;
                var microbit = MicrobitWheel.current;
                return held[0] || held[1]
                    || (keys != null && (keys.spaceKey.isPressed || keys.jKey.isPressed || keys.kKey.isPressed || keys.enterKey.isPressed))
                    || (pad != null && (pad.leftShoulder.isPressed || pad.rightShoulder.isPressed || pad.startButton.isPressed))
                    || (microbit != null && microbit.added && microbit.horn.isPressed)
                    || (Mouse.current != null && Mouse.current.leftButton.isPressed);
            }
        }

        private void Awake() => gameObject.AddComponent<MicrobitController>();

        public void SetWheel(float normalized) => wheel = Mathf.Clamp(normalized, -1, 1);

        public void SetPressure(int pad, float normalized)
        {
            if (pad < 0 || pad > 1 || settings == null) return;
            if (normalized <= Mathf.Min(settings.pressureRelease, settings.pressureThreshold * 0.8f)) held[pad] = false;
            if (!held[pad] && normalized >= settings.pressureThreshold)
            {
                held[pad] = true;
                PadPressed?.Invoke(pad);
            }
        }

        public void PulsePad(int pad)
        {
            if (pad == 0 || pad == 1) PadPressed?.Invoke(pad);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var microbit = MicrobitWheel.current;
            if (microbit != null && !microbit.added) microbit = null;
            Steering = microbit != null ? microbit.steering.ReadValue() : useHardware ? wheel : Gamepad.current?.leftStick.x.ReadValue() ?? 0;
            if (microbit != null && microbit.horn.wasPressedThisFrame) { MicrobitPresses++; PulsePad(0); }
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) StagePressed?.Invoke(TownStage.Town);
                if (keyboard.digit2Key.wasPressedThisFrame) StagePressed?.Invoke(TownStage.BigCity);
                if (keyboard.digit3Key.wasPressedThisFrame) StagePressed?.Invoke(TownStage.CandyTown);
                if (keyboard.digit4Key.wasPressedThisFrame) StagePressed?.Invoke(TownStage.AmusementPark);
                if (!useHardware && microbit == null)
                {
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) Steering = -1;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) Steering = 1;
                }
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.jKey.wasPressedThisFrame) PulsePad(0);
                if (keyboard.kKey.wasPressedThisFrame) PulsePad(1);
                if (keyboard.enterKey.wasPressedThisFrame) StartPressed?.Invoke();
                if (keyboard.escapeKey.wasPressedThisFrame) PausePressed?.Invoke();
                if (keyboard.hKey.wasPressedThisFrame) HelpPressed?.Invoke();
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.leftShoulder.wasPressedThisFrame) PulsePad(0);
                if (pad.rightShoulder.wasPressedThisFrame) PulsePad(1);
                if (pad.startButton.wasPressedThisFrame) StartPressed?.Invoke();
            }
        }

        private void OnDisable()
        {
            Steering = wheel = 0;
            held[0] = held[1] = false;
        }
    }
}
