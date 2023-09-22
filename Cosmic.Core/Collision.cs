global using static Cosmic.Core.Collision;
using System.Collections.Generic;
using static Cosmic.Core.Drawing;

namespace Cosmic.Core;

public static unsafe class Collision
{
    public enum CollisionSides
    {
        CSIDE_FLOOR = 0,
        CSIDE_LWALL = 1,
        CSIDE_RWALL = 2,
        CSIDE_ROOF = 3,
    }

    public enum CollisionModes
    {
        CMODE_FLOOR = 0,
        CMODE_LWALL = 1,
        CMODE_ROOF = 2,
        CMODE_RWALL = 3,
    }

    public enum CollisionSolidity
    {
        SOLID_ALL = 0,
        SOLID_TOP = 1,
        SOLID_LRB = 2,
        SOLID_NONE = 3,
    }

    public enum ObjectCollisionTypes
    {
        C_TOUCH = 0,
        C_BOX = 1,
        C_BOX2 = 2,
        C_PLATFORM = 3,
    }

    public struct CollisionSensor
    {
        public int XPos;
        public int YPos;
        public int angle;
        public bool collided;
    }

    public const int DEBUG_HITBOX_COUNT = (0x400);

    public struct DebugHitboxInfo
    {
        public DebugHitboxTypes type;
        public byte collision;
        public short left;
        public short top;
        public short right;
        public short bottom;
        public int XPos;
        public int YPos;
        public Entity* entity;
    }

    public enum DebugHitboxTypes { H_TYPE_TOUCH, H_TYPE_BOX, H_TYPE_PLAT, H_TYPE_FINGER }

    internal readonly struct Hitbox
    {
        public const int DirectionCount = 8;
        public readonly sbyte[] left;
        public readonly sbyte[] top;
        public readonly sbyte[] right;
        public readonly sbyte[] bottom;

        public Hitbox()
        {
            left = new sbyte[DirectionCount];
            top = new sbyte[DirectionCount];
            right = new sbyte[DirectionCount];
            bottom = new sbyte[DirectionCount];
        }
    }

    internal static readonly List<Hitbox> hitboxList = new();

    static int collisionLeft = 0;
    static int collisionTop = 0;
    static int collisionRight = 0;
    static int collisionBottom = 0;

    static readonly CollisionSensor[] sensors = new CollisionSensor[6];

    public static bool showHitboxes;
    public static bool showTouches;

    internal static int debugHitboxCount = 0;
    public static readonly DebugHitboxInfo[] debugHitboxList = new DebugHitboxInfo[DEBUG_HITBOX_COUNT];

    public static readonly CollisionMask[,] collisionMasks = new CollisionMask[2, TILE_COUNT];

    public static int AddDebugHitbox(byte type, Entity* entity, int left, int top, int right, int bottom)
    {
        int XPos = 0, YPos = 0;
        if (entity != null)
        {
            XPos = entity->XPos;
            YPos = entity->YPos;
        }
        else if ((DebugHitboxTypes)type != DebugHitboxTypes.H_TYPE_FINGER)
        {
            Player player = playerList[activePlayer];
            XPos = player.XPos;
            YPos = player.YPos;
        }

        int i = 0;
        for (; i < debugHitboxCount; ++i)
        {
            if (debugHitboxList[i].left == left && debugHitboxList[i].top == top && debugHitboxList[i].right == right
                && debugHitboxList[i].bottom == bottom && debugHitboxList[i].XPos == XPos && debugHitboxList[i].YPos == YPos
                && debugHitboxList[i].entity == entity)
            {
                return i;
            }
        }

        if (i < DEBUG_HITBOX_COUNT)
        {
            debugHitboxList[i].type = (DebugHitboxTypes)type;
            debugHitboxList[i].entity = entity;
            debugHitboxList[i].collision = 0;
            debugHitboxList[i].left = (short)left;
            debugHitboxList[i].top = (short)top;
            debugHitboxList[i].right = (short)right;
            debugHitboxList[i].bottom = (short)bottom;
            debugHitboxList[i].XPos = XPos;
            debugHitboxList[i].YPos = YPos;

            int id = debugHitboxCount;
            debugHitboxCount++;
            return id;
        }

        return -1;
    }

    static Hitbox GetPlayerHitbox(Player player)
    {
        var animFile = player.animationFile!;
        var hitboxListIndex = Animation.animFrames[Animation.animationList[animFile.aniListOffset + objectEntityList[player.boundEntity].animation].frameListOffset + objectEntityList[player.boundEntity].frame].hitboxID + animFile.hitboxListOffset;
        return hitboxList[hitboxListIndex];
    }

    static void FindFloorPosition(ref Player player, ref CollisionSensor sensor, int startY)
    {
        int angle = sensor.angle;
        const int TileSizeMinus1 = (TILE_SIZE - 1);
        for (int i = 0; i < TILE_SIZE * 3; i += TILE_SIZE)
        {
            if (!sensor.collided)
            {
                int XPos = FixedPointToWhole(sensor.XPos);
                int YPos = FixedPointToWhole(sensor.YPos) + i - TILE_SIZE;
                int chunkX = XPos / CHUNK_SIZE;
                int chunkY = YPos / CHUNK_SIZE;
                int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                if (XPos > -1 && YPos > -1)
                {
                    int tile = stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6;
                    tile += tileX + (tileY << 3);
                    int tileIndex = tiles128x128.tileIndex[tile];
                    if (tiles128x128.collisionFlags[player.collisionPlane, tile] != CollisionSolidity.SOLID_LRB
                        && tiles128x128.collisionFlags[player.collisionPlane, tile] != CollisionSolidity.SOLID_NONE)
                    {
                        var newSensorYPos = (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                        var newSensorAngle = 0;
                        sbyte* masks;
                        uint rawAngle;
                        var maskIndex = (XPos % TILE_SIZE);
                        if (tiles128x128.direction[tile].HasFlag(FlipFlags.FLIP_X))
                        {
                            maskIndex = TileSizeMinus1 - (XPos % TILE_SIZE);
                        }
                        fixed (sbyte* roofPtr = collisionMasks[player.collisionPlane, tileIndex].roof)
                        {
                            fixed (sbyte* floorPtr = collisionMasks[player.collisionPlane, tileIndex].floor)
                            {
                                if (tiles128x128.direction[tile].HasFlag(FlipFlags.FLIP_Y))
                                {
                                    masks = roofPtr;
                                    newSensorYPos += TileSizeMinus1 - masks[maskIndex];
                                    rawAngle = (collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF000000) >> 24;
                                    if (tiles128x128.direction[tile].HasFlag(FlipFlags.FLIP_X))
                                    {
                                        newSensorAngle = 0x100 - (byte)(-0x80 - (byte)(rawAngle));
                                    }
                                    else
                                    {
                                        newSensorAngle = (byte)(-0x80 - (byte)(rawAngle));
                                    }
                                }
                                else
                                {
                                    masks = floorPtr;
                                    newSensorYPos += masks[maskIndex];
                                    rawAngle = collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF;
                                    if (tiles128x128.direction[tile].HasFlag(FlipFlags.FLIP_X))
                                    {
                                        newSensorAngle = (int)(0x100 - (rawAngle));
                                    }
                                    else
                                    {
                                        newSensorAngle = (int)(rawAngle);
                                    }
                                }
                            }
                        }

                        if (System.Math.Abs(masks[maskIndex]) >= 0x40)
                            goto Break;

                        sensor.collided = true;
                        sensor.YPos = newSensorYPos;
                        sensor.angle = newSensorAngle;
                    }

                Break:
                    if (sensor.collided)
                    {
                        sensor.angle = PositiveClampedModuloInclusive(sensor.angle, 0x100);

                        if ((System.Math.Abs(sensor.angle - angle) > DegreesToByteAngle(45)) && (System.Math.Abs(sensor.angle - 0x100 - angle) > DegreesToByteAngle(45))
                            && (System.Math.Abs(sensor.angle + 0x100 - angle) > DegreesToByteAngle(45)))
                        {
                            sensor.YPos = WholeToFixedPoint(startY);
                            sensor.collided = false;
                            sensor.angle = angle;
                            i = TILE_SIZE * 3;
                        }
                        else if (sensor.YPos - startY > (TILE_SIZE - 2))
                        {
                            sensor.YPos = WholeToFixedPoint(startY);
                            sensor.collided = false;
                        }
                        else if (sensor.YPos - startY < -(TILE_SIZE - 2))
                        {
                            sensor.YPos = WholeToFixedPoint(startY);
                            sensor.collided = false;
                        }
                    }
                }
            }
        }
    }

    static void FindLWallPosition(ref Player player, ref CollisionSensor sensor, int startX)
    {
        int c = 0;
        int angle = sensor.angle;
        int tsm1 = (TILE_SIZE - 1);
        for (int i = 0; i < TILE_SIZE * 3; i += TILE_SIZE)
        {
            if (!sensor.collided)
            {
                int XPos = FixedPointToWhole(sensor.XPos) + i - TILE_SIZE;
                int chunkX = XPos / CHUNK_SIZE;
                int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                int YPos = FixedPointToWhole(sensor.YPos);
                int chunkY = YPos / CHUNK_SIZE;
                int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                if (XPos > -1 && YPos > -1)
                {
                    int tile = stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6;
                    tile = tile + tileX + (tileY << 3);
                    int tileIndex = tiles128x128.tileIndex[tile];
                    if (tiles128x128.collisionFlags[player.collisionPlane, tile] < CollisionSolidity.SOLID_NONE)
                    {
                        switch (tiles128x128.direction[tile])
                        {
                            case FlipFlags.FLIP_NONE:
                                {
                                    c = (YPos & tsm1);
                                    if (collisionMasks[player.collisionPlane, tileIndex].leftWall[c] >= 0x40)
                                        break;

                                    sensor.XPos = collisionMasks[player.collisionPlane, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    sensor.angle = (int)((collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF00) >> 8);
                                    break;
                                }
                            case FlipFlags.FLIP_X:
                                {
                                    c = (YPos & tsm1);
                                    if (collisionMasks[player.collisionPlane, tileIndex].rightWall[c] <= -0x40)
                                        break;

                                    sensor.XPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    sensor.angle = 0x100 - FixedPointToWhole((int)(collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF0000));
                                    break;
                                }
                            case FlipFlags.FLIP_Y:
                                {
                                    c = tsm1 - (YPos & tsm1);
                                    if (collisionMasks[player.collisionPlane, tileIndex].leftWall[c] >= 0x40)
                                        break;

                                    sensor.XPos = collisionMasks[player.collisionPlane, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    int cAngle = (int)((collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF00) >> 8);
                                    sensor.angle = (byte)(-0x80 - cAngle);
                                    break;
                                }
                            case FlipFlags.FLIP_XY:
                                {
                                    c = tsm1 - (YPos & tsm1);
                                    if (collisionMasks[player.collisionPlane, tileIndex].rightWall[c] <= -0x40)
                                        break;

                                    sensor.XPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    int cAngle = FixedPointToWhole((int)((collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF0000)));
                                    sensor.angle = 0x100 - (byte)(-0x80 - cAngle);
                                    break;
                                }
                        }
                    }
                    if (sensor.collided)
                    {
                        sensor.angle = PositiveClampedModuloInclusive(sensor.angle, 0x100);

                        if (System.Math.Abs(angle - sensor.angle) > DegreesToByteAngle(45))
                        {
                            sensor.XPos = WholeToFixedPoint(startX);
                            sensor.collided = false;
                            sensor.angle = angle;
                            i = TILE_SIZE * 3;
                        }
                        else if (sensor.XPos - startX > TILE_SIZE - 2)
                        {
                            sensor.XPos = WholeToFixedPoint(startX);
                            sensor.collided = false;
                        }
                        else if (sensor.XPos - startX < -(TILE_SIZE - 2))
                        {
                            sensor.XPos = WholeToFixedPoint(startX);
                            sensor.collided = false;
                        }
                    }
                }
            }
        }
    }
    static void FindRoofPosition(ref Player player, ref CollisionSensor sensor, int startY)
    {
        int c = 0;
        int angle = sensor.angle;
        int tsm1 = (TILE_SIZE - 1);
        for (int i = 0; i < TILE_SIZE * 3; i += TILE_SIZE)
        {
            if (!sensor.collided)
            {
                int XPos = FixedPointToWhole(sensor.XPos);
                int chunkX = XPos / CHUNK_SIZE;
                int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                int YPos = FixedPointToWhole(sensor.YPos) + TILE_SIZE - i;
                int chunkY = YPos / CHUNK_SIZE;
                int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                if (XPos > -1 && YPos > -1)
                {
                    int tile = stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6;
                    tile = tile + tileX + (tileY << 3);
                    int tileIndex = tiles128x128.tileIndex[tile];
                    if (tiles128x128.collisionFlags[player.collisionPlane, tile] < CollisionSolidity.SOLID_NONE)
                    {
                        switch (tiles128x128.direction[tile])
                        {
                            case FlipFlags.FLIP_NONE:
                                {
                                    c = (XPos & tsm1);
                                    if (collisionMasks[player.collisionPlane, tileIndex].roof[c] <= -0x40)
                                        break;

                                    sensor.YPos = collisionMasks[player.collisionPlane, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    sensor.collided = true;
                                    sensor.angle = (int)((collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF000000) >> 24);
                                    break;
                                }
                            case FlipFlags.FLIP_X:
                                {
                                    c = tsm1 - (XPos & tsm1);
                                    if (collisionMasks[player.collisionPlane, tileIndex].roof[c] <= -0x40)
                                        break;

                                    sensor.YPos = collisionMasks[player.collisionPlane, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    sensor.collided = true;
                                    sensor.angle = (int)(0x100 - ((collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF000000) >> 24));
                                    break;
                                }
                            case FlipFlags.FLIP_Y:
                                {
                                    c = (XPos & tsm1);
                                    if (collisionMasks[player.collisionPlane, tileIndex].floor[c] >= 0x40)
                                        break;

                                    sensor.YPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    sensor.collided = true;
                                    byte cAngle = (byte)(collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF);
                                    sensor.angle = (byte)(-0x80 - cAngle);
                                    break;
                                }
                            case FlipFlags.FLIP_XY:
                                {
                                    c = tsm1 - (XPos & tsm1);
                                    if (collisionMasks[player.collisionPlane, tileIndex].floor[c] >= 0x40)
                                        break;

                                    sensor.YPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    sensor.collided = true;
                                    byte cAngle = (byte)(collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF);
                                    sensor.angle = 0x100 - (byte)(-0x80 - cAngle);
                                    break;
                                }
                        }
                    }

                    if (sensor.collided)
                    {
                        sensor.angle = PositiveClampedModuloInclusive(sensor.angle, 0x100);

                        if (System.Math.Abs(sensor.angle - angle) <= DegreesToByteAngle(45))
                        {
                            if (sensor.YPos - startY > tsm1)
                            {
                                sensor.YPos = WholeToFixedPoint(startY);
                                sensor.collided = false;
                            }
                            if (sensor.YPos - startY < -tsm1)
                            {
                                sensor.YPos = WholeToFixedPoint(startY);
                                sensor.collided = false;
                            }
                        }
                        else
                        {
                            sensor.YPos = WholeToFixedPoint(startY);
                            sensor.collided = false;
                            sensor.angle = angle;
                            i = TILE_SIZE * 3;
                        }
                    }
                }
            }
        }
    }

    static void FindRWallPosition(ref Player player, ref CollisionSensor sensor, int startX)
    {
        int c;
        int angle = sensor.angle;
        int tsm1 = (TILE_SIZE - 1);
        for (int i = 0; i < TILE_SIZE * 3; i += TILE_SIZE)
        {
            if (!sensor.collided)
            {
                int XPos = FixedPointToWhole(sensor.XPos) + TILE_SIZE - i;
                int chunkX = XPos / CHUNK_SIZE;
                int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                int YPos = FixedPointToWhole(sensor.YPos);
                int chunkY = YPos / CHUNK_SIZE;
                int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                if (XPos > -1 && YPos > -1)
                {
                    int tile = stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6;
                    tile = tile + tileX + (tileY << 3);
                    int tileIndex = tiles128x128.tileIndex[tile];
                    if (tiles128x128.collisionFlags[player.collisionPlane, tile] < CollisionSolidity.SOLID_NONE)
                    {
                        switch (tiles128x128.direction[tile])
                        {
                            case FlipFlags.FLIP_NONE:
                                {
                                    c = (YPos & tsm1);
                                    if (collisionMasks[player.collisionPlane, tileIndex].rightWall[c] <= -0x40)
                                        break;

                                    sensor.XPos = collisionMasks[player.collisionPlane, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    sensor.angle = FixedPointToWhole((int)((collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF0000)));
                                    break;
                                }
                            case FlipFlags.FLIP_X:
                                {
                                    c = (YPos & tsm1);
                                    if (collisionMasks[player.collisionPlane, tileIndex].leftWall[c] >= 0x40)
                                        break;

                                    sensor.XPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    sensor.angle = (int)(0x100 - ((collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF00) >> 8));
                                    break;
                                }
                            case FlipFlags.FLIP_Y:
                                {
                                    c = tsm1 - (YPos & tsm1);
                                    if (collisionMasks[player.collisionPlane, tileIndex].rightWall[c] <= -0x40)
                                        break;

                                    sensor.XPos = collisionMasks[player.collisionPlane, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    int cAngle = FixedPointToWhole((int)((collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF0000)));
                                    sensor.angle = (byte)(-0x80 - cAngle);
                                    break;
                                }
                            case FlipFlags.FLIP_XY:
                                {
                                    c = tsm1 - (YPos & tsm1);
                                    if (collisionMasks[player.collisionPlane, tileIndex].leftWall[c] >= 0x40)
                                        break;

                                    sensor.XPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    int cAngle = (int)((collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF00) >> 8);
                                    sensor.angle = 0x100 - (byte)(-0x80 - cAngle);
                                    break;
                                }
                        }
                    }
                    if (sensor.collided)
                    {
                        sensor.angle = PositiveClampedModuloInclusive(sensor.angle, 0x100);

                        if (System.Math.Abs(sensor.angle - angle) > DegreesToByteAngle(45))
                        {
                            sensor.XPos = WholeToFixedPoint(startX);
                            sensor.collided = false;
                            sensor.angle = angle;
                            i = TILE_SIZE * 3;
                        }
                        else if (sensor.XPos - startX > (TILE_SIZE - 2))
                        {
                            sensor.XPos = FixedPointToWhole(startX);
                            sensor.collided = false;
                        }
                        else if (sensor.XPos - startX < -(TILE_SIZE - 2))
                        {
                            sensor.XPos = WholeToFixedPoint(startX);
                            sensor.collided = false;
                        }
                    }
                }
            }
        }
    }

    static void FloorCollision(ref Player player, ref CollisionSensor sensor)
    {
        int c;
        int startY = FixedPointToWhole(sensor.YPos);
        int tsm1 = (TILE_SIZE - 1);
        for (int i = 0; i < TILE_SIZE * 3; i += TILE_SIZE)
        {
            if (!sensor.collided)
            {
                int XPos = FixedPointToWhole(sensor.XPos);
                int chunkX = XPos / CHUNK_SIZE;
                int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                int YPos = FixedPointToWhole(sensor.YPos) + i - TILE_SIZE;
                int chunkY = YPos / CHUNK_SIZE;
                int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                if (XPos > -1 && YPos > -1)
                {
                    int tile = stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6;
                    tile += tileX + (tileY << 3);
                    int tileIndex = tiles128x128.tileIndex[tile];
                    if (tiles128x128.collisionFlags[player.collisionPlane, tile] != CollisionSolidity.SOLID_LRB
                        && tiles128x128.collisionFlags[player.collisionPlane, tile] != CollisionSolidity.SOLID_NONE)
                    {
                        switch (tiles128x128.direction[tile])
                        {
                            case FlipFlags.FLIP_NONE:
                                {
                                    c = (XPos & tsm1);
                                    if ((YPos & tsm1) <= collisionMasks[player.collisionPlane, tileIndex].floor[c] + i - TILE_SIZE
                                        || collisionMasks[player.collisionPlane, tileIndex].floor[c] >= tsm1)
                                        break;

                                    sensor.YPos = collisionMasks[player.collisionPlane, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    sensor.collided = true;
                                    sensor.angle = (int)(collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF);
                                    break;
                                }
                            case FlipFlags.FLIP_X:
                                {
                                    c = tsm1 - (XPos & tsm1);
                                    if ((YPos & tsm1) <= collisionMasks[player.collisionPlane, tileIndex].floor[c] + i - TILE_SIZE
                                        || collisionMasks[player.collisionPlane, tileIndex].floor[c] >= tsm1)
                                        break;

                                    sensor.YPos = collisionMasks[player.collisionPlane, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    sensor.collided = true;
                                    sensor.angle = (int)(0x100 - (collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF));
                                    break;
                                }
                            case FlipFlags.FLIP_Y:
                                {
                                    c = (XPos & tsm1);
                                    if ((YPos & tsm1) <= tsm1 - collisionMasks[player.collisionPlane, tileIndex].roof[c] + i - TILE_SIZE)
                                        break;

                                    sensor.YPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    sensor.collided = true;
                                    int cAngle = (int)((collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF000000) >> 24);
                                    sensor.angle = (byte)(-0x80 - cAngle);
                                    break;
                                }
                            case FlipFlags.FLIP_XY:
                                {
                                    c = tsm1 - (XPos & tsm1);
                                    if ((YPos & tsm1) <= tsm1 - collisionMasks[player.collisionPlane, tileIndex].roof[c] + i - TILE_SIZE)
                                        break;

                                    sensor.YPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    sensor.collided = true;
                                    int cAngle = (int)((collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF000000) >> 24);
                                    sensor.angle = 0x100 - (byte)(-0x80 - cAngle);
                                    break;
                                }
                        }
                    }

                    if (sensor.collided)
                    {
                        sensor.angle = PositiveClampedModuloInclusive(sensor.angle, 0x100);

                        if (sensor.YPos - startY > (TILE_SIZE - 2))
                        {
                            sensor.YPos = WholeToFixedPoint(startY);
                            sensor.collided = false;
                        }
                        else if (sensor.YPos - startY < -(TILE_SIZE + 1))
                        {
                            sensor.YPos = WholeToFixedPoint(startY);
                            sensor.collided = false;
                        }
                    }
                }
            }
        }
    }

    static void LWallCollision(ref Player player, ref CollisionSensor sensor)
    {
        int c;
        int startX = FixedPointToWhole(sensor.XPos);
        int tsm1 = (TILE_SIZE - 1);
        for (int i = 0; i < TILE_SIZE * 3; i += TILE_SIZE)
        {
            if (!sensor.collided)
            {
                int XPos = FixedPointToWhole(sensor.XPos) + i - TILE_SIZE;
                int chunkX = XPos / CHUNK_SIZE;
                int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                int YPos = FixedPointToWhole(sensor.YPos);
                int chunkY = YPos / CHUNK_SIZE;
                int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                if (XPos > -1 && YPos > -1)
                {
                    int tile = stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6;
                    tile += tileX + (tileY << 3);
                    int tileIndex = tiles128x128.tileIndex[tile];
                    if (tiles128x128.collisionFlags[player.collisionPlane, tile] != CollisionSolidity.SOLID_TOP
                        && tiles128x128.collisionFlags[player.collisionPlane, tile] < CollisionSolidity.SOLID_NONE)
                    {
                        switch (tiles128x128.direction[tile])
                        {
                            case FlipFlags.FLIP_NONE:
                                {
                                    c = (YPos & tsm1);
                                    if ((XPos & tsm1) <= collisionMasks[player.collisionPlane, tileIndex].leftWall[c] + i - TILE_SIZE)
                                        break;

                                    sensor.XPos = collisionMasks[player.collisionPlane, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    break;
                                }
                            case FlipFlags.FLIP_X:
                                {
                                    c = (YPos & tsm1);
                                    if ((XPos & tsm1) <= tsm1 - collisionMasks[player.collisionPlane, tileIndex].rightWall[c] + i - TILE_SIZE)
                                        break;

                                    sensor.XPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    break;
                                }
                            case FlipFlags.FLIP_Y:
                                {
                                    c = tsm1 - (YPos & tsm1);
                                    if ((XPos & tsm1) <= collisionMasks[player.collisionPlane, tileIndex].leftWall[c] + i - TILE_SIZE)
                                        break;

                                    sensor.XPos = collisionMasks[player.collisionPlane, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    break;
                                }
                            case FlipFlags.FLIP_XY:
                                {
                                    c = tsm1 - (YPos & tsm1);
                                    if ((XPos & tsm1) <= tsm1 - collisionMasks[player.collisionPlane, tileIndex].rightWall[c] + i - TILE_SIZE)
                                        break;

                                    sensor.XPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    break;
                                }
                        }
                    }

                    if (sensor.collided)
                    {
                        if (sensor.XPos - startX > tsm1)
                        {
                            sensor.XPos = WholeToFixedPoint(startX);
                            sensor.collided = false;
                        }
                        else if (sensor.XPos - startX < -tsm1)
                        {
                            sensor.XPos = WholeToFixedPoint(startX);
                            sensor.collided = false;
                        }
                    }
                }
            }
        }
    }

    static void RoofCollision(ref Player player, ref CollisionSensor sensor)
    {
        int c;
        int startY = FixedPointToWhole(sensor.YPos);
        int tsm1 = (TILE_SIZE - 1);
        for (int i = 0; i < TILE_SIZE * 3; i += TILE_SIZE)
        {
            if (!sensor.collided)
            {
                int XPos = FixedPointToWhole(sensor.XPos);
                int chunkX = XPos / CHUNK_SIZE;
                int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                int YPos = FixedPointToWhole(sensor.YPos) + TILE_SIZE - i;
                int chunkY = YPos / CHUNK_SIZE;
                int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                if (XPos > -1 && YPos > -1)
                {
                    int tile = stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6;
                    tile += tileX + (tileY << 3);
                    int tileIndex = tiles128x128.tileIndex[tile];
                    if (tiles128x128.collisionFlags[player.collisionPlane, tile] != CollisionSolidity.SOLID_TOP
                        && tiles128x128.collisionFlags[player.collisionPlane, tile] < CollisionSolidity.SOLID_NONE)
                    {
                        switch (tiles128x128.direction[tile])
                        {
                            case FlipFlags.FLIP_NONE:
                                {
                                    c = (XPos & tsm1);
                                    if ((YPos & tsm1) >= collisionMasks[player.collisionPlane, tileIndex].roof[c] + TILE_SIZE - i)
                                        break;

                                    sensor.YPos = collisionMasks[player.collisionPlane, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    sensor.collided = true;
                                    sensor.angle = (int)(((collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF000000) >> 24));
                                    break;
                                }
                            case FlipFlags.FLIP_X:
                                {
                                    c = tsm1 - (XPos & tsm1);
                                    if ((YPos & tsm1) >= collisionMasks[player.collisionPlane, tileIndex].roof[c] + TILE_SIZE - i)
                                        break;

                                    sensor.YPos = collisionMasks[player.collisionPlane, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    sensor.collided = true;
                                    sensor.angle = (int)(0x100 - ((collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF000000) >> 24));
                                    break;
                                }
                            case FlipFlags.FLIP_Y:
                                {
                                    c = (XPos & tsm1);
                                    if ((YPos & tsm1) >= tsm1 - collisionMasks[player.collisionPlane, tileIndex].floor[c] + TILE_SIZE - i)
                                        break;

                                    sensor.YPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    sensor.collided = true;
                                    sensor.angle = (byte)(-0x80 - (collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF));
                                    break;
                                }
                            case FlipFlags.FLIP_XY:
                                {
                                    c = tsm1 - (XPos & tsm1);
                                    if ((YPos & tsm1) >= tsm1 - collisionMasks[player.collisionPlane, tileIndex].floor[c] + TILE_SIZE - i)
                                        break;

                                    sensor.YPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    sensor.collided = true;
                                    sensor.angle = 0x100 - (byte)(-0x80 - (collisionMasks[player.collisionPlane, tileIndex].angles & 0xFF));
                                    break;
                                }
                        }
                    }

                    if (sensor.collided)
                    {
                        sensor.angle = PositiveClampedModuloInclusive(sensor.angle, 0x100);

                        if (sensor.YPos - startY > (TILE_SIZE - 2))
                        {
                            sensor.YPos = WholeToFixedPoint(startY);
                            sensor.collided = false;
                        }
                        else if (sensor.YPos - startY < -(TILE_SIZE - 2))
                        {
                            sensor.YPos = WholeToFixedPoint(startY);
                            sensor.collided = false;
                        }
                    }
                }
            }
        }
    }

    static void RWallCollision(ref Player player, ref CollisionSensor sensor)
    {
        int c;
        int startX = FixedPointToWhole(sensor.XPos);
        int tsm1 = (TILE_SIZE - 1);
        for (int i = 0; i < TILE_SIZE * 3; i += TILE_SIZE)
        {
            if (!sensor.collided)
            {
                int XPos = FixedPointToWhole(sensor.XPos) + TILE_SIZE - i;
                int chunkX = XPos / CHUNK_SIZE;
                int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                int YPos = FixedPointToWhole(sensor.YPos);
                int chunkY = YPos / CHUNK_SIZE;
                int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                if (XPos > -1 && YPos > -1)
                {
                    int tile = stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6;
                    tile += tileX + (tileY << 3);
                    int tileIndex = tiles128x128.tileIndex[tile];
                    if (tiles128x128.collisionFlags[player.collisionPlane, tile] != CollisionSolidity.SOLID_TOP
                        && tiles128x128.collisionFlags[player.collisionPlane, tile] < CollisionSolidity.SOLID_NONE)
                    {
                        switch (tiles128x128.direction[tile])
                        {
                            case FlipFlags.FLIP_NONE:
                                {
                                    c = (YPos & tsm1);
                                    if ((XPos & tsm1) >= collisionMasks[player.collisionPlane, tileIndex].rightWall[c] + TILE_SIZE - i)
                                        break;

                                    sensor.XPos = collisionMasks[player.collisionPlane, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    break;
                                }
                            case FlipFlags.FLIP_X:
                                {
                                    c = (YPos & tsm1);
                                    if ((XPos & tsm1) >= tsm1 - collisionMasks[player.collisionPlane, tileIndex].leftWall[c] + TILE_SIZE - i)
                                        break;

                                    sensor.XPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    break;
                                }
                            case FlipFlags.FLIP_Y:
                                {
                                    c = tsm1 - (YPos & tsm1);
                                    if ((XPos & tsm1) >= collisionMasks[player.collisionPlane, tileIndex].rightWall[c] + TILE_SIZE - i)
                                        break;

                                    sensor.XPos = collisionMasks[player.collisionPlane, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    break;
                                }
                            case FlipFlags.FLIP_XY:
                                {
                                    c = tsm1 - (YPos & tsm1);
                                    if ((XPos & tsm1) >= tsm1 - collisionMasks[player.collisionPlane, tileIndex].leftWall[c] + TILE_SIZE - i)
                                        break;

                                    sensor.XPos = tsm1 - collisionMasks[player.collisionPlane, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    sensor.collided = true;
                                    break;
                                }
                        }
                    }

                    if (sensor.collided)
                    {
                        if (sensor.XPos - startX > tsm1)
                        {
                            sensor.XPos = WholeToFixedPoint(startX);
                            sensor.collided = false;
                        }
                        else if (sensor.XPos - startX < -tsm1)
                        {
                            sensor.XPos = WholeToFixedPoint(startX);
                            sensor.collided = false;
                        }
                    }
                }
            }
        }
    }

    static void ProcessAirCollision(ref Player player)
    {
        Hitbox playerHitbox = GetPlayerHitbox(player);
        collisionLeft = playerHitbox.left[0];
        collisionTop = playerHitbox.top[0];
        collisionRight = playerHitbox.right[0];
        collisionBottom = playerHitbox.bottom[0];

        byte movingDown = 0;
        byte movingUp = 1;
        byte movingLeft = 0;
        byte movingRight = 0;

        if (player.XVelocity < 0)
        {
            movingRight = 0;
        }
        else
        {
            movingRight = 1;
            sensors[0].YPos = player.YPos + WholeToFixedPoint(2);
            sensors[0].collided = false;
            sensors[0].XPos = player.XPos + WholeToFixedPoint(collisionRight);
        }
        if (player.XVelocity > 0)
        {
            movingLeft = 0;
        }
        else
        {
            movingLeft = 1;
            sensors[1].YPos = player.YPos + WholeToFixedPoint(2);
            sensors[1].collided = false;
            sensors[1].XPos = player.XPos + WholeToFixedPoint(collisionLeft - 1);
        }
        sensors[2].XPos = player.XPos + WholeToFixedPoint(playerHitbox.left[1]);
        sensors[3].XPos = player.XPos + WholeToFixedPoint(playerHitbox.right[1]);
        sensors[2].collided = false;
        sensors[3].collided = false;
        sensors[4].XPos = sensors[2].XPos;
        sensors[5].XPos = sensors[3].XPos;
        sensors[4].collided = false;
        sensors[5].collided = false;
        if (player.YVelocity < 0)
        {
            movingDown = 0;
        }
        else
        {
            movingDown = 1;
            sensors[2].YPos = player.YPos + WholeToFixedPoint(collisionBottom);
            sensors[3].YPos = player.YPos + WholeToFixedPoint(collisionBottom);
        }
        sensors[4].YPos = player.YPos + WholeToFixedPoint(collisionTop - 1);
        sensors[5].YPos = player.YPos + WholeToFixedPoint(collisionTop - 1);
        int cnt = (System.Math.Abs(player.XVelocity) <= System.Math.Abs(player.YVelocity) ? (System.Math.Abs(player.YVelocity) >> 19) + 1 : (System.Math.Abs(player.XVelocity) >> 19) + 1);
        int XVel = player.XVelocity / cnt;
        int YVel = player.YVelocity / cnt;
        int XVel2 = player.XVelocity - XVel * (cnt - 1);
        int YVel2 = player.YVelocity - YVel * (cnt - 1);
        while (cnt > 0)
        {
            if (cnt < 2)
            {
                XVel = XVel2;
                YVel = YVel2;
            }
            cnt--;

            if (movingRight == 1)
            {
                sensors[0].XPos += XVel + WholeToFixedPoint(1);
                sensors[0].YPos += YVel;
                LWallCollision(ref player, ref sensors[0]);
                if (sensors[0].collided)
                    movingRight = 2;
            }

            if (movingLeft == 1)
            {
                sensors[1].XPos += XVel - WholeToFixedPoint(1);
                sensors[1].YPos += YVel;
                RWallCollision(ref player, ref sensors[1]);
                if (sensors[1].collided)
                    movingLeft = 2;
            }

            if (movingRight == 2)
            {
                player.XVelocity = 0;
                player.speed = 0;
                player.XPos = WholeToFixedPoint(sensors[0].XPos - collisionRight);
                sensors[2].XPos = player.XPos + WholeToFixedPoint(collisionLeft + 1);
                sensors[3].XPos = player.XPos + WholeToFixedPoint(collisionRight - 2);
                sensors[4].XPos = sensors[2].XPos;
                sensors[5].XPos = sensors[3].XPos;
                XVel = 0;
                XVel2 = 0;
                movingRight = 3;
            }

            if (movingLeft == 2)
            {
                player.XVelocity = 0;
                player.speed = 0;
                player.XPos = WholeToFixedPoint(sensors[1].XPos - collisionLeft + 1);
                sensors[2].XPos = player.XPos + WholeToFixedPoint(collisionLeft + 1);
                sensors[3].XPos = player.XPos + WholeToFixedPoint(collisionRight - 2);
                sensors[4].XPos = sensors[2].XPos;
                sensors[5].XPos = sensors[3].XPos;
                XVel = 0;
                XVel2 = 0;
                movingLeft = 3;
            }

            if (movingDown == 1)
            {
                for (int i = 2; i < 4; i++)
                {
                    if (!sensors[i].collided)
                    {
                        sensors[i].XPos += XVel;
                        sensors[i].YPos += YVel;
                        FloorCollision(ref player, ref sensors[i]);
                    }
                }
                if (sensors[2].collided || sensors[3].collided)
                {
                    movingDown = 2;
                    cnt = 0;
                }
            }

            if (movingUp == 1)
            {
                for (int i = 4; i < 6; i++)
                {
                    if (!sensors[i].collided)
                    {
                        sensors[i].XPos += XVel;
                        sensors[i].YPos += YVel;
                        RoofCollision(ref player, ref sensors[i]);
                    }
                }
                if (sensors[4].collided || sensors[5].collided)
                {
                    movingUp = 2;
                    cnt = 0;
                }
            }
        }

        if (movingRight < 2 && movingLeft < 2)
            player.XPos += player.XVelocity;

        if (movingUp < 2 && movingDown < 2)
        {
            player.YPos += player.YVelocity;
            return;
        }

        if (movingDown == 2)
        {
            player.gravity = 0;
            if (sensors[2].collided && sensors[3].collided)
            {
                if (sensors[2].YPos >= sensors[3].YPos)
                {
                    player.YPos = WholeToFixedPoint(sensors[3].YPos - collisionBottom);
                    player.angle = sensors[3].angle;
                }
                else
                {
                    player.YPos = WholeToFixedPoint(sensors[2].YPos - collisionBottom);
                    player.angle = sensors[2].angle;
                }
            }
            else if (sensors[2].collided)
            {
                player.YPos = WholeToFixedPoint(sensors[2].YPos - collisionBottom);
                player.angle = sensors[2].angle;
            }
            else if (sensors[3].collided)
            {
                player.YPos = WholeToFixedPoint(sensors[3].YPos - collisionBottom);
                player.angle = sensors[3].angle;
            }
            if (player.angle > 0xA0 && player.angle < 0xE0 && player.collisionMode != (byte)CollisionModes.CMODE_LWALL)
            {
                player.collisionMode = (byte)CollisionModes.CMODE_LWALL;
                player.XPos -= WholeToFixedPoint(4);
            }
            if (player.angle > DegreesToByteAngle(45) && player.angle < DegreesToByteAngle(135) && player.collisionMode != (byte)CollisionModes.CMODE_RWALL)
            {
                player.collisionMode = (byte)CollisionModes.CMODE_RWALL;
                player.XPos += WholeToFixedPoint(4);
            }
            if (player.angle < DegreesToByteAngle(45) || player.angle > 0xE0)
            {
                player.controlLock = 0;
            }
            objectEntityList[player.boundEntity].rotation = player.angle << 1;

            int speed;
            if (player.down != 0)
            {
                if (player.angle < 128)
                {
                    if (player.angle < 16)
                    {
                        speed = player.XVelocity;
                    }
                    else if (player.angle >= 32)
                    {
                        speed = System.Math.Abs(player.XVelocity) <= System.Math.Abs(player.YVelocity) ? player.YVelocity + (player.YVelocity / 12) : player.XVelocity;
                    }
                    else
                    {
                        speed = System.Math.Abs(player.XVelocity) <= System.Math.Abs(player.YVelocity >> 1) ? (player.YVelocity + (player.YVelocity / 12)) >> 1 : player.XVelocity;
                    }
                }
                else if (player.angle > 240)
                {
                    speed = player.XVelocity;
                }
                else if (player.angle <= 224)
                {
                    speed = (System.Math.Abs(player.XVelocity) <= System.Math.Abs(player.YVelocity) ? -(player.YVelocity + player.YVelocity / 12) : player.XVelocity);
                }
                else
                {
                    speed = (System.Math.Abs(player.XVelocity) <= System.Math.Abs(player.YVelocity >> 1) ? -((player.YVelocity + player.YVelocity / 12) >> 1)
                                                                                   : player.XVelocity);
                }
            }
            else if (player.angle < DegreesToByteAngle(180))
            {
                if (player.angle < 0x10)
                {
                    speed = player.XVelocity;
                }
                else if (player.angle >= DegreesToByteAngle(45))
                {
                    speed = (System.Math.Abs(player.XVelocity) <= System.Math.Abs(player.YVelocity) ? player.YVelocity : player.XVelocity);
                }
                else
                {
                    speed = (System.Math.Abs(player.XVelocity) <= System.Math.Abs(player.YVelocity >> 1) ? player.YVelocity >> 1 : player.XVelocity);
                }
            }
            else if (player.angle > 0xF0)
            {
                speed = player.XVelocity;
            }
            else if (player.angle <= 0xE0)
            {
                speed = (System.Math.Abs(player.XVelocity) <= System.Math.Abs(player.YVelocity) ? -player.YVelocity : player.XVelocity);
            }
            else
            {
                speed = (System.Math.Abs(player.XVelocity) <= System.Math.Abs(player.YVelocity >> 1) ? -(player.YVelocity >> 1) : player.XVelocity);
            }

            if (speed < -WholeToFixedPoint(0x18))
                speed = -WholeToFixedPoint(0x18);
            if (speed > WholeToFixedPoint(0x18))
                speed = WholeToFixedPoint(0x18);
            player.speed = speed;
            player.YVelocity = 0;
            scriptEng.checkResult = 1;
        }

        if (movingUp == 2)
        {
            int sensorAngle = 0;
            if (sensors[4].collided && sensors[5].collided)
            {
                if (sensors[4].YPos <= sensors[5].YPos)
                {
                    player.YPos = WholeToFixedPoint(sensors[5].YPos - collisionTop + 1);
                    sensorAngle = sensors[5].angle;
                }
                else
                {
                    player.YPos = WholeToFixedPoint(sensors[4].YPos - collisionTop + 1);
                    sensorAngle = sensors[4].angle;
                }
            }
            else if (sensors[4].collided)
            {
                player.YPos = WholeToFixedPoint(sensors[4].YPos - collisionTop + 1);
                sensorAngle = sensors[4].angle;
            }
            else if (sensors[5].collided)
            {
                player.YPos = WholeToFixedPoint(sensors[5].YPos - collisionTop + 1);
                sensorAngle = sensors[5].angle;
            }
            sensorAngle &= 0xFF;

            int angle = ArcTanLookup(player.XVelocity, player.YVelocity);
            if (sensorAngle > DegreesToByteAngle(90) && sensorAngle < 0x62 && angle > DegreesToByteAngle(225) && angle < 0xC2)
            {
                player.gravity = 0;
                player.angle = sensorAngle;
                objectEntityList[player.boundEntity].rotation = player.angle << 1;
                player.collisionMode = (byte)CollisionModes.CMODE_RWALL;
                player.XPos += WholeToFixedPoint(4);
                player.YPos -= WholeToFixedPoint(2);
                if (player.angle <= DegreesToByteAngle(135))
                    player.speed = player.YVelocity;
                else
                    player.speed = player.YVelocity >> 1;
            }
            if (sensorAngle > 0x9E && sensorAngle < DegreesToByteAngle(270) && angle > 0xBE && angle < DegreesToByteAngle(315))
            {
                player.gravity = 0;
                player.angle = sensorAngle;
                objectEntityList[player.boundEntity].rotation = player.angle << 1;
                player.collisionMode = (byte)CollisionModes.CMODE_LWALL;
                player.XPos -= WholeToFixedPoint(4);
                player.YPos -= WholeToFixedPoint(2);
                if (player.angle >= 0xA0)
                    player.speed = -player.YVelocity;
                else
                    player.speed = -player.YVelocity >> 1;
            }
            if (player.YVelocity < 0)
                player.YVelocity = 0;
            scriptEng.checkResult = 2;
        }
    }

    static void ProcessPathGrip(ref Player player)
    {
        int cosValue256;
        int sinValue256;
        sensors[4].XPos = player.XPos;
        sensors[4].YPos = player.YPos;
        for (int i = 0; i < 6; ++i)
        {
            sensors[i].angle = player.angle;
            sensors[i].collided = false;
        }
        SetPathGripSensors(ref player);
        int absSpeed = System.Math.Abs(player.speed);
        int checkDist = absSpeed >> 18;
        absSpeed &= 0x3FFFF;
        byte cMode = player.collisionMode;

        while (checkDist > -1)
        {
            if (checkDist >= 1)
            {
                cosValue256 = cos256LookupTable[player.angle] << 10;
                sinValue256 = sin256LookupTable[player.angle] << 10;
                checkDist--;
            }
            else
            {
                cosValue256 = absSpeed * cos256LookupTable[player.angle] >> 8;
                sinValue256 = absSpeed * sin256LookupTable[player.angle] >> 8;
                checkDist = -1;
            }

            if (player.speed < 0)
            {
                cosValue256 = -cosValue256;
                sinValue256 = -sinValue256;
            }

            sensors[0].collided = false;
            sensors[1].collided = false;
            sensors[2].collided = false;
            sensors[3].XPos += cosValue256;
            sensors[3].YPos += sinValue256;
            sensors[4].XPos += cosValue256;
            sensors[4].YPos += sinValue256;
            int tileDistance = -1;
            switch ((CollisionModes)player.collisionMode)
            {
                case CollisionModes.CMODE_FLOOR:
                    if (player.speed > 0)
                        LWallCollision(ref player, ref sensors[3]);

                    if (player.speed < 0)
                        RWallCollision(ref player, ref sensors[3]);

                    if (sensors[3].collided)
                    {
                        cosValue256 = 0;
                        checkDist = -1;
                    }

                    for (int i = 0; i < 3; i++)
                    {
                        sensors[i].XPos += cosValue256;
                        sensors[i].YPos += sinValue256;
                        FindFloorPosition(ref player, ref sensors[i], FixedPointToWhole(sensors[i].YPos));
                    }

                    tileDistance = -1;
                    for (int i = 0; i < 3; i++)
                    {
                        if (tileDistance > -1)
                        {
                            if (sensors[i].collided)
                            {
                                if (sensors[i].YPos < sensors[tileDistance].YPos)
                                    tileDistance = i;

                                if (sensors[i].YPos == sensors[tileDistance].YPos && (sensors[i].angle < 0x08 || sensors[i].angle > 0xF8))
                                    tileDistance = i;
                            }
                        }
                        else if (sensors[i].collided)
                        {
                            tileDistance = i;
                        }
                    }

                    if (tileDistance <= -1)
                    {
                        checkDist = -1;
                    }
                    else
                    {
                        sensors[0].YPos = WholeToFixedPoint(sensors[tileDistance].YPos);
                        sensors[0].angle = sensors[tileDistance].angle;
                        sensors[1].YPos = sensors[0].YPos;
                        sensors[1].angle = sensors[0].angle;
                        sensors[2].YPos = sensors[0].YPos;
                        sensors[2].angle = sensors[0].angle;
                        sensors[3].YPos = sensors[0].YPos - WholeToFixedPoint(4);
                        sensors[3].angle = sensors[0].angle;
                        sensors[4].XPos = sensors[1].XPos;
                        sensors[4].YPos = sensors[0].YPos - WholeToFixedPoint(collisionBottom);
                    }

                    if (sensors[0].angle < 0xDE && sensors[0].angle > DegreesToByteAngle(180))
                        player.collisionMode = (byte)CollisionModes.CMODE_LWALL;
                    if (sensors[0].angle > 0x22 && sensors[0].angle < DegreesToByteAngle(180))
                        player.collisionMode = (byte)CollisionModes.CMODE_RWALL;
                    break;
                case CollisionModes.CMODE_LWALL:
                    if (player.speed > 0)
                        RoofCollision(ref player, ref sensors[3]);

                    if (player.speed < 0)
                        FloorCollision(ref player, ref sensors[3]);

                    if (sensors[3].collided)
                    {
                        sinValue256 = 0;
                        checkDist = -1;
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        sensors[i].XPos += cosValue256;
                        sensors[i].YPos += sinValue256;
                        FindLWallPosition(ref player, ref sensors[i], FixedPointToWhole(sensors[i].XPos));
                    }

                    tileDistance = -1;
                    for (int i = 0; i < 3; i++)
                    {
                        if (tileDistance > -1)
                        {
                            if (sensors[i].XPos < sensors[tileDistance].XPos && sensors[i].collided)
                            {
                                tileDistance = i;
                            }
                        }
                        else if (sensors[i].collided)
                        {
                            tileDistance = i;
                        }
                    }

                    if (tileDistance <= -1)
                    {
                        checkDist = -1;
                    }
                    else
                    {
                        sensors[0].XPos = WholeToFixedPoint(sensors[tileDistance].XPos);
                        sensors[0].angle = sensors[tileDistance].angle;
                        sensors[1].XPos = sensors[0].XPos;
                        sensors[1].angle = sensors[0].angle;
                        sensors[2].XPos = sensors[0].XPos;
                        sensors[2].angle = sensors[0].angle;
                        sensors[4].YPos = sensors[1].YPos;
                        sensors[4].XPos = sensors[1].XPos - WholeToFixedPoint(collisionRight);
                    }

                    if (sensors[0].angle > 0xE2)
                        player.collisionMode = (byte)CollisionModes.CMODE_FLOOR;
                    if (sensors[0].angle < 0x9E)
                        player.collisionMode = (byte)CollisionModes.CMODE_ROOF;
                    break;
                case CollisionModes.CMODE_ROOF:
                    if (player.speed > 0)
                        RWallCollision(ref player, ref sensors[3]);

                    if (player.speed < 0)
                        LWallCollision(ref player, ref sensors[3]);

                    if (sensors[3].collided)
                    {
                        cosValue256 = 0;
                        checkDist = -1;
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        sensors[i].XPos += cosValue256;
                        sensors[i].YPos += sinValue256;
                        FindRoofPosition(ref player, ref sensors[i], FixedPointToWhole(sensors[i].YPos));
                    }

                    tileDistance = -1;
                    for (int i = 0; i < 3; i++)
                    {
                        if (tileDistance > -1)
                        {
                            if (sensors[i].YPos > sensors[tileDistance].YPos && sensors[i].collided)
                            {
                                tileDistance = i;
                            }
                        }
                        else if (sensors[i].collided)
                        {
                            tileDistance = i;
                        }
                    }

                    if (tileDistance <= -1)
                    {
                        checkDist = -1;
                    }
                    else
                    {
                        sensors[0].YPos = WholeToFixedPoint(sensors[tileDistance].YPos);
                        sensors[0].angle = sensors[tileDistance].angle;
                        sensors[1].YPos = sensors[0].YPos;
                        sensors[1].angle = sensors[0].angle;
                        sensors[2].YPos = sensors[0].YPos;
                        sensors[2].angle = sensors[0].angle;
                        sensors[3].YPos = sensors[0].YPos + WholeToFixedPoint(4);
                        sensors[3].angle = sensors[0].angle;
                        sensors[4].XPos = sensors[1].XPos;
                        sensors[4].YPos = sensors[0].YPos - WholeToFixedPoint(collisionTop - 1);
                    }

                    if (sensors[0].angle > 0xA2)
                        player.collisionMode = (byte)CollisionModes.CMODE_LWALL;
                    if (sensors[0].angle < 0x5E)
                        player.collisionMode = (byte)CollisionModes.CMODE_RWALL;
                    break;
                case CollisionModes.CMODE_RWALL:
                    if (player.speed > 0)
                        FloorCollision(ref player, ref sensors[3]);

                    if (player.speed < 0)
                        RoofCollision(ref player, ref sensors[3]);

                    if (sensors[3].collided)
                    {
                        sinValue256 = 0;
                        checkDist = -1;
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        sensors[i].XPos += cosValue256;
                        sensors[i].YPos += sinValue256;
                        FindRWallPosition(ref player, ref sensors[i], FixedPointToWhole(sensors[i].XPos));
                    }

                    tileDistance = -1;
                    for (int i = 0; i < 3; i++)
                    {
                        if (tileDistance > -1)
                        {
                            if (sensors[i].XPos > sensors[tileDistance].XPos && sensors[i].collided)
                            {
                                tileDistance = i;
                            }
                        }
                        else if (sensors[i].collided)
                        {
                            tileDistance = i;
                        }
                    }

                    if (tileDistance <= -1)
                    {
                        checkDist = -1;
                    }
                    else
                    {
                        sensors[0].XPos = WholeToFixedPoint(sensors[tileDistance].XPos);
                        sensors[0].angle = sensors[tileDistance].angle;
                        sensors[1].XPos = sensors[0].XPos;
                        sensors[1].angle = sensors[0].angle;
                        sensors[2].XPos = sensors[0].XPos;
                        sensors[2].angle = sensors[0].angle;
                        sensors[4].XPos = sensors[1].XPos - WholeToFixedPoint(collisionLeft - 1);
                        sensors[4].YPos = sensors[1].YPos;
                    }

                    if (sensors[0].angle < 0x1E)
                        player.collisionMode = (byte)CollisionModes.CMODE_FLOOR;
                    if (sensors[0].angle > 0x62)
                        player.collisionMode = (byte)CollisionModes.CMODE_ROOF;
                    break;
            }
            if (tileDistance > -1)
                player.angle = sensors[0].angle;

            if (!sensors[3].collided)
                SetPathGripSensors(ref player);
            else
                checkDist = -2;
        }

        switch ((CollisionModes)cMode)
        {
            case CollisionModes.CMODE_FLOOR:
                {
                    if (sensors[0].collided || sensors[1].collided || sensors[2].collided)
                    {
                        player.angle = sensors[0].angle;
                        objectEntityList[player.boundEntity].rotation = player.angle << 1;
                        player.flailing[0] = (byte)(sensors[0].collided ? 1 : 0);
                        player.flailing[1] = (byte)(sensors[1].collided ? 1 : 0);
                        player.flailing[2] = (byte)(sensors[2].collided ? 1 : 0);
                        if (!sensors[3].collided)
                        {
                            player.pushing = 0;
                            player.XPos = sensors[4].XPos;
                        }
                        else
                        {
                            if (player.speed > 0)
                                player.XPos = WholeToFixedPoint(sensors[3].XPos - collisionRight);

                            if (player.speed < 0)
                                player.XPos = WholeToFixedPoint(sensors[3].XPos - collisionLeft + 1);

                            player.speed = 0;
                            if ((player.left != 0 || player.right != 0) && player.pushing < 2)
                                player.pushing++;
                        }
                        player.YPos = sensors[4].YPos;
                        return;
                    }
                    player.gravity = 1;
                    player.collisionMode = (byte)CollisionModes.CMODE_FLOOR;
                    player.XVelocity = cos256LookupTable[player.angle] * player.speed >> 8;
                    player.YVelocity = sin256LookupTable[player.angle] * player.speed >> 8;
                    if (player.YVelocity < -WholeToFixedPoint(TILE_SIZE))
                        player.YVelocity = -WholeToFixedPoint(TILE_SIZE);

                    if (player.YVelocity > WholeToFixedPoint(TILE_SIZE))
                        player.YVelocity = WholeToFixedPoint(TILE_SIZE);

                    player.speed = player.XVelocity;
                    player.angle = 0;
                    if (!sensors[3].collided)
                    {
                        player.pushing = 0;
                        player.XPos += player.XVelocity;
                    }
                    else
                    {
                        if (player.speed > 0)
                            player.XPos = WholeToFixedPoint(sensors[3].XPos - collisionRight);
                        if (player.speed < 0)
                            player.XPos = WholeToFixedPoint(sensors[3].XPos - collisionLeft + 1);

                        player.speed = 0;
                        if ((player.left != 0 || player.right != 0) && player.pushing < 2)
                            player.pushing++;
                    }
                    player.YPos += player.YVelocity;
                    return;
                }
            case CollisionModes.CMODE_LWALL:
                {
                    if (!sensors[0].collided && !sensors[1].collided && !sensors[2].collided)
                    {
                        player.gravity = 1;
                        player.collisionMode = (byte)CollisionModes.CMODE_FLOOR;
                        player.XVelocity = cos256LookupTable[player.angle] * player.speed >> 8;
                        player.YVelocity = sin256LookupTable[player.angle] * player.speed >> 8;
                        if (player.YVelocity < -WholeToFixedPoint(TILE_SIZE))
                        {
                            player.YVelocity = -WholeToFixedPoint(TILE_SIZE);
                        }
                        if (player.YVelocity > WholeToFixedPoint(TILE_SIZE))
                        {
                            player.YVelocity = WholeToFixedPoint(TILE_SIZE);
                        }
                        player.speed = player.XVelocity;
                        player.angle = 0;
                    }
                    else if (player.speed >= 0x28000 || player.speed <= -0x28000 || player.controlLock != 0)
                    {
                        player.angle = sensors[0].angle;
                        objectEntityList[player.boundEntity].rotation = player.angle << 1;
                    }
                    else
                    {
                        player.gravity = 1;
                        player.angle = 0;
                        player.collisionMode = (byte)CollisionModes.CMODE_FLOOR;
                        player.speed = player.XVelocity;
                        player.controlLock = 30;
                    }
                    if (!sensors[3].collided)
                    {
                        player.YPos = sensors[4].YPos;
                    }
                    else
                    {
                        if (player.speed > 0)
                            player.YPos = WholeToFixedPoint(sensors[3].YPos - collisionTop);

                        if (player.speed < 0)
                            player.YPos = WholeToFixedPoint(sensors[3].YPos - collisionBottom);

                        player.speed = 0;
                    }
                    player.XPos = sensors[4].XPos;
                    return;
                }
            case CollisionModes.CMODE_ROOF:
                {
                    if (!sensors[0].collided && !sensors[1].collided && !sensors[2].collided)
                    {
                        player.gravity = 1;
                        player.collisionMode = (byte)CollisionModes.CMODE_FLOOR;
                        player.XVelocity = cos256LookupTable[player.angle] * player.speed >> 8;
                        player.YVelocity = sin256LookupTable[player.angle] * player.speed >> 8;
                        player.flailing[0] = 0;
                        player.flailing[1] = 0;
                        player.flailing[2] = 0;
                        if (player.YVelocity < -WholeToFixedPoint(TILE_SIZE))
                            player.YVelocity = -WholeToFixedPoint(TILE_SIZE);

                        if (player.YVelocity > WholeToFixedPoint(TILE_SIZE))
                            player.YVelocity = WholeToFixedPoint(TILE_SIZE);

                        player.angle = 0;
                        player.speed = player.XVelocity;
                        if (!sensors[3].collided)
                        {
                            player.XPos += player.XVelocity;
                        }
                        else
                        {
                            if (player.speed > 0)
                                player.XPos = WholeToFixedPoint(sensors[3].XPos - collisionRight);

                            if (player.speed < 0)
                                player.XPos = WholeToFixedPoint(sensors[3].XPos - collisionLeft + 1);

                            player.speed = 0;
                        }
                    }
                    else if (player.speed <= -0x28000 || player.speed >= 0x28000)
                    {
                        player.angle = sensors[0].angle;
                        objectEntityList[player.boundEntity].rotation = player.angle << 1;
                        if (!sensors[3].collided)
                        {
                            player.XPos = sensors[4].XPos;
                        }
                        else
                        {
                            if (player.speed < 0)
                                player.XPos = WholeToFixedPoint(sensors[3].XPos - collisionRight);

                            if (player.speed > 0)
                                player.XPos = WholeToFixedPoint(sensors[3].XPos - collisionLeft + 1);
                            player.speed = 0;
                        }
                    }
                    else
                    {
                        player.gravity = 1;
                        player.angle = 0;
                        player.collisionMode = (byte)CollisionModes.CMODE_FLOOR;
                        player.speed = player.XVelocity;
                        player.flailing[0] = 0;
                        player.flailing[1] = 0;
                        player.flailing[2] = 0;
                        if (!sensors[3].collided)
                        {
                            player.XPos += player.XVelocity;
                        }
                        else
                        {
                            if (player.speed > 0)
                                player.XPos = WholeToFixedPoint(sensors[3].XPos - collisionRight);

                            if (player.speed < 0)
                                player.XPos = WholeToFixedPoint(sensors[3].XPos - collisionLeft + 1);
                            player.speed = 0;
                        }
                    }
                    player.YPos = sensors[4].YPos;
                    return;
                }
            case CollisionModes.CMODE_RWALL:
                {
                    if (!sensors[0].collided && !sensors[1].collided && !sensors[2].collided)
                    {
                        player.gravity = 1;
                        player.collisionMode = (byte)CollisionModes.CMODE_FLOOR;
                        player.XVelocity = cos256LookupTable[player.angle] * player.speed >> 8;
                        player.YVelocity = sin256LookupTable[player.angle] * player.speed >> 8;
                        if (player.YVelocity < -WholeToFixedPoint(TILE_SIZE))
                            player.YVelocity = -WholeToFixedPoint(TILE_SIZE);

                        if (player.YVelocity > WholeToFixedPoint(TILE_SIZE))
                            player.YVelocity = WholeToFixedPoint(TILE_SIZE);

                        player.speed = player.XVelocity;
                        player.angle = 0;
                    }
                    else if (player.speed <= -0x28000 || player.speed >= 0x28000 || player.controlLock != 0)
                    {
                        player.angle = sensors[0].angle;
                        objectEntityList[player.boundEntity].rotation = player.angle << 1;
                    }
                    else
                    {
                        player.gravity = 1;
                        player.angle = 0;
                        player.collisionMode = (byte)CollisionModes.CMODE_FLOOR;
                        player.speed = player.XVelocity;
                        player.controlLock = 30;
                    }
                    if (!sensors[3].collided)
                    {
                        player.YPos = sensors[4].YPos;
                    }
                    else
                    {
                        if (player.speed > 0)
                            player.YPos = WholeToFixedPoint(sensors[3].YPos - collisionBottom);

                        if (player.speed < 0)
                            player.YPos = WholeToFixedPoint(sensors[3].YPos - collisionTop + 1);

                        player.speed = 0;
                    }
                    player.XPos = sensors[4].XPos;
                    return;
                }
            default: return;
        }
    }

    static void SetPathGripSensors(ref Player player)
    {
        Hitbox playerHitbox = GetPlayerHitbox(player);
        switch ((CollisionModes)player.collisionMode)
        {
            case CollisionModes.CMODE_FLOOR:
                collisionLeft = playerHitbox.left[0];
                collisionTop = playerHitbox.top[0];
                collisionRight = playerHitbox.right[0];
                collisionBottom = playerHitbox.bottom[0];
                sensors[0].XPos = sensors[4].XPos + WholeToFixedPoint(playerHitbox.left[1] - 1);
                sensors[0].YPos = sensors[4].YPos + WholeToFixedPoint(collisionBottom);
                sensors[1].XPos = sensors[4].XPos;
                sensors[1].YPos = sensors[4].YPos + WholeToFixedPoint(collisionBottom);
                sensors[2].XPos = sensors[4].XPos + WholeToFixedPoint(playerHitbox.right[1]);
                sensors[2].YPos = sensors[4].YPos + WholeToFixedPoint(collisionBottom);
                if (player.speed > 0)
                {
                    sensors[3].XPos = sensors[4].XPos + WholeToFixedPoint(collisionRight + 1);
                }
                else
                {
                    sensors[3].XPos = sensors[4].XPos + WholeToFixedPoint(collisionLeft - 1);
                }
                sensors[3].YPos = sensors[4].YPos + WholeToFixedPoint(4);
                break;
            case CollisionModes.CMODE_LWALL:
                collisionLeft = playerHitbox.left[2];
                collisionTop = playerHitbox.top[2];
                collisionRight = playerHitbox.right[2];
                collisionBottom = playerHitbox.bottom[2];
                sensors[0].XPos = sensors[4].XPos + WholeToFixedPoint(collisionRight);
                sensors[0].YPos = sensors[4].YPos + WholeToFixedPoint(playerHitbox.top[3] - 1);
                sensors[1].XPos = sensors[4].XPos + WholeToFixedPoint(collisionRight);
                sensors[1].YPos = sensors[4].YPos;
                sensors[2].XPos = sensors[4].XPos + WholeToFixedPoint(collisionRight);
                sensors[2].YPos = sensors[4].YPos + WholeToFixedPoint(playerHitbox.bottom[3]);
                sensors[3].XPos = sensors[4].XPos + WholeToFixedPoint(4);
                if (player.speed > 0)
                {
                    sensors[3].YPos = sensors[4].YPos + WholeToFixedPoint(collisionTop);
                }
                else
                {
                    sensors[3].YPos = sensors[4].YPos + WholeToFixedPoint(collisionBottom - 1);
                }
                break;
            case CollisionModes.CMODE_ROOF:
                collisionLeft = playerHitbox.left[4];
                collisionTop = playerHitbox.top[4];
                collisionRight = playerHitbox.right[4];
                collisionBottom = playerHitbox.bottom[4];
                sensors[0].XPos = sensors[4].XPos + WholeToFixedPoint(playerHitbox.left[5] - 1);
                sensors[0].YPos = sensors[4].YPos + WholeToFixedPoint(collisionTop - 1);
                sensors[1].XPos = sensors[4].XPos;
                sensors[1].YPos = sensors[4].YPos + WholeToFixedPoint(collisionTop - 1);
                sensors[2].YPos = sensors[4].YPos + WholeToFixedPoint(collisionTop - 1);
                sensors[2].XPos = sensors[4].XPos + WholeToFixedPoint(playerHitbox.right[5]);
                if (player.speed < 0)
                {
                    sensors[3].XPos = sensors[4].XPos + WholeToFixedPoint(collisionRight + 1);
                }
                else
                {
                    sensors[3].XPos = sensors[4].XPos + WholeToFixedPoint(collisionLeft - 1);
                }
                sensors[3].YPos = sensors[4].YPos - WholeToFixedPoint(4);
                break;
            case CollisionModes.CMODE_RWALL:
                collisionLeft = playerHitbox.left[6];
                collisionTop = playerHitbox.top[6];
                collisionRight = playerHitbox.right[6];
                collisionBottom = playerHitbox.bottom[6];
                sensors[0].XPos = sensors[4].XPos + WholeToFixedPoint(collisionLeft - 1);
                sensors[0].YPos = sensors[4].YPos + WholeToFixedPoint((playerHitbox.top[7] - 1));
                sensors[1].XPos = sensors[4].XPos + WholeToFixedPoint(collisionLeft - 1);
                sensors[1].YPos = sensors[4].YPos;
                sensors[2].XPos = sensors[4].XPos + WholeToFixedPoint(collisionLeft - 1);
                sensors[2].YPos = sensors[4].YPos + WholeToFixedPoint(playerHitbox.bottom[7]);
                sensors[3].XPos = sensors[4].XPos - WholeToFixedPoint(4);
                if (player.speed > 0)
                {
                    sensors[3].YPos = sensors[4].YPos + WholeToFixedPoint(collisionBottom);
                }
                else
                {
                    sensors[3].YPos = sensors[4].YPos + WholeToFixedPoint((collisionTop - 1));
                }
                break;
            default: break;
        }
    }

    internal static void ProcessPlayerTileCollisions(ref Player player)
    {
        player.flailing[0] = 0;
        player.flailing[1] = 0;
        player.flailing[2] = 0;
        scriptEng.checkResult = 0;
        if (player.gravity == 1)
            ProcessAirCollision(ref player);
        else
            ProcessPathGrip(ref player);
    }

    public static void ObjectFloorCollision(int xOffset, int yOffset, int cPath)
    {
        scriptEng.checkResult = 0;
        fixed (Entity* entity = &objectEntityList[objectLoop])
        {
            int c = 0;
            int XPos = FixedPointToWhole(entity->XPos) + xOffset;
            int YPos = FixedPointToWhole(entity->YPos) + yOffset;
            if (XPos > 0 && XPos < stageLayouts[0].xsize * CHUNK_SIZE && YPos > 0 && YPos < stageLayouts[0].ysize * CHUNK_SIZE)
            {
                int chunkX = XPos / CHUNK_SIZE;
                int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                int chunkY = YPos / CHUNK_SIZE;
                int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                int chunk = (stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6) + tileX + (tileY << 3);
                int tileIndex = tiles128x128.tileIndex[chunk];
                if ((CollisionSolidity)tiles128x128.collisionFlags[cPath, chunk] != CollisionSolidity.SOLID_LRB && (CollisionSolidity)tiles128x128.collisionFlags[cPath, chunk] != CollisionSolidity.SOLID_NONE)
                {
                    switch (tiles128x128.direction[chunk])
                    {
                        case 0:
                            {
                                c = (XPos % TILE_SIZE);
                                if ((YPos % TILE_SIZE) <= collisionMasks[cPath, tileIndex].floor[c])
                                {
                                    break;
                                }
                                YPos = collisionMasks[cPath, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                        case (FlipFlags)1:
                            {
                                c = 15 - (XPos % TILE_SIZE);
                                if ((YPos % TILE_SIZE) <= collisionMasks[cPath, tileIndex].floor[c])
                                {
                                    break;
                                }
                                YPos = collisionMasks[cPath, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                        case (FlipFlags)2:
                            {
                                c = (XPos % TILE_SIZE);
                                if ((YPos % TILE_SIZE) <= 15 - collisionMasks[cPath, tileIndex].roof[c])
                                {
                                    break;
                                }
                                YPos = 15 - collisionMasks[cPath, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                        case (FlipFlags)3:
                            {
                                c = 15 - (XPos % TILE_SIZE);
                                if ((YPos % TILE_SIZE) <= 15 - collisionMasks[cPath, tileIndex].roof[c])
                                {
                                    break;
                                }
                                YPos = 15 - collisionMasks[cPath, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                    }
                }
                if (scriptEng.checkResult != 0)
                {
                    entity->YPos = WholeToFixedPoint(YPos - yOffset);
                }
            }
        }
    }

    public static void ObjectLWallCollision(int xOffset, int yOffset, int cPath)
    {
        int c;
        scriptEng.checkResult = 0;
        fixed (Entity* entity = &objectEntityList[objectLoop])
        {
            int XPos = FixedPointToWhole(entity->XPos) + xOffset;
            int YPos = FixedPointToWhole(entity->YPos) + yOffset;
            if (XPos > 0 && XPos < stageLayouts[0].xsize * CHUNK_SIZE && YPos > 0 && YPos < stageLayouts[0].ysize * CHUNK_SIZE)
            {
                int chunkX = XPos / CHUNK_SIZE;
                int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                int chunkY = YPos / CHUNK_SIZE;
                int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                int chunk = stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6;
                chunk = chunk + tileX + (tileY << 3);
                int tileIndex = tiles128x128.tileIndex[chunk];
                if (tiles128x128.collisionFlags[cPath, chunk] != CollisionSolidity.SOLID_TOP && tiles128x128.collisionFlags[cPath, chunk] < CollisionSolidity.SOLID_NONE)
                {
                    switch (tiles128x128.direction[chunk])
                    {
                        case (FlipFlags)0:
                            {
                                c = (YPos % TILE_SIZE);
                                if ((XPos % TILE_SIZE) <= collisionMasks[cPath, tileIndex].leftWall[c])
                                {
                                    break;
                                }
                                XPos = collisionMasks[cPath, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                        case (FlipFlags)1:
                            {
                                c = (YPos % TILE_SIZE);
                                if ((XPos % TILE_SIZE) <= 15 - collisionMasks[cPath, tileIndex].rightWall[c])
                                {
                                    break;
                                }
                                XPos = 15 - collisionMasks[cPath, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                        case (FlipFlags)2:
                            {
                                c = 15 - (YPos % TILE_SIZE);
                                if ((XPos % TILE_SIZE) <= collisionMasks[cPath, tileIndex].leftWall[c])
                                {
                                    break;
                                }
                                XPos = collisionMasks[cPath, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                        case (FlipFlags)3:
                            {
                                c = 15 - (YPos % TILE_SIZE);
                                if ((XPos % TILE_SIZE) <= 15 - collisionMasks[cPath, tileIndex].rightWall[c])
                                {
                                    break;
                                }
                                XPos = 15 - collisionMasks[cPath, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                    }
                }
                if (scriptEng.checkResult != 0)
                {
                    entity->XPos = WholeToFixedPoint(XPos - xOffset);
                }
            }
        }
    }

    public static void ObjectRoofCollision(int xOffset, int yOffset, int cPath)
    {
        int c;
        scriptEng.checkResult = 0;
        fixed (Entity* entity = &objectEntityList[objectLoop])
        {
            int XPos = FixedPointToWhole(entity->XPos) + xOffset;
            int YPos = FixedPointToWhole(entity->YPos) + yOffset;
            if (XPos > 0 && XPos < stageLayouts[0].xsize * CHUNK_SIZE && YPos > 0 && YPos < stageLayouts[0].ysize * CHUNK_SIZE)
            {
                int chunkX = XPos / CHUNK_SIZE;
                int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                int chunkY = YPos / CHUNK_SIZE;
                int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                int chunk = stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6;
                chunk = chunk + tileX + (tileY << 3);
                int tileIndex = tiles128x128.tileIndex[chunk];
                if (tiles128x128.collisionFlags[cPath, chunk] != CollisionSolidity.SOLID_TOP && tiles128x128.collisionFlags[cPath, chunk] < CollisionSolidity.SOLID_NONE)
                {
                    switch (tiles128x128.direction[chunk])
                    {
                        case 0:
                            {
                                c = (XPos % TILE_SIZE);
                                if ((YPos % TILE_SIZE) >= collisionMasks[cPath, tileIndex].roof[c])
                                {
                                    break;
                                }
                                YPos = collisionMasks[cPath, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                        case (FlipFlags)1:
                            {
                                c = 15 - (XPos % TILE_SIZE);
                                if ((YPos % TILE_SIZE) >= collisionMasks[cPath, tileIndex].roof[c])
                                {
                                    break;
                                }
                                YPos = collisionMasks[cPath, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                        case (FlipFlags)2:
                            {
                                c = (XPos % TILE_SIZE);
                                if ((YPos % TILE_SIZE) >= 15 - collisionMasks[cPath, tileIndex].floor[c])
                                {
                                    break;
                                }
                                YPos = 15 - collisionMasks[cPath, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                        case (FlipFlags)3:
                            {
                                c = 15 - (XPos % TILE_SIZE);
                                if ((YPos % TILE_SIZE) >= 15 - collisionMasks[cPath, tileIndex].floor[c])
                                {
                                    break;
                                }
                                YPos = 15 - collisionMasks[cPath, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                    }
                }
                if (scriptEng.checkResult != 0)
                {
                    entity->YPos = WholeToFixedPoint(YPos - yOffset);
                }
            }
        }
    }

    public static void ObjectRWallCollision(int xOffset, int yOffset, int cPath)
    {
        int c;
        scriptEng.checkResult = 0;
        fixed (Entity* entity = &objectEntityList[objectLoop])
        {
            int XPos = FixedPointToWhole(entity->XPos) + xOffset;
            int YPos = FixedPointToWhole(entity->YPos) + yOffset;
            if (XPos > 0 && XPos < stageLayouts[0].xsize * CHUNK_SIZE && YPos > 0 && YPos < stageLayouts[0].ysize * CHUNK_SIZE)
            {
                int chunkX = XPos / CHUNK_SIZE;
                int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                int chunkY = YPos / CHUNK_SIZE;
                int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                int chunk = stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6;
                chunk = chunk + tileX + (tileY << 3);
                int tileIndex = tiles128x128.tileIndex[chunk];
                if (tiles128x128.collisionFlags[cPath, chunk] != CollisionSolidity.SOLID_TOP && tiles128x128.collisionFlags[cPath, chunk] < CollisionSolidity.SOLID_NONE)
                {
                    switch (tiles128x128.direction[chunk])
                    {
                        case 0:
                            {
                                c = (YPos % TILE_SIZE);
                                if ((XPos % TILE_SIZE) >= collisionMasks[cPath, tileIndex].rightWall[c])
                                {
                                    break;
                                }
                                XPos = collisionMasks[cPath, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                        case (FlipFlags)1:
                            {
                                c = (YPos % TILE_SIZE);
                                if ((XPos % TILE_SIZE) >= 15 - collisionMasks[cPath, tileIndex].leftWall[c])
                                {
                                    break;
                                }
                                XPos = 15 - collisionMasks[cPath, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                        case (FlipFlags)2:
                            {
                                c = 15 - (YPos % TILE_SIZE);
                                if ((XPos % TILE_SIZE) >= collisionMasks[cPath, tileIndex].rightWall[c])
                                {
                                    break;
                                }
                                XPos = collisionMasks[cPath, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                        case (FlipFlags)3:
                            {
                                c = 15 - (YPos % TILE_SIZE);
                                if ((XPos % TILE_SIZE) >= 15 - collisionMasks[cPath, tileIndex].leftWall[c])
                                {
                                    break;
                                }
                                XPos = 15 - collisionMasks[cPath, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                scriptEng.checkResult = 1;
                                break;
                            }
                    }
                }
                if (scriptEng.checkResult != 0)
                {
                    entity->XPos = WholeToFixedPoint(XPos - xOffset);
                }
            }
        }
    }

    public static void ObjectFloorGrip(int xOffset, int yOffset, int cPath)
    {
        int c;
        scriptEng.checkResult = 0;
        fixed (Entity* entity = &objectEntityList[objectLoop])
        {
            int XPos = FixedPointToWhole(entity->XPos) + xOffset;
            int YPos = FixedPointToWhole(entity->YPos) + yOffset;
            int chunkX = YPos;
            YPos -= TILE_SIZE;
            for (int i = 3; i > 0; i--)
            {
                if (XPos > 0 && XPos < stageLayouts[0].xsize * CHUNK_SIZE && YPos > 0 && YPos < stageLayouts[0].ysize * CHUNK_SIZE && scriptEng.checkResult == 0)
                {
                    int chunkXInner = XPos / CHUNK_SIZE;
                    int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                    int chunkY = YPos / CHUNK_SIZE;
                    int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                    int chunk = (stageLayouts[0].tiles[chunkXInner + (chunkY << 8)] << 6) + tileX + (tileY << 3);
                    int tileIndex = tiles128x128.tileIndex[chunk];
                    if (tiles128x128.collisionFlags[cPath, chunk] != CollisionSolidity.SOLID_LRB && tiles128x128.collisionFlags[cPath, chunk] != CollisionSolidity.SOLID_NONE)
                    {
                        switch (tiles128x128.direction[chunk])
                        {
                            case 0:
                                {
                                    c = (XPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].floor[c] >= 64)
                                    {
                                        break;
                                    }
                                    entity->YPos = collisionMasks[cPath, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                            case (FlipFlags)1:
                                {
                                    c = 15 - (XPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].floor[c] >= 64)
                                    {
                                        break;
                                    }
                                    entity->YPos = collisionMasks[cPath, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                            case (FlipFlags)2:
                                {
                                    c = (XPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].roof[c] <= -64)
                                    {
                                        break;
                                    }
                                    entity->YPos = 15 - collisionMasks[cPath, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                            case (FlipFlags)3:
                                {
                                    c = 15 - (XPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].roof[c] <= -64)
                                    {
                                        break;
                                    }
                                    entity->YPos = 15 - collisionMasks[cPath, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                        }
                    }
                }
                YPos += TILE_SIZE;
            }
            if (scriptEng.checkResult != 0)
            {
                if (System.Math.Abs(entity->YPos - chunkX) < TILE_SIZE)
                {
                    entity->YPos = WholeToFixedPoint(entity->YPos - yOffset);
                    return;
                }
                entity->YPos = WholeToFixedPoint(chunkX - yOffset);
                scriptEng.checkResult = 0;
            }
        }
    }

    public static void ObjectLWallGrip(int xOffset, int yOffset, int cPath)
    {
        int c;
        scriptEng.checkResult = 0;
        fixed (Entity* entity = &objectEntityList[objectLoop])
        {
            int XPos = FixedPointToWhole(entity->XPos) + xOffset;
            int YPos = FixedPointToWhole(entity->YPos) + yOffset;
            int startX = XPos;
            XPos -= TILE_SIZE;
            for (int i = 3; i > 0; i--)
            {
                if (XPos > 0 && XPos < stageLayouts[0].xsize * CHUNK_SIZE && YPos > 0 && YPos < stageLayouts[0].ysize * CHUNK_SIZE && scriptEng.checkResult == 0)
                {
                    int chunkX = XPos / CHUNK_SIZE;
                    int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                    int chunkY = YPos / CHUNK_SIZE;
                    int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                    int chunk = (stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6) + tileX + (tileY << 3);
                    int tileIndex = tiles128x128.tileIndex[chunk];
                    if (tiles128x128.collisionFlags[cPath, chunk] < CollisionSolidity.SOLID_NONE)
                    {
                        switch (tiles128x128.direction[chunk])
                        {
                            case 0:
                                {
                                    c = (YPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].leftWall[c] >= 64)
                                    {
                                        break;
                                    }
                                    entity->XPos = collisionMasks[cPath, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                            case (FlipFlags)1:
                                {
                                    c = (YPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].rightWall[c] <= -64)
                                    {
                                        break;
                                    }
                                    entity->XPos = 15 - collisionMasks[cPath, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                            case (FlipFlags)2:
                                {
                                    c = 15 - (YPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].leftWall[c] >= 64)
                                    {
                                        break;
                                    }
                                    entity->XPos = collisionMasks[cPath, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                            case (FlipFlags)3:
                                {
                                    c = 15 - (YPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].rightWall[c] <= -64)
                                    {
                                        break;
                                    }
                                    entity->XPos = 15 - collisionMasks[cPath, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                        }
                    }
                }
                XPos += TILE_SIZE;
            }
            if (scriptEng.checkResult != 0)
            {
                if (System.Math.Abs(entity->XPos - startX) < TILE_SIZE)
                {
                    entity->XPos = WholeToFixedPoint(entity->XPos - xOffset);
                    return;
                }
                entity->XPos = WholeToFixedPoint(startX - xOffset);
                scriptEng.checkResult = 0;
            }
        }
    }

    public static void ObjectRoofGrip(int xOffset, int yOffset, int cPath)
    {
        int c;
        scriptEng.checkResult = 0;
        fixed (Entity* entity = &objectEntityList[objectLoop])
        {
            int XPos = FixedPointToWhole(entity->XPos) + xOffset;
            int YPos = FixedPointToWhole(entity->YPos) + yOffset;
            int startY = YPos;
            YPos += TILE_SIZE;
            for (int i = 3; i > 0; i--)
            {
                if (XPos > 0 && XPos < stageLayouts[0].xsize * CHUNK_SIZE && YPos > 0 && YPos < stageLayouts[0].ysize * CHUNK_SIZE && scriptEng.checkResult == 0)
                {
                    int chunkX = XPos / CHUNK_SIZE;
                    int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                    int chunkY = YPos / CHUNK_SIZE;
                    int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                    int chunk = (stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6) + tileX + (tileY << 3);
                    int tileIndex = tiles128x128.tileIndex[chunk];
                    if (tiles128x128.collisionFlags[cPath, chunk] < CollisionSolidity.SOLID_NONE)
                    {
                        switch (tiles128x128.direction[chunk])
                        {
                            case 0:
                                {
                                    c = (XPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].roof[c] <= -64)
                                    {
                                        break;
                                    }
                                    entity->YPos = collisionMasks[cPath, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                            case (FlipFlags)1:
                                {
                                    c = 15 - (XPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].roof[c] <= -64)
                                    {
                                        break;
                                    }
                                    entity->YPos = collisionMasks[cPath, tileIndex].roof[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                            case (FlipFlags)2:
                                {
                                    c = (XPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].floor[c] >= 64)
                                    {
                                        break;
                                    }
                                    entity->YPos = 15 - collisionMasks[cPath, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                            case (FlipFlags)3:
                                {
                                    c = 15 - (XPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].floor[c] >= 64)
                                    {
                                        break;
                                    }
                                    entity->YPos = 15 - collisionMasks[cPath, tileIndex].floor[c] + (chunkY * CHUNK_SIZE) + (tileY * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                        }
                    }
                }
                YPos -= TILE_SIZE;
            }
            if (scriptEng.checkResult != 0)
            {
                if (System.Math.Abs(entity->YPos - startY) < TILE_SIZE)
                {
                    entity->YPos = WholeToFixedPoint(entity->YPos - yOffset);
                    return;
                }
                entity->YPos = WholeToFixedPoint(startY - yOffset);
                scriptEng.checkResult = 0;
            }
        }
    }

    public static void ObjectRWallGrip(int xOffset, int yOffset, int cPath)
    {
        int c;
        scriptEng.checkResult = 0;
        fixed (Entity* entity = &objectEntityList[objectLoop])
        {
            int XPos = FixedPointToWhole(entity->XPos) + xOffset;
            int YPos = FixedPointToWhole(entity->YPos) + yOffset;
            int startX = XPos;
            XPos += TILE_SIZE;
            for (int i = 3; i > 0; i--)
            {
                if (XPos > 0 && XPos < stageLayouts[0].xsize * CHUNK_SIZE && YPos > 0 && YPos < stageLayouts[0].ysize * CHUNK_SIZE && scriptEng.checkResult == 0)
                {
                    int chunkX = XPos / CHUNK_SIZE;
                    int tileX = (XPos % CHUNK_SIZE) / TILE_SIZE;
                    int chunkY = YPos / CHUNK_SIZE;
                    int tileY = (YPos % CHUNK_SIZE) / TILE_SIZE;
                    int chunk = (stageLayouts[0].tiles[chunkX + (chunkY << 8)] << 6) + tileX + (tileY << 3);
                    int tileIndex = tiles128x128.tileIndex[chunk];
                    if (tiles128x128.collisionFlags[cPath, chunk] < CollisionSolidity.SOLID_NONE)
                    {
                        switch (tiles128x128.direction[chunk])
                        {
                            case 0:
                                {
                                    c = (YPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].rightWall[c] <= -64)
                                    {
                                        break;
                                    }
                                    entity->XPos = collisionMasks[cPath, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                            case (FlipFlags)1:
                                {
                                    c = (YPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].leftWall[c] >= 64)
                                    {
                                        break;
                                    }
                                    entity->XPos = 15 - collisionMasks[cPath, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                            case (FlipFlags)2:
                                {
                                    c = 15 - (YPos % TILE_SIZE);
                                    if (collisionMasks[cPath, tileIndex].rightWall[c] <= -64)
                                    {
                                        break;
                                    }
                                    entity->XPos = collisionMasks[cPath, tileIndex].rightWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                            case (FlipFlags)3:
                                {
                                    c = 15 - (YPos & 15);
                                    if (collisionMasks[cPath, tileIndex].leftWall[c] >= 64)
                                    {
                                        break;
                                    }
                                    entity->XPos = 15 - collisionMasks[cPath, tileIndex].leftWall[c] + (chunkX * CHUNK_SIZE) + (tileX * TILE_SIZE);
                                    scriptEng.checkResult = 1;
                                    break;
                                }
                        }
                    }
                }
                XPos -= TILE_SIZE;
            }
            if (scriptEng.checkResult != 0)
            {
                if (System.Math.Abs(entity->XPos - startX) < TILE_SIZE)
                {
                    entity->XPos = WholeToFixedPoint(entity->XPos - xOffset);
                    return;
                }
                entity->XPos = WholeToFixedPoint(startX - xOffset);
                scriptEng.checkResult = 0;
            }
        }
    }

    public static void TouchCollision(int left, int top, int right, int bottom)
    {
        fixed (Player* player = &playerList[activePlayer])
        {
            Hitbox playerHitbox = GetPlayerHitbox(*player);
            collisionLeft = FixedPointToWhole(player->XPos);
            collisionTop = FixedPointToWhole(player->YPos);
            collisionRight = collisionLeft;
            collisionBottom = collisionTop;
            collisionLeft += playerHitbox.left[0];
            collisionTop += playerHitbox.top[0];
            collisionRight += playerHitbox.right[0];
            collisionBottom += playerHitbox.bottom[0];
            scriptEng.checkResult = (collisionRight > left && collisionLeft < right && collisionBottom > top && collisionTop < bottom) ? 1 : 0;

            if (showHitboxes)
            {
                fixed (Entity* entity = &objectEntityList[objectLoop])
                {
                    left -= FixedPointToWhole(entity->XPos);
                    top -= FixedPointToWhole(entity->YPos);
                    right -= FixedPointToWhole(entity->XPos);
                    bottom -= FixedPointToWhole(entity->YPos);

                    int thisHitboxID = AddDebugHitbox((byte)DebugHitboxTypes.H_TYPE_TOUCH, entity, left, top, right, bottom);
                    if (thisHitboxID >= 0 && scriptEng.checkResult != 0)
                        debugHitboxList[thisHitboxID].collision |= 1;

                    int otherHitboxID =
                        AddDebugHitbox((byte)DebugHitboxTypes.H_TYPE_TOUCH, null, playerHitbox.left[0], playerHitbox.top[0], playerHitbox.right[0], playerHitbox.bottom[0]);
                    if (otherHitboxID >= 0)
                    {
                        debugHitboxList[otherHitboxID].XPos = player->XPos;
                        debugHitboxList[otherHitboxID].YPos = player->YPos;

                        if (scriptEng.checkResult != 0)
                            debugHitboxList[otherHitboxID].collision |= 1;
                    }
                }
            }
        }
    }

    public static void BoxCollision(int left, int top, int right, int bottom)
    {
        fixed (Player* player = &playerList[activePlayer])
        {
            Hitbox playerHitbox = GetPlayerHitbox(*player);

            collisionLeft = playerHitbox.left[0];
            collisionTop = playerHitbox.top[0];
            collisionRight = playerHitbox.right[0];
            collisionBottom = playerHitbox.bottom[0];
            scriptEng.checkResult = 0;

            int spd = 0;
            switch ((CollisionModes)player->collisionMode)
            {
                case CollisionModes.CMODE_FLOOR:
                case CollisionModes.CMODE_ROOF:
                    if (player->XVelocity != 0)
                        spd = System.Math.Abs(player->XVelocity);
                    else
                        spd = System.Math.Abs(player->speed);
                    break;
                case CollisionModes.CMODE_LWALL:
                case CollisionModes.CMODE_RWALL: spd = System.Math.Abs(player->XVelocity); break;
                default: break;
            }
            if (spd <= System.Math.Abs(player->YVelocity))
            {
                sensors[0].collided = false;
                sensors[1].collided = false;
                sensors[2].collided = false;
                sensors[0].XPos = player->XPos + WholeToFixedPoint((collisionLeft + 2));
                sensors[1].XPos = player->XPos;
                sensors[2].XPos = player->XPos + WholeToFixedPoint((collisionRight - 2));
                sensors[0].YPos = player->YPos + WholeToFixedPoint(collisionBottom);
                sensors[1].YPos = sensors[0].YPos;
                sensors[2].YPos = sensors[0].YPos;
                if (player->YVelocity > -1)
                {
                    for (int i = 0; i < 3; ++i)
                    {
                        if (sensors[i].XPos > left && sensors[i].XPos < right && sensors[i].YPos >= top && player->YPos - player->YVelocity < top)
                        {
                            sensors[i].collided = true;
                            player->flailing[i] = 1;
                        }
                    }
                }
                if (sensors[2].collided || sensors[1].collided || sensors[0].collided)
                {
                    if (player->gravity == 0 && (player->collisionMode == (byte)CollisionModes.CMODE_RWALL || player->collisionMode == (byte)CollisionModes.CMODE_LWALL))
                    {
                        player->XVelocity = 0;
                        player->speed = 0;
                    }
                    player->YPos = top - WholeToFixedPoint(collisionBottom);
                    player->gravity = 0;
                    player->YVelocity = 0;
                    player->angle = 0;
                    objectEntityList[player->boundEntity].rotation = 0;
                    player->controlLock = 0;
                    scriptEng.checkResult = 1;
                }
                else
                {
                    sensors[0].collided = false;
                    sensors[1].collided = false;
                    sensors[0].XPos = player->XPos + WholeToFixedPoint((collisionLeft + 2));
                    sensors[1].XPos = player->XPos + WholeToFixedPoint((collisionRight - 2));
                    sensors[0].YPos = player->YPos + WholeToFixedPoint(collisionTop);
                    sensors[1].YPos = sensors[0].YPos;
                    for (int i = 0; i < 2; ++i)
                    {
                        if (sensors[i].XPos > left && sensors[i].XPos < right && sensors[i].YPos <= bottom && player->YPos - player->YVelocity > bottom)
                        {
                            sensors[i].collided = true;
                        }
                    }
                    if (sensors[1].collided || sensors[0].collided)
                    {
                        if (player->gravity == 1)
                        {
                            player->YPos = bottom - WholeToFixedPoint(collisionTop);
                        }
                        if (player->YVelocity < 1)
                            player->YVelocity = 0;
                        scriptEng.checkResult = 4;
                    }
                    else
                    {
                        sensors[0].collided = false;
                        sensors[1].collided = false;
                        sensors[0].XPos = player->XPos + WholeToFixedPoint(collisionRight);
                        sensors[1].XPos = sensors[0].XPos;
                        sensors[0].YPos = player->YPos - WholeToFixedPoint(2);
                        sensors[1].YPos = player->YPos + WholeToFixedPoint(8);
                        for (int i = 0; i < 2; ++i)
                        {
                            if (sensors[i].XPos >= left && player->XPos - player->XVelocity < left && sensors[1].YPos > top && sensors[0].YPos < bottom)
                            {
                                sensors[i].collided = true;
                            }
                        }
                        if (sensors[1].collided || sensors[0].collided)
                        {
                            player->XPos = left - WholeToFixedPoint(collisionRight);
                            if (player->XVelocity > 0)
                            {
                                if (objectEntityList[player->boundEntity].direction == 0)
                                    player->pushing = 2;
                                player->XVelocity = 0;
                                player->speed = 0;
                            }
                            scriptEng.checkResult = 2;
                        }
                        else
                        {
                            sensors[0].collided = false;
                            sensors[1].collided = false;
                            sensors[0].XPos = player->XPos + WholeToFixedPoint(collisionLeft);
                            sensors[1].XPos = sensors[0].XPos;
                            sensors[0].YPos = player->YPos - WholeToFixedPoint(2);
                            sensors[1].YPos = player->YPos + WholeToFixedPoint(8);
                            for (int i = 0; i < 2; ++i)
                            {
                                if (sensors[i].XPos <= right && player->XPos - player->XVelocity > right && sensors[1].YPos > top
                                    && sensors[0].YPos < bottom)
                                {
                                    sensors[i].collided = true;
                                }
                            }

                            if (sensors[1].collided || (sensors[0].collided))
                            {
                                player->XPos = right - WholeToFixedPoint(collisionLeft);
                                if (player->XVelocity < 0)
                                {
                                    if (objectEntityList[player->boundEntity].direction == (byte)FlipFlags.FLIP_X)
                                        player->pushing = 2;
                                    player->XVelocity = 0;
                                    player->speed = 0;
                                }
                                scriptEng.checkResult = 3;
                            }
                        }
                    }
                }
            }
            else
            {
                sensors[0].collided = false;
                sensors[1].collided = false;
                sensors[0].XPos = player->XPos + WholeToFixedPoint(collisionRight);
                sensors[1].XPos = sensors[0].XPos;
                sensors[0].YPos = player->YPos - WholeToFixedPoint(2);
                sensors[1].YPos = player->YPos + WholeToFixedPoint(8);
                for (int i = 0; i < 2; ++i)
                {
                    if (sensors[i].XPos >= left && player->XPos - player->XVelocity < left && sensors[1].YPos > top && sensors[0].YPos < bottom)
                    {
                        sensors[i].collided = true;
                    }
                }
                if (sensors[1].collided || sensors[0].collided)
                {
                    player->XPos = left - WholeToFixedPoint(collisionRight);
                    if (player->XVelocity > 0)
                    {
                        if (objectEntityList[player->boundEntity].direction == 0)
                            player->pushing = 2;
                        player->XVelocity = 0;
                        player->speed = 0;
                    }
                    scriptEng.checkResult = 2;
                }
                else
                {
                    sensors[0].collided = false;
                    sensors[1].collided = false;
                    sensors[0].XPos = player->XPos + WholeToFixedPoint(collisionLeft);
                    sensors[1].XPos = sensors[0].XPos;
                    sensors[0].YPos = player->YPos - WholeToFixedPoint(2);
                    sensors[1].YPos = player->YPos + WholeToFixedPoint(8);
                    for (int i = 0; i < 2; ++i)
                    {
                        if (sensors[i].XPos <= right && player->XPos - player->XVelocity > right && sensors[1].YPos > top && sensors[0].YPos < bottom)
                        {
                            sensors[i].collided = true;
                        }
                    }
                    if (sensors[1].collided || sensors[0].collided)
                    {
                        player->XPos = right - WholeToFixedPoint(collisionLeft);
                        if (player->XVelocity < 0)
                        {
                            if (objectEntityList[player->boundEntity].direction == (byte)FlipFlags.FLIP_X)
                            {
                                player->pushing = 2;
                            }
                            player->XVelocity = 0;
                            player->speed = 0;
                        }
                        scriptEng.checkResult = 3;
                    }
                    else
                    {
                        sensors[0].collided = false;
                        sensors[1].collided = false;
                        sensors[2].collided = false;
                        sensors[0].XPos = player->XPos + WholeToFixedPoint((collisionLeft + 2));
                        sensors[1].XPos = player->XPos;
                        sensors[2].XPos = player->XPos + WholeToFixedPoint((collisionRight - 2));
                        sensors[0].YPos = player->YPos + WholeToFixedPoint(collisionBottom);
                        sensors[1].YPos = sensors[0].YPos;
                        sensors[2].YPos = sensors[0].YPos;
                        if (player->YVelocity > -1)
                        {
                            for (int i = 0; i < 3; ++i)
                            {
                                if (sensors[i].XPos > left && sensors[i].XPos < right && sensors[i].YPos >= top && player->YPos - player->YVelocity < top)
                                {
                                    sensors[i].collided = true;
                                    player->flailing[i] = 1;
                                }
                            }
                        }
                        if (sensors[2].collided || sensors[1].collided || sensors[0].collided)
                        {
                            if (player->gravity == 0 && (player->collisionMode == (byte)CollisionModes.CMODE_RWALL || player->collisionMode == (byte)CollisionModes.CMODE_LWALL))
                            {
                                player->XVelocity = 0;
                                player->speed = 0;
                            }
                            player->YPos = top - WholeToFixedPoint(collisionBottom);
                            player->gravity = 0;
                            player->YVelocity = 0;
                            player->angle = 0;
                            objectEntityList[player->boundEntity].rotation = 0;
                            player->controlLock = 0;
                            scriptEng.checkResult = 1;
                        }
                        else
                        {
                            sensors[0].collided = false;
                            sensors[1].collided = false;
                            sensors[0].XPos = player->XPos + WholeToFixedPoint((collisionLeft + 2));
                            sensors[1].XPos = player->XPos + WholeToFixedPoint((collisionRight - 2));
                            sensors[0].YPos = player->YPos + WholeToFixedPoint(collisionTop);
                            sensors[1].YPos = sensors[0].YPos;
                            for (int i = 0; i < 2; ++i)
                            {
                                if (sensors[i].XPos > left && sensors[i].XPos < right && sensors[i].YPos <= bottom
                                    && player->YPos - player->YVelocity > bottom)
                                {
                                    sensors[i].collided = true;
                                }
                            }

                            if (sensors[1].collided || sensors[0].collided)
                            {
                                if (player->gravity == 1)
                                {
                                    player->YPos = bottom - WholeToFixedPoint(collisionTop);
                                }
                                if (player->YVelocity < 1)
                                    player->YVelocity = 0;
                                scriptEng.checkResult = 4;
                            }
                        }
                    }
                }
            }

            int thisHitboxID = 0;
            if (showHitboxes)
            {
                fixed (Entity* entity = &objectEntityList[objectLoop])
                {
                    left -= entity->XPos;
                    top -= entity->YPos;
                    right -= entity->XPos;
                    bottom -= entity->YPos;

                    thisHitboxID = AddDebugHitbox(
                        (byte)DebugHitboxTypes.H_TYPE_BOX,
                        entity,
                        FixedPointToWhole(left),
                        FixedPointToWhole(top),
                        FixedPointToWhole(right),
                        FixedPointToWhole(bottom)
                    );
                    if (thisHitboxID >= 0 && scriptEng.checkResult != 0)
                        debugHitboxList[thisHitboxID].collision |= (byte)(1 << (scriptEng.checkResult - 1));

                    int otherHitboxID =
                        AddDebugHitbox((byte)DebugHitboxTypes.H_TYPE_BOX, null, playerHitbox.left[0], playerHitbox.top[0], playerHitbox.right[0], playerHitbox.bottom[0]);
                    if (otherHitboxID >= 0)
                    {
                        debugHitboxList[otherHitboxID].XPos = player->XPos;
                        debugHitboxList[otherHitboxID].YPos = player->YPos;

                        if (scriptEng.checkResult != 0)
                            debugHitboxList[otherHitboxID].collision |= (byte)(1 << (4 - scriptEng.checkResult));
                    }
                }
            }
        }
    }

    public static void BoxCollision2(int left, int top, int right, int bottom)
    {
        fixed (Player* player = &playerList[activePlayer])
        {
            Hitbox playerHitbox = GetPlayerHitbox(*player);

            collisionLeft = playerHitbox.left[0];
            collisionTop = playerHitbox.top[0];
            collisionRight = playerHitbox.right[0];
            collisionBottom = playerHitbox.bottom[0];
            scriptEng.checkResult = 0;
            int spd = 0;
            switch ((CollisionModes)player->collisionMode)
            {
                case CollisionModes.CMODE_FLOOR:
                case CollisionModes.CMODE_ROOF:
                    if (player->XVelocity != 0)
                        spd = System.Math.Abs(player->XVelocity);
                    else
                        spd = System.Math.Abs(player->speed);
                    break;
                case CollisionModes.CMODE_LWALL:
                case CollisionModes.CMODE_RWALL: spd = System.Math.Abs(player->XVelocity); break;
                default: break;
            }
            if (spd <= System.Math.Abs(player->YVelocity))
            {
                sensors[0].collided = false;
                sensors[1].collided = false;
                sensors[2].collided = false;
                sensors[0].XPos = player->XPos + WholeToFixedPoint((collisionLeft + 2));
                sensors[1].XPos = player->XPos;
                sensors[2].XPos = player->XPos + WholeToFixedPoint((collisionRight - 2));
                sensors[0].YPos = player->YPos + WholeToFixedPoint(collisionBottom);
                sensors[1].YPos = sensors[0].YPos;
                sensors[2].YPos = sensors[0].YPos;
                if (player->YVelocity > -1)
                {
                    for (int i = 0; i < 3; ++i)
                    {
                        if (sensors[i].XPos > left && sensors[i].XPos < right && sensors[i].YPos >= top && player->YPos - player->YVelocity < top)
                        {
                            sensors[i].collided = true;
                            player->flailing[i] = 1;
                        }
                    }
                }
                if (sensors[2].collided || sensors[1].collided || sensors[0].collided)
                {
                    if (player->gravity == 0 && (player->collisionMode == (byte)CollisionModes.CMODE_RWALL || player->collisionMode == (byte)CollisionModes.CMODE_LWALL))
                    {
                        player->XVelocity = 0;
                        player->speed = 0;
                    }
                    player->YPos = top - WholeToFixedPoint(collisionBottom);
                    player->gravity = 0;
                    player->YVelocity = 0;
                    player->angle = 0;
                    objectEntityList[player->boundEntity].rotation = 0;
                    player->controlLock = 0;
                    scriptEng.checkResult = 1;
                }
                else
                {
                    sensors[0].collided = false;
                    sensors[1].collided = false;
                    sensors[0].XPos = player->XPos + WholeToFixedPoint((collisionLeft + 2));
                    sensors[1].XPos = player->XPos + WholeToFixedPoint((collisionRight - 2));
                    sensors[0].YPos = player->YPos + WholeToFixedPoint(collisionTop);
                    sensors[1].YPos = player->YPos + WholeToFixedPoint(collisionTop);

                    for (int i = 0; i < 2; ++i)
                    {
                        if (left < sensors[i].XPos && right > sensors[i].XPos && bottom >= sensors[i].YPos && bottom < player->YPos - player->YVelocity)
                        {
                            sensors[i].collided = true;
                        }
                    }

                    if (sensors[1].collided || sensors[0].collided)
                    {
                        if (player->gravity == 1)
                            player->YPos = bottom - WholeToFixedPoint(collisionTop);

                        if (player->YVelocity < 1)
                            player->YVelocity = 0;
                        scriptEng.checkResult = 4;
                    }
                    else
                    {
                        sensors[0].collided = false;
                        sensors[1].collided = false;
                        sensors[0].XPos = player->XPos + WholeToFixedPoint(collisionRight);
                        sensors[1].XPos = player->XPos + WholeToFixedPoint(collisionRight);
                        sensors[0].YPos = player->YPos + WholeToFixedPoint((collisionBottom - 2));
                        sensors[1].YPos = player->YPos + WholeToFixedPoint((collisionTop + 2));
                        for (int i = 0; i < 2; ++i)
                        {
                            if (sensors[i].XPos >= left && player->XPos - player->XVelocity < left && sensors[0].YPos > top && sensors[1].YPos < bottom)
                            {
                                sensors[i].collided = true;
                            }
                        }

                        if (sensors[1].collided || sensors[0].collided)
                        {
                            player->XPos = left - WholeToFixedPoint(collisionRight);
                            if (player->XVelocity > 0)
                            {
                                if (objectEntityList[player->boundEntity].direction == (byte)FlipFlags.FLIP_NONE)
                                    player->pushing = 2;
                                player->XVelocity = 0;
                                player->speed = 0;
                            }
                            scriptEng.checkResult = 2;
                        }
                        else
                        {
                            sensors[0].collided = false;
                            sensors[1].collided = false;
                            sensors[0].XPos = sensors[0].XPos;
                            sensors[1].XPos = player->XPos + WholeToFixedPoint(collisionLeft);
                            sensors[0].YPos = player->YPos + WholeToFixedPoint((collisionBottom - 2));
                            sensors[1].YPos = player->YPos + WholeToFixedPoint(collisionTop + 2);
                            for (int i = 0; i < 2; ++i)
                            {
                                if (sensors[i].XPos <= right && player->XPos - player->XVelocity > right && sensors[0].YPos > top
                                    && sensors[1].YPos < bottom)
                                {
                                    sensors[i].collided = true;
                                }
                            }

                            if (sensors[1].collided || (sensors[0].collided))
                            {
                                player->XPos = right - WholeToFixedPoint(collisionLeft);
                                if (player->XVelocity < 0)
                                {
                                    if (objectEntityList[player->boundEntity].direction == (byte)FlipFlags.FLIP_X)
                                        player->pushing = 2;
                                    player->XVelocity = 0;
                                    player->speed = 0;
                                }
                                scriptEng.checkResult = 3;
                            }
                        }
                    }
                }
            }
            else
            {
                sensors[0].collided = false;
                sensors[1].collided = false;
                sensors[0].XPos = player->XPos + WholeToFixedPoint(collisionRight);
                sensors[1].XPos = player->XPos + WholeToFixedPoint(collisionRight);
                sensors[0].YPos = player->YPos + WholeToFixedPoint(collisionBottom - 2);
                sensors[1].YPos = player->YPos + WholeToFixedPoint(collisionTop + 2);
                for (int i = 0; i < 2; ++i)
                {
                    if (sensors[i].XPos >= left && player->XPos - player->XVelocity < left && sensors[0].YPos > top && sensors[1].YPos < bottom)
                    {
                        sensors[i].collided = true;
                    }
                }

                if (sensors[1].collided || sensors[0].collided)
                {
                    player->XPos = left - WholeToFixedPoint(collisionRight);
                    if (player->XVelocity > 0)
                    {
                        if (objectEntityList[player->boundEntity].direction == 0)
                            player->pushing = 2;
                        player->XVelocity = 0;
                        player->speed = 0;
                    }
                    scriptEng.checkResult = 2;
                }
                else
                {
                    sensors[0].collided = false;
                    sensors[1].collided = false;
                    sensors[0].XPos = sensors[0].XPos;
                    sensors[1].XPos = player->XPos + WholeToFixedPoint(collisionLeft);
                    sensors[0].YPos = player->YPos + WholeToFixedPoint(collisionBottom - 2);
                    sensors[1].YPos = player->YPos + WholeToFixedPoint(collisionTop + 2);
                    for (int i = 0; i < 2; ++i)
                    {
                        if (sensors[i].XPos <= right && player->XPos - player->XVelocity > right && sensors[0].YPos > top && sensors[1].YPos < bottom)
                        {
                            sensors[i].collided = true;
                        }
                    }

                    if (sensors[1].collided || sensors[0].collided)
                    {
                        player->XPos = right - WholeToFixedPoint(collisionLeft);
                        if (player->XVelocity < 0)
                        {
                            if (objectEntityList[player->boundEntity].direction == (byte)FlipFlags.FLIP_X)
                            {
                                player->pushing = 2;
                            }
                            player->XVelocity = 0;
                            player->speed = 0;
                        }
                        scriptEng.checkResult = 3;
                    }
                    else
                    {
                        sensors[0].collided = false;
                        sensors[1].collided = false;
                        sensors[2].collided = false;
                        sensors[0].XPos = player->XPos + WholeToFixedPoint(collisionLeft + 2);
                        sensors[1].XPos = player->XPos;
                        sensors[2].XPos = player->XPos + WholeToFixedPoint(collisionRight - 2);
                        sensors[0].YPos = player->YPos + WholeToFixedPoint(collisionBottom);
                        sensors[1].YPos = sensors[0].YPos;
                        sensors[2].YPos = sensors[0].YPos;
                        if (player->YVelocity > -1)
                        {
                            for (int i = 0; i < 3; ++i)
                            {
                                if (sensors[i].XPos > left && sensors[i].XPos < right && sensors[i].YPos >= top && player->YPos - player->YVelocity < top)
                                {
                                    sensors[i].collided = true;
                                    player->flailing[i] = 1;
                                }
                            }
                        }
                        if (sensors[2].collided || sensors[1].collided || sensors[0].collided)
                        {
                            if (player->gravity == 0 && (player->collisionMode == (byte)CollisionModes.CMODE_RWALL || player->collisionMode == (byte)CollisionModes.CMODE_LWALL))
                            {
                                player->XVelocity = 0;
                                player->speed = 0;
                            }
                            player->YPos = top - WholeToFixedPoint(collisionBottom);
                            player->gravity = 0;
                            player->YVelocity = 0;
                            player->angle = 0;
                            objectEntityList[player->boundEntity].rotation = 0;
                            player->controlLock = 0;
                            scriptEng.checkResult = 1;
                        }
                        else
                        {
                            sensors[0].collided = false;
                            sensors[1].collided = false;
                            sensors[0].XPos = player->XPos + WholeToFixedPoint(collisionLeft + 2);
                            sensors[1].XPos = player->XPos + WholeToFixedPoint(collisionRight - 2);
                            sensors[0].YPos = player->YPos + WholeToFixedPoint(collisionTop);
                            sensors[1].YPos = player->YPos + WholeToFixedPoint(collisionTop);

                            for (int i = 0; i < 2; ++i)
                            {
                                if (left < sensors[i].XPos && right > sensors[i].XPos && bottom >= sensors[i].YPos
                                    && bottom < player->YPos - player->YVelocity)
                                {
                                    sensors[i].collided = true;
                                }
                            }

                            if (sensors[1].collided || sensors[0].collided)
                            {
                                if (player->gravity == 1)
                                {
                                    player->YPos = bottom - WholeToFixedPoint(collisionTop);
                                }
                                if (player->YVelocity < 1)
                                    player->YVelocity = 0;
                                scriptEng.checkResult = 4;
                            }
                        }
                    }
                }
            }

            int thisHitboxID = 0;
            if (showHitboxes)
            {
                fixed (Entity* entity = &objectEntityList[objectLoop])
                {
                    left -= entity->XPos;
                    top -= entity->YPos;
                    right -= entity->XPos;
                    bottom -= entity->YPos;

                    thisHitboxID = AddDebugHitbox(
                        (byte)DebugHitboxTypes.H_TYPE_BOX,
                        entity,
                        FixedPointToWhole(left),
                        FixedPointToWhole(top),
                        FixedPointToWhole(right),
                        FixedPointToWhole(bottom)
                    );
                    if (thisHitboxID >= 0 && scriptEng.checkResult != 0)
                        debugHitboxList[thisHitboxID].collision |= (byte)(1 << (scriptEng.checkResult - 1));

                    int otherHitboxID =
                        AddDebugHitbox((byte)DebugHitboxTypes.H_TYPE_BOX, null, playerHitbox.left[0], playerHitbox.top[0], playerHitbox.right[0], playerHitbox.bottom[0]);
                    if (otherHitboxID >= 0)
                    {
                        debugHitboxList[otherHitboxID].XPos = player->XPos;
                        debugHitboxList[otherHitboxID].YPos = player->YPos;

                        if (scriptEng.checkResult != 0)
                            debugHitboxList[otherHitboxID].collision |= (byte)(1 << (4 - scriptEng.checkResult));
                    }
                }
            }
        }
    }

    public static int PlatformCollision(int left, int top, int right, int bottom)
    {
        fixed (Player* player = &playerList[activePlayer])
        {
            Hitbox playerHitbox = GetPlayerHitbox(*player);

            collisionLeft = playerHitbox.left[0];
            collisionTop = playerHitbox.top[0];
            collisionRight = playerHitbox.right[0];
            collisionBottom = playerHitbox.bottom[0];
            sensors[0].XPos = player->XPos + WholeToFixedPoint(collisionLeft + 1);
            sensors[0].YPos = player->YPos + WholeToFixedPoint(collisionBottom);
            sensors[0].collided = false;
            sensors[1].XPos = player->XPos;
            sensors[1].YPos = sensors[0].YPos;
            sensors[1].collided = false;
            sensors[2].XPos = player->XPos + WholeToFixedPoint(collisionRight);
            sensors[2].YPos = sensors[0].YPos;
            sensors[2].collided = false;
            var result = 0;
            for (int i = 0; i < 3; ++i)
            {
                if (sensors[i].XPos > left && sensors[i].XPos < right && sensors[i].YPos > top - 2 && sensors[i].YPos < bottom && player->YVelocity >= 0)
                {
                    sensors[i].collided = true;
                    player->flailing[i] = 1;
                }
            }

            if (sensors[0].collided || sensors[1].collided || sensors[2].collided)
            {
                if (player->gravity == 0 && (player->collisionMode == (byte)CollisionModes.CMODE_RWALL || player->collisionMode == (byte)CollisionModes.CMODE_LWALL))
                {
                    player->XVelocity = 0;
                    player->speed = 0;
                }
                player->YPos = top - WholeToFixedPoint(collisionBottom);
                player->gravity = 0;
                player->YVelocity = 0;
                player->angle = 0;
                objectEntityList[player->boundEntity].rotation = 0;
                player->controlLock = 0;
                result = 1;
            }

            int thisHitboxID = 0;
            if (showHitboxes)
            {
                fixed (Entity* entity = &objectEntityList[objectLoop])
                {
                    left -= entity->XPos;
                    top -= entity->YPos;
                    right -= entity->XPos;
                    bottom -= entity->YPos;

                    thisHitboxID = AddDebugHitbox(
                        (byte)DebugHitboxTypes.H_TYPE_PLAT,
                        entity,
                        FixedPointToWhole(left),
                        FixedPointToWhole(top),
                        FixedPointToWhole(right),
                        FixedPointToWhole(bottom)
                    );
                    if (thisHitboxID >= 0 && result != 0)
                        debugHitboxList[thisHitboxID].collision |= 1 << 0;

                    int otherHitboxID =
                        AddDebugHitbox((byte)DebugHitboxTypes.H_TYPE_PLAT, null, playerHitbox.left[0], playerHitbox.top[0], playerHitbox.right[0], playerHitbox.bottom[0]);
                    if (otherHitboxID >= 0)
                    {
                        debugHitboxList[otherHitboxID].XPos = player->XPos;
                        debugHitboxList[otherHitboxID].YPos = player->YPos;

                        if (result != 0)
                            debugHitboxList[otherHitboxID].collision |= 1 << 3;
                    }
                }
            }

            return result;
        }
    }
}