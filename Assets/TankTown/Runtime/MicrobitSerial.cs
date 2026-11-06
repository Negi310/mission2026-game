#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TankTown
{
    // Windows USB CDC transport without a System.IO.Ports plugin or external bridge process.
    internal sealed class MicrobitSerial : IDisposable
    {
        private readonly SafeFileHandle handle;
        internal static string[] FindPorts()
        {
            var ports = new List<string>();
            IntPtr usb = OpenKey(new IntPtr(unchecked((int)0x80000002)), @"SYSTEM\CurrentControlSet\Enum\USB");
            if (usb == IntPtr.Zero) return ports.ToArray();
            try
            {
                foreach (string id in SubKeys(usb))
                {
                    if (!id.StartsWith("VID_0D28&PID_0204", StringComparison.OrdinalIgnoreCase)) continue;
                    IntPtr device = OpenKey(usb, id);
                    if (device == IntPtr.Zero) continue;
                    try
                    {
                        foreach (string instance in SubKeys(device))
                        {
                            IntPtr parameters = OpenKey(device, instance + @"\Device Parameters");
                            if (parameters == IntPtr.Zero) continue;
                            try
                            {
                                var data = new byte[512]; uint size = (uint)data.Length;
                                if (RegQueryValueEx(parameters, "PortName", IntPtr.Zero, out uint type, data, ref size) == 0 && type == 1)
                                {
                                    string port = System.Text.Encoding.Unicode.GetString(data, 0, (int)size).TrimEnd('\0');
                                    if (!ports.Contains(port)) ports.Add(port);
                                }
                            }
                            finally { RegCloseKey(parameters); }
                        }
                    }
                    finally { RegCloseKey(device); }
                }
            }
            finally { RegCloseKey(usb); }
            return ports.ToArray();
        }
        private static IntPtr OpenKey(IntPtr parent, string path)
        {
            int result = RegOpenKeyEx(parent, path, 0, 0x20019, out var key);
            if (result == 2) return IntPtr.Zero;
            if (result != 0) throw new Win32Exception(result);
            return key;
        }
        private static IEnumerable<string> SubKeys(IntPtr key)
        {
            for (uint index = 0; ; index++)
            {
                var name = new System.Text.StringBuilder(256); uint length = 256;
                int result = RegEnumKeyEx(key, index, name, ref length, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (result == 259) yield break;
                if (result != 0) throw new Win32Exception(result);
                yield return name.ToString();
            }
        }
        internal MicrobitSerial(string port)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(port, @"^COM[1-9][0-9]*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) throw new ArgumentException("Expected COM port");
            handle = CreateFile(@"\\.\" + port, 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(); }
            try
            {
                var dcb = new Dcb { Length = (uint)Marshal.SizeOf<Dcb>() };
                if (!GetCommState(handle, ref dcb)) throw new Win32Exception();
                dcb.Baud = 115200; dcb.Flags = 1 | (1 << 4) | (1 << 12); dcb.ByteSize = 8; dcb.Parity = 0; dcb.StopBits = 0;
                var timeout = new Timeouts { Interval = uint.MaxValue };
                if (!SetCommState(handle, ref dcb) || !SetCommTimeouts(handle, ref timeout)) throw new Win32Exception();
            }
            catch { handle.Dispose(); throw; }
        }
        internal int Read(byte[] buffer)
        {
            if (!ReadFile(handle, buffer, (uint)buffer.Length, out uint count, IntPtr.Zero)) throw new Win32Exception();
            return (int)count;
        }
        public void Dispose() => handle.Dispose();
        [StructLayout(LayoutKind.Sequential)]
        private struct Dcb
        {
            public uint Length, Baud, Flags;
            public ushort Reserved, XonLimit, XoffLimit;
            public byte ByteSize, Parity, StopBits, Xon, Xoff, Error, Eof, Event;
            public ushort Reserved1;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Timeouts { public uint Interval, ReadMultiplier, ReadConstant, WriteMultiplier, WriteConstant; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetCommState(SafeFileHandle h, ref Dcb dcb);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetCommState(SafeFileHandle h, ref Dcb dcb);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetCommTimeouts(SafeFileHandle h, ref Timeouts timeouts);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadFile(SafeFileHandle h, [Out] byte[] buffer, uint size, out uint count, IntPtr overlapped);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegOpenKeyEx(IntPtr key, string path, uint options, uint access, out IntPtr result);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegEnumKeyEx(IntPtr key, uint index, System.Text.StringBuilder name, ref uint length, IntPtr reserved, IntPtr className, IntPtr classLength, IntPtr lastWrite);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegQueryValueEx(IntPtr key, string name, IntPtr reserved, out uint type, [Out] byte[] value, ref uint size);
        [DllImport("advapi32.dll")] private static extern int RegCloseKey(IntPtr key);
    }
}
#endif
