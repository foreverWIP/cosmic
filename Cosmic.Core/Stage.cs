global using static Cosmic.Core.Stage;
using System;
using System.IO;
using static Cosmic.Core.Drawing;

namespace Cosmic.Core;

public static unsafe class Stage
{
    public const int LAYER_COUNT = (9);
    const int DEFORM_STORE = (256);
    const int DEFORM_SIZE = (320);
    const int DEFORM_COUNT = (DEFORM_STORE + DEFORM_SIZE);
    public const int PARALLAX_COUNT = (0x100);

    public const int TILE_COUNT = (0x400);
    public const int TILE_SIZE = (0x10);
    public const int CHUNK_SIZE = (0x80);
    const int TILE_DATASIZE = (TILE_SIZE * TILE_SIZE);
    const int TILESET_SIZE = (TILE_COUNT * TILE_DATASIZE);

    const int TILELAYER_CHUNK_W = (0x100);
    const int TILELAYER_CHUNK_H = (0x100);
    const int TILELAYER_CHUNK_COUNT = (TILELAYER_CHUNK_W * TILELAYER_CHUNK_H);
    const int TILELAYER_SCROLL_COUNT = (TILELAYER_CHUNK_H * CHUNK_SIZE);

    const int CHUNKTILE_COUNT = (0x200 * (8 * 8));

    const int CPATH_COUNT = (2);

    public enum StageListNames
    {
        STAGELIST_PRESENTATION,
        STAGELIST_REGULAR,
        STAGELIST_SPECIAL,
        STAGELIST_BONUS,
        STAGELIST_MAX, // StageList size
    }

    public enum TileLayerTypes
    {
        LAYER_NOSCROLL,
        LAYER_HSCROLL,
        LAYER_VSCROLL,
        LAYER_3DFLOOR,
        LAYER_3DSKY,
    }

    public enum StageModes
    {
        STAGEMODE_LOAD,
        STAGEMODE_NORMAL,
        STAGEMODE_PAUSED,
        STAGEMODE_PAUSED_LOOP,
    }

    public enum TileInfo
    {
        TILEINFO_INDEX,
        TILEINFO_DIRECTION,
        TILEINFO_VISUALPLANE,
        TILEINFO_SOLIDITYA,
        TILEINFO_SOLIDITYB,
        TILEINFO_FLAGSA,
        TILEINFO_ANGLEA,
        TILEINFO_FLAGSB,
        TILEINFO_ANGLEB,
    }

    public enum DeformationModes
    {
        DEFORM_FG,
        DEFORM_FG_WATER,
        DEFORM_BG,
        DEFORM_BG_WATER,
    }

    public enum CameraStyles
    {
        CAMERASTYLE_FOLLOW,
        CAMERASTYLE_EXTENDED,
        CAMERASTYLE_EXTENDED_OFFSET_L,
        CAMERASTYLE_EXTENDED_OFFSET_R,
        CAMERASTYLE_HLOCKED,
    }

    public struct SceneInfo
    {
        public string name;
        public string folder;
        public string id;
        public bool highlighted;
    }

    public struct CollisionMasks
    {
        public fixed sbyte floorMasks[TILE_COUNT * TILE_SIZE];
        public fixed sbyte lWallMasks[TILE_COUNT * TILE_SIZE];
        public fixed sbyte rWallMasks[TILE_COUNT * TILE_SIZE];
        public fixed sbyte roofMasks[TILE_COUNT * TILE_SIZE];
        public fixed uint angles[TILE_COUNT];
        public fixed byte flags[TILE_COUNT];
    }

    public struct CollisionMask
    {
        public sbyte[] floor => _floor ??= new sbyte[TILE_SIZE];
        sbyte[] _floor;
        public sbyte[] leftWall => _leftWall ??= new sbyte[TILE_SIZE];
        sbyte[] _leftWall;
        public sbyte[] rightWall => _rightWall ??= new sbyte[TILE_SIZE];
        sbyte[] _rightWall;
        public sbyte[] roof => _roof ??= new sbyte[TILE_SIZE];
        sbyte[] _roof;
        public uint angles;
        public byte flags;
    }

    public struct TileLayer
    {
        public ushort[] tiles => _tiles ??= new ushort[TILELAYER_CHUNK_COUNT];
        ushort[] _tiles;
        public byte[] lineScroll => _lineScroll ??= new byte[TILELAYER_SCROLL_COUNT];
        byte[] _lineScroll;
        public int parallaxFactor;
        public int scrollSpeed;
        public int scrollPos;
        public int angle;
        public int XPos;
        public int YPos;
        public int ZPos;
        public int deformationOffset;
        public int deformationOffsetW;
        public TileLayerTypes type;
        public byte xsize;
        public byte ysize;
    }

    public struct LineScroll
    {
        /*
        public readonly int[] parallaxFactor;
        public readonly int[] scrollSpeed;
        public readonly int[] scrollPos;
        public readonly int[] linePos;
        public readonly int[] deform;
        public byte entryCount;

        public LineScroll()
        {
            parallaxFactor = new int[PARALLAX_COUNT];
            scrollSpeed = new int[PARALLAX_COUNT];
            scrollPos = new int[PARALLAX_COUNT];
            linePos = new int[PARALLAX_COUNT];
            deform = new int[PARALLAX_COUNT];
        }
        */

        public fixed int parallaxFactor[PARALLAX_COUNT];
        public fixed int scrollSpeed[PARALLAX_COUNT];
        public fixed int scrollPos[PARALLAX_COUNT];
        public fixed int linePos[PARALLAX_COUNT];
        public fixed int deform[PARALLAX_COUNT];
        public byte entryCount;
    }

    public readonly struct Tiles128x128
    {
        public readonly int[] gfxDataPos;
        public readonly ushort[] tileIndex;
        public readonly FlipFlags[] direction;
        public readonly byte[] visualPlane;
        public readonly CollisionSolidity[,] collisionFlags;

        public Tiles128x128()
        {
            gfxDataPos = new int[CHUNKTILE_COUNT];
            tileIndex = new ushort[CHUNKTILE_COUNT];
            direction = new FlipFlags[CHUNKTILE_COUNT];
            visualPlane = new byte[CHUNKTILE_COUNT];
            collisionFlags = new CollisionSolidity[CPATH_COUNT, CHUNKTILE_COUNT];
        }
    }

    public static void ResetCurrentStageFolder() { currentStageFolder = string.Empty; }

    static bool CheckCurrentStageFolder(int stage)
    {
        if (currentStageFolder == stageList[(int)activeStageList, stage].folder)
        {
            return true;
        }
        else
        {
            currentStageFolder = stageList[(int)activeStageList, stage].folder;
            return false;
        }
    }

    static void UpdateFloorGFXManaged(int tileId, int totalCx, int totalCy)
    {
        var flip = tiles128x128.direction[tileId];
        var index = tiles128x128.tileIndex[tileId];

        tile3DFloorData[totalCy * 256 + totalCx] = (ushort)(index | (((ushort)flip) << 10));
    }

    static void Init3DFloorBuffer(int layoutID)
    {
        var layer = stageLayouts[layoutID];

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
                        var tileId = (layer.tiles[lx + (ly << 8)] * 8 * 8) + 8 * cy + cx;

                        UpdateFloorGFXManaged(tileId, totalCx, totalCy);
                    }
                }
            }
        }
    }

    public static bool shouldUpdateChunks { get; internal set; }

    public static void Copy16x16Tile(ushort dest, ushort src)
    {
        Array.Copy(tilesetGFXData, TILE_DATASIZE * src, tilesetGFXData, TILE_DATASIZE * dest, TILE_DATASIZE);
        shouldUpdateChunks = true;
    }

    public static readonly int[] stageListCount = new int[(int)StageListNames.STAGELIST_MAX];
    public static readonly string[] stageListNames = new string[(int)StageListNames.STAGELIST_MAX] {
        "Presentation Stages",
        "Regular Stages",
        "Special Stages",
        "Bonus Stages",
    };
    public static readonly SceneInfo[,] stageList = new SceneInfo[(int)StageListNames.STAGELIST_MAX, 0x100];

    public static StageModes stageMode = StageModes.STAGEMODE_LOAD;

    internal static int cameraTarget = -1;
    internal static CameraStyles cameraStyle = CameraStyles.CAMERASTYLE_FOLLOW;
    internal static int cameraEnabled = 0;
    internal static int cameraAdjustY = 0;
    internal static int xScrollOffset = 0;
    internal static int yScrollOffsetPixels = 0;
    internal static int yScrollA = 0;
    internal static int yScrollB = SCREEN_YSIZE;
    internal static int xScrollA = 0;
    internal static int xScrollB = SCREEN_XSIZE;
    static int yScrollMove = 0;
    internal static int cameraShakeX = 0;
    internal static int cameraShakeY = 0;
    static int cameraLag = 0;
    static int cameraLagStyle = 0;

    internal static int xBoundary1 = 0;
    internal static int newXBoundary1 = 0;
    internal static int yBoundary1 = 0;
    internal static int newYBoundary1 = 0;
    internal static int xBoundary2 = 0;
    internal static int yBoundary2 = 0;
    internal static int waterLevel = 0;
    internal static int waterDrawPos = 0;
    internal static int newXBoundary2 = 0;
    internal static int newYBoundary2 = 0;

    public const int SCREEN_SCROLL_UP = ((SCREEN_YSIZE / 2) - 16);
    public const int SCREEN_SCROLL_DOWN = ((SCREEN_YSIZE / 2) + 16);
    public const int SCREEN_SCROLL_LEFT = SCREEN_CENTERX - 8;
    public const int SCREEN_SCROLL_RIGHT = SCREEN_CENTERX + 8;

    internal static int lastXSizeInChunks = -1;

    internal static bool pauseEnabled = true;
    internal static bool timeEnabled = true;
    public static bool debugMode = false;
    internal static int frameCounter = 0;
    internal static int stageMilliseconds = 0;
    internal static int stageSeconds = 0;
    internal static int stageMinutes = 0;

    // Category and Scene IDs
    public static StageListNames activeStageList = 0;
    public static int stageListPosition = 0;
    public static string currentStageFolder = string.Empty;
    internal static int actID = 0;

    internal static string titleCardText = string.Empty;
    internal static byte titleCardWord2 = 0;

    internal static readonly byte[] activeTileLayers = new byte[4];
    internal static byte tLayerMidPoint;
    internal static readonly TileLayer[] stageLayouts = new TileLayer[LAYER_COUNT];

    internal static readonly int[] bgDeformationData0 = new int[DEFORM_COUNT];
    internal static readonly int[] bgDeformationData1 = new int[DEFORM_COUNT];
    internal static readonly int[] bgDeformationData2 = new int[DEFORM_COUNT];
    internal static readonly int[] bgDeformationData3 = new int[DEFORM_COUNT];

    internal static LineScroll hParallax = new();
    internal static LineScroll vParallax = new();

    internal static Tiles128x128 tiles128x128 = new();

    public static readonly byte[] tilesetGFXData = new byte[TILESET_SIZE];

    public static readonly ushort[] tile3DFloorData = new ushort[256 * 256];

    public static void InitFirstStage()
    {
        xScrollOffset = 0;
        yScrollOffsetPixels = 0;
        StopMusic();
        StopAllSfx();
        ReleaseStageSfx();
        fadeMode = 0;
        activePlayer = 0;
        Sprite.ClearAll();
        Animation.ClearAnimationData();
        activePalette = 0;
        LoadPalette("MasterPalette.act", 0, 0, 0, 256);
        stageMode = StageModes.STAGEMODE_LOAD;
        gameMode = EngineStates.ENGINE_MAINGAME;
        activeStageList = platform.startList;
        stageListPosition = platform.startStage;
    }

    public static void ProcessStage()
    {
        debugHitboxCount = 0;

        switch (stageMode)
        {
            case StageModes.STAGEMODE_LOAD: // Startup
                fadeMode = 0;
                SetActivePalette(0, 0, 256);

                cameraEnabled = 1;
                cameraTarget = -1;
                cameraAdjustY = 0;
                xScrollOffset = 0;
                yScrollOffsetPixels = 0;
                yScrollA = 0;
                yScrollB = SCREEN_YSIZE;
                xScrollA = 0;
                xScrollB = SCREEN_XSIZE;
                yScrollMove = 0;
                cameraShakeX = 0;
                cameraShakeY = 0;

                vertexCount = 0;
                faceCount = 0;
                for (int i = 0; i < PLAYER_COUNT; ++i)
                {
                    playerList[i].XPos = 0;
                    playerList[i].YPos = 0;
                    playerList[i].XVelocity = 0;
                    playerList[i].YVelocity = 0;
                    playerList[i].angle = 0;
                    playerList[i].visible = 1;
                    playerList[i].collisionPlane = 0;
                    playerList[i].collisionMode = 0;
                    playerList[i].gravity = 1; // Air
                    playerList[i].speed = 0;
                    playerList[i].tileCollisions = 1;
                    playerList[i].objectInteractions = 1;
                    playerList[i].values[0] = 0;
                    playerList[i].values[1] = 0;
                    playerList[i].values[2] = 0;
                    playerList[i].values[3] = 0;
                    playerList[i].values[4] = 0;
                    playerList[i].values[5] = 0;
                    playerList[i].values[6] = 0;
                    playerList[i].values[7] = 0;
                }
                pauseEnabled = false;
                timeEnabled = false;
                frameCounter = 0;
                stageMilliseconds = 0;
                stageSeconds = 0;
                stageMinutes = 0;
                Engine.framesSinceSceneLoad = 0;
                stageMode = StageModes.STAGEMODE_NORMAL;
                ResetBackgroundSettings();
                LoadStageFiles();
                if (
                    stageLayouts[0].type == TileLayerTypes.LAYER_3DFLOOR ||
                    stageLayouts[0].type == TileLayerTypes.LAYER_3DSKY
                )
                {
                    Init3DFloorBuffer(0);
                    platform.UpdateHWChunks();
                }
                break;

            case StageModes.STAGEMODE_NORMAL:

                if (fadeMode > 0)
                    fadeMode--;

                if (limitedFadeActivated)
                {
                    limitedFadeActivated = false;
                    SetActivePalette(0, 0, 256);
                }

                lastXSizeInChunks = -1;
                CheckKeyDown(ref keyDown[0], 0xFF);
                CheckKeyPress(ref keyPress[0], 0xFF);
                if (pauseEnabled && keyPress[0].start)
                {
                    stageMode = StageModes.STAGEMODE_PAUSED;
                    PauseSound();
                }

                if (timeEnabled)
                {
                    if (++frameCounter == platform.refreshRate)
                    {
                        frameCounter = 0;
                        if (++stageSeconds > 59)
                        {
                            stageSeconds = 0;
                            if (++stageMinutes > 59)
                                stageMinutes = 0;
                        }
                    }
                    stageMilliseconds = 100 * frameCounter / platform.refreshRate;
                }
                else
                {
                    frameCounter = platform.refreshRate * stageMilliseconds / 100;
                }

                // Update
                ProcessObjects();

                if (cameraTarget > -1)
                {
                    if (cameraEnabled == 1)
                    {
                        switch (cameraStyle)
                        {
                            case CameraStyles.CAMERASTYLE_FOLLOW: SetPlayerScreenPosition(ref playerList[cameraTarget]); break;
                            case CameraStyles.CAMERASTYLE_EXTENDED:
                            case CameraStyles.CAMERASTYLE_EXTENDED_OFFSET_L:
                            case CameraStyles.CAMERASTYLE_EXTENDED_OFFSET_R: SetPlayerScreenPositionCDStyle(ref playerList[cameraTarget]); break;
                            case CameraStyles.CAMERASTYLE_HLOCKED: SetPlayerHLockedScreenPosition(ref playerList[cameraTarget]); break;
                            default: break;
                        }
                    }
                    else
                    {
                        SetPlayerLockedScreenPosition(ref playerList[cameraTarget]);
                    }
                }

                DrawStageGFX();
                break;

            case StageModes.STAGEMODE_PAUSED:
                stageMode = StageModes.STAGEMODE_PAUSED_LOOP;
                goto PausedFallthrough;
            case StageModes.STAGEMODE_PAUSED_LOOP:
            PausedFallthrough:
                if (fadeMode > 0)
                    fadeMode--;

                if (limitedFadeActivated)
                {
                    limitedFadeActivated = false;
                    SetActivePalette(0, 0, 256);
                }
                lastXSizeInChunks = -1;
                CheckKeyDown(ref keyDown[0], 0xFF);
                CheckKeyPress(ref keyPress[0], 0xFF);

                // Update
                ProcessPausedObjects();

                DrawBackbuffer();
                DrawObjectList(0, Objects0Depth);
                DrawObjectList(1, Objects1Depth);
                DrawObjectList(2, Objects2Depth);
                DrawObjectList(3, Objects3Depth);
                DrawObjectList(4, Objects4Depth);
                DrawObjectList(5, Objects5Depth);
                // Hacky fix for Tails Object not working properly on non-Origins bytecode
                if (GetGlobalVariableByName("NOTIFY_1P_VS_SELECT") != 0)
                    DrawObjectList(7, Objects7Depth); // Extra Origins draw list (who knows why it comes before 6)
                DrawObjectList(6, Objects6Depth);
                DrawDebugOverlays();
                // platform.CopyToLastFramebuffer = true;

                if (pauseEnabled && keyPress[0].start)
                {
                    stageMode = StageModes.STAGEMODE_NORMAL;
                    ResumeSound();
                }
                break;

        }
        Engine.framesSinceSceneLoad++;
    }

    static void LoadStageFiles()
    {
        StopAllSfx();
        byte fileBuffer = 0;
        byte fileBuffer2 = 0;
        int scriptID = 1;
        var strBuffer = string.Empty;

        if (!CheckCurrentStageFolder(stageListPosition))
        {
            platform.PrintLog($"Loading Scene {stageListNames[(int)activeStageList]} - {stageList[(int)activeStageList, stageListPosition].name}");
            ReleaseStageSfx();
            LoadPalette("MasterPalette.act", 0, 0, 0, 256);
            ClearScriptData();
            Sprite.ClearAll();

            bool loadGlobalScripts = false;

            if (LoadStageFile("StageConfig.bin", stageListPosition, out var info))
            {
                byte buf = 0;
                buf = info.ReadByte();
                loadGlobalScripts = buf != 0;
                info.Close();
            }

            if (loadGlobalScripts && platform.LoadFile("Data/Game/GameConfig.bin", out info))
            {
                var gameConfig = new Cosmic.Formats.GameConfig(new Kaitai.KaitaiStream(info.BaseStream));

                for (var i = 0; i < gameConfig.ScriptTypes.Count; i++)
                {
                    SetObjectTypeName(gameConfig.ScriptTypes[i].Contents, i + scriptID);
                }

                for (byte i = 0; i < gameConfig.ScriptTypes.Count; ++i)
                {
                    ScriptParsing.ParseScriptFile(gameConfig.ScriptPaths[i].Contents, scriptID++);
                    if (gameMode == EngineStates.ENGINE_SCRIPTERROR)
                        return;
                }

                info.Close();
            }

            if (LoadStageFile("StageConfig.bin", stageListPosition, out info))
            {
                fileBuffer = info.ReadByte(); // Load Globals
                for (int i = 96; i < 128; ++i)
                {
                    var clr = info.ReadBytes(3);
                    SetPaletteEntry(0xff, (byte)i, clr[0], clr[1], clr[2]);
                }

                byte stageObjectCount = info.ReadByte();
                for (byte i = 0; i < stageObjectCount; ++i)
                {
                    SetObjectTypeName(info.ReadPascalString(), scriptID + i);
                }

                for (byte i = 0; i < stageObjectCount; ++i)
                {
                    ScriptParsing.ParseScriptFile(info.ReadPascalString(), scriptID + i);
                    if (gameMode == EngineStates.ENGINE_SCRIPTERROR)
                        return;
                }

                stageSFXCount = info.ReadByte();
                for (int i = 0; i < stageSFXCount; ++i)
                {
                    LoadSfx(info.ReadPascalString(), (byte)(globalSFXCount + i));
                }
                info.Close();
            }
            if (LoadStageFile("16x16Tiles.gif", stageListPosition, out info))
            {
                LoadStageGIFFile(info);
                info.Dispose();
            }
            LoadStageCollisions();
            LoadStageBackground();
        }
        else
        {
            platform.PrintLog($"Reloading Scene {stageListNames[(int)activeStageList]} - {stageList[(int)activeStageList, stageListPosition].name}");
        }
        LoadStageChunks();
        for (int i = 0; i < TRACK_COUNT; ++i) SetMusicTrack("", (byte)i, false, 0);
        for (int i = 0; i < ENTITY_COUNT; ++i)
        {
            objectEntityList[i] = new()
            {
                drawOrder = 3,
                scale = 512
            };
        }
        LoadActLayout();
        Init3DFloorBuffer(0);
        ProcessStartupObjects();
        xScrollA = FixedPointToWhole(playerList[0].XPos) - SCREEN_CENTERX;
        xScrollB = FixedPointToWhole(playerList[0].XPos) - SCREEN_CENTERX + SCREEN_XSIZE;
        yScrollA = FixedPointToWhole(playerList[0].YPos) - SCREEN_SCROLL_UP;
        yScrollB = FixedPointToWhole(playerList[0].YPos) - SCREEN_SCROLL_UP + SCREEN_YSIZE;
    }

    static bool LoadActFile(string ext, int stageID, out System.IO.BinaryReader info)
    {
        var dest = "Data/Stages/";
        dest += stageList[(int)activeStageList, stageID].folder;
        dest += "/Act";
        dest += stageList[(int)activeStageList, stageID].id;
        dest += ext;

        actID = int.Parse(stageList[(int)activeStageList, stageID].id);

        return platform.LoadFile(dest, out info);
    }

    static bool LoadStageFile(string filePath, int stageID, out System.IO.BinaryReader info)
    {
        return platform.LoadFile("Data/Stages/" + stageList[(int)activeStageList, stageID].folder + "/" + filePath, out info);
    }

    static void LoadActLayout()
    {
        if (LoadActFile(".bin", stageListPosition, out var info))
        {
            byte length = info.ReadByte();
            titleCardWord2 = (byte)length;
            titleCardText = string.Empty;
            for (int i = 0; i < length; i++)
            {
                var c = 0;
                c = info.ReadByte();
                if (c == '-')
                    titleCardWord2 = (byte)(i + 1);
                titleCardText += (char)c;
            }

            // READ TILELAYER
            Array.Copy(info.ReadBytes(4), activeTileLayers, 4);
            tLayerMidPoint = info.ReadByte();

            stageLayouts[0].xsize = info.ReadByte();
            stageLayouts[0].ysize = info.ReadByte();
            xBoundary1 = 0;
            newXBoundary1 = 0;
            yBoundary1 = 0;
            newYBoundary1 = 0;
            xBoundary2 = stageLayouts[0].xsize << 7;
            yBoundary2 = stageLayouts[0].ysize << 7;
            waterLevel = yBoundary2 + 128;
            newXBoundary2 = stageLayouts[0].xsize << 7;
            newYBoundary2 = stageLayouts[0].ysize << 7;

            for (int i = 0; i < 0x10000; ++i) stageLayouts[0].tiles[i] = 0;

            byte fileBuffer = 0;
            for (int y = 0; y < stageLayouts[0].ysize; ++y)
            {
                fixed (ushort* tiles = &stageLayouts[0].tiles[(y * 0x100)])
                {
                    for (int x = 0; x < stageLayouts[0].xsize; ++x)
                    {
                        tiles[x] = info.ReadUInt16BE();
                    }
                }
            }

            // READ TYPENAMES
            fileBuffer = info.ReadByte();
            int typenameCnt = fileBuffer;
            if (fileBuffer != 0)
            {
                for (int i = 0; i < typenameCnt; ++i)
                {
                    fileBuffer = info.ReadByte();
                    int nameLen = fileBuffer;
                    for (int l = 0; l < nameLen; ++l) fileBuffer = info.ReadByte();
                }
            }

            // READ OBJECTS
            fileBuffer = info.ReadByte();
            int ObjectCount = fileBuffer;
            fileBuffer = info.ReadByte();
            ObjectCount = (ObjectCount << 8) + fileBuffer;

            if (ObjectCount > 0x400)
                platform.PrintLog($"WARNING: object count {ObjectCount} exceeds the object limit");

            fixed (Entity* objPtr = &objectEntityList[32])
            {
                Entity* @object = objPtr;
                for (int i = 0; i < ObjectCount; ++i)
                {
                    fileBuffer = info.ReadByte();
                    @object->type = fileBuffer;

                    fileBuffer = info.ReadByte();
                    @object->propertyValue = fileBuffer;

                    @object->XPos = info.ReadUInt16BE();
                    @object->XPos <<= 16;

                    @object->YPos = info.ReadUInt16BE();
                    @object->YPos <<= 16;

                    ++@object;
                }
            }
            stageLayouts[0].type = TileLayerTypes.LAYER_HSCROLL;
            info.Dispose();
        }
    }

    static void LoadStageBackground()
    {
        for (int i = 0; i < LAYER_COUNT; ++i)
        {
            stageLayouts[i].type = TileLayerTypes.LAYER_NOSCROLL;
            stageLayouts[i].deformationOffset = 0;
            stageLayouts[i].deformationOffsetW = 0;
        }
        for (int i = 0; i < PARALLAX_COUNT; ++i)
        {
            hParallax.scrollPos[i] = 0;
            vParallax.scrollPos[i] = 0;
        }

        if (LoadStageFile("Backgrounds.bin", stageListPosition, out var info))
        {
            byte fileBuffer = 0;
            byte layerCount = 0;
            layerCount = info.ReadByte();
            hParallax.entryCount = info.ReadByte();
            for (int i = 0; i < hParallax.entryCount; ++i)
            {
                hParallax.parallaxFactor[i] = info.ReadUInt16BE();

                fileBuffer = info.ReadByte();
                hParallax.scrollSpeed[i] = fileBuffer << 10;

                hParallax.scrollPos[i] = 0;

                hParallax.deform[i] = info.ReadByte();
            }

            vParallax.entryCount = info.ReadByte();
            for (int i = 0; i < vParallax.entryCount; ++i)
            {
                vParallax.parallaxFactor[i] = info.ReadUInt16BE();

                fileBuffer = info.ReadByte();
                vParallax.scrollSpeed[i] = fileBuffer << 10;

                vParallax.scrollPos[i] = 0;

                vParallax.deform[i] = info.ReadByte();
            }

            for (int i = 1; i < layerCount + 1; ++i)
            {
                fileBuffer = info.ReadByte();
                stageLayouts[i].xsize = fileBuffer;
                fileBuffer = info.ReadByte();
                stageLayouts[i].ysize = fileBuffer;
                fileBuffer = info.ReadByte();
                stageLayouts[i].type = (TileLayerTypes)fileBuffer;
                fileBuffer = info.ReadByte();
                stageLayouts[i].parallaxFactor = fileBuffer << 8;
                fileBuffer = info.ReadByte();
                stageLayouts[i].parallaxFactor += fileBuffer;
                fileBuffer = info.ReadByte();
                stageLayouts[i].scrollSpeed = fileBuffer << 10;
                stageLayouts[i].scrollPos = 0;

                Array.Clear(stageLayouts[i].tiles);
                fixed (byte* lineScrollPtrPtr = stageLayouts[i].lineScroll)
                {
                    byte* lineScrollPtr = lineScrollPtrPtr;
                    Array.Clear(stageLayouts[i].lineScroll);

                    // Read Line Scroll
                    var buf = new byte[3];
                    while (true)
                    {
                        buf[0] = info.ReadByte();
                        if (buf[0] == 0xFF)
                        {
                            buf[1] = info.ReadByte();
                            if (buf[1] == 0xFF)
                            {
                                break;
                            }
                            else
                            {
                                buf[2] = info.ReadByte();
                                int val = buf[1];
                                int cnt = buf[2] - 1;
                                for (int c = 0; c < cnt; ++c) *lineScrollPtr++ = (byte)val;
                            }
                        }
                        else
                        {
                            *lineScrollPtr++ = buf[0];
                        }
                    }

                    // Read Layout
                    for (int y = 0; y < stageLayouts[i].ysize; ++y)
                    {
                        fixed (ushort* chunksPtr = &stageLayouts[i].tiles[y * 0x100])
                        {
                            ushort* chunks = chunksPtr;
                            for (int x = 0; x < stageLayouts[i].xsize; ++x)
                            {
                                *chunks = info.ReadUInt16BE();
                                ++chunks;
                            }
                        }
                    }
                }
            }

            info.Dispose();
        }
    }

    static void LoadStageChunks()
    {
        var entry = new byte[3];

        if (LoadStageFile("128x128Tiles.bin", stageListPosition, out var info))
        {
            for (int i = 0; i * 3 < info.BaseStream.Length; ++i)
            {
                entry = info.ReadBytes(3);
                entry[0] -= (byte)((entry[0] >> 6) << 6);

                tiles128x128.visualPlane[i] = (byte)(entry[0] >> 4);
                entry[0] -= (byte)(16 * (entry[0] >> 4));

                tiles128x128.direction[i] = (FlipFlags)(entry[0] >> 2);
                entry[0] -= (byte)(4 * (entry[0] >> 2));

                tiles128x128.tileIndex[i] = (ushort)(entry[1] + (entry[0] << 8));

                tiles128x128.gfxDataPos[i] = tiles128x128.tileIndex[i] << 8;

                tiles128x128.collisionFlags[0, i] = (CollisionSolidity)(entry[2] >> 4);
                tiles128x128.collisionFlags[1, i] = (CollisionSolidity)(entry[2] - ((entry[2] >> 4) << 4));
            }
            info.Dispose();
        }
        platform.UpdateHWChunks();
    }

    static void LoadStageCollisions()
    {
        if (LoadStageFile("CollisionMasks.bin", stageListPosition, out var info))
        {

            byte fileBuffer = 0;
            int tileIndex = 0;
            for (int t = 0; t < 1024; ++t)
            {
                for (int p = 0; p < 2; ++p)
                {
                    fileBuffer = info.ReadByte();
                    bool isCeiling = (fileBuffer >> 4) != 0;
                    collisionMasks[p, t].flags = (byte)(fileBuffer & 0xF);
                    collisionMasks[p, t].angles = info.ReadUInt32LE();

                    if (isCeiling) // Ceiling Tile
                    {
                        for (int c = 0; c < TILE_SIZE; c += 2)
                        {
                            fileBuffer = info.ReadByte();
                            collisionMasks[p, t].roof[c] = (sbyte)(fileBuffer >> 4);
                            collisionMasks[p, t].roof[c + 1] = (sbyte)(fileBuffer & 0xF);
                        }

                        // Has Collision (Pt 1)
                        fileBuffer = info.ReadByte();
                        int id = 1;
                        for (int c = 0; c < TILE_SIZE / 2; ++c)
                        {
                            if ((fileBuffer & id) != 0)
                            {
                                collisionMasks[p, t].floor[c + 8] = 0;
                            }
                            else
                            {
                                collisionMasks[p, t].floor[c + 8] = 0x40;
                                collisionMasks[p, t].roof[c + 8] = -0x40;
                            }
                            id <<= 1;
                        }

                        // Has Collision (Pt 2)
                        fileBuffer = info.ReadByte();
                        id = 1;
                        for (int c = 0; c < TILE_SIZE / 2; ++c)
                        {
                            if ((fileBuffer & id) != 0)
                            {
                                collisionMasks[p, t].floor[c] = 0;
                            }
                            else
                            {
                                collisionMasks[p, t].floor[c] = 0x40;
                                collisionMasks[p, t].roof[c] = -0x40;
                            }
                            id <<= 1;
                        }

                        // LWall rotations
                        for (int c = 0; c < TILE_SIZE; ++c)
                        {
                            int h = 0;
                            while (h > -1)
                            {
                                if (h >= TILE_SIZE)
                                {
                                    collisionMasks[p, t].leftWall[c] = 0x40;
                                    h = -1;
                                }
                                else if (c > collisionMasks[p, t].roof[h])
                                {
                                    ++h;
                                }
                                else
                                {
                                    collisionMasks[p, t].leftWall[c] = (sbyte)h;
                                    h = -1;
                                }
                            }
                        }

                        // RWall rotations
                        for (int c = 0; c < TILE_SIZE; ++c)
                        {
                            int h = TILE_SIZE - 1;
                            while (h < TILE_SIZE)
                            {
                                if (h <= -1)
                                {
                                    collisionMasks[p, t].rightWall[c] = -0x40;
                                    h = TILE_SIZE;
                                }
                                else if (c > collisionMasks[p, t].roof[h])
                                {
                                    --h;
                                }
                                else
                                {
                                    collisionMasks[p, t].rightWall[c] = (sbyte)h;
                                    h = TILE_SIZE;
                                }
                            }
                        }
                    }
                    else // Regular Tile
                    {
                        for (int c = 0; c < TILE_SIZE; c += 2)
                        {
                            fileBuffer = info.ReadByte();
                            collisionMasks[p, t].floor[c] = (sbyte)(fileBuffer >> 4);
                            collisionMasks[p, t].floor[c + 1] = (sbyte)(fileBuffer & 0xF);
                        }
                        fileBuffer = info.ReadByte();
                        int id = 1;
                        for (int c = 0; c < TILE_SIZE / 2; ++c) // HasCollision
                        {
                            if ((fileBuffer & id) != 0)
                            {
                                collisionMasks[p, t].roof[c + 8] = 0xF;
                            }
                            else
                            {
                                collisionMasks[p, t].floor[c + 8] = 0x40;
                                collisionMasks[p, t].roof[c + 8] = -0x40;
                            }
                            id <<= 1;
                        }

                        fileBuffer = info.ReadByte();
                        id = 1;
                        for (int c = 0; c < TILE_SIZE / 2; ++c) // HasCollision (pt 2)
                        {
                            if ((fileBuffer & id) != 0)
                            {
                                collisionMasks[p, t].roof[c] = 0xF;
                            }
                            else
                            {
                                collisionMasks[p, t].floor[c] = 0x40;
                                collisionMasks[p, t].roof[c] = -0x40;
                            }
                            id <<= 1;
                        }

                        // LWall rotations
                        for (int c = 0; c < TILE_SIZE; ++c)
                        {
                            int h = 0;
                            while (h > -1)
                            {
                                if (h >= TILE_SIZE)
                                {
                                    collisionMasks[p, t].leftWall[c] = 0x40;
                                    h = -1;
                                }
                                else if (c < collisionMasks[p, t].floor[h])
                                {
                                    ++h;
                                }
                                else
                                {
                                    collisionMasks[p, t].leftWall[c] = (sbyte)h;
                                    h = -1;
                                }
                            }
                        }

                        // RWall rotations
                        for (int c = 0; c < TILE_SIZE; ++c)
                        {
                            int h = TILE_SIZE - 1;
                            while (h < TILE_SIZE)
                            {
                                if (h <= -1)
                                {
                                    collisionMasks[p, t].rightWall[c] = -0x40;
                                    h = TILE_SIZE;
                                }
                                else if (c < collisionMasks[p, t].floor[h])
                                {
                                    --h;
                                }
                                else
                                {
                                    collisionMasks[p, t].rightWall[c] = (sbyte)h;
                                    h = TILE_SIZE;
                                }
                            }
                        }
                    }
                }
                tileIndex += 16;
            }
            info.Dispose();
        }
    }

    static void LoadStageGIFFile(BinaryReader info)
    {
        var gif = new Cosmic.Formats.Gif(new Kaitai.KaitaiStream(info.BaseStream));

        if (gif.GlobalColorTable.Entries.Count == 256)
        {
            for (int c = 0x80; c < 0x100; ++c)
            {
                var color = gif.GlobalColorTable.Entries[c];
                SetPaletteEntry(0xff, (byte)c, color.Red, color.Green, color.Blue);
            }
        }

        var decompressed = Cosmic.GifDecoder.GetDecodedData(gif);
        Array.Copy(decompressed, tilesetGFXData, decompressed.Length);

        byte transparent = tilesetGFXData[0];
        for (int i = 0; i < 0x40000; ++i)
        {
            if (tilesetGFXData[i] == transparent)
                tilesetGFXData[i] = 0;
        }

        info.Dispose();
    }

    static void ResetBackgroundSettings()
    {
        for (int i = 0; i < LAYER_COUNT; ++i)
        {
            stageLayouts[i].deformationOffset = 0;
            stageLayouts[i].deformationOffsetW = 0;
            stageLayouts[i].scrollPos = 0;
        }

        for (int i = 0; i < PARALLAX_COUNT; ++i)
        {
            hParallax.scrollPos[i] = 0;
            vParallax.scrollPos[i] = 0;
        }
        hParallax = new();
        vParallax = new();

        for (int i = 0; i < DEFORM_COUNT; ++i)
        {
            bgDeformationData0[i] = 0;
            bgDeformationData1[i] = 0;
            bgDeformationData2[i] = 0;
            bgDeformationData3[i] = 0;
        }
    }

    public static void SetLayerDeformation(int selectedDef, int waveLength, int waveWidth, int waveType, int YPos, int waveSize)
    {
        fixed (int* bgDeformationData0Ptr = bgDeformationData0)
        {
            fixed (int* bgDeformationData1Ptr = bgDeformationData1)
            {
                fixed (int* bgDeformationData2Ptr = bgDeformationData2)
                {
                    fixed (int* bgDeformationData3Ptr = bgDeformationData3)
                    {
                        int* deformPtr = null;
                        switch ((DeformationModes)selectedDef)
                        {
                            case DeformationModes.DEFORM_FG: deformPtr = bgDeformationData0Ptr; break;
                            case DeformationModes.DEFORM_FG_WATER: deformPtr = bgDeformationData1Ptr; break;
                            case DeformationModes.DEFORM_BG: deformPtr = bgDeformationData2Ptr; break;
                            case DeformationModes.DEFORM_BG_WATER: deformPtr = bgDeformationData3Ptr; break;
                            default: break;
                        }

                        int shift = 9;

                        int id = 0;
                        if (waveType == 1)
                        {
                            id = YPos;
                            for (int i = 0; i < waveSize; ++i)
                            {
                                deformPtr[id] = waveWidth * sin512LookupTable[(i << 9) / waveLength & 0x1FF] >> shift;
                                ++id;
                            }
                        }
                        else
                        {
                            for (int i = 0; i < 0x200 * 0x100; i += 0x200)
                            {
                                int val = waveWidth * sin512LookupTable[i / waveLength & 0x1FF] >> shift;
                                deformPtr[id] = val;
                                if (deformPtr[id] >= waveWidth)
                                    deformPtr[id] = waveWidth - 1;
                                ++id;
                            }
                        }

                        switch ((DeformationModes)selectedDef)
                        {
                            case DeformationModes.DEFORM_FG:
                                for (int i = DEFORM_STORE; i < DEFORM_COUNT; ++i) bgDeformationData0[i] = bgDeformationData0[i - DEFORM_STORE];
                                break;
                            case DeformationModes.DEFORM_FG_WATER:
                                for (int i = DEFORM_STORE; i < DEFORM_COUNT; ++i) bgDeformationData1[i] = bgDeformationData1[i - DEFORM_STORE];
                                break;
                            case DeformationModes.DEFORM_BG:
                                for (int i = DEFORM_STORE; i < DEFORM_COUNT; ++i) bgDeformationData2[i] = bgDeformationData2[i - DEFORM_STORE];
                                break;
                            case DeformationModes.DEFORM_BG_WATER:
                                for (int i = DEFORM_STORE; i < DEFORM_COUNT; ++i) bgDeformationData3[i] = bgDeformationData3[i - DEFORM_STORE];
                                break;
                            default: break;
                        }
                    }
                }
            }
        }
    }

    static void SetPlayerScreenPosition(ref Player player)
    {
        int playerXPos = FixedPointToWhole(player.XPos);
        int playerYPos = FixedPointToWhole(player.YPos);
        if (newYBoundary1 > yBoundary1)
        {
            if (yScrollOffsetPixels <= newYBoundary1)
                yBoundary1 = yScrollOffsetPixels;
            else
                yBoundary1 = newYBoundary1;
        }
        if (newYBoundary1 < yBoundary1)
        {
            if (yScrollOffsetPixels <= yBoundary1)
                --yBoundary1;
            else
                yBoundary1 = newYBoundary1;
        }
        if (newYBoundary2 < yBoundary2)
        {
            if (yScrollOffsetPixels + SCREEN_YSIZE >= yBoundary2 || yScrollOffsetPixels + SCREEN_YSIZE <= newYBoundary2)
                --yBoundary2;
            else
                yBoundary2 = yScrollOffsetPixels + SCREEN_YSIZE;
        }
        if (newYBoundary2 > yBoundary2)
        {
            if (yScrollOffsetPixels + SCREEN_YSIZE >= yBoundary2)
                ++yBoundary2;
            else
                yBoundary2 = newYBoundary2;
        }
        if (newXBoundary1 > xBoundary1)
        {
            if (xScrollOffset <= newXBoundary1)
                xBoundary1 = xScrollOffset;
            else
                xBoundary1 = newXBoundary1;
        }
        if (newXBoundary1 < xBoundary1)
        {
            if (xScrollOffset <= xBoundary1)
            {
                --xBoundary1;
                if (player.XVelocity < 0)
                {
                    xBoundary1 += FixedPointToWhole(player.XVelocity);
                    if (xBoundary1 < newXBoundary1)
                        xBoundary1 = newXBoundary1;
                }
            }
            else
            {
                xBoundary1 = newXBoundary1;
            }
        }
        if (newXBoundary2 < xBoundary2)
        {
            if (SCREEN_XSIZE + xScrollOffset >= xBoundary2)
                xBoundary2 = SCREEN_XSIZE + xScrollOffset;
            else
                xBoundary2 = newXBoundary2;
        }
        if (newXBoundary2 > xBoundary2)
        {
            if (SCREEN_XSIZE + xScrollOffset >= xBoundary2)
            {
                ++xBoundary2;
                if (player.XVelocity > 0)
                {
                    xBoundary2 += FixedPointToWhole(player.XVelocity);
                    if (xBoundary2 > newXBoundary2)
                        xBoundary2 = newXBoundary2;
                }
            }
            else
            {
                xBoundary2 = newXBoundary2;
            }
        }
        int xscrollA = xScrollA;
        int xscrollB = xScrollB;
        int scrollAmount = playerXPos - (SCREEN_CENTERX + xScrollA);
        if (System.Math.Abs(playerXPos - (SCREEN_CENTERX + xScrollA)) >= 25)
        {
            if (scrollAmount <= 0)
                xscrollA -= 16;
            else
                xscrollA += 16;
            xscrollB = SCREEN_XSIZE + xscrollA;
        }
        else
        {
            if (playerXPos > SCREEN_SCROLL_RIGHT + xscrollA)
            {
                xscrollA = playerXPos - SCREEN_SCROLL_RIGHT;
                xscrollB = SCREEN_XSIZE + playerXPos - SCREEN_SCROLL_RIGHT;
            }
            if (playerXPos < SCREEN_SCROLL_LEFT + xscrollA)
            {
                xscrollA = playerXPos - SCREEN_SCROLL_LEFT;
                xscrollB = SCREEN_XSIZE + playerXPos - SCREEN_SCROLL_LEFT;
            }
        }
        if (xscrollA < xBoundary1)
        {
            xscrollA = xBoundary1;
            xscrollB = SCREEN_XSIZE + xBoundary1;
        }
        if (xscrollB > xBoundary2)
        {
            xscrollB = xBoundary2;
            xscrollA = xBoundary2 - SCREEN_XSIZE;
        }

        xScrollA = xscrollA;
        xScrollB = xscrollB;
        if (playerXPos <= SCREEN_CENTERX + xscrollA)
        {
            player.screenXPos = cameraShakeX + playerXPos - xscrollA;
            xScrollOffset = xscrollA - cameraShakeX;
        }
        else
        {
            xScrollOffset = cameraShakeX + playerXPos - SCREEN_CENTERX;
            player.screenXPos = SCREEN_CENTERX - cameraShakeX;
            if (playerXPos > xscrollB - SCREEN_CENTERX)
            {
                player.screenXPos = cameraShakeX + SCREEN_CENTERX + playerXPos - (xscrollB - SCREEN_CENTERX);
                xScrollOffset = xscrollB - SCREEN_XSIZE - cameraShakeX;
            }
        }

        int yscrollA = yScrollA;
        int yscrollB = yScrollB;
        int adjustYPos = cameraAdjustY + playerYPos;
        int adjustAmount = player.lookPos + adjustYPos - (yscrollA + SCREEN_SCROLL_UP);
        if (player.trackScroll != 0)
        {
            yScrollMove = 32;
        }
        else
        {
            if (yScrollMove == 32)
            {
                yScrollMove = 2 * ((SCREEN_SCROLL_UP - player.screenYPos - player.lookPos) >> 1);
                if (yScrollMove > 32)
                    yScrollMove = 32;
                if (yScrollMove < -32)
                    yScrollMove = -32;
            }
            if (yScrollMove > 0)
                yScrollMove -= 6;
            yScrollMove += yScrollMove < 0 ? 6 : 0;
        }

        if (System.Math.Abs(adjustAmount) >= System.Math.Abs(yScrollMove) + 17)
        {
            if (adjustAmount <= 0)
                yscrollA -= 16;
            else
                yscrollA += 16;
            yscrollB = yscrollA + SCREEN_YSIZE;
        }
        else if (yScrollMove == 32)
        {
            if (player.lookPos + adjustYPos > yscrollA + yScrollMove + SCREEN_SCROLL_UP)
            {
                yscrollA = player.lookPos + adjustYPos - (yScrollMove + SCREEN_SCROLL_UP);
                yscrollB = yscrollA + SCREEN_YSIZE;
            }
            if (player.lookPos + adjustYPos < yscrollA + SCREEN_SCROLL_UP - yScrollMove)
            {
                yscrollA = player.lookPos + adjustYPos - (SCREEN_SCROLL_UP - yScrollMove);
                yscrollB = yscrollA + SCREEN_YSIZE;
            }
        }
        else
        {
            yscrollA = player.lookPos + adjustYPos + yScrollMove - SCREEN_SCROLL_UP;
            yscrollB = yscrollA + SCREEN_YSIZE;
        }
        if (yscrollA < yBoundary1)
        {
            yscrollA = yBoundary1;
            yscrollB = yBoundary1 + SCREEN_YSIZE;
        }
        if (yscrollB > yBoundary2)
        {
            yscrollB = yBoundary2;
            yscrollA = yBoundary2 - SCREEN_YSIZE;
        }
        yScrollA = yscrollA;
        yScrollB = yscrollB;
        if (player.lookPos + adjustYPos <= yScrollA + SCREEN_SCROLL_UP)
        {
            player.screenYPos = adjustYPos - yScrollA - cameraShakeY;
            yScrollOffsetPixels = cameraShakeY + yScrollA;
        }
        else
        {
            yScrollOffsetPixels = cameraShakeY + adjustYPos + player.lookPos - SCREEN_SCROLL_UP;
            player.screenYPos = SCREEN_SCROLL_UP - player.lookPos - cameraShakeY;
            if (player.lookPos + adjustYPos > yScrollB - SCREEN_SCROLL_DOWN)
            {
                player.screenYPos = adjustYPos - (yScrollB - SCREEN_SCROLL_DOWN) + cameraShakeY + SCREEN_SCROLL_UP;
                yScrollOffsetPixels = yScrollB - SCREEN_YSIZE - cameraShakeY;
            }
        }
        player.screenYPos -= cameraAdjustY;

        if (cameraShakeX != 0)
        {
            if (cameraShakeX <= 0)
            {
                cameraShakeX = ~cameraShakeX;
            }
            else
            {
                cameraShakeX = -cameraShakeX;
            }
        }

        if (cameraShakeY != 0)
        {
            if (cameraShakeY <= 0)
            {
                cameraShakeY = ~cameraShakeY;
            }
            else
            {
                cameraShakeY = -cameraShakeY;
            }
        }
    }

    static void SetPlayerScreenPositionCDStyle(ref Player player)
    {
        int playerXPos = FixedPointToWhole(player.XPos);
        int playerYPos = FixedPointToWhole(player.YPos);
        if (newYBoundary1 > yBoundary1)
        {
            if (yScrollOffsetPixels <= newYBoundary1)
                yBoundary1 = yScrollOffsetPixels;
            else
                yBoundary1 = newYBoundary1;
        }
        if (newYBoundary1 < yBoundary1)
        {
            if (yScrollOffsetPixels <= yBoundary1)
                --yBoundary1;
            else
                yBoundary1 = newYBoundary1;
        }
        if (newYBoundary2 < yBoundary2)
        {
            if (yScrollOffsetPixels + SCREEN_YSIZE >= yBoundary2 || yScrollOffsetPixels + SCREEN_YSIZE <= newYBoundary2)
                --yBoundary2;
            else
                yBoundary2 = yScrollOffsetPixels + SCREEN_YSIZE;
        }
        if (newYBoundary2 > yBoundary2)
        {
            if (yScrollOffsetPixels + SCREEN_YSIZE >= yBoundary2)
                ++yBoundary2;
            else
                yBoundary2 = newYBoundary2;
        }
        if (newXBoundary1 > xBoundary1)
        {
            if (xScrollOffset <= newXBoundary1)
                xBoundary1 = xScrollOffset;
            else
                xBoundary1 = newXBoundary1;
        }
        if (newXBoundary1 < xBoundary1)
        {
            if (xScrollOffset <= xBoundary1)
            {
                --xBoundary1;
                if (player.XVelocity < 0)
                {
                    xBoundary1 += FixedPointToWhole(player.XVelocity);
                    if (xBoundary1 < newXBoundary1)
                        xBoundary1 = newXBoundary1;
                }
            }
            else
            {
                xBoundary1 = newXBoundary1;
            }
        }
        if (newXBoundary2 < xBoundary2)
        {
            if (SCREEN_XSIZE + xScrollOffset >= xBoundary2)
                xBoundary2 = SCREEN_XSIZE + xScrollOffset;
            else
                xBoundary2 = newXBoundary2;
        }
        if (newXBoundary2 > xBoundary2)
        {
            if (SCREEN_XSIZE + xScrollOffset >= xBoundary2)
            {
                ++xBoundary2;
                if (player.XVelocity > 0)
                {
                    xBoundary2 += FixedPointToWhole(player.XVelocity);
                    if (xBoundary2 > newXBoundary2)
                        xBoundary2 = newXBoundary2;
                }
            }
            else
            {
                xBoundary2 = newXBoundary2;
            }
        }
        if (player.gravity == 0)
        {
            if (objectEntityList[player.boundEntity].direction != 0)
            {
                if (cameraStyle == CameraStyles.CAMERASTYLE_EXTENDED_OFFSET_R || player.speed < -0x5F5C2)
                    cameraLagStyle = 2;
                else
                    cameraLagStyle = 0;
            }
            else
            {
                cameraLagStyle = (cameraStyle == CameraStyles.CAMERASTYLE_EXTENDED_OFFSET_L || player.speed > 0x5F5C2) ? 1 : 0;
            }
        }
        if (cameraLagStyle != 0)
        {
            if (cameraLagStyle == 1)
            {
                if (cameraLag > -64)
                    cameraLag -= 2;
            }
            else if (cameraLagStyle == 2 && cameraLag < 64)
            {
                cameraLag += 2;
            }
        }
        else
        {
            cameraLag += cameraLag < 0 ? 2 : 0;
            if (cameraLag > 0)
                cameraLag -= 2;
        }
        if (playerXPos <= cameraLag + SCREEN_CENTERX + xBoundary1)
        {
            player.screenXPos = cameraShakeX + playerXPos - xBoundary1;
            xScrollOffset = xBoundary1 - cameraShakeX;
        }
        else
        {
            xScrollOffset = cameraShakeX + playerXPos - SCREEN_CENTERX - cameraLag;
            player.screenXPos = cameraLag + SCREEN_CENTERX - cameraShakeX;
            if (playerXPos - cameraLag > xBoundary2 - SCREEN_CENTERX)
            {
                player.screenXPos = cameraShakeX + SCREEN_CENTERX + playerXPos - (xBoundary2 - SCREEN_CENTERX);
                xScrollOffset = xBoundary2 - SCREEN_XSIZE - cameraShakeX;
            }
        }
        xScrollA = xScrollOffset;
        xScrollB = SCREEN_XSIZE + xScrollOffset;
        int yscrollA = yScrollA;
        int yscrollB = yScrollB;
        int adjustY = cameraAdjustY + playerYPos;
        int adjustOffset = player.lookPos + adjustY - (yScrollA + SCREEN_SCROLL_UP);
        if (player.trackScroll == 1)
        {
            yScrollMove = 32;
        }
        else
        {
            if (yScrollMove == 32)
            {
                yScrollMove = 2 * ((SCREEN_SCROLL_UP - player.screenYPos - player.lookPos) >> 1);
                if (yScrollMove > 32)
                    yScrollMove = 32;
                if (yScrollMove < -32)
                    yScrollMove = -32;
            }
            if (yScrollMove > 0)
                yScrollMove -= 6;
            yScrollMove += yScrollMove < 0 ? 6 : 0;
        }

        int absAdjust = System.Math.Abs(adjustOffset);
        if (absAdjust >= System.Math.Abs(yScrollMove) + 17)
        {
            if (adjustOffset <= 0)
                yscrollA -= 16;
            else
                yscrollA += 16;
            yscrollB = yscrollA + SCREEN_YSIZE;
        }
        else if (yScrollMove == 32)
        {
            if (player.lookPos + adjustY > yscrollA + yScrollMove + SCREEN_SCROLL_UP)
            {
                yscrollA = player.lookPos + adjustY - (yScrollMove + SCREEN_SCROLL_UP);
                yscrollB = yscrollA + SCREEN_YSIZE;
            }
            if (player.lookPos + adjustY < yscrollA + SCREEN_SCROLL_UP - yScrollMove)
            {
                yscrollA = player.lookPos + adjustY - (SCREEN_SCROLL_UP - yScrollMove);
                yscrollB = yscrollA + SCREEN_YSIZE;
            }
        }
        else
        {
            yscrollA = player.lookPos + adjustY + yScrollMove - SCREEN_SCROLL_UP;
            yscrollB = yscrollA + SCREEN_YSIZE;
        }
        if (yscrollA < yBoundary1)
        {
            yscrollA = yBoundary1;
            yscrollB = yBoundary1 + SCREEN_YSIZE;
        }
        if (yscrollB > yBoundary2)
        {
            yscrollB = yBoundary2;
            yscrollA = yBoundary2 - SCREEN_YSIZE;
        }
        yScrollA = yscrollA;
        yScrollB = yscrollB;
        if (player.lookPos + adjustY <= yscrollA + SCREEN_SCROLL_UP)
        {
            player.screenYPos = adjustY - yscrollA - cameraShakeY;
            yScrollOffsetPixels = cameraShakeY + yscrollA;
        }
        else
        {
            yScrollOffsetPixels = cameraShakeY + adjustY + player.lookPos - SCREEN_SCROLL_UP;
            player.screenYPos = SCREEN_SCROLL_UP - player.lookPos - cameraShakeY;
            if (player.lookPos + adjustY > yscrollB - SCREEN_SCROLL_DOWN)
            {
                player.screenYPos = adjustY - (yscrollB - SCREEN_SCROLL_DOWN) + cameraShakeY + SCREEN_SCROLL_UP;
                yScrollOffsetPixels = yscrollB - SCREEN_YSIZE - cameraShakeY;
            }
        }
        player.screenYPos -= cameraAdjustY;

        if (cameraShakeX != 0)
        {
            if (cameraShakeX <= 0)
            {
                cameraShakeX = ~cameraShakeX;
            }
            else
            {
                cameraShakeX = -cameraShakeX;
            }
        }

        if (cameraShakeY != 0)
        {
            if (cameraShakeY <= 0)
            {
                cameraShakeY = ~cameraShakeY;
            }
            else
            {
                cameraShakeY = -cameraShakeY;
            }
        }
    }

    static void SetPlayerHLockedScreenPosition(ref Player player)
    {
        int playerXPos = FixedPointToWhole(player.XPos);
        int playerYPos = FixedPointToWhole(player.YPos);
        if (newYBoundary1 > yBoundary1)
        {
            if (yScrollOffsetPixels <= newYBoundary1)
                yBoundary1 = yScrollOffsetPixels;
            else
                yBoundary1 = newYBoundary1;
        }
        if (newYBoundary1 < yBoundary1)
        {
            if (yScrollOffsetPixels <= yBoundary1)
                --yBoundary1;
            else
                yBoundary1 = newYBoundary1;
        }
        if (newYBoundary2 < yBoundary2)
        {
            if (yScrollOffsetPixels + SCREEN_YSIZE >= yBoundary2 || yScrollOffsetPixels + SCREEN_YSIZE <= newYBoundary2)
                --yBoundary2;
            else
                yBoundary2 = yScrollOffsetPixels + SCREEN_YSIZE;
        }
        if (newYBoundary2 > yBoundary2)
        {
            if (yScrollOffsetPixels + SCREEN_YSIZE >= yBoundary2)
                ++yBoundary2;
            else
                yBoundary2 = newYBoundary2;
        }

        int xscrollA = xScrollA;
        int xscrollB = xScrollB;
        if (playerXPos <= SCREEN_CENTERX + xScrollA)
        {
            player.screenXPos = cameraShakeX + playerXPos - xScrollA;
            xScrollOffset = xscrollA - cameraShakeX;
        }
        else
        {
            xScrollOffset = cameraShakeX + playerXPos - SCREEN_CENTERX;
            player.screenXPos = SCREEN_CENTERX - cameraShakeX;
            if (playerXPos > xscrollB - SCREEN_CENTERX)
            {
                player.screenXPos = cameraShakeX + SCREEN_CENTERX + playerXPos - (xscrollB - SCREEN_CENTERX);
                xScrollOffset = xscrollB - SCREEN_XSIZE - cameraShakeX;
            }
        }

        int yscrollA = yScrollA;
        int yscrollB = yScrollB;
        int adjustY = cameraAdjustY + playerYPos;
        int lookOffset = player.lookPos + adjustY - (yScrollA + SCREEN_SCROLL_UP);
        if (player.trackScroll == 1)
        {
            yScrollMove = 32;
        }
        else
        {
            if (yScrollMove == 32)
            {
                yScrollMove = 2 * ((SCREEN_SCROLL_UP - player.screenYPos - player.lookPos) >> 1);
                if (yScrollMove > 32)
                    yScrollMove = 32;
                if (yScrollMove < -32)
                    yScrollMove = -32;
            }
            if (yScrollMove > 0)
                yScrollMove -= 6;
            yScrollMove += yScrollMove < 0 ? 6 : 0;
        }

        int absLook = System.Math.Abs(lookOffset);
        if (absLook >= System.Math.Abs(yScrollMove) + 17)
        {
            if (lookOffset <= 0)
                yscrollA -= 16;
            else
                yscrollA += 16;
            yscrollB = yscrollA + SCREEN_YSIZE;
        }
        else if (yScrollMove == 32)
        {
            if (player.lookPos + adjustY > yscrollA + yScrollMove + SCREEN_SCROLL_UP)
            {
                yscrollA = player.lookPos + adjustY - (yScrollMove + SCREEN_SCROLL_UP);
                yscrollB = yscrollA + SCREEN_YSIZE;
            }
            if (player.lookPos + adjustY < yscrollA + SCREEN_SCROLL_UP - yScrollMove)
            {
                yscrollA = player.lookPos + adjustY - (SCREEN_SCROLL_UP - yScrollMove);
                yscrollB = yscrollA + SCREEN_YSIZE;
            }
        }
        else
        {
            yscrollA = player.lookPos + adjustY + yScrollMove - SCREEN_SCROLL_UP;
            yscrollB = yscrollA + SCREEN_YSIZE;
        }
        if (yscrollA < yBoundary1)
        {
            yscrollA = yBoundary1;
            yscrollB = yBoundary1 + SCREEN_YSIZE;
        }
        if (yscrollB > yBoundary2)
        {
            yscrollB = yBoundary2;
            yscrollA = yBoundary2 - SCREEN_YSIZE;
        }
        yScrollA = yscrollA;
        yScrollB = yscrollB;
        if (player.lookPos + adjustY <= yscrollA + SCREEN_SCROLL_UP)
        {
            player.screenYPos = adjustY - yscrollA - cameraShakeY;
            yScrollOffsetPixels = cameraShakeY + yscrollA;
        }
        else
        {
            yScrollOffsetPixels = cameraShakeY + adjustY + player.lookPos - SCREEN_SCROLL_UP;
            player.screenYPos = SCREEN_SCROLL_UP - player.lookPos - cameraShakeY;
            if (player.lookPos + adjustY > yscrollB - SCREEN_SCROLL_DOWN)
            {
                player.screenYPos = adjustY - (yscrollB - SCREEN_SCROLL_DOWN) + cameraShakeY + SCREEN_SCROLL_UP;
                yScrollOffsetPixels = yscrollB - SCREEN_YSIZE - cameraShakeY;
            }
        }
        player.screenYPos -= cameraAdjustY;

        if (cameraShakeX != 0)
        {
            if (cameraShakeX <= 0)
            {
                cameraShakeX = ~cameraShakeX;
            }
            else
            {
                cameraShakeX = -cameraShakeX;
            }
        }

        if (cameraShakeY != 0)
        {
            if (cameraShakeY <= 0)
            {
                cameraShakeY = ~cameraShakeY;
            }
            else
            {
                cameraShakeY = -cameraShakeY;
            }
        }
    }

    static void SetPlayerLockedScreenPosition(ref Player player)
    {
        int playerXPos = FixedPointToWhole(player.XPos);
        int playerYPos = FixedPointToWhole(player.YPos);
        int xscrollA = xScrollA;
        int xscrollB = xScrollB;
        if (playerXPos <= SCREEN_CENTERX + xScrollA)
        {
            player.screenXPos = cameraShakeX + playerXPos - xScrollA;
            xScrollOffset = xscrollA - cameraShakeX;
        }
        else
        {
            xScrollOffset = cameraShakeX + playerXPos - SCREEN_CENTERX;
            player.screenXPos = SCREEN_CENTERX - cameraShakeX;
            if (playerXPos > xscrollB - SCREEN_CENTERX)
            {
                player.screenXPos = cameraShakeX + SCREEN_CENTERX + playerXPos - (xscrollB - SCREEN_CENTERX);
                xScrollOffset = xscrollB - SCREEN_XSIZE - cameraShakeX;
            }
        }

        int yscrollA = yScrollA;
        int yscrollB = yScrollB;
        int adjustY = cameraAdjustY + playerYPos;
        if (player.lookPos + adjustY <= yScrollA + SCREEN_SCROLL_UP)
        {
            player.screenYPos = adjustY - yScrollA - cameraShakeY;
            yScrollOffsetPixels = cameraShakeY + yscrollA;
        }
        else
        {
            yScrollOffsetPixels = cameraShakeY + adjustY + player.lookPos - SCREEN_SCROLL_UP;
            player.screenYPos = SCREEN_SCROLL_UP - player.lookPos - cameraShakeY;
            if (player.lookPos + adjustY > yscrollB - SCREEN_SCROLL_DOWN)
            {
                player.screenYPos = adjustY - (yscrollB - SCREEN_SCROLL_DOWN) + cameraShakeY + SCREEN_SCROLL_UP;
                yScrollOffsetPixels = yscrollB - SCREEN_YSIZE - cameraShakeY;
            }
        }
        player.screenYPos -= cameraAdjustY;

        if (cameraShakeX != 0)
        {
            if (cameraShakeX <= 0)
            {
                cameraShakeX = ~cameraShakeX;
            }
            else
            {
                cameraShakeX = -cameraShakeX;
            }
        }

        if (cameraShakeY != 0)
        {
            if (cameraShakeY <= 0)
            {
                cameraShakeY = ~cameraShakeY;
            }
            else
            {
                cameraShakeY = -cameraShakeY;
            }
        }
    }
}