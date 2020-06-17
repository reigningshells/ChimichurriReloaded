using System;
using System.Runtime.InteropServices;

namespace ChimichurriReloaded
{
    class ServiceTrigger
    {
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private class EVENT_DESCRIPTOR
        {
            [FieldOffset(0)] ushort Id = 1;
            [FieldOffset(2)] byte Version = 0;
            [FieldOffset(3)] byte Channel = 0;
            [FieldOffset(4)] byte Level = 4;
            [FieldOffset(5)] byte Opcode = 0;
            [FieldOffset(6)] ushort Task = 0;
            [FieldOffset(8)] long Keyword = 0;
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct EventData
        {
            public UInt64 DataPointer;
            public uint Size;
            public int Reserved;
        }

        private static class Advapi32
        {
            [DllImport("Advapi32.dll", SetLastError = true)]
            public static extern uint EventRegister(
                ref Guid guid,
                [Optional] IntPtr EnableCallback,
                [Optional] IntPtr CallbackContext,
                [In][Out] ref long RegHandle);

            [DllImport("Advapi32.dll", SetLastError = true)]
            public static extern uint EventWrite(
                long RegHandle,
                ref EVENT_DESCRIPTOR EventDescriptor,
                uint UserDataCount,
                IntPtr UserData);

            [DllImport("Advapi32.dll", SetLastError = true)]
            public static extern uint EventUnregister(long RegHandle);
        }
        public static bool Start(string serviceGuid)
        {
            long handle = 0;
            bool success = false;
            Guid triggerGuid = new Guid(serviceGuid);
            if (Advapi32.EventRegister(ref triggerGuid, IntPtr.Zero, IntPtr.Zero, ref handle) == 0)
            {
                EVENT_DESCRIPTOR desc = new EVENT_DESCRIPTOR();
                success = Advapi32.EventWrite(handle, ref desc, 0, IntPtr.Zero) == 0;
                Advapi32.EventUnregister(handle);
                return success;
            }
            return success;
        }
    }
}
