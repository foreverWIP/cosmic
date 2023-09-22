global using static Cosmic.Core.DrawSpriteStuff;
using System.Drawing;
using static Cosmic.Core.Drawing;

namespace Cosmic.Core;

static class DrawSpriteStuff
{
    public static void DrawSprite(int XPos, int YPos, int width, int height, int sprX, int sprY, int sheetID)
    {
        DrawSpriteRotozoom(FlipFlags.FLIP_NONE, XPos, YPos, 0, 0, sprX, sprY, width, height, 0, 512, sheetID);
    }

    public static void DrawSpriteFlipped(int XPos, int YPos, int width, int height, int sprX, int sprY, FlipFlags direction, int sheetID)
    {
        switch (direction)
        {
            case FlipFlags.FLIP_NONE:
                DrawSpriteRotozoom(direction, XPos, YPos, 0, 0, sprX, sprY, width, height, 0, 512, sheetID);
                break;
            case FlipFlags.FLIP_X:
                DrawSpriteRotozoom(direction, XPos, YPos, width, 0, sprX, sprY, width, height, 0, 512, sheetID);
                break;
            case FlipFlags.FLIP_Y:
                DrawSpriteRotozoom(FlipFlags.FLIP_X, XPos, YPos, 0, height, sprX, sprY, width, height, 256, 512, sheetID);
                break;
            case FlipFlags.FLIP_XY:
                DrawSpriteRotozoom(FlipFlags.FLIP_NONE, XPos, YPos, width, height, sprX, sprY, width, height, 256, 512, sheetID);
                break;
            default: break;
        }
    }

    public static void DrawSpriteScaled(FlipFlags direction, int XPos, int YPos, int pivotX, int pivotY, int scale, int width, int height, int sprX, int sprY,
                          int sheetID)
    {
        DrawSpriteRotozoom(direction, XPos, YPos, pivotX, pivotY, sprX, sprY, width, height, 0, scale, sheetID);
    }

    public static void DrawSpriteRotated(FlipFlags direction, int XPos, int YPos, int pivotX, int pivotY, int sprX, int sprY, int width, int height, int rotation,
                           int sheetID)
    {
        DrawSpriteRotozoom(direction, XPos, YPos, pivotX, pivotY, sprX, sprY, width, height, rotation, 512, sheetID);
    }

    public static void DrawSpriteRotozoom(FlipFlags direction, int XPos, int YPos, int pivotX, int pivotY, int sprX, int sprY, int width, int height, int rotation, int scale,
                            int sheetID, int alpha = 0xff, DrawBlendMode hwBlendMode = DrawBlendMode.Alpha)
    {
        if (scale == 0 || alpha == 0)
            return;

        if (hwBlendMode == DrawBlendMode.Alpha && alpha == 0xff)
        {
            hwBlendMode = DrawBlendMode.Opaque;
        }
        if (alpha != 0xff)
        {
            SubmitDrawList();
        }

        var surface = Sprite.GetSpriteAt(sheetID);
        sprX += (int)surface.dataStartLocation.X;
        sprY += (int)surface.dataStartLocation.Y;
        rotation -= rotation >> 9 << 9;
        if (rotation < 0)
            rotation += 0x200;
        if (rotation != 0)
            rotation = 0x200 - rotation;

        var sin = sin512LookupTable[rotation] * scale / 512;
        var cos = cos512LookupTable[rotation] * scale / 512;
        if (XPos > -512 && XPos < 872 && YPos > -512 && YPos < 752)
        {
            if (direction == FlipFlags.FLIP_NONE)
            {
                int x = -pivotX;
                int y = -pivotY;
                var vert0 = new DrawVertex(
                    XPos + ((x * cos + y * sin) / 512),
                    YPos + ((y * cos - x * sin) / 512),
                    currentDrawVertDepth,
                    sprX,
                    sprY,
                    surface.dataStartLocation.Z,
                    Color.FromArgb(alpha, Color.White)
                );

                x = width - pivotX;
                y = -pivotY;
                var vert1 = new DrawVertex(
                    XPos + ((x * cos + y * sin) / 512),
                    YPos + ((y * cos - x * sin) / 512),
                    currentDrawVertDepth,
                    sprX + width,
                    sprY,
                    surface.dataStartLocation.Z,
                    Color.FromArgb(alpha, Color.White)
                );

                x = -pivotX;
                y = height - pivotY;
                var vert2 = new DrawVertex(
                    XPos + ((x * cos + y * sin) / 512),
                    YPos + ((y * cos - x * sin) / 512),
                    currentDrawVertDepth,
                    sprX,
                    sprY + height,
                    surface.dataStartLocation.Z,
                    Color.FromArgb(alpha, Color.White)
                );

                x = width - pivotX;
                y = height - pivotY;
                var vert3 = new DrawVertex(
                    XPos + ((x * cos + y * sin) / 512),
                    YPos + ((y * cos - x * sin) / 512),
                    currentDrawVertDepth,
                    sprX + width,
                    sprY + height,
                    surface.dataStartLocation.Z,
                    Color.FromArgb(alpha, Color.White)
                );

                AddNewDrawFace(
                    hwBlendMode,
                    new DrawVertexFace(
                    vert0,
                    vert1,
                    vert2,
                    vert3
                ));
            }
            else
            {
                int x = pivotX;
                int y = -pivotY;
                var vert0 = new DrawVertex(
                    XPos + ((x * cos + y * sin) / 512),
                    YPos + ((y * cos - x * sin) / 512),
                    currentDrawVertDepth,
                    sprX,
                    sprY,
                    surface.dataStartLocation.Z,
                    Color.FromArgb(alpha, Color.White)
                );

                x = pivotX - width;
                y = -pivotY;
                var vert1 = new DrawVertex(
                    XPos + ((x * cos + y * sin) / 512),
                    YPos + ((y * cos - x * sin) / 512),
                    currentDrawVertDepth,
                    sprX + width,
                    sprY,
                    surface.dataStartLocation.Z,
                    Color.FromArgb(alpha, Color.White)
                );

                x = pivotX;
                y = height - pivotY;
                var vert2 = new DrawVertex(
                    XPos + ((x * cos + y * sin) / 512),
                    YPos + ((y * cos - x * sin) / 512),
                    currentDrawVertDepth,
                    sprX,
                    sprY + height,
                    surface.dataStartLocation.Z,
                    Color.FromArgb(alpha, Color.White)
                );

                x = pivotX - width;
                y = height - pivotY;
                var vert3 = new DrawVertex(
                    XPos + ((x * cos + y * sin) / 512),
                    YPos + ((y * cos - x * sin) / 512),
                    currentDrawVertDepth,
                    sprX + width,
                    sprY + height,
                    surface.dataStartLocation.Z,
                    Color.FromArgb(alpha, Color.White)
                );

                AddNewDrawFace(
                    hwBlendMode,
                    new DrawVertexFace(
                    vert0,
                    vert1,
                    vert2,
                    vert3
                ));
            }
        }
    }

    public static void DrawBlendedSprite(int XPos, int YPos, int width, int height, int sprX, int sprY, int sheetID)
    {
        SubmitDrawList();
        DrawSpriteRotozoom(FlipFlags.FLIP_NONE, XPos, YPos, 0, 0, sprX, sprY, width, height, 0, 512, sheetID, 0x80);
    }

    public static void DrawAlphaBlendedSprite(int XPos, int YPos, int width, int height, int sprX, int sprY, int alpha, int sheetID)
    {
        if (alpha == 0)
        {
            return;
        }
        if (alpha > 0xff)
        {
            alpha = 0xff;
        }
        if (alpha != 0xff)
        {
            SubmitDrawList();
        }
        DrawSpriteRotozoom(FlipFlags.FLIP_NONE, XPos, YPos, 0, 0, sprX, sprY, width, height, 0, 512, sheetID, alpha);
    }

    public static void DrawAdditiveBlendedSprite(int XPos, int YPos, int width, int height, int sprX, int sprY, int alpha, int sheetID)
    {
        DrawSpriteRotozoom(FlipFlags.FLIP_NONE, XPos, YPos, 0, 0, sprX, sprY, width, height, 0, 512, sheetID, alpha, DrawBlendMode.Additive);
    }

    public static void DrawSubtractiveBlendedSprite(int XPos, int YPos, int width, int height, int sprX, int sprY, int alpha, int sheetID)
    {
        DrawSpriteRotozoom(FlipFlags.FLIP_NONE, XPos, YPos, 0, 0, sprX, sprY, width, height, 0, 512, sheetID, alpha, DrawBlendMode.Subtractive);
    }
}