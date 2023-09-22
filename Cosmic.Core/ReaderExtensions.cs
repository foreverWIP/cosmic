using System;
using System.IO;
using System.Text;
namespace Cosmic.Core;

static class ReaderExtensions
{
    public static string ReadPascalString(this BinaryReader reader)
    {
        return Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadByte()));
    }

    static ushort SwapEndian(ushort preswapped)
    {
        return (ushort)(((preswapped & 0xff) << 8) | ((preswapped & 0xff00) >> 8));
    }

    static uint SwapEndian(uint preswapped)
    {
        return (uint)(((preswapped & 0xff00_0000) >> 24) | ((preswapped & 0x00ff_0000) >> 8) | ((preswapped & 0x0000_ff00) << 8) | ((preswapped & 0x0000_00ff) << 24));
    }

    public static ushort ReadUInt16LE(this BinaryReader reader)
    {
        if (BitConverter.IsLittleEndian)
        {
            return reader.ReadUInt16();
        }
        return SwapEndian(reader.ReadUInt16());
    }

    public static ushort ReadUInt16BE(this BinaryReader reader)
    {
        if (BitConverter.IsLittleEndian)
        {
            return SwapEndian(reader.ReadUInt16());
        }
        return reader.ReadUInt16();
    }

    public static short ReadInt16LE(this BinaryReader reader)
    {
        if (BitConverter.IsLittleEndian)
        {
            return reader.ReadInt16();
        }
        return (short)SwapEndian(reader.ReadUInt16());
    }

    public static short ReadInt16BE(this BinaryReader reader)
    {
        if (BitConverter.IsLittleEndian)
        {
            return (short)SwapEndian(reader.ReadUInt16());
        }
        return reader.ReadInt16();
    }

    public static uint ReadUInt32LE(this BinaryReader reader)
    {
        if (BitConverter.IsLittleEndian)
        {
            return reader.ReadUInt32();
        }
        return SwapEndian(reader.ReadUInt32());
    }

    public static uint ReadUInt32BE(this BinaryReader reader)
    {
        if (BitConverter.IsLittleEndian)
        {
            return SwapEndian(reader.ReadUInt32());
        }
        return reader.ReadUInt32();
    }

    public static int ReadInt32LE(this BinaryReader reader)
    {
        if (BitConverter.IsLittleEndian)
        {
            return reader.ReadInt32();
        }
        return (int)SwapEndian(reader.ReadUInt32());
    }

    public static int ReadInt32BE(this BinaryReader reader)
    {
        if (BitConverter.IsLittleEndian)
        {
            return (int)SwapEndian(reader.ReadUInt32());
        }
        return reader.ReadInt32();
    }
}