using System;
using UnityEngine;

namespace GameplayAbilities
{
    // TODO: record struct
    public struct TimerHandle : IEquatable<TimerHandle>
    {
        private const ushort IndexBits = 24;
        private const ushort SerialNumberBits = 40;
        private const int MaxIndex = 1 << IndexBits;
        public const ulong MaxSerialNumber = 1 << SerialNumberBits;
        private ulong Handle;

        public bool IsValid()
        {
            return Handle != 0;
        }

        public void Invalidate()
        {
            Handle = 0;
        }

        public override string ToString()
        {
            return Handle.ToString();
        }

        public void SetIndexAndSerialNumber(int index, ulong serialNumber)
        {
            Debug.Assert(index >= 0 && index < MaxIndex);
            Debug.Assert(serialNumber < MaxSerialNumber);
            Handle = (serialNumber << IndexBits) | (uint)index;
        }

        public int GetIndex()
        {
            return (int)(Handle & (MaxIndex - 1));
        }

        public ulong GetSerialNumber()
        {
            return Handle >> IndexBits;
        }

        public static bool operator ==(TimerHandle a, TimerHandle b)
        {
            return a.Handle == b.Handle;
        }

        public static bool operator !=(TimerHandle a, TimerHandle b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (obj is TimerHandle other)
            {
                return Equals(other);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Handle.GetHashCode();
        }

        public bool Equals(TimerHandle other)
        {
            return Handle == other.Handle;
        }
    }

    public static class TimerHandleGenerator
    {
        private static ulong LastAssignedSerialNumber = 0;

        public static TimerHandle GenerateHandle(int index)
        {
            ulong newSerialNumber = ++LastAssignedSerialNumber;
            if (newSerialNumber == TimerHandle.MaxSerialNumber)
            {
                newSerialNumber = 1;
            }

            TimerHandle result = new();
            result.SetIndexAndSerialNumber(index, newSerialNumber);
            return result;
        }
    }
}
