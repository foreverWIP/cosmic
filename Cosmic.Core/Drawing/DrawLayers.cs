global using static Cosmic.Core.DrawLayers;
using System;
using System.Drawing;
using System.Numerics;
using static Cosmic.Core.Drawing;

namespace Cosmic.Core;

public static unsafe class DrawLayers
{
    static void AddHChunkStripToList(
        int layerID,
        int chunkIndex,
        FlipFlags direction,
        CollisionSolidity solidity,
        int x, int y,
        int height,
        int vStart
    )
    {
        if (showHitboxes && activeTileLayers[layerID] != 0)
        {
            return;
        }

        const int width = TILE_SIZE;

        var vEnd = vStart + height;

        var uStart = 0;
        var uEnd = TILE_SIZE;

        if (direction.HasFlag(FlipFlags.FLIP_X))
        {
            (uStart, uEnd) = (uEnd, uStart);
        }

        if (direction.HasFlag(FlipFlags.FLIP_Y))
        {
            vStart++;
            vEnd = vStart - height;
        }

        var blendMode = showHitboxes ? DrawBlendMode.TilesetDebug : DrawBlendMode.Tileset;
        var color = Color.White;
        if (showHitboxes)
        {
            color = solidity switch
            {
                CollisionSolidity.SOLID_ALL => Color.White,
                CollisionSolidity.SOLID_TOP => Color.Blue,
                CollisionSolidity.SOLID_LRB => Color.Yellow,
                _ => Color.FromArgb(31, Color.Magenta)
            };
        }

        AddNewDrawFace(
            blendMode,
            new DrawVertexFace(
            new DrawVertex(
                x,
                y,
                currentDrawVertDepth,
                uStart / (float)TILE_SIZE,
                vStart / (float)TILE_SIZE,
                chunkIndex / (float)TILE_COUNT,
                color
            ),
            new DrawVertex(
                (x + width),
                y,
                currentDrawVertDepth,
                uEnd / (float)TILE_SIZE,
                vStart / (float)TILE_SIZE,
                chunkIndex / (float)TILE_COUNT,
                color
            ),
            new DrawVertex(
                x,
                (y + height),
                currentDrawVertDepth,
                uStart / (float)TILE_SIZE,
                vEnd / (float)TILE_SIZE,
                chunkIndex / (float)TILE_COUNT,
                color
            ),
            new DrawVertex(
                (x + width),
                (y + height),
                currentDrawVertDepth,
                uEnd / (float)TILE_SIZE,
                vEnd / (float)TILE_SIZE,
                chunkIndex / (float)TILE_COUNT,
                color
            )
        ));
    }

    public static void DrawHLineScrollLayer(int layerID)
    {
        fixed (TileLayer* layer = &stageLayouts[activeTileLayers[layerID]])
        {
            fixed (int* bgDeformationData0Ptr = bgDeformationData0)
            {
                fixed (int* bgDeformationData1Ptr = bgDeformationData1)
                {
                    fixed (int* bgDeformationData2Ptr = bgDeformationData2)
                    {
                        fixed (int* bgDeformationData3Ptr = bgDeformationData3)
                        {
                            int screenwidthInTiles = (SCREEN_XSIZE / TILE_SIZE) - 1;
                            int layerWidthInChunks = layer->xsize;
                            int layerHeightInChunks = layer->ysize;
                            bool aboveMidPoint = layerID >= tLayerMidPoint;

                            byte[] lineScroll = layer->lineScroll;
                            int* deformationData;
                            int* deformationDataW;

                            int yscrollOffset = 0;

                            if (activeTileLayers[layerID] != 0)
                            {
                                // BG Layer
                                int yScroll = yScrollOffsetPixels * layer->parallaxFactor >> 8;
                                int fullheight = layerHeightInChunks << 7;
                                layer->scrollPos += layer->scrollSpeed;
                                if (layer->scrollPos > WholeToFixedPoint(fullheight))
                                {
                                    layer->scrollPos -= WholeToFixedPoint(fullheight);
                                }
                                yscrollOffset = (yScroll + FixedPointToWhole(layer->scrollPos)) % fullheight;
                                layerHeightInChunks = fullheight >> 7;
                                deformationData = &bgDeformationData2Ptr[(byte)(yscrollOffset + layer->deformationOffset)];
                                deformationDataW = &bgDeformationData3Ptr[(byte)(yscrollOffset + waterDrawPos + layer->deformationOffsetW)];
                            }
                            else
                            {
                                // FG Layer
                                lastXSizeInChunks = layer->xsize;
                                yscrollOffset = yScrollOffsetPixels;
                                for (int i = 0; i < PARALLAX_COUNT; ++i)
                                {
                                    hParallax.linePos[i] = xScrollOffset;
                                }
                                deformationData = &bgDeformationData0Ptr[(byte)(yscrollOffset + layer->deformationOffset)];
                                deformationDataW = &bgDeformationData1Ptr[(byte)(yscrollOffset + waterDrawPos + layer->deformationOffsetW)];
                            }

                            if (layer->type == TileLayerTypes.LAYER_HSCROLL)
                            {
                                if (lastXSizeInChunks != layerWidthInChunks)
                                {
                                    int fullLayerwidth = layerWidthInChunks << 7;
                                    for (int i = 0; i < hParallax.entryCount; ++i)
                                    {
                                        hParallax.linePos[i] = xScrollOffset * hParallax.parallaxFactor[i] >> 8;
                                        hParallax.scrollPos[i] += hParallax.scrollSpeed[i];
                                        if (hParallax.scrollPos[i] > WholeToFixedPoint(fullLayerwidth))
                                        {
                                            hParallax.scrollPos[i] -= WholeToFixedPoint(fullLayerwidth);
                                        }
                                        if (hParallax.scrollPos[i] < 0)
                                        {
                                            hParallax.scrollPos[i] += WholeToFixedPoint(fullLayerwidth);
                                        }
                                        hParallax.linePos[i] += FixedPointToWhole(hParallax.scrollPos[i]);
                                        hParallax.linePos[i] %= fullLayerwidth;
                                    }
                                }
                                lastXSizeInChunks = layerWidthInChunks;
                            }

                            int tileYPos = yscrollOffset % (layerHeightInChunks * CHUNK_SIZE);
                            tileYPos -= TILE_SIZE;
                            if (tileYPos < 0)
                            {
                                tileYPos += layerHeightInChunks << 7;
                            }
                            fixed (byte* lineScrollPtr = lineScroll)
                            {
                                byte* scrollIndex = &lineScrollPtr[tileYPos];

                                int tileY16 = tileYPos % TILE_SIZE;
                                int chunkY = tileYPos / CHUNK_SIZE;
                                int tileY = (tileYPos % CHUNK_SIZE) / TILE_SIZE;

                                var chunkXStore = 0;
                                var tileYPosStore = 0;
                                var tileXPxRemainStore = 0;
                                var tileHeightStore = 0;
                                var chunkStore = new int[screenwidthInTiles + 3];
                                var tileVStartStore = new int[screenwidthInTiles + 3];
                                var finalTileOffsetYStore = new int[screenwidthInTiles + 3];

                                var drawableLinesCount = -TILE_SIZE;
                                {
                                    while (drawableLinesCount < SCREEN_YSIZE + TILE_SIZE)
                                    {
                                        tileHeightStore++;

                                        int chunkX = hParallax.linePos[*scrollIndex];
                                        chunkX -= TILE_SIZE;
                                        if (drawableLinesCount < waterDrawPos)
                                        {
                                            int deform = 0;
                                            if (hParallax.deform[*scrollIndex] != 0)
                                                deform = *deformationData;

                                            chunkX += deform;
                                            ++deformationData;
                                        }
                                        else if (!showHitboxes)
                                        {
                                            if (hParallax.deform[*scrollIndex] != 0)
                                                chunkX += *deformationDataW;
                                            ++deformationDataW;
                                        }
                                        ++scrollIndex;
                                        chunkX = PositiveClampedModuloInclusive(chunkX, layerWidthInChunks * CHUNK_SIZE);
                                        if (drawableLinesCount == -TILE_SIZE)
                                        {
                                            chunkXStore = chunkX;
                                        }
                                        int chunkXPosInChunks = chunkX / CHUNK_SIZE;
                                        int tileXPxRemain = TILE_SIZE - (chunkX % TILE_SIZE);
                                        if (drawableLinesCount == -TILE_SIZE)
                                        {
                                            tileXPxRemainStore = tileXPxRemain;
                                        }
                                        int chunk = (layer->tiles[(chunkX >> 7) + (chunkY << 8)] << 6) + ((chunkX % CHUNK_SIZE) / TILE_SIZE) + 8 * tileY;

                                        int chunkTileX = ((chunkX % CHUNK_SIZE) >> 4) + 1;

                                        if (chunkXStore != chunkX || tileY16 == 0)
                                        {
                                            for (var x = 0; x < screenwidthInTiles + 3; x++)
                                            {
                                                if (chunkTileX <= 7)
                                                {
                                                    ++chunk;
                                                }
                                                else
                                                {
                                                    if (++chunkXPosInChunks == layerWidthInChunks)
                                                        chunkXPosInChunks = 0;
                                                    chunkTileX = 0;
                                                    chunk = (layer->tiles[chunkXPosInChunks + (chunkY << 8)] * 8 * 8) + 8 * tileY;
                                                }

                                                var tileIndex = (tiles128x128.gfxDataPos[chunkStore[x]] + finalTileOffsetYStore[x]) / TILE_SIZE / TILE_SIZE;
                                                var chunkIndex = layer->tiles[chunkXPosInChunks + (chunkY << 8)];

                                                if (tiles128x128.visualPlane[chunkStore[x]] == (byte)(aboveMidPoint ? 1 : 0))
                                                {
                                                    if (tileIndex > 0)
                                                    {
                                                        AddHChunkStripToList(
                                                            layerID,
                                                            tileIndex,
                                                            tiles128x128.direction[chunkStore[x]],
                                                            tiles128x128.collisionFlags[playerList[activePlayer].collisionPlane, chunkStore[x]],
                                                            ((x - 1) * TILE_SIZE) + tileXPxRemainStore,
                                                            tileYPosStore,
                                                            tileHeightStore,
                                                            tileVStartStore[x]
                                                        );
                                                    }
                                                }

                                                var finalTileOffsetY = tiles128x128.direction[chunk] switch
                                                {
                                                    FlipFlags.FLIP_NONE => TILE_SIZE * tileY16,
                                                    FlipFlags.FLIP_X => TILE_SIZE * tileY16 + (TILE_SIZE - 1),
                                                    FlipFlags.FLIP_Y => TILE_SIZE * ((TILE_SIZE - 1) - tileY16),
                                                    FlipFlags.FLIP_XY => TILE_SIZE * ((TILE_SIZE - 1) - tileY16) + (TILE_SIZE - 1),
                                                    _ => throw new InvalidOperationException("what")
                                                };

                                                tileVStartStore[x] = ((tiles128x128.gfxDataPos[chunk] + finalTileOffsetY) / TILE_SIZE) % TILE_SIZE;
                                                chunkStore[x] = chunk;
                                                finalTileOffsetYStore[x] = finalTileOffsetY;

                                                ++chunkTileX;
                                            }
                                        }

                                        if (chunkXStore != chunkX || tileY16 == 0)
                                        {
                                            tileXPxRemainStore = tileXPxRemain;
                                            tileYPosStore = drawableLinesCount;
                                            chunkXStore = chunkX;
                                            tileHeightStore = 0;
                                        }

                                        if (++tileY16 > TILE_SIZE - 1)
                                        {
                                            tileY16 = 0;
                                            ++tileY;
                                        }
                                        if (tileY > 7)
                                        {
                                            if (++chunkY == layerHeightInChunks)
                                            {
                                                chunkY = 0;
                                                scrollIndex -= CHUNK_SIZE * layerHeightInChunks;
                                            }
                                            tileY = 0;
                                        }

                                        drawableLinesCount++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public static void DrawVLineScrollLayer(int layerID)
    {
        TileLayer layer = stageLayouts[activeTileLayers[layerID]];
        if (layer.xsize == 0 || layer.ysize == 0)
            return;

        /*
        int layerwidth = layer.xsize;
        int layerheight = layer.ysize;
        bool aboveMidPoint = layerID >= tLayerMidPoint;

        byte[] lineScroll;
        int* deformationData;

        int xscrollOffset = 0;
        fixed (int* bgDeformationData0Ptr = bgDeformationData0)
        {
            fixed (int* bgDeformationData2Ptr = bgDeformationData2)
            {
                if (activeTileLayers[layerID] != 0)
                { // BG Layer
                    int xScroll = xScrollOffset * layer.parallaxFactor >> 8;
                    int fullLayerwidth = layerwidth << 7;
                    stageLayouts[activeTileLayers[layerID]].scrollPos += layer.scrollSpeed;
                    layer = stageLayouts[activeTileLayers[layerID]];
                    if (layer.scrollPos > WholeToFixedPoint(fullLayerwidth))
                    {
                        stageLayouts[activeTileLayers[layerID]].scrollPos -= WholeToFixedPoint(fullLayerwidth);
                        layer = stageLayouts[activeTileLayers[layerID]];
                    }
                    xscrollOffset = (xScroll + FixedPointToWhole(layer.scrollPos)) % fullLayerwidth;
                    layerwidth = fullLayerwidth >> 7;
                    lineScroll = layer.lineScroll;
                    deformationData = &bgDeformationData2Ptr[(byte)(xscrollOffset + layer.deformationOffset)];
                }
                else
                { // FG Layer
                    lastYSize = layer.ysize;
                    xscrollOffset = xScrollOffset;
                    lineScroll = layer.lineScroll;
                    vParallax.linePos[0] = yScrollOffsetPixels;
                    vParallax.deform[0] = 1;
                    deformationData = &bgDeformationData0Ptr[(byte)(xScrollOffset + layer.deformationOffset)];
                }

                if (layer.type == TileLayerTypes.LAYER_VSCROLL)
                {
                    if (lastYSize != layerheight)
                    {
                        int fullLayerheight = layerheight << 7;
                        for (int i = 0; i < vParallax.entryCount; ++i)
                        {
                            vParallax.linePos[i] = yScrollOffsetPixels * vParallax.parallaxFactor[i] >> 8;

                            vParallax.scrollPos[i] += WholeToFixedPoint(vParallax.scrollPos[i]);
                            if (vParallax.scrollPos[i] > WholeToFixedPoint(fullLayerheight))
                                vParallax.scrollPos[i] -= WholeToFixedPoint(fullLayerheight);

                            vParallax.linePos[i] += FixedPointToWhole(vParallax.scrollPos[i]);
                            vParallax.linePos[i] %= fullLayerheight;
                        }
                        layerheight = fullLayerheight >> 7;
                    }
                    lastYSize = layerheight;
                }

                uint* frameBufferPtr = (uint*)swFrameBuffer;
                activePalette = gfxLineBuffer[0];
                int tileXPos = xscrollOffset % (layerheight << 7);
                if (tileXPos < 0)
                    tileXPos += layerheight << 7;
                fixed (byte* scrollIndexPtr = lineScroll)
                {
                    byte* scrollIndex = &scrollIndexPtr[tileXPos];
                    int chunkX = tileXPos >> 7;
                    int tileX16 = tileXPos & 0xF;
                    int tileX = (tileXPos & 0x7F) >> 4;

                    // Draw Above Water (if applicable)
                    int drawableLines = SCREEN_XSIZE;
                    fixed (byte* tilesetGFXDataPtr = tilesetGFXData)
                    {
                        while (drawableLines-- != 0)
                        {
                            int chunkY = vParallax.linePos[*scrollIndex];
                            if (vParallax.deform[*scrollIndex] != 0)
                                chunkY += *deformationData;
                            ++deformationData;
                            ++scrollIndex;

                            int fullLayerHeight = layerheight << 7;
                            if (chunkY < 0)
                                chunkY += fullLayerHeight;
                            if (chunkY >= fullLayerHeight)
                                chunkY -= fullLayerHeight;

                            int chunkYPos = chunkY >> 7;
                            int tileY = chunkY & 0xF;
                            int tileYPxRemain = TILE_SIZE - tileY;
                            int chunk = (layer.tiles[chunkX + (chunkY >> 7 << 8)] << 6) + tileX + 8 * ((chunkY & 0x7F) >> 4);
                            int tileOffsetXFlipX = 0xF - tileX16;
                            int tileOffsetXFlipY = tileX16 + SCREEN_YSIZE;
                            int tileOffsetXFlipXY = 0xFF - tileX16;
                            int lineRemain = SCREEN_YSIZE;

                            byte* gfxDataPtr = null;
                            int tilePxLineCnt = tileYPxRemain;

                            // Draw the first tile to the left
                            if (tiles128x128.visualPlane[chunk] == (byte)(aboveMidPoint ? 1 : 0))
                            {
                                lineRemain -= tilePxLineCnt;
                                switch (tiles128x128.direction[chunk])
                                {
                                    case FlipFlags.FLIP_NONE:
                                        gfxDataPtr = &tilesetGFXDataPtr[TILE_SIZE * tileY + tileX16 + tiles128x128.gfxDataPos[chunk]];
                                        while (tilePxLineCnt-- != 0)
                                        {
                                            if (*gfxDataPtr > 0)
                                            {
                                                // *frameBufferPtr = fullPalette32[activePalette, *gfxDataPtr];
                                            }
                                            frameBufferPtr += SCREEN_XSIZE;
                                            gfxDataPtr += TILE_SIZE;
                                        }
                                        break;

                                    case FlipFlags.FLIP_X:
                                        gfxDataPtr = &tilesetGFXDataPtr[TILE_SIZE * tileY + tileOffsetXFlipX + tiles128x128.gfxDataPos[chunk]];
                                        while (tilePxLineCnt-- != 0)
                                        {
                                            if (*gfxDataPtr > 0)
                                            {
                                                // *frameBufferPtr = fullPalette32[activePalette, *gfxDataPtr];
                                            }
                                            frameBufferPtr += SCREEN_XSIZE;
                                            gfxDataPtr += TILE_SIZE;
                                        }
                                        break;

                                    case FlipFlags.FLIP_Y:
                                        gfxDataPtr = &tilesetGFXDataPtr[tileOffsetXFlipY + tiles128x128.gfxDataPos[chunk] - TILE_SIZE * tileY];
                                        while (tilePxLineCnt-- != 0)
                                        {
                                            if (*gfxDataPtr > 0)
                                            {
                                                // *frameBufferPtr = fullPalette32[activePalette, *gfxDataPtr];
                                            }
                                            frameBufferPtr += SCREEN_XSIZE;
                                            gfxDataPtr -= TILE_SIZE;
                                        }
                                        break;

                                    case FlipFlags.FLIP_XY:
                                        gfxDataPtr = &tilesetGFXDataPtr[tileOffsetXFlipXY + tiles128x128.gfxDataPos[chunk] - TILE_SIZE * tileY];
                                        while (tilePxLineCnt-- != 0)
                                        {
                                            if (*gfxDataPtr > 0)
                                            {
                                                // *frameBufferPtr = fullPalette32[activePalette, *gfxDataPtr];
                                            }
                                            frameBufferPtr += SCREEN_XSIZE;
                                            gfxDataPtr -= TILE_SIZE;
                                        }
                                        break;

                                    default: break;
                                }
                            }
                            else
                            {
                                frameBufferPtr += SCREEN_XSIZE * tileYPxRemain;
                                lineRemain -= tilePxLineCnt;
                            }

                            // Draw the bulk of the tiles
                            int chunkTileY = ((chunkY & 0x7F) >> 4) + 1;
                            int tilesPerLine = (SCREEN_YSIZE >> 4) - 1;

                            while (tilesPerLine-- != 0)
                            {
                                if (chunkTileY < 8)
                                {
                                    chunk += 8;
                                }
                                else
                                {
                                    if (++chunkYPos == layerheight)
                                        chunkYPos = 0;

                                    chunkTileY = 0;
                                    chunk = (layer.tiles[chunkX + (chunkYPos << 8)] << 6) + tileX;
                                }
                                lineRemain -= TILE_SIZE;

                                if (tiles128x128.visualPlane[chunk] == (byte)(aboveMidPoint ? 1 : 0))
                                {
                                    switch (tiles128x128.direction[chunk])
                                    {
                                        case FlipFlags.FLIP_NONE:
                                            gfxDataPtr = &tilesetGFXDataPtr[tiles128x128.gfxDataPos[chunk] + tileX16];
                                            for (var p = 0; p < 16; p++)
                                            {
                                                if (*gfxDataPtr > 0)
                                                {
                                                    // *frameBufferPtr = fullPalette32[activePalette, *gfxDataPtr];
                                                }
                                                frameBufferPtr += SCREEN_XSIZE;
                                                if (p < 15)
                                                    gfxDataPtr += TILE_SIZE;
                                            }
                                            break;

                                        case FlipFlags.FLIP_X:
                                            gfxDataPtr = &tilesetGFXDataPtr[tiles128x128.gfxDataPos[chunk] + tileOffsetXFlipX];
                                            for (var p = 0; p < 16; p++)
                                            {
                                                if (*gfxDataPtr > 0)
                                                {
                                                    // *frameBufferPtr = fullPalette32[activePalette, *gfxDataPtr];
                                                }
                                                frameBufferPtr += SCREEN_XSIZE;
                                                if (p < 15)
                                                    gfxDataPtr += TILE_SIZE;
                                            }
                                            break;

                                        case FlipFlags.FLIP_Y:
                                            gfxDataPtr = &tilesetGFXDataPtr[tiles128x128.gfxDataPos[chunk] + tileOffsetXFlipY];
                                            for (var p = 0; p < 16; p++)
                                            {
                                                if (*gfxDataPtr > 0)
                                                {
                                                    // *frameBufferPtr = fullPalette32[activePalette, *gfxDataPtr];
                                                }
                                                frameBufferPtr += SCREEN_XSIZE;
                                                if (p < 15)
                                                    gfxDataPtr -= TILE_SIZE;
                                            }
                                            break;

                                        case FlipFlags.FLIP_XY:
                                            gfxDataPtr = &tilesetGFXDataPtr[tiles128x128.gfxDataPos[chunk] + tileOffsetXFlipXY];
                                            for (var p = 0; p < 16; p++)
                                            {
                                                if (*gfxDataPtr > 0)
                                                {
                                                    // *frameBufferPtr = fullPalette32[activePalette, *gfxDataPtr];
                                                }
                                                frameBufferPtr += SCREEN_XSIZE;
                                                if (p < 15)
                                                    gfxDataPtr -= TILE_SIZE;
                                            }
                                            break;
                                    }
                                }
                                else
                                {
                                    frameBufferPtr += SCREEN_XSIZE * TILE_SIZE;
                                }
                                ++chunkTileY;
                            }

                            // Draw any remaining tiles
                            while (lineRemain > 0)
                            {
                                if (chunkTileY < 8)
                                {
                                    chunk += 8;
                                }
                                else
                                {
                                    if (++chunkYPos == layerheight)
                                        chunkYPos = 0;

                                    chunkTileY = 0;
                                    chunk = (layer.tiles[chunkX + (chunkYPos << 8)] << 6) + tileX;
                                }

                                tilePxLineCnt = lineRemain >= TILE_SIZE ? TILE_SIZE : lineRemain;
                                lineRemain -= tilePxLineCnt;

                                if (tiles128x128.visualPlane[chunk] == (byte)(aboveMidPoint ? 1 : 0))
                                {
                                    switch (tiles128x128.direction[chunk])
                                    {
                                        case FlipFlags.FLIP_NONE:
                                            gfxDataPtr = &tilesetGFXDataPtr[tiles128x128.gfxDataPos[chunk] + tileX16];
                                            while (tilePxLineCnt-- != 0)
                                            {
                                                if (*gfxDataPtr > 0)
                                                {
                                                    // *frameBufferPtr = fullPalette32[activePalette, *gfxDataPtr];
                                                }
                                                frameBufferPtr += SCREEN_XSIZE;
                                                gfxDataPtr += TILE_SIZE;
                                            }
                                            break;

                                        case FlipFlags.FLIP_X:
                                            gfxDataPtr = &tilesetGFXDataPtr[tiles128x128.gfxDataPos[chunk] + tileOffsetXFlipX];
                                            while (tilePxLineCnt-- != 0)
                                            {
                                                if (*gfxDataPtr > 0)
                                                {
                                                    // *frameBufferPtr = fullPalette32[activePalette, *gfxDataPtr];
                                                }
                                                frameBufferPtr += SCREEN_XSIZE;
                                                gfxDataPtr += TILE_SIZE;
                                            }
                                            break;

                                        case FlipFlags.FLIP_Y:
                                            gfxDataPtr = &tilesetGFXDataPtr[tiles128x128.gfxDataPos[chunk] + tileOffsetXFlipY];
                                            while (tilePxLineCnt-- != 0)
                                            {
                                                if (*gfxDataPtr > 0)
                                                {
                                                    // *frameBufferPtr = fullPalette32[activePalette, *gfxDataPtr];
                                                }
                                                frameBufferPtr += SCREEN_XSIZE;
                                                gfxDataPtr -= TILE_SIZE;
                                            }
                                            break;

                                        case FlipFlags.FLIP_XY:
                                            gfxDataPtr = &tilesetGFXDataPtr[tiles128x128.gfxDataPos[chunk] + tileOffsetXFlipXY];
                                            while (tilePxLineCnt-- != 0)
                                            {
                                                if (*gfxDataPtr > 0)
                                                {
                                                    // *frameBufferPtr = fullPalette32[activePalette, *gfxDataPtr];
                                                }
                                                frameBufferPtr += SCREEN_XSIZE;
                                                gfxDataPtr -= TILE_SIZE;
                                            }
                                            break;

                                        default: break;
                                    }
                                }
                                else
                                {
                                    frameBufferPtr += SCREEN_XSIZE * tilePxLineCnt;
                                }
                                chunkTileY++;
                            }

                            if (++tileX16 >= TILE_SIZE)
                            {
                                tileX16 = 0;
                                ++tileX;
                            }

                            if (tileX >= 8)
                            {
                                if (++chunkX == layerwidth)
                                {
                                    chunkX = 0;
                                    scrollIndex -= 0x80 * layerwidth;
                                }
                                tileX = 0;
                            }

                            frameBufferPtr -= SCREEN_XSIZE - 1;
                        }
                    }
                }
            }
        }
        */
    }

    /*
    public static void Draw3DFloorLayer(int layerID)
    {
        return;

        fixed (TileLayer* layer = &stageLayouts[activeTileLayers[layerID]])
        {
            fixed (byte* gfxLineBufferPtrPtr = gfxLineBuffer)
            {
                fixed (byte* tilesetGFXDataPtr = tilesetGFXData)
                {
                    if (layer->xsize == 0 || layer->ysize == 0)
                        return;

                    int layerWidth = layer->xsize << 7;
                    int layerHeight = layer->ysize << 7;
                    int layerYPos = layer->YPos;
                    int layerZPos = layer->ZPos;
                    int sinValue = sinMLookupTable[layer->angle];
                    int cosValue = cosMLookupTable[layer->angle];
                    byte* gfxLineBufferPtr = &gfxLineBufferPtrPtr[((SCREEN_YSIZE / 2) + 12)];
                    uint* frameBufferPtr = &((uint*)swFrameBuffer)[((SCREEN_YSIZE / 2) + 12) * SCREEN_XSIZE];
                    int layerXPos = layer->XPos >> 4;
                    int ZBuffer = layerZPos >> 4;
                    for (int i = 4; i < ((SCREEN_YSIZE / 2) - 8); ++i)
                    {
                        if ((i & 1) == 0)
                        {
                            activePalette = *gfxLineBufferPtr;
                            gfxLineBufferPtr++;
                        }
                        int XBuffer = layerYPos / (i * 512) * -cosValue >> 8;
                        int YBuffer = sinValue * (layerYPos / (i * 512)) >> 8;
                        int XPos = layerXPos + (3 * sinValue * (layerYPos / (i * 512)) / 4) - XBuffer * SCREEN_CENTERX;
                        int YPos = ZBuffer + (3 * cosValue * (layerYPos / (i * 512)) / 4) - YBuffer * SCREEN_CENTERX;
                        int lineBuffer = 0;
                        while (lineBuffer < SCREEN_XSIZE)
                        {
                            int tileX = XPos / 4096;
                            int tileY = YPos / 4096;
                            if (tileX > -1 && tileX < layerWidth && tileY > -1 && tileY < layerHeight)
                            {
                                int chunk = tile3DFloorIndices[(FixedPointToWhole(YPos) << 8) + FixedPointToWhole(XPos)];
                                byte* tilePixel = &tilesetGFXDataPtr[tiles128x128.gfxDataPos[chunk]];
                                switch (tiles128x128.direction[chunk])
                                {
                                    case FlipFlags.FLIP_NONE: tilePixel += 16 * (tileY % TILE_SIZE) + (tileX % TILE_SIZE); break;
                                    case FlipFlags.FLIP_X: tilePixel += 16 * (tileY % TILE_SIZE) + 15 - (tileX % TILE_SIZE); break;
                                    case FlipFlags.FLIP_Y: tilePixel += (tileX % TILE_SIZE) + SCREEN_YSIZE - 16 * (tileY % TILE_SIZE); break;
                                    case FlipFlags.FLIP_XY: tilePixel += 15 - (tileX % TILE_SIZE) + SCREEN_YSIZE - 16 * (tileY % TILE_SIZE); break;
                                    default: break;
                                }

                                if (*tilePixel > 0)
                                {
                                    // *frameBufferPtr = fullPalette32[activePalette, *tilePixel];
                                }
                            }
                            ++frameBufferPtr;
                            ++lineBuffer;
                            XPos += XBuffer;
                            YPos += YBuffer;
                        }
                    }
                }
            }
        }
    }
    */

    public static int Floor3DX;
    public static int Floor3DY;
    public static int Floor3DZ;
    public static int Floor3DAngle;

    public static void Draw3DSkyLayer(int layerID)
    {
        TileLayer layer = stageLayouts[activeTileLayers[layerID]];
        if (layer.xsize == 0 || layer.ysize == 0)
            return;

        Floor3DX = layer.XPos;
        Floor3DY = layer.YPos;
        Floor3DZ = layer.ZPos;
        Floor3DAngle = layer.angle;

        /*
        for (var ly = 0; ly < layer.ysize; ly++)
        {
            for (var lx = 0; lx < layer.xsize; lx++)
            {
                for (var cy = 0; cy < 8; cy++)
                {
                    for (var cx = 0; cx < 8; cx++)
                    {
                        var totalCx = lx * 8 + cx;
                        var totalCy = ly * 8 + cy;
                        var tiles128x128Index = (layer.tiles[lx + (ly << 8)] * 8 * 8) + 8 * cy + cx;
                        var chunk = tiles128x128.tileIndex[tiles128x128Index];
                        var flip = tiles128x128.direction[tiles128x128Index];

                        var aPos = new Vector3(totalCx * TILE_SIZE, totalCy * TILE_SIZE, 0);
                        var bPos = new Vector3(totalCx * TILE_SIZE + TILE_SIZE, totalCy * TILE_SIZE, 0);
                        var cPos = new Vector3(totalCx * TILE_SIZE, totalCy * TILE_SIZE + TILE_SIZE, 0);
                        var dPos = new Vector3(totalCx * TILE_SIZE + TILE_SIZE, totalCy * TILE_SIZE + TILE_SIZE, 0);

                        var uvTop = flip.HasFlag(FlipFlags.FLIP_Y) ? 1 : 0;
                        var uvBottom = flip.HasFlag(FlipFlags.FLIP_Y) ? 0 : 1;
                        var uvLeft = flip.HasFlag(FlipFlags.FLIP_X) ? 1 : 0;
                        var uvRight = flip.HasFlag(FlipFlags.FLIP_X) ? 0 : 1;

                        AddNewDrawFace(
                            DrawBlendMode.Floor3D,
                            new DrawVertexFace(
                                new DrawVertex(aPos, uvLeft, uvTop, chunk / (float)TILE_COUNT, Color.White),
                                new DrawVertex(bPos, uvRight, uvTop, chunk / (float)TILE_COUNT, Color.White),
                                new DrawVertex(cPos, uvLeft, uvBottom, chunk / (float)TILE_COUNT, Color.White),
                                new DrawVertex(dPos, uvRight, uvBottom, chunk / (float)TILE_COUNT, Color.White)
                            ),
                            true
                        );
                    }
                }
            }
        }
        */


        const int floorStart = SCREEN_YSIZE / 2 + 12;
        // const int floorStart = 0;

        AddNewDrawFace(
            DrawBlendMode.Floor3D,
            new DrawVertexFace(
            new DrawVertex(
                0, floorStart, currentDrawVertDepth,
                0, 0, -1,
                Color.White
            ),
            new DrawVertex(
                SCREEN_XSIZE, floorStart, currentDrawVertDepth,
                1, 0, -1,
                Color.White
            ),
            new DrawVertex(
                0, SCREEN_YSIZE, currentDrawVertDepth,
                0, 1, -1,
                Color.White
            ),
            new DrawVertex(
                SCREEN_XSIZE, SCREEN_YSIZE, currentDrawVertDepth,
                1, 1, -1,
                Color.White
            )
        ));

        /*
        for (int y = 0; y < 256; ++y) {
            for (int x = 0; x < 256; ++x) {
                int c = stageLayouts[layerID].tiles[(x / 8) + ((y / 8) * 256)] * 64;
                int tx = x % 8;
                tile3DFloorBuffer[x + (y * 256)] = c + tx + ((y % 8) * 8);
            }
        }
        */

        /*
        fixed (byte* gfxLineBufferPtrPtr = gfxLineBuffer)
        {
            fixed (byte* tilesetGFXDataPtr = tilesetGFXData)
            {
                if (layer.xsize == 0 || layer.ysize == 0)
                    return;

                int layerWidth = layer.xsize << 7;
                int layerHeight = layer.ysize << 7;
                int layerYPos = layer.YPos;
                int layerZPos = layer.ZPos;
                int sinValue = sinMLookupTable[layer.angle];
                int cosValue = cosMLookupTable[layer.angle];
                int layerXPos = layer.XPos >> 4;
                int ZBuffer = layerZPos >> 4;
                for (int i = 4; i < ((SCREEN_YSIZE / 2) - 8); ++i)
                {
                    int XBuffer = layerYPos / (i * 512) * -cosValue >> 8;
                    int YBuffer = sinValue * (layerYPos / (i * 512)) >> 8;
                    int XPos = layerXPos + (3 * sinValue * (layerYPos / (i * 512)) / 4) - XBuffer * SCREEN_CENTERX;
                    int YPos = ZBuffer + (3 * cosValue * (layerYPos / (i * 512)) / 4) - YBuffer * SCREEN_CENTERX;
                    int lineBuffer = 0;
                    while (lineBuffer < SCREEN_XSIZE)
                    {
                        int tileX = XPos / 4096;
                        int tileY = YPos / 4096;
                        if (tileX > -1 && tileX < layerWidth && tileY > -1 && tileY < layerHeight)
                        {
                            int chunk = tile3DFloorIndices[(FixedPointToWhole(YPos) << 8) + FixedPointToWhole(XPos)];
                            byte* tilePixel = &tilesetGFXDataPtr[tiles128x128.gfxDataPos[chunk]];
                            switch (tiles128x128.direction[chunk])
                            {
                                case FlipFlags.FLIP_NONE: tilePixel += 16 * (tileY % TILE_SIZE) + (tileX % TILE_SIZE); break;
                                case FlipFlags.FLIP_X: tilePixel += 16 * (tileY % TILE_SIZE) + 15 - (tileX % TILE_SIZE); break;
                                case FlipFlags.FLIP_Y: tilePixel += (tileX % TILE_SIZE) + SCREEN_YSIZE - 16 * (tileY % TILE_SIZE); break;
                                case FlipFlags.FLIP_XY: tilePixel += 15 - (tileX % TILE_SIZE) + SCREEN_YSIZE - 16 * (tileY % TILE_SIZE); break;
                                default: break;
                            }

                            if (*tilePixel > 0)
                            {

                            }
                        }
                        ++lineBuffer;
                        XPos += XBuffer;
                        YPos += YBuffer;
                    }
                }
            }
        }
        */
    }
}