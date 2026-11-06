using System;
using System.Text;

namespace TankTown
{
    // BLE notifications and serial reads may split a line anywhere or contain several lines.
    internal sealed class MicrobitLineDecoder
    {
        private readonly StringBuilder line = new StringBuilder(64);
        private bool overflow;
        internal void Feed(byte[] bytes, int count, Action<MicrobitFrame> receive)
        {
            for (int i = 0; i < count; i++)
            {
                char c = (char)bytes[i];
                if (c == '\n')
                {
                    if (!overflow && MicrobitFrame.TryParse(line.ToString(), out var frame)) receive(frame);
                    line.Clear(); overflow = false;
                }
                else if (!overflow && line.Length < 64) line.Append(c);
                else overflow = true;
            }
        }
    }
}
