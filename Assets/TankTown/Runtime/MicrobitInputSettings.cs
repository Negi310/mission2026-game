using System;

namespace TankTown
{
    [Serializable]
    public sealed class MicrobitInputSettings
    {
        public string transport = "auto";
        public string serialPort = "";
        public string bluetoothDevice = "";
        public string serviceUuid = "6e400001-b5a3-f393-e0a9-e50e24dcca9e";
        // Empty selects the notify/indicate characteristic in the UART service.
        public string receiveUuid = "";
        public float angleCenter = 0;
        public float fullSteerAngle = 90;
        public float deadzoneDegrees = 3;
        public bool invertSteering;
        public int piezoThreshold = 350;
        public int piezoRelease = 180;
        public float piezoCooldownSeconds = .12f;
        public float timeoutSeconds = .35f;

        internal void Validate()
        {
            transport = (transport ?? "auto").Trim().ToLowerInvariant();
            if (transport != "auto" && transport != "usb" && transport != "serial" && transport != "bluetooth" && transport != "off")
                throw new ArgumentException("transport must be auto, usb, serial, bluetooth or off");
            if (!Finite(fullSteerAngle) || fullSteerAngle <= 0 || fullSteerAngle > 90 || !Finite(angleCenter) || Math.Abs(angleCenter) > 90 ||
                !Finite(deadzoneDegrees) || deadzoneDegrees < 0 || deadzoneDegrees >= fullSteerAngle ||
                piezoThreshold < 1 || piezoThreshold > 1023 || piezoRelease < 0 || piezoRelease >= piezoThreshold ||
                !Finite(piezoCooldownSeconds) || piezoCooldownSeconds < 0 || piezoCooldownSeconds > 2 ||
                !Finite(timeoutSeconds) || timeoutSeconds < .1f || timeoutSeconds > 5)
                throw new ArgumentException("Invalid micro:bit angle, piezo or timeout settings");
            if (!Guid.TryParse(serviceUuid, out _) || (!string.IsNullOrWhiteSpace(receiveUuid) && !Guid.TryParse(receiveUuid, out _)))
                throw new ArgumentException("Invalid Bluetooth serviceUuid/receiveUuid");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal float NormalizeAngle(float angle)
        {
            float normalized = Math.Max(-1, Math.Min(1, (angle - angleCenter) / fullSteerAngle));
            return ApplyDeadzone(normalized, deadzoneDegrees / fullSteerAngle) * (invertSteering ? -1 : 1);
        }
        internal static float ApplyDeadzone(float value, float deadzone) => Math.Abs(value) <= deadzone ? 0 :
            Math.Sign(value) * (Math.Abs(value) - deadzone) / (1 - deadzone);
    }
}
