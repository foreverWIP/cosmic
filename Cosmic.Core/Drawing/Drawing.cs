using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using static Cosmic.Core.Animation;

namespace Cosmic.Core;

public enum DrawBlendMode
{
    StraightColor,
    Opaque,
    Additive,
    Subtractive,
    Alpha,
    Tileset,
    TilesetDebug,
    Floor3D,
    Monochrome,
    PauseMenu,
}

[StructLayout(LayoutKind.Sequential, Size = SizeInBytes, Pack = 0)]
public struct DrawVertex
{
    public const int SizeInBytes = sizeof(float) * 10;

    public float x, y, z;
    public float u, v, surfaceIndex;
    public float b;
    public float g;
    public float r;
    public float a;
    public Color color
    {
        readonly get => Color.FromArgb((int)(a * 255), (int)(r * 255), (int)(g * 255), (int)(b * 255));
        set
        {
            r = value.R / 255.0f;
            g = value.G / 255.0f;
            b = value.B / 255.0f;
            a = value.A / 255.0f;
        }
    }

    public DrawVertex(float x, float y, float z, float u, float v, float surfaceIndex, Color color/*, DrawBlendMode blendMode = DrawBlendMode.Alpha*/)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.u = u;
        this.v = v;
        this.surfaceIndex = surfaceIndex;
        this.color = color;
    }

    public DrawVertex(Vector3 pos, float u, float v, float surfaceIndex, Color color)
    {
        this.x = pos.X;
        this.y = pos.Y;
        this.z = pos.Z;
        this.u = u;
        this.v = v;
        this.surfaceIndex = surfaceIndex;
        this.color = color;
    }
}

[StructLayout(LayoutKind.Sequential, Size = SizeInBytes, Pack = 0)]
public readonly struct DrawVertexFace
{
    public const int SizeInBytes = DrawVertex.SizeInBytes * 4;

    public readonly DrawVertex a;
    public readonly DrawVertex b;
    public readonly DrawVertex c;
    public readonly DrawVertex d;

    public float leftmost => MathF.Min(MathF.Min(a.x, b.x), MathF.Min(c.x, d.x));
    public float rightmost => MathF.Max(MathF.Max(a.x, b.x), MathF.Max(c.x, d.x));
    public float topmost => MathF.Min(MathF.Min(a.y, b.y), MathF.Min(c.y, d.y));
    public float bottommost => MathF.Max(MathF.Max(a.y, b.y), MathF.Max(c.y, d.y));

    public DrawVertexFace(DrawVertex a, DrawVertex b, DrawVertex c, DrawVertex d)
    {
        this.a = a;
        this.b = b;
        this.c = c;
        this.d = d;
    }
}

public static class Drawing
{
    public const int DRAWLAYER_COUNT = (8);

    public enum FlipFlags { FLIP_NONE, FLIP_X, FLIP_Y, FLIP_XY }
    public enum InkFlags { INK_NONE, INK_BLEND, INK_ALPHA, INK_ADD, INK_SUB }
    public enum DrawFXFlags { FX_SCALE, FX_ROTATE, FX_ROTOZOOM, FX_INK, FX_TINT, FX_FLIP }

    public struct ObjectDrawListEntry
    {
        public int[] entityRefs
        {
            get
            {
                return _entityRefs ??= new int[ENTITY_COUNT];
            }
        }
        int[] _entityRefs;
        public int listSize;
    }

    internal static bool forceNextDraw = false;

    internal static void SubmitDrawList()
    {
        if (drawFacesCount > 0)
        {
            platform.SubmitDrawList(drawFacesCount, drawFaces, lastDrawBlendMode);
        }
        drawFacesCount = 0;
    }

    internal static DrawBlendMode lastDrawBlendMode;

    internal static void AddNewDrawFace(DrawBlendMode blendMode, DrawVertexFace face, bool skipClip = true)
    {
        if (!skipClip)
        {
            if (face.rightmost < 0)
            {
                return;
            }
            if (face.leftmost > SCREEN_XSIZE)
            {
                return;
            }
            if (face.bottommost < 0)
            {
                return;
            }
            if (face.topmost > SCREEN_YSIZE)
            {
                return;
            }
        }
        if (blendMode != lastDrawBlendMode)
        {
            SubmitDrawList();
            lastDrawBlendMode = blendMode;
        }
        drawFaces[drawFacesCount++] = face;
    }

    public const int SCREEN_XSIZE = 424;
    public const int SCREEN_CENTERX = SCREEN_XSIZE / 2;

    public static readonly ObjectDrawListEntry[] drawListEntries = new ObjectDrawListEntry[DRAWLAYER_COUNT];

    internal static float currentDrawVertDepth;
    internal const float DrawVertDepthIncrement = 1 / 64.0f;
    internal const float Objects0Depth = 0.5f;
    internal const float TileLayer0Depth = Objects0Depth + DrawVertDepthIncrement;
    internal const float Objects1Depth = TileLayer0Depth + DrawVertDepthIncrement;
    internal const float TileLayer1Depth = Objects1Depth + DrawVertDepthIncrement;
    internal const float Objects2Depth = TileLayer1Depth + DrawVertDepthIncrement;
    internal const float TileLayer2Depth = Objects2Depth + DrawVertDepthIncrement;
    internal const float Objects3Depth = TileLayer2Depth + DrawVertDepthIncrement;
    internal const float Objects4Depth = Objects3Depth + DrawVertDepthIncrement;
    internal const float TileLayer3Depth = Objects4Depth + DrawVertDepthIncrement;
    internal const float Objects5Depth = TileLayer3Depth + DrawVertDepthIncrement;
    internal const float Objects7Depth = Objects5Depth + DrawVertDepthIncrement;
    internal const float Objects6Depth = Objects7Depth + DrawVertDepthIncrement;
    internal const float FadeOverlayDepth = Objects6Depth + DrawVertDepthIncrement;
    internal const float DebugOverlayDepth = FadeOverlayDepth + DrawVertDepthIncrement;

    public const int MaxDrawFaces = 0x10000;
    public const int MaxDrawVertices = MaxDrawFaces * 6;
    internal static readonly DrawVertexFace[] drawFaces = new DrawVertexFace[MaxDrawFaces];
    static uint drawFacesCount = 0;

    public static void ClearScreen(byte index)
    {
        AddNewDrawFace(
            DrawBlendMode.StraightColor,
            new DrawVertexFace(
            new DrawVertex(
                0, 0, currentDrawVertDepth,
                0, 0, 0,
                fullPalette[activePalette, index]
            ),
            new DrawVertex(
                SCREEN_XSIZE, 0, currentDrawVertDepth,
                0, 0, 0,
                fullPalette[activePalette, index]
            ),
            new DrawVertex(
                SCREEN_XSIZE, SCREEN_YSIZE, currentDrawVertDepth,
                0, 0, 0,
                fullPalette[activePalette, index]
            ),
            new DrawVertex(
                0, SCREEN_YSIZE, currentDrawVertDepth,
                0, 0, 0,
                fullPalette[activePalette, index]
            )
        ));
    }

    public static void DrawObjectList(int Layer, float depth)
    {
        currentDrawVertDepth = depth;
        int size = drawListEntries[Layer].listSize;
        for (int i = 0; i < size; ++i)
        {
            objectLoop = drawListEntries[Layer].entityRefs[i];
            int type = objectEntityList[objectLoop].type;

            if (GamePlatform == GamePlatformTypes.Standard && activeStageList == StageListNames.STAGELIST_SPECIAL)
            {
                if (typeNames[type] == "TouchControls")
                    type = BlankObjectID;
            }

            if (type != 0)
            {
                activePlayer = 0;
                if (scriptCode[objectScriptList[type].subDraw.scriptCodePtr] > 0)
                    ProcessScript(objectScriptList[type].subDraw.scriptCodePtr, objectScriptList[type].subDraw.jumpTablePtr, ScriptSubs.SUB_DRAW);

                if (forceNextDraw)
                {
                    SubmitDrawList();
                    forceNextDraw = false;
                }
            }
        }
        if (size > 0)
        {
            SubmitDrawList();
        }
    }

    static void DrawTileLayerGeneral(int index, float drawVertDepth)
    {
        currentDrawVertDepth = drawVertDepth;
        if (activeTileLayers[index] < LAYER_COUNT)
        {
            switch (stageLayouts[activeTileLayers[index]].type)
            {
                case TileLayerTypes.LAYER_HSCROLL:
                    DrawHLineScrollLayer(index); break;
                case TileLayerTypes.LAYER_VSCROLL: DrawVLineScrollLayer(index); break;
                case TileLayerTypes.LAYER_3DFLOOR:
                    // Draw3DFloorLayer(index);
                    break;
                case TileLayerTypes.LAYER_3DSKY:
                    Draw3DSkyLayer(index);
                    break;
                default: break;
            }
        }

        // comment this out to break multiple cycling palettes in a level
        SubmitDrawList();
    }

    public static void DrawStageGFX()
    {
        forceNextDraw = false;

        waterDrawPos = waterLevel - yScrollOffsetPixels;

        if (waterDrawPos < 0)
            waterDrawPos = 0;

        if (waterDrawPos > SCREEN_YSIZE)
            waterDrawPos = SCREEN_YSIZE;

        DrawObjectList(0, Objects0Depth);
        DrawTileLayerGeneral(0, TileLayer0Depth);
        DrawObjectList(1, Objects1Depth);
        DrawTileLayerGeneral(1, TileLayer1Depth);
        DrawObjectList(2, Objects2Depth);
        DrawTileLayerGeneral(2, TileLayer2Depth);
        DrawObjectList(3, Objects3Depth);
        DrawObjectList(4, Objects4Depth);
        DrawTileLayerGeneral(3, TileLayer3Depth);
        DrawObjectList(5, Objects5Depth);
        // Hacky fix for Tails Object not working properly on non-Origins bytecode
        if (GetGlobalVariableByName("NOTIFY_1P_VS_SELECT") != 0)
        {
            DrawObjectList(7, Objects7Depth); // Extra Origins draw list (who knows why it comes before 6)
        }
        DrawObjectList(6, Objects6Depth);
        platform.UpdatePrevFramebuffer();

        if (fadeMode > 0)
        {
            currentDrawVertDepth = FadeOverlayDepth;
            DrawRectangle(0, 0, SCREEN_XSIZE, SCREEN_YSIZE, fadeColor.R, fadeColor.G, fadeColor.B, fadeColor.A);
        }
        SubmitDrawList();

        currentDrawVertDepth = FadeOverlayDepth;
        DrawDebugOverlays();

        SubmitDrawList();
    }

    public static void DrawBackbuffer()
    {
        AddNewDrawFace(
            DrawBlendMode.PauseMenu,
            new DrawVertexFace(
            new DrawVertex(
                0, 0, 0,
                0, 0, 0,
                Color.White
            ),
            new DrawVertex(
                SCREEN_XSIZE, 0, 0,
                0, 0, 0,
                Color.White
            ),
            new DrawVertex(
                0, SCREEN_YSIZE, 0,
                0, 0, 0,
                Color.White
            ),
            new DrawVertex(
                SCREEN_XSIZE, SCREEN_YSIZE, 0,
                0, 0, 0,
                Color.White
            )
        ));

        SubmitDrawList();
    }

    public static void DrawDebugOverlays()
    {
        if (showHitboxes)
        {
            for (int i = 0; i < debugHitboxCount; ++i)
            {
                DebugHitboxInfo info = debugHitboxList[i];
                {
                    int x = info.XPos + WholeToFixedPoint(info.left);
                    int y = info.YPos + WholeToFixedPoint(info.top);
                    int w = FixedPointToWhole(System.Math.Abs((info.XPos + WholeToFixedPoint(info.right)) - x));
                    int h = FixedPointToWhole(System.Math.Abs((info.YPos + WholeToFixedPoint(info.bottom)) - y));
                    x = FixedPointToWhole(x) - xScrollOffset;
                    y = FixedPointToWhole(y) - yScrollOffsetPixels;

                    switch (info.type)
                    {
                        case DebugHitboxTypes.H_TYPE_TOUCH:
                            if (showHitboxes)
                                DrawRectangle(x, y, w, h, (info.collision != 0) ? 0x80 : 0xFF, (info.collision != 0) ? 0x80 : 0x00, 0x00, 0x60);
                            break;

                        case DebugHitboxTypes.H_TYPE_BOX:
                            if (showHitboxes)
                            {
                                DrawRectangle(x, y, w, h, 0x00, 0x00, 0xFF, 0x60);
                                if ((info.collision & 1) != 0) // top
                                    DrawRectangle(x, y, w, 1, 0xFF, 0xFF, 0x00, 0xC0);
                                if ((info.collision & 8) != 0) // bottom
                                    DrawRectangle(x, y + h, w, 1, 0xFF, 0xFF, 0x00, 0xC0);
                                if ((info.collision & 2) != 0)
                                { // left
                                    int sy = y;
                                    int sh = h;
                                    if ((info.collision & 1) != 0)
                                    {
                                        sy++;
                                        sh--;
                                    }
                                    if ((info.collision & 8) != 0)
                                        sh--;
                                    DrawRectangle(x, sy, 1, sh, 0xFF, 0xFF, 0x00, 0xC0);
                                }
                                if ((info.collision & 4) != 0)
                                { // right
                                    int sy = y;
                                    int sh = h;
                                    if ((info.collision & 1) != 0)
                                    {
                                        sy++;
                                        sh--;
                                    }
                                    if ((info.collision & 8) != 0)
                                        sh--;
                                    DrawRectangle(x + w, sy, 1, sh, 0xFF, 0xFF, 0x00, 0xC0);
                                }
                            }
                            break;

                        case DebugHitboxTypes.H_TYPE_PLAT:
                            if (showHitboxes)
                            {
                                DrawRectangle(x, y, w, h, 0x00, 0xFF, 0x00, 0x60);
                                if ((info.collision & 1) != 0) // top
                                    DrawRectangle(x, y, w, 1, 0xFF, 0xFF, 0x00, 0xC0);
                                if ((info.collision & 8) != 0) // bottom
                                    DrawRectangle(x, y + h, w, 1, 0xFF, 0xFF, 0x00, 0xC0);
                            }
                            break;

                        case DebugHitboxTypes.H_TYPE_FINGER:
                            if (showTouches)
                                DrawRectangle(x + xScrollOffset, y + yScrollOffsetPixels, w, h, 0xF0, 0x00, 0xF0, 0x60);
                            break;
                    }
                }
            }
        }
        if (Engine.showPaletteOverlay)
        {
            for (var p = 0; p < PALETTE_COUNT; p++)
            {
                for (var c = 0; c < PALETTE_SIZE; c++)
                {
                    DrawRectangle(
                        SCREEN_XSIZE - (PALETTE_SIZE - c),
                        SCREEN_YSIZE - (PALETTE_COUNT - p),
                        1, 1,
                        fullPalette[p, c].R,
                        fullPalette[p, c].G,
                        fullPalette[p, c].B,
                        0xFF
                    );
                }
            }
            DrawRectangle(
                SCREEN_XSIZE - PALETTE_SIZE - 1,
                SCREEN_YSIZE - PALETTE_COUNT + activePalette,
                1, 1,
                0xff, 0, 0xff, 0xff
            );
        }
        SubmitDrawList();
    }

    public static void DrawRectangle(int XPos, int YPos, int width, int height, int R, int G, int B, int A)
    {
        if (A > 0xFF)
            A = 0xFF;

        if (width + XPos > SCREEN_XSIZE)
            width = SCREEN_XSIZE - XPos;
        if (XPos < 0)
        {
            width += XPos;
            XPos = 0;
        }

        if (height + YPos > SCREEN_YSIZE)
            height = SCREEN_YSIZE - YPos;
        if (YPos < 0)
        {
            height += YPos;
            YPos = 0;
        }
        if (width <= 0 || height <= 0 || A <= 0)
            return;

        if (A != 0xff)
        {
            SubmitDrawList();
        }

        AddNewDrawFace(
            DrawBlendMode.StraightColor,
            new DrawVertexFace(
            new DrawVertex(
                XPos,
                YPos,
                currentDrawVertDepth,
                0,
                0,
                0,
                Color.FromArgb(A, R, G, B)
            ),
            new DrawVertex(
                XPos + width,
                YPos,
                currentDrawVertDepth,
                0,
                0,
                0,
                Color.FromArgb(A, R, G, B)
            ),
            new DrawVertex(
                XPos,
                YPos + height,
                currentDrawVertDepth,
                0,
                0,
                0,
                Color.FromArgb(A, R, G, B)
            ),
            new DrawVertex(
                XPos + width,
                YPos + height,
                currentDrawVertDepth,
                0,
                0,
                0,
                Color.FromArgb(A, R, G, B)
            )
        ));
    }

    public static void DrawTintRectangle(int XPos, int YPos, int width, int height)
    {
        if (width + XPos > SCREEN_XSIZE)
            width = SCREEN_XSIZE - XPos;
        if (XPos < 0)
        {
            width += XPos;
            XPos = 0;
        }

        if (height + YPos > SCREEN_YSIZE)
            height = SCREEN_YSIZE - YPos;
        if (YPos < 0)
        {
            height += YPos;
            YPos = 0;
        }
        if (width <= 0 || height <= 0)
            return;

        SubmitDrawList();

        AddNewDrawFace(
            DrawBlendMode.StraightColor,
            new DrawVertexFace(
            new DrawVertex(
                XPos,
                YPos,
                currentDrawVertDepth,
                0,
                0,
                0,
                Color.White
            ),
            new DrawVertex(
                XPos + width,
                YPos,
                currentDrawVertDepth,
                0,
                0,
                0,
                Color.White
            ),
            new DrawVertex(
                XPos,
                YPos + height,
                currentDrawVertDepth,
                0,
                0,
                0,
                Color.White
            ),
            new DrawVertex(
                XPos + width,
                YPos + height,
                currentDrawVertDepth,
                0,
                0,
                0,
                Color.White
            )
        ));
    }

    public static void DrawScaledTintMask(int direction, int XPos, int YPos, int pivotX, int pivotY, int scale, int width, int height, int sprX, int sprY,
                            int sheetID)
    {
        DrawSpriteRotozoom((FlipFlags)direction, XPos, YPos, pivotX, pivotY, sprX, sprY, width, height, 0, scale, sheetID, hwBlendMode: DrawBlendMode.Monochrome);
    }

    public static void DrawObjectAnimation(ref ObjectScript objectScript, ref Entity entity, int XPos, int YPos)
    {
        SpriteAnimation sprAnim = animationList[objectScript.animFile.aniListOffset + entity.animation];
        SpriteFrame frame = animFrames[sprAnim.frameListOffset + entity.frame];

        int rotation;

        switch (sprAnim.rotationStyle)
        {
            case AnimrotationFlags.ROTSTYLE_NONE:
                switch ((FlipFlags)entity.direction)
                {
                    case FlipFlags.FLIP_NONE:
                        DrawSpriteFlipped(frame.pivotX + XPos, frame.pivotY + YPos, frame.width, frame.height, frame.sprX, frame.sprY, FlipFlags.FLIP_NONE,
                                          frame.sheetID);
                        break;
                    case FlipFlags.FLIP_X:
                        DrawSpriteFlipped(XPos - frame.width - frame.pivotX, frame.pivotY + YPos, frame.width, frame.height, frame.sprX,
                                          frame.sprY, FlipFlags.FLIP_X, frame.sheetID);
                        break;
                    case FlipFlags.FLIP_Y:
                        DrawSpriteFlipped(frame.pivotX + XPos, YPos - frame.height - frame.pivotY, frame.width, frame.height, frame.sprX,
                                          frame.sprY, FlipFlags.FLIP_Y, frame.sheetID);
                        break;
                    case FlipFlags.FLIP_XY:
                        DrawSpriteFlipped(XPos - frame.width - frame.pivotX, YPos - frame.height - frame.pivotY, frame.width, frame.height,
                                          frame.sprX, frame.sprY, FlipFlags.FLIP_XY, frame.sheetID);
                        break;
                    default: break;
                }
                break;
            case AnimrotationFlags.ROTSTYLE_FULL:
                DrawSpriteRotated((FlipFlags)entity.direction, XPos, YPos, -frame.pivotX, -frame.pivotY, frame.sprX, frame.sprY, frame.width, frame.height,
                                  entity.rotation, frame.sheetID);
                break;
            case AnimrotationFlags.ROTSTYLE_45DEG:
                if (entity.rotation >= 0x100)
                    DrawSpriteRotated((FlipFlags)entity.direction, XPos, YPos, -frame.pivotX, -frame.pivotY, frame.sprX, frame.sprY, frame.width,
                                      frame.height, 0x200 - ((532 - entity.rotation) >> 6 << 6), frame.sheetID);
                else
                    DrawSpriteRotated((FlipFlags)entity.direction, XPos, YPos, -frame.pivotX, -frame.pivotY, frame.sprX, frame.sprY, frame.width,
                                      frame.height, (entity.rotation + 20) >> 6 << 6, frame.sheetID);
                break;
            case AnimrotationFlags.ROTSTYLE_STATICFRAMES:
                {
                    if (entity.rotation >= 0x100)
                        rotation = 8 - ((532 - entity.rotation) >> 6);
                    else
                        rotation = (entity.rotation + 20) >> 6;
                    int frameID = entity.frame;
                    switch (rotation)
                    {
                        case 0: // 0 deg
                        case 8: // 360 deg
                            rotation = 0x00;
                            break;
                        case 1: // 45 deg
                            frameID += sprAnim.frameCount;
                            if (entity.direction != 0)
                                rotation = 0;
                            else
                                rotation = 0x80;
                            break;
                        case 2: // 90 deg
                            rotation = 0x80;
                            break;
                        case 3: // 135 deg
                            frameID += sprAnim.frameCount;
                            if (entity.direction != 0)
                                rotation = 0x80;
                            else
                                rotation = 0x100;
                            break;
                        case 4: // 180 deg
                            rotation = 0x100;
                            break;
                        case 5: // 225 deg
                            frameID += sprAnim.frameCount;
                            if (entity.direction != 0)
                                rotation = 0x100;
                            else
                                rotation = 384;
                            break;
                        case 6: // 270 deg
                            rotation = 384;
                            break;
                        case 7: // 315 deg
                            frameID += sprAnim.frameCount;
                            if (entity.direction != 0)
                                rotation = 384;
                            else
                                rotation = 0;
                            break;
                        default: break;
                    }

                    frame = animFrames[sprAnim.frameListOffset + frameID];
                    DrawSpriteRotated((FlipFlags)entity.direction, XPos, YPos, -frame.pivotX, -frame.pivotY, frame.sprX, frame.sprY, frame.width, frame.height,
                                      rotation, frame.sheetID);
                    break;
                }
            default: break;
        }
    }

    public static void DrawFace(ScriptVertex[] verts, Color colour)
    {
        int alpha = (int)((colour.A & 0x7f) / 127.0f * 255);
        colour = Color.FromArgb(alpha, colour);
        if (alpha < 1)
            return;
        if (alpha > 0xFF)
            alpha = 0xFF;
        if (alpha != 0xff)
        {
            SubmitDrawList();
        }

        AddNewDrawFace(
            DrawBlendMode.StraightColor,
            new DrawVertexFace(
            ConvertVert(verts[0]),
            ConvertVert(verts[1]),
            ConvertVert(verts[2]),
            ConvertVert(verts[3])
        ));

        DrawVertex ConvertVert(ScriptVertex v)
        {
            return new DrawVertex(
                v.x,
                v.y,
                currentDrawVertDepth + v.z,
                v.u,
                v.v,
                0,
                colour
            );
        }
    }

    public static void DrawTexturedFace(ScriptVertex[] verts, int sheetID)
    {
        var surface = Sprite.GetSpriteAt(sheetID);
        AddNewDrawFace(
            DrawBlendMode.Opaque,
            new DrawVertexFace(
            ConvertVert(verts[0]),
            ConvertVert(verts[1]),
            ConvertVert(verts[2]),
            ConvertVert(verts[3])
        ));

        DrawVertex ConvertVert(ScriptVertex v)
        {
            return new DrawVertex(
                v.x,
                v.y,
                currentDrawVertDepth + v.z,
                surface.dataStartLocation.X + v.u,
                surface.dataStartLocation.Y + v.v,
                surface.dataStartLocation.Z,
                Color.White
            );
        }
    }

    public static void DrawBitmapText(ref TextMenu tMenu, int XPos, int YPos, int scale, int spacing, int rowStart, int rowCount)
    {
        int Y = YPos << 9;
        if (rowCount < 0)
            rowCount = tMenu.rowCount;
        if (rowStart + rowCount > tMenu.rowCount)
            rowCount = tMenu.rowCount - rowStart;

        while (rowCount > 0)
        {
            int X = XPos << 9;
            for (int i = 0; i < tMenu.entrySize[rowStart]; ++i)
            {
                ushort c = tMenu.textData[tMenu.entryStart[rowStart] + i];
                FontCharacter fChar = fontCharacterList[c];
                {
                    DrawSpriteScaled(FlipFlags.FLIP_NONE, X >> 9, Y >> 9, -fChar.pivotX, -fChar.pivotY, scale, fChar.width, fChar.height, fChar.srcX,
                                 fChar.srcY, textMenuSurfaceNo);
                    X += fChar.xAdvance * scale;
                }
            }
            Y += spacing * scale;
            rowStart++;
            rowCount--;
        }
    }

    public static void Sort3DDrawList()
    {
        for (int i = 0; i < faceCount; ++i)
        {
            drawList3D[i].depth = (vertexBufferT[faceBuffer[i].d].z + vertexBufferT[faceBuffer[i].c].z + vertexBufferT[faceBuffer[i].b].z
                                   + vertexBufferT[faceBuffer[i].a].z)
                                  >> 2;
            drawList3D[i].faceID = i;
        }

        for (int i = 0; i < faceCount; ++i)
        {
            for (int j = faceCount - 1; j > i; --j)
            {
                if (drawList3D[j].depth > drawList3D[j - 1].depth)
                {
                    int faceID = drawList3D[j].faceID;
                    int depth = drawList3D[j].depth;
                    drawList3D[j].faceID = drawList3D[j - 1].faceID;
                    drawList3D[j].depth = drawList3D[j - 1].depth;
                    drawList3D[j - 1].faceID = faceID;
                    drawList3D[j - 1].depth = depth;
                }
            }
        }
    }

    public static void Draw3DScene(int spriteSheetID)
    {
        Sort3DDrawList();

        var quad = new ScriptVertex[4];
        for (int i = 0; i < faceCount; ++i)
        {
            ScriptFace face = faceBuffer[drawList3D[i].faceID];
            switch (face.flags)
            {
                default: break;
                case FaceFlags.FACE_FLAG_TEXTURED_3D:
                    if (vertexBufferT[face.a].z > 0x100 && vertexBufferT[face.b].z > 0x100 && vertexBufferT[face.c].z > 0x100
                        && vertexBufferT[face.d].z > 0x100)
                    {
                        quad[0].x = SCREEN_CENTERX + projectionX * vertexBufferT[face.a].x / vertexBufferT[face.a].z;
                        quad[0].y = SCREEN_CENTERY - projectionY * vertexBufferT[face.a].y / vertexBufferT[face.a].z;
                        quad[1].x = SCREEN_CENTERX + projectionX * vertexBufferT[face.b].x / vertexBufferT[face.b].z;
                        quad[1].y = SCREEN_CENTERY - projectionY * vertexBufferT[face.b].y / vertexBufferT[face.b].z;
                        quad[2].x = SCREEN_CENTERX + projectionX * vertexBufferT[face.c].x / vertexBufferT[face.c].z;
                        quad[2].y = SCREEN_CENTERY - projectionY * vertexBufferT[face.c].y / vertexBufferT[face.c].z;
                        quad[3].x = SCREEN_CENTERX + projectionX * vertexBufferT[face.d].x / vertexBufferT[face.d].z;
                        quad[3].y = SCREEN_CENTERY - projectionY * vertexBufferT[face.d].y / vertexBufferT[face.d].z;
                        quad[0].u = vertexBuffer[face.a].u;
                        quad[0].v = vertexBuffer[face.a].v;
                        quad[1].u = vertexBuffer[face.b].u;
                        quad[1].v = vertexBuffer[face.b].v;
                        quad[2].u = vertexBuffer[face.c].u;
                        quad[2].v = vertexBuffer[face.c].v;
                        quad[3].u = vertexBuffer[face.d].u;
                        quad[3].v = vertexBuffer[face.d].v;
                        DrawTexturedFace(quad, spriteSheetID);
                    }
                    break;
                case FaceFlags.FACE_FLAG_TEXTURED_2D:
                    quad[0].x = vertexBuffer[face.a].x;
                    quad[0].y = vertexBuffer[face.a].y;
                    quad[1].x = vertexBuffer[face.b].x;
                    quad[1].y = vertexBuffer[face.b].y;
                    quad[2].x = vertexBuffer[face.c].x;
                    quad[2].y = vertexBuffer[face.c].y;
                    quad[3].x = vertexBuffer[face.d].x;
                    quad[3].y = vertexBuffer[face.d].y;
                    quad[0].u = vertexBuffer[face.a].u;
                    quad[0].v = vertexBuffer[face.a].v;
                    quad[1].u = vertexBuffer[face.b].u;
                    quad[1].v = vertexBuffer[face.b].v;
                    quad[2].u = vertexBuffer[face.c].u;
                    quad[2].v = vertexBuffer[face.c].v;
                    quad[3].u = vertexBuffer[face.d].u;
                    quad[3].v = vertexBuffer[face.d].v;
                    DrawTexturedFace(quad, spriteSheetID);
                    break;
                case FaceFlags.FACE_FLAG_COLOURED_3D:
                    if (vertexBufferT[face.a].z > 0x100 && vertexBufferT[face.b].z > 0x100 && vertexBufferT[face.c].z > 0x100
                        && vertexBufferT[face.d].z > 0x100)
                    {
                        quad[0].x = SCREEN_CENTERX + projectionX * vertexBufferT[face.a].x / vertexBufferT[face.a].z;
                        quad[0].y = SCREEN_CENTERY - projectionY * vertexBufferT[face.a].y / vertexBufferT[face.a].z;
                        quad[1].x = SCREEN_CENTERX + projectionX * vertexBufferT[face.b].x / vertexBufferT[face.b].z;
                        quad[1].y = SCREEN_CENTERY - projectionY * vertexBufferT[face.b].y / vertexBufferT[face.b].z;
                        quad[2].x = SCREEN_CENTERX + projectionX * vertexBufferT[face.c].x / vertexBufferT[face.c].z;
                        quad[2].y = SCREEN_CENTERY - projectionY * vertexBufferT[face.c].y / vertexBufferT[face.c].z;
                        quad[3].x = SCREEN_CENTERX + projectionX * vertexBufferT[face.d].x / vertexBufferT[face.d].z;
                        quad[3].y = SCREEN_CENTERY - projectionY * vertexBufferT[face.d].y / vertexBufferT[face.d].z;
                        DrawFace(quad, face.colour);
                    }
                    break;
                case FaceFlags.FACE_FLAG_COLOURED_2D:
                    quad[0].x = vertexBuffer[face.a].x;
                    quad[0].y = vertexBuffer[face.a].y;
                    quad[1].x = vertexBuffer[face.b].x;
                    quad[1].y = vertexBuffer[face.b].y;
                    quad[2].x = vertexBuffer[face.c].x;
                    quad[2].y = vertexBuffer[face.c].y;
                    quad[3].x = vertexBuffer[face.d].x;
                    quad[3].y = vertexBuffer[face.d].y;
                    DrawFace(quad, face.colour);
                    break;
            }
        }
    }
}