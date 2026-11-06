using System.Collections.Generic;

namespace TankTown
{
    // Preserve short low/high piezo samples received between rendered frames.
    internal sealed class MicrobitReceiveBuffer
    {
        private readonly object gate = new object();
        private readonly Queue<Sample> samples = new Queue<Sample>();
        private bool reset;
        internal void Reset() { lock (gate) { samples.Clear(); reset = true; } }
        internal void Enqueue(MicrobitFrame frame, double timestamp)
        {
            lock (gate)
            {
                if (samples.Count == 128) { samples.Clear(); reset = true; }
                samples.Enqueue(new Sample { Frame = frame, At = timestamp });
            }
        }
        internal bool Process(MicrobitSession session, double now, double timeout)
        {
            lock (gate)
            {
                if (reset) { session.Reset(); reset = false; }
                bool fire = false;
                while (samples.Count > 0)
                {
                    var sample = samples.Dequeue();
                    if (now - sample.At > timeout) { session.Reset(); continue; }
                    fire |= session.Receive(sample.Frame, sample.At);
                }
                session.Expire(now);
                return fire;
            }
        }
        private struct Sample { internal MicrobitFrame Frame; internal double At; }
    }
}
