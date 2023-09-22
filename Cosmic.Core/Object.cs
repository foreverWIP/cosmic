global using static Cosmic.Core.Object;
using static Cosmic.Core.Drawing;

namespace Cosmic.Core;

public static class Object
{
    public const int ENTITY_COUNT = (0x4A0);
    public const int TEMPENTITY_START = (ENTITY_COUNT - 0x80);
    public const int OBJECT_COUNT = (0x100);

    public struct Entity
    {
        public int XPos;
        public int YPos;
        public readonly int[] values;
        public int scale;
        public int rotation;
        public int animationTimer;
        public int animationSpeed;
        public byte type;
        public byte propertyValue;
        public byte state;
        public byte priority;
        public byte drawOrder;
        public byte direction;
        public byte inkEffect;
        public byte alpha;
        public byte animation;
        public byte prevAnimation;
        public byte frame;

        public Entity()
        {
            values = new int[8];
        }
    }

    public const byte BlankObjectID = 0;

    public enum ObjectPriority
    {
        // The entity is active if the entity is on screen or within 128 pixels of the screen borders on any axis
        PRIORITY_BOUNDS,
        // The entity is always active, unless the stage state is PAUSED or FROZEN
        PRIORITY_ACTIVE,
        // Same as PRIORITY_ACTIVE, the entity even runs when the stage state is PAUSED or FROZEN
        PRIORITY_ALWAYS,
        // Same as PRIORITY_BOUNDS, however it only does checks on the x-axis, so when in bounds on the x-axis, the y position doesn't matter
        PRIORITY_XBOUNDS,
        // Same as PRIORITY_BOUNDS, however the entity's type will be set to BLANK OBJECT when it becomes inactive
        PRIORITY_BOUNDS_DESTROY,
        // Never Active.
        PRIORITY_INACTIVE,
    }

    internal static int objectLoop = 0;
    public static readonly Entity[] objectEntityList = new Entity[ENTITY_COUNT];

    public static readonly string[] typeNames = new string[OBJECT_COUNT];

    public const int OBJECT_BORDER_X1 = 0x80;
    public const int OBJECT_BORDER_X2 = SCREEN_XSIZE + OBJECT_BORDER_X1;
    public const int OBJECT_BORDER_Y1 = 0x100;
    public const int OBJECT_BORDER_Y2 = SCREEN_YSIZE + OBJECT_BORDER_Y1;

    public static void SetObjectTypeName(string objectName, int objectID)
    {
        typeNames[objectID] = objectName.Replace(" ", string.Empty);
        platform.PrintLog($"Set Object ({objectID}) name to: {objectName}");
    }

    public static void ProcessStartupObjects()
    {
        Animation.scriptFrameCount = 0;
        Animation.ClearAnimationData();
        activePlayer = 0;
        activePlayerCount = 1;
        scriptEng.arrayPosition[2] = TEMPENTITY_START;
        for (int i = 0; i < OBJECT_COUNT; ++i)
        {
            objectLoop = TEMPENTITY_START;
            objectScriptList[i].frameListOffset = Animation.scriptFrameCount;
            objectScriptList[i].spriteSheetID = 0;
            objectEntityList[TEMPENTITY_START].type = (byte)i;
            if (scriptCode[objectScriptList[i].subStartup.scriptCodePtr] > 0)
                ProcessScript(objectScriptList[i].subStartup.scriptCodePtr, objectScriptList[i].subStartup.jumpTablePtr, ScriptSubs.SUB_SETUP);
            objectScriptList[i].frameCount = Animation.scriptFrameCount - objectScriptList[i].frameListOffset;
        }
        objectEntityList[TEMPENTITY_START].type = 0;
    }

    public static void ProcessObjects()
    {
        for (int i = 0; i < DRAWLAYER_COUNT; ++i) drawListEntries[i].listSize = 0;

        static void ProcessObject(ref Entity entity)
        {
            bool active = false;
            int x = 0, y = 0;

            switch ((ObjectPriority)entity.priority)
            {
                case ObjectPriority.PRIORITY_BOUNDS:
                    x = FixedPointToWhole(entity.XPos);
                    y = FixedPointToWhole(entity.YPos);
                    active = x > xScrollOffset - OBJECT_BORDER_X1 && x < OBJECT_BORDER_X2 + xScrollOffset && y > yScrollOffsetPixels - OBJECT_BORDER_Y1
                             && y < yScrollOffsetPixels + OBJECT_BORDER_Y2;
                    break;

                case ObjectPriority.PRIORITY_ACTIVE:
                case ObjectPriority.PRIORITY_ALWAYS: active = true; break;

                case ObjectPriority.PRIORITY_XBOUNDS:
                    x = FixedPointToWhole(entity.XPos);
                    active = x > xScrollOffset - OBJECT_BORDER_X1 && x < OBJECT_BORDER_X2 + xScrollOffset;
                    break;

                case ObjectPriority.PRIORITY_BOUNDS_DESTROY:
                    x = FixedPointToWhole(entity.XPos);
                    y = FixedPointToWhole(entity.YPos);
                    if (x <= xScrollOffset - OBJECT_BORDER_X1 || x >= OBJECT_BORDER_X2 + xScrollOffset || y <= yScrollOffsetPixels - OBJECT_BORDER_Y1
                        || y >= yScrollOffsetPixels + OBJECT_BORDER_Y2)
                    {
                        active = false;
                        entity.type = BlankObjectID;
                    }
                    else
                    {
                        active = true;
                    }
                    break;

                case ObjectPriority.PRIORITY_INACTIVE: active = false; break;

                default: break;
            }

            if (active && entity.type != BlankObjectID)
            {
                ObjectScript scriptInfo = objectScriptList[entity.type];
                activePlayer = 0;
                if (scriptCode[scriptInfo.subMain.scriptCodePtr] > 0)
                    ProcessScript(scriptInfo.subMain.scriptCodePtr, scriptInfo.subMain.jumpTablePtr, ScriptSubs.SUB_MAIN);
                if (scriptCode[scriptInfo.subPlayerInteraction.scriptCodePtr] > 0)
                {
                    while (activePlayer < activePlayerCount)
                    {
                        if (playerList[activePlayer].objectInteractions != 0)
                        {
                            ProcessScript(scriptInfo.subPlayerInteraction.scriptCodePtr, scriptInfo.subPlayerInteraction.jumpTablePtr,
                                          ScriptSubs.SUB_PLAYERINTERACTION);
                        }
                        ++activePlayer;
                    }
                }

                if (entity.drawOrder < DRAWLAYER_COUNT)
                    drawListEntries[entity.drawOrder].entityRefs[drawListEntries[entity.drawOrder].listSize++] = objectLoop;
            }
        }

        for (objectLoop = 0; objectLoop < ENTITY_COUNT; ++objectLoop)
        {
            ProcessObject(ref objectEntityList[objectLoop]);
        }
    }

    public static void ProcessPausedObjects()
    {
        for (int i = 0; i < DRAWLAYER_COUNT; ++i) drawListEntries[i].listSize = 0;

        for (objectLoop = 0; objectLoop < ENTITY_COUNT; ++objectLoop)
        {
            var entity = objectEntityList[objectLoop];
            if (entity.priority == (byte)ObjectPriority.PRIORITY_ALWAYS && entity.type != BlankObjectID)
            {
                var scriptInfo = objectScriptList[entity.type];
                activePlayer = 0;
                if (scriptCode[scriptInfo.subMain.scriptCodePtr] > 0)
                    ProcessScript(scriptInfo.subMain.scriptCodePtr, scriptInfo.subMain.jumpTablePtr, ScriptSubs.SUB_MAIN);
                if (scriptCode[scriptInfo.subPlayerInteraction.scriptCodePtr] > 0)
                {
                    while (activePlayer < PLAYER_COUNT)
                    {
                        if (playerList[activePlayer].objectInteractions != 0)
                            ProcessScript(scriptInfo.subPlayerInteraction.scriptCodePtr, scriptInfo.subPlayerInteraction.jumpTablePtr,
                                          ScriptSubs.SUB_PLAYERINTERACTION);
                        ++activePlayer;
                    }
                }

                if (entity.drawOrder < DRAWLAYER_COUNT)
                    drawListEntries[entity.drawOrder].entityRefs[drawListEntries[entity.drawOrder].listSize++] = objectLoop;
            }
        }
    }

    internal static bool ObjectInBounds(int entityIndex)
    {
        int pos = FixedPointToWhole(objectEntityList[entityIndex].XPos);
        return pos > xScrollOffset - OBJECT_BORDER_X1 && pos < OBJECT_BORDER_X2 + xScrollOffset;
    }
}