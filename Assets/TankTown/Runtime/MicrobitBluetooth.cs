#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TankTown
{
    // Windows paired BLE UART services. Pairing is performed in Windows Settings.
    // No Unity API is called by a Bluetooth callback.
    internal sealed class MicrobitBluetooth : IDisposable
    {
        private readonly object gate = new object();
        private readonly Action<byte[], int> receive;
        private readonly Action<string> trace;
        private readonly SafeFileHandle handle;
        private IntPtr subscription;
        private GCHandle context;
        private bool disposed;
        private static readonly GattCallback callback = OnValue;

        internal static string[] FindServices(string serviceUuid, string deviceFilter)
        {
            var result = new List<string>();
            var guid = new Guid("6e3bb679-4372-40c8-9eaa-4509df260cd8");
            IntPtr set = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x12);
            if (set == new IntPtr(-1)) throw new Win32Exception();
            try
            {
                for (uint index = 0; ; index++)
                {
                    var item = new InterfaceData { Size = Marshal.SizeOf<InterfaceData>() };
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref item))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == 259) break;
                        throw new Win32Exception(error);
                    }
                    SetupDiGetDeviceInterfaceDetail(set, ref item, IntPtr.Zero, 0, out uint size, IntPtr.Zero);
                    if (size < 6 || size > 65536) throw new Win32Exception();
                    IntPtr detail = Marshal.AllocHGlobal((int)size);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref item, detail, size, out _, IntPtr.Zero)) throw new Win32Exception();
                        string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                        if (MatchesService(path, serviceUuid, deviceFilter)) result.Add(path);
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return result.ToArray();
        }

        internal static bool MatchesService(string path, string uuid, string filter) =>
            Canonical(path).Contains(Canonical(uuid)) && (string.IsNullOrWhiteSpace(filter) || Canonical(path).Contains(Canonical(filter)));
        private static string Canonical(string value) => (value ?? "").Replace("-", "").Replace(":", "").Replace("{", "").Replace("}", "").ToLowerInvariant();

        internal MicrobitBluetooth(string path, string receiveUuid, Action<byte[], int> receive, Action<string> trace = null)
        {
            this.receive = receive;
            this.trace = trace;
            handle = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(); }
            try
            {
                CheckBufferResult(BluetoothGATTGetCharacteristics(handle, IntPtr.Zero, 0, null, out ushort count, 0));
                if (count == 0) throw new System.IO.IOException("BLE UART has no characteristics");
                var items = new Characteristic[count];
                Check(BluetoothGATTGetCharacteristics(handle, IntPtr.Zero, count, items, out count, 0));
                bool found = TrySelectCharacteristic(items, count, receiveUuid, out Characteristic selected);
                if (!found) throw new System.IO.IOException("No matching BLE UART notify/indicate characteristic");
                trace?.Invoke("特性 " + selected.Uuid.AsGuid + " / notify=" + selected.Notifiable + " / indicate=" + selected.Indicatable);
                CheckBufferResult(BluetoothGATTGetDescriptors(handle, ref selected, 0, null, out ushort descriptorsCount, 0));
                if (descriptorsCount == 0) throw new System.IO.IOException("BLE UART has no CCCD");
                var descriptors = new Descriptor[descriptorsCount];
                Check(BluetoothGATTGetDescriptors(handle, ref selected, descriptorsCount, descriptors, out descriptorsCount, 0));
                Descriptor cccd = default; found = false;
                for (int i = 0; i < descriptorsCount; i++)
                    if (descriptors[i].Type == 2) { cccd = descriptors[i]; found = true; break; }
                if (!found) throw new System.IO.IOException("BLE UART has no CCCD");

                context = GCHandle.Alloc(this);
                var registration = new Registration { Count = 1, Characteristic = selected };
                Check(BluetoothGATTRegisterEvent(handle, 0, ref registration, callback, GCHandle.ToIntPtr(context), out subscription, 0));
                var value = new DescriptorValue { Type = 2, Notify = (byte)(selected.Notifiable != 0 ? 1 : 0), Indicate = (byte)(selected.Notifiable == 0 ? 1 : 0) };
                Check(BluetoothGATTSetDescriptorValue(handle, ref cccd, ref value, 0));
                trace?.Invoke("購読完了: " + selected.Uuid.AsGuid + (value.Indicate != 0 ? " indication" : " notification"));
            }
            catch { Dispose(); throw; }
        }

        internal static bool TrySelectCharacteristic(Characteristic[] items, int count, string receiveUuid, out Characteristic selected)
        {
            selected = default;
            bool explicitUuid = Guid.TryParse(receiveUuid, out var expected);
            var microbitTx = new Guid("6e400002-b5a3-f393-e0a9-e50e24dcca9e");
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var item = items[i];
                if (item.Notifiable == 0 && item.Indicatable == 0) continue;
                var uuid = item.Uuid.AsGuid;
                if (explicitUuid && uuid != expected) continue;
                if (!found || (!explicitUuid && uuid == microbitTx)) { selected = item; found = true; }
                if (explicitUuid || uuid == microbitTx) break;
            }
            return found;
        }

        [AOT.MonoPInvokeCallback(typeof(GattCallback))]
        private static void OnValue(int eventType, IntPtr output, IntPtr callbackContext)
        {
            // Never let an exception cross the unmanaged callback boundary.
            MicrobitBluetooth target = null;
            try
            {
                target = (MicrobitBluetooth)GCHandle.FromIntPtr(callbackContext).Target;
                lock (target.gate)
                {
                    if (target.disposed) return;
                    target.trace?.Invoke("callback");
                    if (!TryCopyValue(output, out var bytes, out string error)) { target.trace?.Invoke("callback error: " + error); return; }
                    target.receive(bytes, bytes.Length);
                }
            }
            catch (Exception e) { try { target?.trace?.Invoke("callback error: " + e.GetType().Name + " " + e.Message); } catch { } }
        }

        internal static bool TryCopyValue(IntPtr output, out byte[] bytes, out string error)
        {
            bytes = null; error = "";
            if (output == IntPtr.Zero) { error = "empty event"; return false; }
            var changed = Marshal.PtrToStructure<ValueChanged>(output);
            if (changed.Value == IntPtr.Zero || changed.DataSize.ToUInt64() == 0) { error = "missing value/size=" + changed.DataSize; return false; }
            int count = Marshal.ReadInt32(changed.Value);
            // Some Windows BLE callbacks report the payload length, while others
            // include the four-byte DataSize field. The user's device reports 7/7.
            ulong size = changed.DataSize.ToUInt64();
            if (count <= 0 || count > 512 || (size != (ulong)count && size < (ulong)(count + 4))) { error = "count=" + count + " size=" + changed.DataSize; return false; }
            bytes = new byte[count];
            Marshal.Copy(IntPtr.Add(changed.Value, 4), bytes, 0, count);
            return true;
        }

        public void Dispose()
        {
            lock (gate) { if (disposed) return; disposed = true; }
            // Unregister waits for callbacks; do not hold gate while waiting.
            if (subscription != IntPtr.Zero) { BluetoothGATTUnregisterEvent(subscription, 0); subscription = IntPtr.Zero; }
            if (context.IsAllocated) context.Free();
            handle?.Dispose();
        }
        private static void Check(int result) { if (result < 0) Marshal.ThrowExceptionForHR(result); }
        private static void CheckBufferResult(int result) { if (result != unchecked((int)0x800700EA)) Check(result); }

        [StructLayout(LayoutKind.Sequential)]
        private struct InterfaceData { public int Size; public Guid ClassGuid; public uint Flags; public UIntPtr Reserved; }
        [StructLayout(LayoutKind.Explicit, Size = 20)]
        internal struct Uuid
        {
            [FieldOffset(0)] public byte Short;
            [FieldOffset(4)] public ushort ShortValue;
            [FieldOffset(4)] public Guid LongValue;
            internal Guid AsGuid => Short == 0 ? LongValue : new Guid(ShortValue.ToString("x8") + "-0000-1000-8000-00805f9b34fb");
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Characteristic
        {
            public ushort ServiceHandle;
            public Uuid Uuid;
            public ushort AttributeHandle, ValueHandle;
            public byte Broadcastable, Readable, Writable, WritableWithoutResponse, SignedWritable, Notifiable, Indicatable, ExtendedProperties;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Descriptor { public ushort ServiceHandle, CharacteristicHandle; public int Type; public Uuid Uuid; public ushort AttributeHandle; }
        [StructLayout(LayoutKind.Explicit, Size = 80)]
        internal struct DescriptorValue
        {
            [FieldOffset(0)] public int Type;
            [FieldOffset(24)] public byte Notify;
            [FieldOffset(25)] public byte Indicate;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Registration { public ushort Count; public Characteristic Characteristic; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct ValueChanged { public ushort Handle; public UIntPtr DataSize; public IntPtr Value; }
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void GattCallback(int eventType, IntPtr output, IntPtr context);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid guid, uint index, ref InterfaceData data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint required, IntPtr info);
        [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("BluetoothAPIs.dll")] private static extern int BluetoothGATTGetCharacteristics(SafeFileHandle handle, IntPtr service, ushort count, [Out] Characteristic[] items, out ushort required, uint flags);
        [DllImport("BluetoothAPIs.dll")] private static extern int BluetoothGATTGetDescriptors(SafeFileHandle handle, ref Characteristic characteristic, ushort count, [Out] Descriptor[] items, out ushort required, uint flags);
        [DllImport("BluetoothAPIs.dll")] private static extern int BluetoothGATTSetDescriptorValue(SafeFileHandle handle, ref Descriptor descriptor, ref DescriptorValue value, uint flags);
        [DllImport("BluetoothAPIs.dll")] private static extern int BluetoothGATTRegisterEvent(SafeFileHandle handle, int type, ref Registration registration, GattCallback callback, IntPtr context, out IntPtr subscription, uint flags);
        [DllImport("BluetoothAPIs.dll")] private static extern int BluetoothGATTUnregisterEvent(IntPtr subscription, uint flags);
    }
}
#endif
