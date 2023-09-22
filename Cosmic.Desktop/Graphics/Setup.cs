using Cosmic.Core;
using Cosmic.Graphics;
using static Cosmic.Core.Collision;
using static Cosmic.Core.EngineStuff;
using static Cosmic.Core.Palette;
using static Cosmic.Core.Stage;

namespace Cosmic.Desktop;

sealed partial class DesktopCosmicPlatform : ICosmicPlatform
{
    Renderer renderer;

    public unsafe bool InitRenderDevice()
    {
        renderer = new Renderer(this, borderless);

        return true;
    }

    public void ReleaseRenderDevice()
    {
        renderer?.Dispose();
    }

    public unsafe void FlipScreen()
    {
        if (gameMode == EngineStates.ENGINE_EXITGAME)
            return;

        if (!enginePaused)
        {
            if (Engine.dimTimer < dimLimit)
            {
                if (Engine.dimPercent < 1.0f)
                {
                    Engine.dimPercent += 0.05f;
                    if (Engine.dimPercent > 1.0f)
                        Engine.dimPercent = 1.0f;
                }
            }
            else if (Engine.dimPercent > 0.25f && dimLimit >= 0)
            {
                Engine.dimPercent *= 0.9f;
            }
        }

        renderer.FlipScreen();
    }

    public void ClearDrawLists()
    {
        renderer.ClearDrawLists();
    }

    public void UpdateHWSurface(int index, int x, int y, int w, int h, byte[] data)
    {
        renderer.UpdateHWSurface(index, x, y, w, h, data);
    }

    void UpdateCol()
    {
        var colTextureManaged = new byte[TILE_SIZE * TILE_SIZE * TILE_COUNT];
        for (var colLayerIndex = 0; colLayerIndex < collisionMasks.GetLength(0); colLayerIndex++)
        {
            for (var tileNum = 0; tileNum < TILE_COUNT; tileNum++)
            {
                for (var tileX = 0; tileX < TILE_SIZE; tileX++)
                {
                    var height = collisionMasks[colLayerIndex, tileNum].floor[tileX];
                    if (height >= 0x40)
                    {
                        continue;
                    }
                    for (var tileY = TILE_SIZE - 1; tileY >= height; tileY--)
                    {
                        colTextureManaged[
                            tileNum * TILE_SIZE * TILE_SIZE +
                            tileY * TILE_SIZE +
                            tileX
                        ] = 1;
                    }
                }

                /*
                for (var tileY = 0; tileY < TILE_SIZE; tileY++)
                {
                    var width = collisionMasks[colLayerIndex].lWallMasks[tileNum * TILE_SIZE + tileY];
                    if (width >= 0x40)
                    {
                        continue;
                    }
                    colTextureManaged[
                        tileNum * TILE_SIZE * TILE_SIZE +
                        tileY * TILE_SIZE +
                        width
                    ] = 1;
                }

                for (var tileX = 0; tileX < TILE_SIZE; tileX++)
                {
                    var height = collisionMasks[colLayerIndex].roofMasks[tileNum * TILE_SIZE + tileX];
                    if (height <= -0x40)
                    {
                        continue;
                    }
                    colTextureManaged[
                        tileNum * TILE_SIZE * TILE_SIZE +
                        height * TILE_SIZE +
                        tileX
                    ] = 1;
                }

                for (var tileY = TILE_SIZE - 1; tileY >= 0; tileY--)
                {
                    var width = collisionMasks[colLayerIndex].rWallMasks[tileNum * TILE_SIZE + tileY];
                    if (width <= -0x40)
                    {
                        continue;
                    }
                    colTextureManaged[
                        tileNum * TILE_SIZE * TILE_SIZE +
                        tileY * TILE_SIZE +
                        width
                    ] = 1;
                }
                */
            }

            if (colLayerIndex == 0)
            {
                renderer.UpdateTileColLowTexture(colTextureManaged);
            }

            if (colLayerIndex == 1)
            {
                renderer.UpdateTileColHighTexture(colTextureManaged);
            }
        }
    }

    public void UpdateHWChunks()
    {
        renderer.UpdateFloor3DTiledataTexture();
        renderer.UpdateTilesetTexture();
        UpdateCol();
    }

    public void UpdatePalettes()
    {
        var paletteLineConverted = new int[PALETTE_COUNT * PALETTE_SIZE];
        for (var i = 0; i < PALETTE_COUNT; i++)
        {
            for (var j = 0; j < PALETTE_SIZE; j++)
            {
                paletteLineConverted[i * PALETTE_SIZE + j] = fullPalette[i, j].ToArgb();
            }
        }
        renderer.UpdatePaletteBufferTexture(paletteLineConverted);
    }

    public void UpdatePaletteIndices()
    {
        renderer.UpdatePaletteIndicesTexture();
    }

    public void UpdatePrevFramebuffer()
    {
        renderer.UpdatePrevFramebuffer();
    }

    public unsafe void SubmitDrawList(uint drawFacesCount, DrawVertexFace[] drawFacesList, DrawBlendMode blendMode)
    {
        renderer.SubmitDrawList(showHitboxes, drawFacesCount, drawFacesList, blendMode);
    }
}