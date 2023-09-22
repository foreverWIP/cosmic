global using static Cosmic.Core.Palette;
using System.Drawing;
using static Cosmic.Core.Drawing;

namespace Cosmic.Core;

public static class Palette
{
    public const int PALETTE_COUNT = (0x8);
    public const int PALETTE_SIZE = (0x100);

    public static void SetActivePalette(byte newActivePal, int startLine, int endLine)
    {
        if (newActivePal < PALETTE_COUNT)
            for (int l = startLine; l < endLine && l < SCREEN_YSIZE; l++) gfxLineBuffer[l] = newActivePal;

        activePalette = gfxLineBuffer[0];
        platform.UpdatePaletteIndices();
        forceNextDraw = true;
    }

    public static void SetPaletteEntry(byte paletteIndex, byte index, byte r, byte g, byte b)
    {
        if (paletteIndex != 0xFF)
        {
            fullPalette[paletteIndex, index] = Color.FromArgb(r, g, b);
        }
        else
        {
            fullPalette[activePalette, index] = Color.FromArgb(r, g, b);
        }
        platform.UpdatePalettes();
        forceNextDraw = true;
    }

    public static void CopyPalette(byte src, byte dest)
    {
        if (src < PALETTE_COUNT && dest < PALETTE_COUNT)
        {
            for (int i = 0; i < PALETTE_SIZE; ++i)
            {
                fullPalette[dest, i] = fullPalette[src, i];
            }
        }
        platform.UpdatePalettes();
        forceNextDraw = true;
    }

    public static void RotatePalette(byte startIndex, byte endIndex, bool right)
    {
        if (right)
        {
            var startClr32 = fullPalette[activePalette, endIndex];
            for (int i = endIndex; i > startIndex; --i)
            {
                fullPalette[activePalette, i] = fullPalette[activePalette, i - 1];
            }
            fullPalette[activePalette, startIndex] = startClr32;
        }
        else
        {
            var startClr32 = fullPalette[activePalette, startIndex];
            for (int i = startIndex; i < endIndex; ++i)
            {
                fullPalette[activePalette, i] = fullPalette[activePalette, i + 1];
            }
            fullPalette[activePalette, endIndex] = startClr32;
        }
        platform.UpdatePalettes();
        forceNextDraw = true;
    }

    public static void SetFade(byte R, byte G, byte B, ushort A)
    {
        fadeMode = 1;
        fadeColor = Color.FromArgb(
            (byte)(A > 0xFF ? 0xFF : A),
            R,
            G,
            B
        );
    }

    // Palettes (as RGB888 Colours)
    public static readonly Color[,] fullPalette = new Color[PALETTE_COUNT, PALETTE_SIZE];
    internal static byte activePalette;

    public static readonly byte[] gfxLineBuffer = new byte[SCREEN_YSIZE]; // Pointers to active palette

    public static int fadeMode = 0;
    public static Color fadeColor;

    public static bool limitedFadeActivated;

    public static void LoadPalette(string filePath, int paletteID, int startPaletteIndex, int startIndex, int endIndex)
    {
        var fullPath = "Data/Palettes/";
        fullPath += filePath;

        if (platform.LoadFile(fullPath, out var info))
        {
            info.BaseStream.Seek(3 * startIndex, System.IO.SeekOrigin.Begin);
            if (paletteID >= PALETTE_COUNT || paletteID < 0)
                paletteID = 0;

            byte[] colour;
            if (paletteID != 0)
            {
                for (int i = startIndex; i < endIndex; ++i)
                {
                    colour = info.ReadBytes(3);
                    if (colour.Length == 0)
                    {
                        colour = new byte[3];
                    }
                    SetPaletteEntry((byte)paletteID, (byte)startPaletteIndex++, colour[0], colour[1], colour[2]);
                }
            }
            else
            {
                for (int i = startIndex; i < endIndex; ++i)
                {
                    colour = info.ReadBytes(3);
                    SetPaletteEntry(0xff, (byte)startPaletteIndex++, colour[0], colour[1], colour[2]);
                }
            }
            info.Dispose();
        }
    }

    public static void SetLimitedFade(byte paletteID, byte R, byte G, byte B, ushort alpha, int startIndex, int endIndex)
    {
        if (paletteID >= PALETTE_COUNT)
            return;
        limitedFadeActivated = true;
        activePalette = paletteID;

        if (alpha >= 0x100)
            alpha = 0xFF;

        if (startIndex >= endIndex)
            return;

        uint alpha2 = (uint)(0xFF - alpha);
        for (int i = startIndex; i < endIndex; ++i)
        {
            fullPalette[activePalette, i] = Color.FromArgb(
                (byte)((ushort)((R * alpha) + (alpha2 * fullPalette[activePalette, i].R)) >> 8),
                (byte)((ushort)((G * alpha) + (alpha2 * fullPalette[activePalette, i].G)) >> 8),
                (byte)((ushort)((B * alpha) + (alpha2 * fullPalette[activePalette, i].B)) >> 8)
            );
        }
        platform.UpdatePaletteIndices();
        platform.UpdatePalettes();
        forceNextDraw = true;
    }
}