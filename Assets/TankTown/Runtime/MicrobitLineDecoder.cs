using System;
using System.Text;

namespace TankTown
{
    // BLE notifications and serial reads may split a line anywhere or contain several lines.
    internal sealed class MicrobitLineDecoder
    {
        private readonly StringBuilder line = new StringBuilder(64);
        private bool overflow;
        private bool backslash;
        internal Action<string, bool> LineReceived;
        internal void Feed(byte[] bytes, int count, Action<MicrobitFrame> receive)
        {
            for (int i = 0; i < count; i++)
            {
                char c = (char)bytes[i];
                bool literalNewline = backslash && c == 'n';
                if (backslash && !literalNewline) Append('\\');
                backslash = false;
                if (c == '\n' || literalNewline)
                {
                    string text = line.ToString();
                    MicrobitFrame frame = default;
                    bool valid = !overflow && MicrobitFrame.TryParse(text, out frame);
                    LineReceived?.Invoke(overflow ? text + " [too long]" : text, valid);
                    if (valid) receive(frame);
                    line.Clear(); overflow = false;
                }
                else if (c == '\\') backslash = true;
                else Append(c);
            }
        }
        private void Append(char c)
        {
            if (!overflow && line.Length < 64) line.Append(c);
            else overflow = true;
        }
    }
}
