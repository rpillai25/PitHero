using System;
using System.IO;
using Nez.Persistence.Binary;

namespace PitHero.Services.Replay
{
    /// <summary>
    /// Binary helpers for replay files. Nez's <see cref="IPersistableWriter"/> has no long/ulong/byte[]
    /// writers, so 64-bit values travel as two ints and byte blobs as base64 strings. Also serializes a
    /// <see cref="SaveData"/> to memory so a replay can carry the exact state it started from.
    /// </summary>
    public static class ReplayIO
    {
        /// <summary>Writes a long as high and low ints.</summary>
        public static void WriteLong(IPersistableWriter writer, long value)
        {
            writer.Write((int)(value >> 32));
            writer.Write((int)(value & 0xFFFFFFFFL));
        }

        /// <summary>Reads a long written by <see cref="WriteLong"/>.</summary>
        public static long ReadLong(IPersistableReader reader)
        {
            long hi = reader.ReadInt();
            long lo = (uint)reader.ReadInt();
            return (hi << 32) | lo;
        }

        /// <summary>Writes a ulong as two uints.</summary>
        public static void WriteULong(IPersistableWriter writer, ulong value)
        {
            writer.Write((uint)(value >> 32));
            writer.Write((uint)(value & 0xFFFFFFFFUL));
        }

        /// <summary>Reads a ulong written by <see cref="WriteULong"/>.</summary>
        public static ulong ReadULong(IPersistableReader reader)
        {
            ulong hi = reader.ReadUInt();
            ulong lo = reader.ReadUInt();
            return (hi << 32) | lo;
        }

        /// <summary>Writes a byte blob as a base64 string (empty string for null/empty).</summary>
        public static void WriteBytes(IPersistableWriter writer, byte[] bytes)
        {
            writer.Write(bytes == null || bytes.Length == 0 ? string.Empty : Convert.ToBase64String(bytes));
        }

        /// <summary>Reads a byte blob written by <see cref="WriteBytes"/> (null for empty).</summary>
        public static byte[] ReadBytes(IPersistableReader reader)
        {
            var s = reader.ReadString();
            return string.IsNullOrEmpty(s) ? null : Convert.FromBase64String(s);
        }

        /// <summary>Serializes a SaveData to bytes using the same binary format as the save files.</summary>
        public static byte[] SerializeSaveData(SaveData data)
        {
            if (data == null)
                return null;
            using (var ms = new MemoryStream())
            {
                var writer = new ReuseableBinaryWriter(ms);
                writer.Write(data);
                writer.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>Deserializes bytes produced by <see cref="SerializeSaveData"/> into a fresh SaveData.</summary>
        public static SaveData DeserializeSaveData(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;
            using (var ms = new MemoryStream(bytes))
            {
                var reader = new ReuseableBinaryReader(ms);
                var data = new SaveData();
                reader.ReadPersistableInto(data);
                return data;
            }
        }

        /// <summary>FNV-1a 64-bit hash step for an int.</summary>
        public static ulong Hash(ulong h, int value)
        {
            unchecked
            {
                h ^= (uint)value;
                h *= 0x100000001B3UL;
                return h;
            }
        }

        /// <summary>FNV-1a 64-bit hash step for a long.</summary>
        public static ulong Hash(ulong h, long value)
        {
            unchecked
            {
                h = Hash(h, (int)(value & 0xFFFFFFFFL));
                h = Hash(h, (int)(value >> 32));
                return h;
            }
        }

        /// <summary>FNV-1a 64-bit hash step for a uint.</summary>
        public static ulong Hash(ulong h, uint value)
        {
            unchecked
            {
                h ^= value;
                h *= 0x100000001B3UL;
                return h;
            }
        }

        /// <summary>FNV-1a 64-bit hash step for a float (by bit pattern).</summary>
        public static ulong Hash(ulong h, float value)
        {
            return Hash(h, BitConverter.SingleToInt32Bits(value));
        }

        /// <summary>FNV-1a 64-bit hash step for a string (ordinal chars; null hashes as -1).</summary>
        public static ulong Hash(ulong h, string value)
        {
            if (value == null)
                return Hash(h, -1);
            h = Hash(h, value.Length);
            for (int i = 0; i < value.Length; i++)
                h = Hash(h, (int)value[i]);
            return h;
        }

        /// <summary>FNV-1a 64-bit offset basis.</summary>
        public const ulong HashSeed = 0xCBF29CE484222325UL;
    }
}
