using System;
using System.Globalization;

namespace TankTown
{
    internal readonly struct MicrobitFrame
    {
        internal readonly float Steering;
        internal readonly ushort Horn;
        internal readonly bool IsAnalog;
        internal readonly float Angle;
        internal readonly int Piezo;
        internal MicrobitFrame(float steering, ushort horn) : this() { Steering = steering; Horn = horn; }
        private MicrobitFrame(float angle, int piezo, bool analog) : this()
        { Angle = angle; Piezo = piezo; IsAnalog = analog; Steering = angle / 90f; Horn = 0; }
        internal static bool TryParse(string line, out MicrobitFrame frame)
        {
            frame = default;
            if (line == null || line.Length > 64) return false;
            var fields = line.Trim().Split(',');
            if (fields.Length == 2)
            {
                if (!float.TryParse(fields[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float angle) ||
                    float.IsNaN(angle) || float.IsInfinity(angle) || angle < -90 || angle > 90 ||
                    !int.TryParse(fields[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int piezo) || piezo < 0 || piezo > 1023) return false;
                frame = new MicrobitFrame(angle, piezo, true); return true;
            }
            if (fields.Length != 3 || fields[0] != "TT1" ||
                !int.TryParse(fields[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int steer) ||
                steer < -1000 || steer > 1000 ||
                !ushort.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out ushort horn)) return false;
            frame = new MicrobitFrame(steer / 1000f, horn); return true;
        }
    }

    // First packet establishes a baseline, including after a board reset or USB reconnect.
    internal sealed class MicrobitSession
    {
        private bool active;
        private ushort horn;
        private double lastAt;
        private bool analog, armed;
        private double firedAt = double.NegativeInfinity;
        private readonly MicrobitInputSettings settings;
        internal MicrobitSession(MicrobitInputSettings settings = null) { this.settings = settings ?? new MicrobitInputSettings(); }
        internal bool Connected => active;
        internal float Steering { get; private set; }
        internal float Angle { get; private set; }
        internal int Piezo { get; private set; }
        internal bool Receive(MicrobitFrame frame, double now)
        {
            bool continuous = active && now - lastAt <= settings.timeoutSeconds && analog == frame.IsAnalog;
            bool fire;
            if (frame.IsAnalog)
            {
                if (!continuous) { armed = false; firedAt = double.NegativeInfinity; }
                if (frame.Piezo <= settings.piezoRelease) armed = true;
                fire = continuous && armed && frame.Piezo >= settings.piezoThreshold && now - firedAt >= settings.piezoCooldownSeconds;
                // Consume an edge even during cooldown; a held peak must never fire later.
                if (frame.Piezo >= settings.piezoThreshold) armed = false;
                if (fire) firedAt = now;
                Angle = frame.Angle; Piezo = frame.Piezo;
                Steering = settings.NormalizeAngle(frame.Angle);
            }
            else
            {
                fire = continuous && (ushort)(frame.Horn - horn) > 0 && (ushort)(frame.Horn - horn) < 32768;
                Angle = frame.Steering * 90; Piezo = 0;
                Steering = MicrobitInputSettings.ApplyDeadzone(frame.Steering, .06f) * (settings.invertSteering ? -1 : 1);
            }
            active = true; analog = frame.IsAnalog; horn = frame.Horn; lastAt = now;
            return fire;
        }
        internal bool Expire(double now)
        {
            if (!active || now - lastAt <= settings.timeoutSeconds) return false;
            Reset(); return true;
        }
        internal void Reset() { active = armed = false; Steering = Angle = Piezo = 0; firedAt = double.NegativeInfinity; }
    }
}
