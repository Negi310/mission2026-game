using System;
using System.Text;

namespace TankTown
{
    // Captures short peaks and rejected lines before frame coalescing.
    internal sealed class MicrobitDiagnostics
    {
        private readonly object gate = new object();
        private Snapshot state = new Snapshot { Minimum = 1023, Status = "接続待ち", Chunk = "", Line = "" };
        internal struct Snapshot
        {
            internal string Status, Chunk, Line;
            internal long Bytes, Valid, Invalid;
            internal long BleCallbacks, BleCallbackErrors;
            internal string BleStage, BleError;
            internal float Angle;
            internal int Piezo, Minimum, Maximum;
        }
        internal void Status(string value) { lock (gate) { state.Status = value; } }
        internal void BleTrace(string value)
        {
            lock (gate)
            {
                if (value == "callback") state.BleCallbacks++;
                else if (value.StartsWith("callback error: ", StringComparison.Ordinal)) { state.BleCallbackErrors++; state.BleError = value; }
                else state.BleStage = value;
            }
        }
        internal void Bytes(byte[] bytes, int count)
        {
            if (count == 0) return;
            lock (gate)
            {
                state.Bytes += count;
                state.Chunk = Encoding.ASCII.GetString(bytes, 0, Math.Min(count, 64)).Replace("\r", "\\r").Replace("\n", "\\n");
            }
        }
        internal void Line(string text, bool valid)
        {
            lock (gate)
            {
                state.Line = text.Replace("\r", "\\r");
                if (valid) state.Valid++; else state.Invalid++;
            }
        }
        internal void Frame(MicrobitFrame frame)
        {
            if (!frame.IsAnalog) return;
            lock (gate)
            {
                state.Angle = frame.Angle; state.Piezo = frame.Piezo;
                state.Minimum = Math.Min(state.Minimum, frame.Piezo);
                state.Maximum = Math.Max(state.Maximum, frame.Piezo);
            }
        }
        internal void ResetPeaks() { lock (gate) { state.Minimum = 1023; state.Maximum = 0; } }
        internal Snapshot Read() { lock (gate) { return state; } }
    }
}
