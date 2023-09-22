using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;

namespace Cosmic.Core;

public readonly struct Sprite
{
    public readonly int width, height;
    // x and y are offsets in pixels
    // z is the offset in textures
    public readonly Vector3 dataStartLocation = new();

    Sprite(int width, int height, int x, int y, int z)
    {
        this.width = width;
        this.height = height;
        dataStartLocation = new(x, y, z);
    }

    public const int MaxHwTextures = 2;
    public const int MaxHwTextureDimension = 2048;
    static readonly Dictionary<int, Sprite> gfxSurfaceLookup = new();
    const int SpritesheetMinTakeup = 128;
    static readonly bool[,,] spritesheetTakeup = new bool[MaxHwTextureDimension / SpritesheetMinTakeup, MaxHwTextureDimension / SpritesheetMinTakeup, MaxHwTextures];

    public static void ClearAll()
    {
        gfxSurfaceLookup.Clear();
        Array.Clear(spritesheetTakeup);
    }

    public static Sprite GetSpriteAt(int sheetID)
    {
        if (gfxSurfaceLookup.TryGetValue(sheetID, out Sprite value))
        {
            return value;
        }
        return default;
    }

    public static int Add(string filePath)
    {
        var filePathHash = filePath.GetHashCode();
        if (gfxSurfaceLookup.ContainsKey(filePathHash))
        {
            return filePathHash;
        }

        var sheetPath = "Data/Sprites/";
        sheetPath += filePath;
        byte fileExtension = (byte)sheetPath[(sheetPath.Length - 1) & 0xFF];
        var graphicData = (char)fileExtension switch
        {
            'f' => LoadGIFFile(sheetPath),
            _ => throw new NotImplementedException("invalid image format")
        };

        var ourRect = new Rectangle
        {
            Width = graphicData.w,
            Height = graphicData.h
        };
        var textureWriteIndex = 0;
        var compareRect = new Rectangle();
        var freeSpace = false;
        for (var z = 0; z < spritesheetTakeup.GetLength(2); z++)
        {
            textureWriteIndex = z;
            for (var y = 0; y < spritesheetTakeup.GetLength(1) - (ourRect.Height / SpritesheetMinTakeup); y++)
            {
                ourRect.Y = y * SpritesheetMinTakeup;
                for (var x = 0; x < spritesheetTakeup.GetLength(0) - (ourRect.Width / SpritesheetMinTakeup); x++)
                {
                    ourRect.X = x * SpritesheetMinTakeup;
                    var allClear = true;
                    for (var h = 0; h < ourRect.Height / SpritesheetMinTakeup; h++)
                    {
                        for (var w = 0; w < ourRect.Width / SpritesheetMinTakeup; w++)
                        {
                            allClear &= !spritesheetTakeup[x + w, y + h, z];
                        }
                    }
                    if (allClear)
                    {
                        for (var h = 0; h < ourRect.Height / SpritesheetMinTakeup; h++)
                        {
                            for (var w = 0; w < ourRect.Width / SpritesheetMinTakeup; w++)
                            {
                                spritesheetTakeup[x + w, y + h, z] = true;
                            }
                        }
                        freeSpace = true;
                        goto DoneSearching;
                    }
                }
            }
        }
    DoneSearching:

        if (!freeSpace)
        {
            platform.PrintLog("Ran out of surface space!");
            return 0;
        }

        gfxSurfaceLookup.Add(filePathHash, new Sprite(ourRect.Width, ourRect.Height, ourRect.X, ourRect.Y, textureWriteIndex));
        platform.UpdateHWSurface(textureWriteIndex, ourRect.X, ourRect.Y, graphicData.w, graphicData.h, graphicData.data);

        return filePathHash;
    }

    public static void Remove(string path)
    {
        if (gfxSurfaceLookup.TryGetValue(path.GetHashCode(), out Sprite value))
        {
            for (var h = 0; h < value.height / SpritesheetMinTakeup; h++)
            {
                for (var w = 0; w < value.width / SpritesheetMinTakeup; w++)
                {
                    spritesheetTakeup[
                        ((int)value.dataStartLocation.X / SpritesheetMinTakeup) + w,
                        ((int)value.dataStartLocation.Y / SpritesheetMinTakeup) + h,
                        ((int)value.dataStartLocation.Z / SpritesheetMinTakeup)
                    ] = true;
                }
            }
            gfxSurfaceLookup.Remove(path.GetHashCode());
        }
    }

    static (int w, int h, byte[] data) LoadGIFFile(string filePath)
    {
        if (platform.LoadFile(filePath, out var info))
        {
            var gif = new Cosmic.Formats.Gif(new Kaitai.KaitaiStream(info.BaseStream));

            var data = new byte[gif.LogicalScreenDescriptor.ScreenWidth * gif.LogicalScreenDescriptor.ScreenHeight];

            var decompressed = Cosmic.GifDecoder.GetDecodedData(gif);
            Array.Copy(decompressed, data, data.Length);

            info.Dispose();

            return (gif.LogicalScreenDescriptor.ScreenWidth, gif.LogicalScreenDescriptor.ScreenHeight, data);
        }
        return (0, 0, Array.Empty<byte>());
    }
}