global using static Cosmic.Core.Script;
using System;
using System.Collections.Generic;
using System.Drawing;
using static Cosmic.Core.Animation;
using static Cosmic.Core.Drawing;
using static Cosmic.Core.ScriptParsing;

namespace Cosmic.Core;

public static unsafe class Script
{
    const int SCRIPTDATA_COUNT = (0x40000);
    const int JUMPTABLE_COUNT = (0x4000);

    public struct ScriptPtr
    {
        public int scriptCodePtr;
        public int jumpTablePtr;
    }

    public struct ScriptFunction
    {
        public string name;
        public ScriptPtr ptr;
    }

    public struct ObjectScript
    {
        public int frameCount;
        public int spriteSheetID;
        public ScriptPtr subMain;
        public ScriptPtr subPlayerInteraction;
        public ScriptPtr subDraw;
        public ScriptPtr subStartup;
        public int frameListOffset;
        public Animation.AnimationFile animFile;
    }

    public struct ScriptEngine
    {
        public readonly int[] operands;
        public readonly int[] tempValue;
        public readonly int[] arrayPosition;
        public int checkResult;

        public ScriptEngine()
        {
            operands = new int[10];
            tempValue = new int[8];
            arrayPosition = new int[3];
        }
    }

    public enum ScriptSubs { SUB_MAIN = 0, SUB_PLAYERINTERACTION = 1, SUB_DRAW = 2, SUB_SETUP = 3 }

    public static readonly ObjectScript[] objectScriptList = new ObjectScript[OBJECT_COUNT];

    public static readonly List<ScriptFunction> scriptFunctionList = new();

    public static int GetGlobalVariableByName(string name)
    {
        for (int v = 0; v < Engine.globalVariables.Count; ++v)
        {
            if (name == Engine.globalVariableNames[v])
                return Engine.globalVariables[v];
        }
        return 0;
    }

    public static void SetGlobalVariableByName(string name, int value)
    {
        for (int v = 0; v < Engine.globalVariables.Count; ++v)
        {
            if (name == Engine.globalVariableNames[v])
            {
                Engine.globalVariables[v] = value;
                break;
            }
        }
    }

    public static readonly int[] scriptCode = new int[SCRIPTDATA_COUNT];
    public static readonly int[] jumpTable = new int[JUMPTABLE_COUNT];
    public static readonly Stack<int> jumpTableStack = new();
    static readonly Stack<int> functionStack = new();

    internal static int scriptCodePos = 0;
    internal static int scriptCodeOffset = 0;
    internal static int jumpTablePos = 0;
    internal static int jumpTableOffset = 0;

    internal static ScriptEngine scriptEng = new();
    internal static string scriptText = string.Empty;

    internal static int lineID = 0;

    public class AliasInfo
    {
        public AliasInfo()
        {
            name = string.Empty;
            value = string.Empty;
        }
        public AliasInfo(string aliasName, string aliasVal)
        {
            name = aliasName;
            value = aliasVal;
        }

        public string name;
        public string value;
    }

    public struct FunctionInfo
    {
        public FunctionInfo()
        {
            name = string.Empty;
            opcodeSize = 0;
        }
        public FunctionInfo(string functionName, int opSize)
        {
            name = functionName;
            opcodeSize = opSize;
        }

        public string name;
        public int opcodeSize;
    }

    public static void LoadBytecode(int stageListID, int scriptID)
    {
        var scriptPath = string.Empty;
        switch ((StageListNames)stageListID)
        {
            case StageListNames.STAGELIST_PRESENTATION:
            case StageListNames.STAGELIST_REGULAR:
            case StageListNames.STAGELIST_SPECIAL:
            case StageListNames.STAGELIST_BONUS:
                scriptPath = "Data/Scripts/ByteCode/";
                scriptPath += stageList[stageListID, stageListPosition].folder;
                scriptPath += ".bin";
                break;

            case (StageListNames)4: scriptPath = "Data/Scripts/ByteCode/GlobalCode.bin"; break;

            default: break;
        }

        if (platform.LoadFile(scriptPath, out var info))
        {
            byte fileBuffer = 0;
            fixed (int* scriptCodePtrPtr = &scriptCode[scriptCodePos])
            {
                int* scriptCodePtr = scriptCodePtrPtr;

                fixed (int* jumpTablePtrPtr = &jumpTable[jumpTablePos])
                {
                    int* jumpTablePtr = jumpTablePtrPtr;

                    int scriptCodeSize = info.ReadInt32LE();

                    while (scriptCodeSize > 0)
                    {
                        fileBuffer = info.ReadByte();
                        int blockSize = fileBuffer & 0x7F;

                        if (fileBuffer >= 0x80)
                        {
                            while (blockSize > 0)
                            {
                                *scriptCodePtr = info.ReadInt32LE();

                                ++scriptCodePtr;
                                ++scriptCodePos;
                                --scriptCodeSize;
                                --blockSize;
                            }
                        }
                        else
                        {
                            while (blockSize > 0)
                            {
                                fileBuffer = info.ReadByte();
                                *scriptCodePtr = fileBuffer;

                                ++scriptCodePtr;
                                ++scriptCodePos;
                                --scriptCodeSize;
                                --blockSize;
                            }
                        }
                    }

                    int jumpTableSize = info.ReadInt32LE();

                    while (jumpTableSize > 0)
                    {
                        fileBuffer = info.ReadByte();
                        int blockSize = fileBuffer & 0x7F;

                        if (fileBuffer >= 0x80)
                        {
                            while (blockSize > 0)
                            {
                                *jumpTablePtr = info.ReadInt32LE();

                                ++jumpTablePtr;
                                ++jumpTablePos;
                                --jumpTableSize;
                                --blockSize;
                            }
                        }
                        else
                        {
                            while (blockSize > 0)
                            {
                                fileBuffer = info.ReadByte();
                                *jumpTablePtr = fileBuffer;

                                ++jumpTablePtr;
                                ++jumpTablePos;
                                --jumpTableSize;
                                --blockSize;
                            }
                        }
                    }
                }
            }

            fileBuffer = info.ReadByte();
            int scriptCount = fileBuffer;
            fileBuffer = info.ReadByte();
            scriptCount |= fileBuffer << 8;

            for (int s = 0; s < scriptCount; ++s)
            {
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subMain.scriptCodePtr = fileBuffer;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subMain.scriptCodePtr |= fileBuffer << 8;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subMain.scriptCodePtr |= fileBuffer << 16;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subMain.scriptCodePtr |= fileBuffer << 24;

                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subPlayerInteraction.scriptCodePtr = fileBuffer;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subPlayerInteraction.scriptCodePtr |= fileBuffer << 8;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subPlayerInteraction.scriptCodePtr |= fileBuffer << 16;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subPlayerInteraction.scriptCodePtr |= fileBuffer << 24;

                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subDraw.scriptCodePtr = fileBuffer;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subDraw.scriptCodePtr |= fileBuffer << 8;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subDraw.scriptCodePtr |= fileBuffer << 16;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subDraw.scriptCodePtr |= fileBuffer << 24;

                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subStartup.scriptCodePtr = fileBuffer;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subStartup.scriptCodePtr |= fileBuffer << 8;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subStartup.scriptCodePtr |= fileBuffer << 16;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subStartup.scriptCodePtr |= fileBuffer << 24;
            }

            for (int s = 0; s < scriptCount; ++s)
            {
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subMain.jumpTablePtr = fileBuffer;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subMain.jumpTablePtr |= fileBuffer << 8;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subMain.jumpTablePtr |= fileBuffer << 16;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subMain.jumpTablePtr |= fileBuffer << 24;

                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subPlayerInteraction.jumpTablePtr = fileBuffer;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subPlayerInteraction.jumpTablePtr |= fileBuffer << 8;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subPlayerInteraction.jumpTablePtr |= fileBuffer << 16;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subPlayerInteraction.jumpTablePtr |= fileBuffer << 24;

                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subDraw.jumpTablePtr = fileBuffer;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subDraw.jumpTablePtr |= fileBuffer << 8;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subDraw.jumpTablePtr |= fileBuffer << 16;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subDraw.jumpTablePtr |= fileBuffer << 24;

                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subStartup.jumpTablePtr = fileBuffer;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subStartup.jumpTablePtr |= fileBuffer << 8;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subStartup.jumpTablePtr |= fileBuffer << 16;
                fileBuffer = info.ReadByte();
                objectScriptList[scriptID + s].subStartup.jumpTablePtr |= fileBuffer << 24;
            }

            fileBuffer = info.ReadByte();
            int functionCount = fileBuffer;
            fileBuffer = info.ReadByte();
            functionCount |= fileBuffer << 8;

            var codePtrs = new int[functionCount];
            for (int f = 0; f < functionCount; ++f)
            {
                var codePtr = 0;
                fileBuffer = info.ReadByte();
                codePtr = fileBuffer;
                fileBuffer = info.ReadByte();
                codePtr |= fileBuffer << 8;
                fileBuffer = info.ReadByte();
                codePtr |= fileBuffer << 16;
                fileBuffer = info.ReadByte();
                codePtr |= fileBuffer << 24;
                codePtrs[f] = codePtr;
            }

            var jumpTablePtrs = new int[functionCount];
            for (int f = 0; f < functionCount; ++f)
            {
                var jumpTablePtr = 0;
                fileBuffer = info.ReadByte();
                jumpTablePtr = fileBuffer;
                fileBuffer = info.ReadByte();
                jumpTablePtr |= fileBuffer << 8;
                fileBuffer = info.ReadByte();
                jumpTablePtr |= fileBuffer << 16;
                fileBuffer = info.ReadByte();
                jumpTablePtr |= fileBuffer << 24;
                jumpTablePtrs[f] = jumpTablePtr;
            }

            scriptFunctionList.Clear();
            for (int f = 0; f < functionCount; ++f)
            {
                scriptFunctionList.Add(new() { name = string.Empty, ptr = new ScriptPtr() { scriptCodePtr = codePtrs[f], jumpTablePtr = jumpTablePtrs[f] } });
            }

            info.Dispose();
        }
    }

    public static void ClearScriptData()
    {
        Array.Clear(scriptCode);
        Array.Clear(jumpTable);

        scriptFrameCount = 0;

        scriptCodePos = 0;
        jumpTablePos = 0;
        jumpTableStack.Clear();
        functionStack.Clear();

        scriptCodePos = 0;
        scriptCodeOffset = 0;
        jumpTablePos = 0;
        jumpTableOffset = 0;

        scriptFunctionList.Clear();

        aliases.Clear();
        aliases.AddRange(defaultAliases);
        lineID = 0;

        Sprite.ClearAll();
        ClearAnimationData();

        for (int p = 0; p < PLAYER_COUNT; ++p)
        {
            playerList[p].animationFile = GetDefaultAnimationRef();
            playerList[p].boundEntity = p;
        }

        for (int o = 0; o < OBJECT_COUNT; ++o)
        {
            objectScriptList[o].subMain.scriptCodePtr = SCRIPTDATA_COUNT - 1;
            objectScriptList[o].subMain.jumpTablePtr = JUMPTABLE_COUNT - 1;
            objectScriptList[o].subPlayerInteraction.scriptCodePtr = SCRIPTDATA_COUNT - 1;
            objectScriptList[o].subPlayerInteraction.jumpTablePtr = JUMPTABLE_COUNT - 1;
            objectScriptList[o].subDraw.scriptCodePtr = SCRIPTDATA_COUNT - 1;
            objectScriptList[o].subDraw.jumpTablePtr = JUMPTABLE_COUNT - 1;
            objectScriptList[o].subStartup.scriptCodePtr = SCRIPTDATA_COUNT - 1;
            objectScriptList[o].subStartup.jumpTablePtr = JUMPTABLE_COUNT - 1;
            objectScriptList[o].frameListOffset = 0;
            objectScriptList[o].spriteSheetID = 0;
            objectScriptList[o].animFile = GetDefaultAnimationRef();
            typeNames[o] = string.Empty;
        }

        SetObjectTypeName("Blank Object", 0);
    }

    public static void ProcessScript(int scriptCodeStart, int jumpTableStart, ScriptSubs scriptSub)
    {
        bool running = true;
        int scriptCodePtr = scriptCodeStart;

        jumpTableStack.Clear();
        functionStack.Clear();
        while (running)
        {
            int opcode = scriptCode[scriptCodePtr++];
            int numOps = functionInfoLookup[opcode].NumOps;
            int scriptCodeOffset = scriptCodePtr;

            // Get Values
            for (int i = 0; i < numOps; ++i)
            {
                var opcodeType = (ScriptVarTypes)scriptCode[scriptCodePtr++];

                if (opcodeType == ScriptVarTypes.SCRIPTVAR_VAR)
                {
                    int arrayVal = 0;
                    switch ((ScriptVarArrTypes)scriptCode[scriptCodePtr++])
                    {
                        case ScriptVarArrTypes.VARARR_NONE: arrayVal = objectLoop; break;
                        case ScriptVarArrTypes.VARARR_ARRAY:
                            if (scriptCode[scriptCodePtr++] == 1)
                                arrayVal = scriptEng.arrayPosition[scriptCode[scriptCodePtr++]];
                            else
                                arrayVal = scriptCode[scriptCodePtr++];
                            break;
                        case ScriptVarArrTypes.VARARR_ENTNOPLUS1:
                            if (scriptCode[scriptCodePtr++] == 1)
                                arrayVal = scriptEng.arrayPosition[scriptCode[scriptCodePtr++]] + objectLoop;
                            else
                                arrayVal = scriptCode[scriptCodePtr++] + objectLoop;
                            break;
                        case ScriptVarArrTypes.VARARR_ENTNOMINUS1:
                            if (scriptCode[scriptCodePtr++] == 1)
                                arrayVal = objectLoop - scriptEng.arrayPosition[scriptCode[scriptCodePtr++]];
                            else
                                arrayVal = objectLoop - scriptCode[scriptCodePtr++];
                            break;
                        default: break;
                    }

                    // Variables
                    switch ((ScrVariable)scriptCode[scriptCodePtr++])
                    {
                        default: break;
                        case ScrVariable.VAR_TEMPVALUE0: scriptEng.operands[i] = scriptEng.tempValue[0]; break;
                        case ScrVariable.VAR_TEMPVALUE1: scriptEng.operands[i] = scriptEng.tempValue[1]; break;
                        case ScrVariable.VAR_TEMPVALUE2: scriptEng.operands[i] = scriptEng.tempValue[2]; break;
                        case ScrVariable.VAR_TEMPVALUE3: scriptEng.operands[i] = scriptEng.tempValue[3]; break;
                        case ScrVariable.VAR_TEMPVALUE4: scriptEng.operands[i] = scriptEng.tempValue[4]; break;
                        case ScrVariable.VAR_TEMPVALUE5: scriptEng.operands[i] = scriptEng.tempValue[5]; break;
                        case ScrVariable.VAR_TEMPVALUE6: scriptEng.operands[i] = scriptEng.tempValue[6]; break;
                        case ScrVariable.VAR_TEMPVALUE7: scriptEng.operands[i] = scriptEng.tempValue[7]; break;
                        case ScrVariable.VAR_CHECKRESULT: scriptEng.operands[i] = scriptEng.checkResult; break;
                        case ScrVariable.VAR_ARRAYPOS0: scriptEng.operands[i] = scriptEng.arrayPosition[0]; break;
                        case ScrVariable.VAR_ARRAYPOS1: scriptEng.operands[i] = scriptEng.arrayPosition[1]; break;
                        case ScrVariable.VAR_GLOBAL: scriptEng.operands[i] = Engine.globalVariables[arrayVal]; break;
                        case ScrVariable.VAR_OBJECTENTITYNO: scriptEng.operands[i] = arrayVal; break;
                        case ScrVariable.VAR_OBJECTTYPE: scriptEng.operands[i] = objectEntityList[arrayVal].type; break;
                        case ScrVariable.VAR_OBJECTPROPERTYVALUE: scriptEng.operands[i] = objectEntityList[arrayVal].propertyValue; break;
                        case ScrVariable.VAR_OBJECTXPOS: scriptEng.operands[i] = objectEntityList[arrayVal].XPos; break;
                        case ScrVariable.VAR_OBJECTYPOS: scriptEng.operands[i] = objectEntityList[arrayVal].YPos; break;
                        case ScrVariable.VAR_OBJECTIXPOS: scriptEng.operands[i] = FixedPointToWhole(objectEntityList[arrayVal].XPos); break;
                        case ScrVariable.VAR_OBJECTIYPOS: scriptEng.operands[i] = FixedPointToWhole(objectEntityList[arrayVal].YPos); break;
                        case ScrVariable.VAR_OBJECTSTATE: scriptEng.operands[i] = objectEntityList[arrayVal].state; break;
                        case ScrVariable.VAR_OBJECTROTATION: scriptEng.operands[i] = objectEntityList[arrayVal].rotation; break;
                        case ScrVariable.VAR_OBJECTSCALE: scriptEng.operands[i] = objectEntityList[arrayVal].scale; break;
                        case ScrVariable.VAR_OBJECTPRIORITY: scriptEng.operands[i] = objectEntityList[arrayVal].priority; break;
                        case ScrVariable.VAR_OBJECTDRAWORDER: scriptEng.operands[i] = objectEntityList[arrayVal].drawOrder; break;
                        case ScrVariable.VAR_OBJECTDIRECTION: scriptEng.operands[i] = objectEntityList[arrayVal].direction; break;
                        case ScrVariable.VAR_OBJECTINKEFFECT: scriptEng.operands[i] = objectEntityList[arrayVal].inkEffect; break;
                        case ScrVariable.VAR_OBJECTALPHA: scriptEng.operands[i] = objectEntityList[arrayVal].alpha; break;
                        case ScrVariable.VAR_OBJECTFRAME: scriptEng.operands[i] = objectEntityList[arrayVal].frame; break;
                        case ScrVariable.VAR_OBJECTANIMATION: scriptEng.operands[i] = objectEntityList[arrayVal].animation; break;
                        case ScrVariable.VAR_OBJECTPREVANIMATION: scriptEng.operands[i] = objectEntityList[arrayVal].prevAnimation; break;
                        case ScrVariable.VAR_OBJECTANIMATIONSPEED: scriptEng.operands[i] = objectEntityList[arrayVal].animationSpeed; break;
                        case ScrVariable.VAR_OBJECTANIMATIONTIMER: scriptEng.operands[i] = objectEntityList[arrayVal].animationTimer; break;
                        case ScrVariable.VAR_OBJECTVALUE0: scriptEng.operands[i] = objectEntityList[arrayVal].values[0]; break;
                        case ScrVariable.VAR_OBJECTVALUE1: scriptEng.operands[i] = objectEntityList[arrayVal].values[1]; break;
                        case ScrVariable.VAR_OBJECTVALUE2: scriptEng.operands[i] = objectEntityList[arrayVal].values[2]; break;
                        case ScrVariable.VAR_OBJECTVALUE3: scriptEng.operands[i] = objectEntityList[arrayVal].values[3]; break;
                        case ScrVariable.VAR_OBJECTVALUE4: scriptEng.operands[i] = objectEntityList[arrayVal].values[4]; break;
                        case ScrVariable.VAR_OBJECTVALUE5: scriptEng.operands[i] = objectEntityList[arrayVal].values[5]; break;
                        case ScrVariable.VAR_OBJECTVALUE6: scriptEng.operands[i] = objectEntityList[arrayVal].values[6]; break;
                        case ScrVariable.VAR_OBJECTVALUE7: scriptEng.operands[i] = objectEntityList[arrayVal].values[7]; break;
                        case ScrVariable.VAR_OBJECTOUTOFBOUNDS:
                            int pos = FixedPointToWhole(objectEntityList[arrayVal].XPos);
                            if (!ObjectInBounds(arrayVal))
                            {
                                scriptEng.operands[i] = 1;
                            }
                            else
                            {
                                pos = FixedPointToWhole(objectEntityList[arrayVal].YPos);
                                scriptEng.operands[i] = (pos <= yScrollOffsetPixels - OBJECT_BORDER_Y1 || pos >= yScrollOffsetPixels + OBJECT_BORDER_Y2) ? 1 : 0;
                            }
                            break;
                        case ScrVariable.VAR_PLAYERSTATE: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].state; break;
                        case ScrVariable.VAR_PLAYERCONTROLMODE: scriptEng.operands[i] = playerList[activePlayer].controlMode; break;
                        case ScrVariable.VAR_PLAYERCONTROLLOCK: scriptEng.operands[i] = playerList[activePlayer].controlLock; break;
                        case ScrVariable.VAR_PLAYERCOLLISIONMODE: scriptEng.operands[i] = playerList[activePlayer].collisionMode; break;
                        case ScrVariable.VAR_PLAYERCOLLISIONPLANE: scriptEng.operands[i] = playerList[activePlayer].collisionPlane; break;
                        case ScrVariable.VAR_PLAYERXPOS: scriptEng.operands[i] = playerList[activePlayer].XPos; break;
                        case ScrVariable.VAR_PLAYERYPOS: scriptEng.operands[i] = playerList[activePlayer].YPos; break;
                        case ScrVariable.VAR_PLAYERIXPOS: scriptEng.operands[i] = FixedPointToWhole(playerList[activePlayer].XPos); break;
                        case ScrVariable.VAR_PLAYERIYPOS: scriptEng.operands[i] = FixedPointToWhole(playerList[activePlayer].YPos); break;
                        case ScrVariable.VAR_PLAYERSCREENXPOS: scriptEng.operands[i] = playerList[activePlayer].screenXPos; break;
                        case ScrVariable.VAR_PLAYERSCREENYPOS: scriptEng.operands[i] = playerList[activePlayer].screenYPos; break;
                        case ScrVariable.VAR_PLAYERSPEED: scriptEng.operands[i] = playerList[activePlayer].speed; break;
                        case ScrVariable.VAR_PLAYERXVELOCITY: scriptEng.operands[i] = playerList[activePlayer].XVelocity; break;
                        case ScrVariable.VAR_PLAYERYVELOCITY: scriptEng.operands[i] = playerList[activePlayer].YVelocity; break;
                        case ScrVariable.VAR_PLAYERGRAVITY: scriptEng.operands[i] = playerList[activePlayer].gravity; break;
                        case ScrVariable.VAR_PLAYERANGLE: scriptEng.operands[i] = playerList[activePlayer].angle; break;
                        case ScrVariable.VAR_PLAYERSKIDDING: scriptEng.operands[i] = playerList[activePlayer].skidding; break;
                        case ScrVariable.VAR_PLAYERPUSHING: scriptEng.operands[i] = playerList[activePlayer].pushing; break;
                        case ScrVariable.VAR_PLAYERTRACKSCROLL: scriptEng.operands[i] = playerList[activePlayer].trackScroll; break;
                        case ScrVariable.VAR_PLAYERUP: scriptEng.operands[i] = playerList[activePlayer].up; break;
                        case ScrVariable.VAR_PLAYERDOWN: scriptEng.operands[i] = playerList[activePlayer].down; break;
                        case ScrVariable.VAR_PLAYERLEFT: scriptEng.operands[i] = playerList[activePlayer].left; break;
                        case ScrVariable.VAR_PLAYERRIGHT: scriptEng.operands[i] = playerList[activePlayer].right; break;
                        case ScrVariable.VAR_PLAYERJUMPPRESS: scriptEng.operands[i] = playerList[activePlayer].jumpPress; break;
                        case ScrVariable.VAR_PLAYERJUMPHOLD: scriptEng.operands[i] = playerList[activePlayer].jumpHold; break;
                        case ScrVariable.VAR_PLAYERFOLLOWPLAYER1: scriptEng.operands[i] = playerList[activePlayer].followPlayer1; break;
                        case ScrVariable.VAR_PLAYERLOOKPOS: scriptEng.operands[i] = playerList[activePlayer].lookPos; break;
                        case ScrVariable.VAR_PLAYERWATER: scriptEng.operands[i] = playerList[activePlayer].water; break;
                        case ScrVariable.VAR_PLAYERTOPSPEED: scriptEng.operands[i] = playerList[activePlayer].topSpeed; break;
                        case ScrVariable.VAR_PLAYERACCELERATION: scriptEng.operands[i] = playerList[activePlayer].acceleration; break;
                        case ScrVariable.VAR_PLAYERDECELERATION: scriptEng.operands[i] = playerList[activePlayer].deceleration; break;
                        case ScrVariable.VAR_PLAYERAIRACCELERATION: scriptEng.operands[i] = playerList[activePlayer].airAcceleration; break;
                        case ScrVariable.VAR_PLAYERAIRDECELERATION: scriptEng.operands[i] = playerList[activePlayer].airDeceleration; break;
                        case ScrVariable.VAR_PLAYERGRAVITYSTRENGTH: scriptEng.operands[i] = playerList[activePlayer].gravityStrength; break;
                        case ScrVariable.VAR_PLAYERJUMPSTRENGTH: scriptEng.operands[i] = playerList[activePlayer].jumpStrength; break;
                        case ScrVariable.VAR_PLAYERJUMPCAP: scriptEng.operands[i] = playerList[activePlayer].jumpCap; break;
                        case ScrVariable.VAR_PLAYERROLLINGACCELERATION: scriptEng.operands[i] = playerList[activePlayer].rollingAcceleration; break;
                        case ScrVariable.VAR_PLAYERROLLINGDECELERATION: scriptEng.operands[i] = playerList[activePlayer].rollingDeceleration; break;
                        case ScrVariable.VAR_PLAYERENTITYNO: scriptEng.operands[i] = playerList[activePlayer].entityNo; break;
                        case ScrVariable.VAR_PLAYERCOLLISIONLEFT:
                            AnimationFile animFile = playerList[activePlayer].animationFile;
                            fixed (Player* plr = &playerList[activePlayer])
                            {
                                if (animFile != null)
                                {
                                    int h = animFrames[animationList[animFile.aniListOffset + objectEntityList[plr->boundEntity].animation].frameListOffset
                                                       + objectEntityList[plr->boundEntity].frame].hitboxID;

                                    if (animFile.hitboxListOffset + h < hitboxList.Count)
                                    {
                                        scriptEng.operands[i] = hitboxList[animFile.hitboxListOffset + h].left[0];
                                    }
                                    else
                                    {
                                        scriptEng.operands[i] = 0;
                                    }
                                }
                                else
                                {
                                    scriptEng.operands[i] = 0;
                                }
                            }
                            break;
                        case ScrVariable.VAR_PLAYERCOLLISIONTOP:
                            animFile = playerList[activePlayer].animationFile;
                            fixed (Player* plr = &playerList[activePlayer])
                            {
                                if (animFile != null)
                                {
                                    int h = animFrames[animationList[animFile.aniListOffset + objectEntityList[plr->boundEntity].animation].frameListOffset
                                                       + objectEntityList[plr->boundEntity].frame].hitboxID;

                                    scriptEng.operands[i] = hitboxList[animFile.hitboxListOffset + h].top[0];
                                }
                                else
                                {
                                    scriptEng.operands[i] = 0;
                                }
                            }
                            break;
                        case ScrVariable.VAR_PLAYERCOLLISIONRIGHT:
                            animFile = playerList[activePlayer].animationFile;
                            fixed (Player* plr = &playerList[activePlayer])
                            {
                                if (animFile != null)
                                {
                                    int h = animFrames[animationList[animFile.aniListOffset + objectEntityList[plr->boundEntity].animation].frameListOffset
                                                       + objectEntityList[plr->boundEntity].frame].hitboxID;

                                    scriptEng.operands[i] = hitboxList[animFile.hitboxListOffset + h].right[0];
                                }
                                else
                                {
                                    scriptEng.operands[i] = 0;
                                }
                            }
                            break;
                        case ScrVariable.VAR_PLAYERCOLLISIONBOTTOM:
                            animFile = playerList[activePlayer].animationFile;
                            fixed (Player* plr = &playerList[activePlayer])
                            {
                                if (animFile != null)
                                {
                                    int h = animFrames[animationList[animFile.aniListOffset + objectEntityList[plr->boundEntity].animation].frameListOffset
                                                       + objectEntityList[plr->boundEntity].frame].hitboxID;

                                    if (animFile.hitboxListOffset + h < hitboxList.Count)
                                    {
                                        scriptEng.operands[i] = hitboxList[animFile.hitboxListOffset + h].bottom[0];
                                    }
                                    else
                                    {
                                        scriptEng.operands[i] = 0;
                                    }
                                }
                                else
                                {
                                    scriptEng.operands[i] = 0;
                                }
                            }
                            break;
                        case ScrVariable.VAR_PLAYERFLAILING: scriptEng.operands[i] = playerList[activePlayer].flailing[arrayVal]; break;
                        case ScrVariable.VAR_PLAYERTIMER: scriptEng.operands[i] = playerList[activePlayer].timer; break;
                        case ScrVariable.VAR_PLAYERTILECOLLISIONS: scriptEng.operands[i] = playerList[activePlayer].tileCollisions; break;
                        case ScrVariable.VAR_PLAYEROBJECTINTERACTION: scriptEng.operands[i] = playerList[activePlayer].objectInteractions; break;
                        case ScrVariable.VAR_PLAYERVISIBLE: scriptEng.operands[i] = playerList[activePlayer].visible; break;
                        case ScrVariable.VAR_PLAYERROTATION: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].rotation; break;
                        case ScrVariable.VAR_PLAYERSCALE: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].scale; break;
                        case ScrVariable.VAR_PLAYERPRIORITY: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].priority; break;
                        case ScrVariable.VAR_PLAYERDRAWORDER: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].drawOrder; break;
                        case ScrVariable.VAR_PLAYERDIRECTION: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].direction; break;
                        case ScrVariable.VAR_PLAYERINKEFFECT: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].inkEffect; break;
                        case ScrVariable.VAR_PLAYERALPHA: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].alpha; break;
                        case ScrVariable.VAR_PLAYERFRAME: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].frame; break;
                        case ScrVariable.VAR_PLAYERANIMATION: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].animation; break;
                        case ScrVariable.VAR_PLAYERPREVANIMATION: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].prevAnimation; break;
                        case ScrVariable.VAR_PLAYERANIMATIONSPEED: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].animationSpeed; break;
                        case ScrVariable.VAR_PLAYERANIMATIONTIMER: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].animationTimer; break;
                        case ScrVariable.VAR_PLAYERVALUE0: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].values[0]; break;
                        case ScrVariable.VAR_PLAYERVALUE1: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].values[1]; break;
                        case ScrVariable.VAR_PLAYERVALUE2: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].values[2]; break;
                        case ScrVariable.VAR_PLAYERVALUE3: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].values[3]; break;
                        case ScrVariable.VAR_PLAYERVALUE4: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].values[4]; break;
                        case ScrVariable.VAR_PLAYERVALUE5: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].values[5]; break;
                        case ScrVariable.VAR_PLAYERVALUE6: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].values[6]; break;
                        case ScrVariable.VAR_PLAYERVALUE7: scriptEng.operands[i] = objectEntityList[playerList[activePlayer].boundEntity].values[7]; break;
                        case ScrVariable.VAR_PLAYERVALUE8: scriptEng.operands[i] = playerList[activePlayer].values[0]; break;
                        case ScrVariable.VAR_PLAYERVALUE9: scriptEng.operands[i] = playerList[activePlayer].values[1]; break;
                        case ScrVariable.VAR_PLAYERVALUE10: scriptEng.operands[i] = playerList[activePlayer].values[2]; break;
                        case ScrVariable.VAR_PLAYERVALUE11: scriptEng.operands[i] = playerList[activePlayer].values[3]; break;
                        case ScrVariable.VAR_PLAYERVALUE12: scriptEng.operands[i] = playerList[activePlayer].values[4]; break;
                        case ScrVariable.VAR_PLAYERVALUE13: scriptEng.operands[i] = playerList[activePlayer].values[5]; break;
                        case ScrVariable.VAR_PLAYERVALUE14: scriptEng.operands[i] = playerList[activePlayer].values[6]; break;
                        case ScrVariable.VAR_PLAYERVALUE15: scriptEng.operands[i] = playerList[activePlayer].values[7]; break;
                        case ScrVariable.VAR_PLAYEROUTOFBOUNDS:
                            pos = FixedPointToWhole(playerList[activePlayer].XPos);
                            if (pos <= xScrollOffset - OBJECT_BORDER_X1 || pos >= OBJECT_BORDER_X2 + xScrollOffset)
                            {
                                scriptEng.operands[i] = 1;
                            }
                            else
                            {
                                pos = FixedPointToWhole(playerList[activePlayer].YPos);
                                scriptEng.operands[i] = (pos <= yScrollOffsetPixels - OBJECT_BORDER_Y1 || pos >= yScrollOffsetPixels + OBJECT_BORDER_Y2) ? 1 : 0;
                            }
                            break;
                        case ScrVariable.VAR_STAGESTATE: scriptEng.operands[i] = (int)stageMode; break;
                        case ScrVariable.VAR_STAGEACTIVELIST: scriptEng.operands[i] = (int)activeStageList; break;
                        case ScrVariable.VAR_STAGELISTPOS: scriptEng.operands[i] = stageListPosition; break;
                        case ScrVariable.VAR_STAGETIMEENABLED: scriptEng.operands[i] = timeEnabled ? 1 : 0; break;
                        case ScrVariable.VAR_STAGEMILLISECONDS: scriptEng.operands[i] = stageMilliseconds; break;
                        case ScrVariable.VAR_STAGESECONDS: scriptEng.operands[i] = stageSeconds; break;
                        case ScrVariable.VAR_STAGEMINUTES: scriptEng.operands[i] = stageMinutes; break;
                        case ScrVariable.VAR_STAGEACTNO: scriptEng.operands[i] = actID; break;
                        case ScrVariable.VAR_STAGEPAUSEENABLED: scriptEng.operands[i] = pauseEnabled ? 1 : 0; break;
                        case ScrVariable.VAR_STAGELISTSIZE: scriptEng.operands[i] = stageListCount[(int)activeStageList]; break;
                        case ScrVariable.VAR_STAGENEWXBOUNDARY1: scriptEng.operands[i] = newXBoundary1; break;
                        case ScrVariable.VAR_STAGENEWXBOUNDARY2: scriptEng.operands[i] = newXBoundary2; break;
                        case ScrVariable.VAR_STAGENEWYBOUNDARY1: scriptEng.operands[i] = newYBoundary1; break;
                        case ScrVariable.VAR_STAGENEWYBOUNDARY2: scriptEng.operands[i] = newYBoundary2; break;
                        case ScrVariable.VAR_STAGEXBOUNDARY1: scriptEng.operands[i] = xBoundary1; break;
                        case ScrVariable.VAR_STAGEXBOUNDARY2: scriptEng.operands[i] = xBoundary2; break;
                        case ScrVariable.VAR_STAGEYBOUNDARY1: scriptEng.operands[i] = yBoundary1; break;
                        case ScrVariable.VAR_STAGEYBOUNDARY2: scriptEng.operands[i] = yBoundary2; break;
                        case ScrVariable.VAR_STAGEDEFORMATIONDATA0: scriptEng.operands[i] = bgDeformationData0[arrayVal]; break;
                        case ScrVariable.VAR_STAGEDEFORMATIONDATA1: scriptEng.operands[i] = bgDeformationData1[arrayVal]; break;
                        case ScrVariable.VAR_STAGEDEFORMATIONDATA2: scriptEng.operands[i] = bgDeformationData2[arrayVal]; break;
                        case ScrVariable.VAR_STAGEDEFORMATIONDATA3: scriptEng.operands[i] = bgDeformationData3[arrayVal]; break;
                        case ScrVariable.VAR_STAGEWATERLEVEL: scriptEng.operands[i] = waterLevel; break;
                        case ScrVariable.VAR_STAGEACTIVELAYER: scriptEng.operands[i] = activeTileLayers[arrayVal]; break;
                        case ScrVariable.VAR_STAGEMIDPOINT: scriptEng.operands[i] = tLayerMidPoint; break;
                        case ScrVariable.VAR_STAGEPLAYERLISTPOS: scriptEng.operands[i] = playerListPos; break;
                        case ScrVariable.VAR_STAGEACTIVEPLAYER: scriptEng.operands[i] = activePlayer; break;
                        case ScrVariable.VAR_SCREENCAMERAENABLED: scriptEng.operands[i] = cameraEnabled; break;
                        case ScrVariable.VAR_SCREENCAMERATARGET: scriptEng.operands[i] = cameraTarget; break;
                        case ScrVariable.VAR_SCREENCAMERASTYLE: scriptEng.operands[i] = (int)cameraStyle; break;
                        case ScrVariable.VAR_SCREENDRAWLISTSIZE: scriptEng.operands[i] = drawListEntries[arrayVal].listSize; break;
                        case ScrVariable.VAR_SCREENCENTERX: scriptEng.operands[i] = SCREEN_CENTERX; break;
                        case ScrVariable.VAR_SCREENCENTERY: scriptEng.operands[i] = SCREEN_CENTERY; break;
                        case ScrVariable.VAR_SCREENXSIZE: scriptEng.operands[i] = SCREEN_XSIZE; break;
                        case ScrVariable.VAR_SCREENYSIZE: scriptEng.operands[i] = SCREEN_YSIZE; break;
                        case ScrVariable.VAR_SCREENXOFFSET: scriptEng.operands[i] = xScrollOffset; break;
                        case ScrVariable.VAR_SCREENYOFFSET: scriptEng.operands[i] = yScrollOffsetPixels; break;
                        case ScrVariable.VAR_SCREENSHAKEX: scriptEng.operands[i] = cameraShakeX; break;
                        case ScrVariable.VAR_SCREENSHAKEY: scriptEng.operands[i] = cameraShakeY; break;
                        case ScrVariable.VAR_SCREENADJUSTCAMERAY: scriptEng.operands[i] = cameraAdjustY; break;
                        case ScrVariable.VAR_TOUCHSCREENDOWN: scriptEng.operands[i] = touchDown[arrayVal]; break;
                        case ScrVariable.VAR_TOUCHSCREENXPOS: scriptEng.operands[i] = touchX[arrayVal]; break;
                        case ScrVariable.VAR_TOUCHSCREENYPOS: scriptEng.operands[i] = touchY[arrayVal]; break;
                        case ScrVariable.VAR_MUSICVOLUME: scriptEng.operands[i] = masterVolume; break;
                        case ScrVariable.VAR_MUSICCURRENTTRACK: scriptEng.operands[i] = trackID; break;
                        case ScrVariable.VAR_KEYDOWNUP: scriptEng.operands[i] = keyDown[arrayVal].up ? 1 : 0; break;
                        case ScrVariable.VAR_KEYDOWNDOWN: scriptEng.operands[i] = keyDown[arrayVal].down ? 1 : 0; break;
                        case ScrVariable.VAR_KEYDOWNLEFT: scriptEng.operands[i] = keyDown[arrayVal].left ? 1 : 0; break;
                        case ScrVariable.VAR_KEYDOWNRIGHT: scriptEng.operands[i] = keyDown[arrayVal].right ? 1 : 0; break;
                        case ScrVariable.VAR_KEYDOWNBUTTONA: scriptEng.operands[i] = keyDown[arrayVal].A ? 1 : 0; break;
                        case ScrVariable.VAR_KEYDOWNBUTTONB: scriptEng.operands[i] = keyDown[arrayVal].B ? 1 : 0; break;
                        case ScrVariable.VAR_KEYDOWNBUTTONC: scriptEng.operands[i] = keyDown[arrayVal].C ? 1 : 0; break;
                        case ScrVariable.VAR_KEYDOWNSTART: scriptEng.operands[i] = keyDown[arrayVal].start ? 1 : 0; break;
                        case ScrVariable.VAR_KEYPRESSUP: scriptEng.operands[i] = keyPress[arrayVal].up ? 1 : 0; break;
                        case ScrVariable.VAR_KEYPRESSDOWN: scriptEng.operands[i] = keyPress[arrayVal].down ? 1 : 0; break;
                        case ScrVariable.VAR_KEYPRESSLEFT: scriptEng.operands[i] = keyPress[arrayVal].left ? 1 : 0; break;
                        case ScrVariable.VAR_KEYPRESSRIGHT: scriptEng.operands[i] = keyPress[arrayVal].right ? 1 : 0; break;
                        case ScrVariable.VAR_KEYPRESSBUTTONA: scriptEng.operands[i] = keyPress[arrayVal].A ? 1 : 0; break;
                        case ScrVariable.VAR_KEYPRESSBUTTONB: scriptEng.operands[i] = keyPress[arrayVal].B ? 1 : 0; break;
                        case ScrVariable.VAR_KEYPRESSBUTTONC: scriptEng.operands[i] = keyPress[arrayVal].C ? 1 : 0; break;
                        case ScrVariable.VAR_KEYPRESSSTART: scriptEng.operands[i] = keyPress[arrayVal].start ? 1 : 0; break;
                        case ScrVariable.VAR_MENU1SELECTION: scriptEng.operands[i] = gameMenu[0].selection1; break;
                        case ScrVariable.VAR_MENU2SELECTION: scriptEng.operands[i] = gameMenu[1].selection1; break;
                        case ScrVariable.VAR_TILELAYERXSIZE: scriptEng.operands[i] = stageLayouts[arrayVal].xsize; break;
                        case ScrVariable.VAR_TILELAYERYSIZE: scriptEng.operands[i] = stageLayouts[arrayVal].ysize; break;
                        case ScrVariable.VAR_TILELAYERTYPE: scriptEng.operands[i] = (int)stageLayouts[arrayVal].type; break;
                        case ScrVariable.VAR_TILELAYERANGLE: scriptEng.operands[i] = stageLayouts[arrayVal].angle; break;
                        case ScrVariable.VAR_TILELAYERXPOS: scriptEng.operands[i] = stageLayouts[arrayVal].XPos; break;
                        case ScrVariable.VAR_TILELAYERYPOS: scriptEng.operands[i] = stageLayouts[arrayVal].YPos; break;
                        case ScrVariable.VAR_TILELAYERZPOS: scriptEng.operands[i] = stageLayouts[arrayVal].ZPos; break;
                        case ScrVariable.VAR_TILELAYERPARALLAXFACTOR: scriptEng.operands[i] = stageLayouts[arrayVal].parallaxFactor; break;
                        case ScrVariable.VAR_TILELAYERSCROLLSPEED: scriptEng.operands[i] = stageLayouts[arrayVal].scrollSpeed; break;
                        case ScrVariable.VAR_TILELAYERSCROLLPOS: scriptEng.operands[i] = stageLayouts[arrayVal].scrollPos; break;
                        case ScrVariable.VAR_TILELAYERDEFORMATIONOFFSET: scriptEng.operands[i] = stageLayouts[arrayVal].deformationOffset; break;
                        case ScrVariable.VAR_TILELAYERDEFORMATIONOFFSETW: scriptEng.operands[i] = stageLayouts[arrayVal].deformationOffsetW; break;
                        case ScrVariable.VAR_HPARALLAXPARALLAXFACTOR: scriptEng.operands[i] = hParallax.parallaxFactor[arrayVal]; break;
                        case ScrVariable.VAR_HPARALLAXSCROLLSPEED: scriptEng.operands[i] = hParallax.scrollSpeed[arrayVal]; break;
                        case ScrVariable.VAR_HPARALLAXSCROLLPOS: scriptEng.operands[i] = hParallax.scrollPos[arrayVal]; break;
                        case ScrVariable.VAR_VPARALLAXPARALLAXFACTOR: scriptEng.operands[i] = vParallax.parallaxFactor[arrayVal]; break;
                        case ScrVariable.VAR_VPARALLAXSCROLLSPEED: scriptEng.operands[i] = vParallax.scrollSpeed[arrayVal]; break;
                        case ScrVariable.VAR_VPARALLAXSCROLLPOS: scriptEng.operands[i] = vParallax.scrollPos[arrayVal]; break;
                        case ScrVariable.VAR_3DSCENENOVERTICES: scriptEng.operands[i] = vertexCount; break;
                        case ScrVariable.VAR_3DSCENENOFACES: scriptEng.operands[i] = faceCount; break;
                        case ScrVariable.VAR_VERTEXBUFFERX: scriptEng.operands[i] = vertexBuffer[arrayVal].x; break;
                        case ScrVariable.VAR_VERTEXBUFFERY: scriptEng.operands[i] = vertexBuffer[arrayVal].y; break;
                        case ScrVariable.VAR_VERTEXBUFFERZ: scriptEng.operands[i] = vertexBuffer[arrayVal].z; break;
                        case ScrVariable.VAR_VERTEXBUFFERU: scriptEng.operands[i] = vertexBuffer[arrayVal].u; break;
                        case ScrVariable.VAR_VERTEXBUFFERV: scriptEng.operands[i] = vertexBuffer[arrayVal].v; break;
                        case ScrVariable.VAR_FACEBUFFERA: scriptEng.operands[i] = faceBuffer[arrayVal].a; break;
                        case ScrVariable.VAR_FACEBUFFERB: scriptEng.operands[i] = faceBuffer[arrayVal].b; break;
                        case ScrVariable.VAR_FACEBUFFERC: scriptEng.operands[i] = faceBuffer[arrayVal].c; break;
                        case ScrVariable.VAR_FACEBUFFERD: scriptEng.operands[i] = faceBuffer[arrayVal].d; break;
                        case ScrVariable.VAR_FACEBUFFERFLAG: scriptEng.operands[i] = (int)faceBuffer[arrayVal].flags; break;
                        case ScrVariable.VAR_FACEBUFFERCOLOR: scriptEng.operands[i] = faceBuffer[arrayVal].colour.ToArgb(); break;
                        case ScrVariable.VAR_3DSCENEPROJECTIONX: scriptEng.operands[i] = projectionX; break;
                        case ScrVariable.VAR_3DSCENEPROJECTIONY: scriptEng.operands[i] = projectionY; break;
                        case ScrVariable.VAR_ENGINESTATE: scriptEng.operands[i] = (int)gameMode; break;
                        case ScrVariable.VAR_STAGEDEBUGMODE: scriptEng.operands[i] = debugMode ? 1 : 0; break;
                        case ScrVariable.VAR_ENGINEMESSAGE: scriptEng.operands[i] = (int)Engine.message; break;
                        case ScrVariable.VAR_SAVERAM: scriptEng.operands[i] = saveRAM[arrayVal]; break;
                        case ScrVariable.VAR_ENGINELANGUAGE: scriptEng.operands[i] = (int)platform.language; break;
                        case ScrVariable.VAR_OBJECTSPRITESHEET: scriptEng.operands[i] = objectScriptList[objectEntityList[arrayVal].type].spriteSheetID; break;
                        case ScrVariable.VAR_ENGINEONLINEACTIVE: scriptEng.operands[i] = platform.onlineActive ? 1 : 0; break;
                        case ScrVariable.VAR_ENGINESFXVOLUME: scriptEng.operands[i] = sfxVolume; break;
                        case ScrVariable.VAR_ENGINEBGMVOLUME: scriptEng.operands[i] = bgmVolume; break;
                        case ScrVariable.VAR_ENGINEPLATFORMID: scriptEng.operands[i] = (int)GamePlatformTypes.Mobile; break;
                        case ScrVariable.VAR_ENGINETRIALMODE: scriptEng.operands[i] = Engine.trialMode ? 1 : 0; break;
                        case ScrVariable.VAR_KEYPRESSANYSTART: scriptEng.operands[i] = anyPress[arrayVal] ? 1 : 0; break;
                        case ScrVariable.VAR_ENGINEHAPTICSENABLED: scriptEng.operands[i] = Engine.hapticsEnabled ? 1 : 0; break;
                        case ScrVariable.VAR_DISABLETOUCHCONTROLS: scriptEng.operands[i] = GamePlatform == GamePlatformTypes.Mobile ? 0 : 1; break;
                    }
                }
                else if (opcodeType == ScriptVarTypes.SCRIPTVAR_INTCONST)
                { // int constant
                    scriptEng.operands[i] = scriptCode[scriptCodePtr++];
                }
                else if (opcodeType == ScriptVarTypes.SCRIPTVAR_STRCONST)
                { // string constant
                    int strLen = scriptCode[scriptCodePtr++];
                    var strChunk = new char[strLen];
                    for (int c = 0; c < strLen; ++c)
                    {
                        switch (c % 4)
                        {
                            case 0: strChunk[c] = (char)(byte)(scriptCode[scriptCodePtr] >> 24); break;

                            case 1: strChunk[c] = (char)(byte)((0xFFFFFF & scriptCode[scriptCodePtr]) >> 16); break;

                            case 2: strChunk[c] = (char)(byte)((0xFFFF & scriptCode[scriptCodePtr]) >> 8); break;

                            case 3: strChunk[c] = (char)(byte)scriptCode[scriptCodePtr++]; break;

                            default: break;
                        }
                    }
                    scriptText = new string(strChunk);
                    scriptCodePtr++;
                }
            }

            ObjectScript scriptInfo = objectScriptList[objectEntityList[objectLoop].type];
            RunFunction(
                ref running,
                ref scriptCodeStart,
                ref scriptCodePtr,
                ref jumpTableStart,
                ref numOps,
                scriptSub,
                opcode,
                scriptInfo,
                ref objectEntityList[objectLoop],
                ref playerList[activePlayer]
            );

            // Set Values
            if (numOps > 0)
                scriptCodePtr -= scriptCodePtr - scriptCodeOffset;
            for (int i = 0; i < numOps; ++i)
            {
                int opcodeType = scriptCode[scriptCodePtr++];
                if (opcodeType == (int)ScriptVarTypes.SCRIPTVAR_VAR)
                {
                    int arrayVal = 0;
                    switch ((ScriptVarArrTypes)scriptCode[scriptCodePtr++])
                    { // variable
                        case ScriptVarArrTypes.VARARR_NONE: arrayVal = objectLoop; break;
                        case ScriptVarArrTypes.VARARR_ARRAY:
                            if (scriptCode[scriptCodePtr++] == 1)
                                arrayVal = scriptEng.arrayPosition[scriptCode[scriptCodePtr++]];
                            else
                                arrayVal = scriptCode[scriptCodePtr++];
                            break;
                        case ScriptVarArrTypes.VARARR_ENTNOPLUS1:
                            if (scriptCode[scriptCodePtr++] == 1)
                                arrayVal = scriptEng.arrayPosition[scriptCode[scriptCodePtr++]] + objectLoop;
                            else
                                arrayVal = scriptCode[scriptCodePtr++] + objectLoop;
                            break;
                        case ScriptVarArrTypes.VARARR_ENTNOMINUS1:
                            if (scriptCode[scriptCodePtr++] == 1)
                                arrayVal = objectLoop - scriptEng.arrayPosition[scriptCode[scriptCodePtr++]];
                            else
                                arrayVal = objectLoop - scriptCode[scriptCodePtr++];
                            break;
                        default: break;
                    }

                    // Variables
                    switch ((ScrVariable)scriptCode[scriptCodePtr++])
                    {
                        default: break;
                        case ScrVariable.VAR_TEMPVALUE0: scriptEng.tempValue[0] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TEMPVALUE1: scriptEng.tempValue[1] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TEMPVALUE2: scriptEng.tempValue[2] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TEMPVALUE3: scriptEng.tempValue[3] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TEMPVALUE4: scriptEng.tempValue[4] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TEMPVALUE5: scriptEng.tempValue[5] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TEMPVALUE6: scriptEng.tempValue[6] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TEMPVALUE7: scriptEng.tempValue[7] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_CHECKRESULT: scriptEng.checkResult = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_ARRAYPOS0: scriptEng.arrayPosition[0] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_ARRAYPOS1: scriptEng.arrayPosition[1] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_GLOBAL: Engine.globalVariables[arrayVal] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_OBJECTTYPE:
                            {
                                objectEntityList[arrayVal].type = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTPROPERTYVALUE:
                            {
                                objectEntityList[arrayVal].propertyValue = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTXPOS:
                            {
                                objectEntityList[arrayVal].XPos = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTYPOS:
                            {
                                objectEntityList[arrayVal].YPos = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTIXPOS:
                            {
                                objectEntityList[arrayVal].XPos = WholeToFixedPoint(scriptEng.operands[i]);
                                break;
                            }
                        case ScrVariable.VAR_OBJECTIYPOS:
                            {
                                objectEntityList[arrayVal].YPos = WholeToFixedPoint(scriptEng.operands[i]);
                                break;
                            }
                        case ScrVariable.VAR_OBJECTSTATE:
                            {
                                objectEntityList[arrayVal].state = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTROTATION:
                            {
                                objectEntityList[arrayVal].rotation = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTSCALE:
                            {
                                objectEntityList[arrayVal].scale = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTPRIORITY:
                            {
                                objectEntityList[arrayVal].priority = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTDRAWORDER:
                            {
                                objectEntityList[arrayVal].drawOrder = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTDIRECTION:
                            {
                                objectEntityList[arrayVal].direction = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTINKEFFECT:
                            {
                                objectEntityList[arrayVal].inkEffect = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTALPHA:
                            {
                                objectEntityList[arrayVal].alpha = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTFRAME:
                            {
                                objectEntityList[arrayVal].frame = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTANIMATION:
                            {
                                objectEntityList[arrayVal].animation = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTPREVANIMATION:
                            {
                                objectEntityList[arrayVal].prevAnimation = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTANIMATIONSPEED:
                            {
                                objectEntityList[arrayVal].animationSpeed = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTANIMATIONTIMER:
                            {
                                objectEntityList[arrayVal].animationTimer = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTVALUE0:
                            {
                                objectEntityList[arrayVal].values[0] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTVALUE1:
                            {
                                objectEntityList[arrayVal].values[1] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTVALUE2:
                            {
                                objectEntityList[arrayVal].values[2] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTVALUE3:
                            {
                                objectEntityList[arrayVal].values[3] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTVALUE4:
                            {
                                objectEntityList[arrayVal].values[4] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTVALUE5:
                            {
                                objectEntityList[arrayVal].values[5] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTVALUE6:
                            {
                                objectEntityList[arrayVal].values[6] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_OBJECTVALUE7:
                            {
                                objectEntityList[arrayVal].values[7] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERSTATE:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].state = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERCONTROLMODE:
                            {
                                playerList[activePlayer].controlMode = (sbyte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERCONTROLLOCK:
                            {
                                playerList[activePlayer].controlLock = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERCOLLISIONMODE:
                            {
                                playerList[activePlayer].collisionMode = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERCOLLISIONPLANE:
                            {
                                playerList[activePlayer].collisionPlane = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERXPOS:
                            {
                                playerList[activePlayer].XPos = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERYPOS:
                            {
                                playerList[activePlayer].YPos = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERIXPOS:
                            {
                                playerList[activePlayer].XPos = WholeToFixedPoint(scriptEng.operands[i]);
                                break;
                            }
                        case ScrVariable.VAR_PLAYERIYPOS:
                            {
                                playerList[activePlayer].YPos = WholeToFixedPoint(scriptEng.operands[i]);
                                break;
                            }
                        case ScrVariable.VAR_PLAYERSCREENXPOS:
                            {
                                playerList[activePlayer].screenXPos = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERSCREENYPOS:
                            {
                                playerList[activePlayer].screenYPos = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERSPEED:
                            {
                                playerList[activePlayer].speed = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERXVELOCITY:
                            {
                                playerList[activePlayer].XVelocity = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERYVELOCITY:
                            {
                                playerList[activePlayer].YVelocity = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERGRAVITY:
                            {
                                playerList[activePlayer].gravity = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERANGLE:
                            {
                                playerList[activePlayer].angle = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERSKIDDING:
                            {
                                playerList[activePlayer].skidding = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERPUSHING:
                            {
                                playerList[activePlayer].pushing = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERTRACKSCROLL:
                            {
                                playerList[activePlayer].trackScroll = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERUP:
                            {
                                playerList[activePlayer].up = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERDOWN:
                            {
                                playerList[activePlayer].down = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERLEFT:
                            {
                                playerList[activePlayer].left = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERRIGHT:
                            {
                                playerList[activePlayer].right = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERJUMPPRESS:
                            {
                                playerList[activePlayer].jumpPress = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERJUMPHOLD:
                            {
                                playerList[activePlayer].jumpHold = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERFOLLOWPLAYER1:
                            {
                                playerList[activePlayer].followPlayer1 = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERLOOKPOS:
                            {
                                playerList[activePlayer].lookPos = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERWATER:
                            {
                                playerList[activePlayer].water = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERTOPSPEED:
                            {
                                playerList[activePlayer].topSpeed = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERACCELERATION:
                            {
                                playerList[activePlayer].acceleration = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERDECELERATION:
                            {
                                playerList[activePlayer].deceleration = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERAIRACCELERATION:
                            {
                                playerList[activePlayer].airAcceleration = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERAIRDECELERATION:
                            {
                                playerList[activePlayer].airDeceleration = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERGRAVITYSTRENGTH:
                            {
                                playerList[activePlayer].gravityStrength = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERJUMPSTRENGTH:
                            {
                                playerList[activePlayer].jumpStrength = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERJUMPCAP:
                            {
                                playerList[activePlayer].jumpCap = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERROLLINGACCELERATION:
                            {
                                playerList[activePlayer].rollingAcceleration = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERROLLINGDECELERATION:
                            {
                                playerList[activePlayer].rollingDeceleration = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERFLAILING:
                            {
                                playerList[activePlayer].flailing[arrayVal] = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERTIMER:
                            {
                                playerList[activePlayer].timer = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERTILECOLLISIONS:
                            {
                                playerList[activePlayer].tileCollisions = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYEROBJECTINTERACTION:
                            {
                                playerList[activePlayer].objectInteractions = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVISIBLE:
                            {
                                playerList[activePlayer].visible = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERROTATION:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].rotation = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERSCALE:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].scale = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERPRIORITY:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].priority = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERDRAWORDER:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].drawOrder = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERDIRECTION:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].direction = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERINKEFFECT:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].inkEffect = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERALPHA:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].alpha = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERFRAME:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].frame = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERANIMATION:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].animation = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERPREVANIMATION:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].prevAnimation = (byte)scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERANIMATIONSPEED:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].animationSpeed = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERANIMATIONTIMER:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].animationTimer = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE0:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].values[0] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE1:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].values[1] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE2:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].values[2] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE3:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].values[3] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE4:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].values[4] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE5:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].values[5] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE6:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].values[6] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE7:
                            {
                                objectEntityList[playerList[activePlayer].boundEntity].values[7] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE8:
                            {
                                playerList[activePlayer].values[0] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE9:
                            {
                                playerList[activePlayer].values[1] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE10:
                            {
                                playerList[activePlayer].values[2] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE11:
                            {
                                playerList[activePlayer].values[3] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE12:
                            {
                                playerList[activePlayer].values[4] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE13:
                            {
                                playerList[activePlayer].values[5] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE14:
                            {
                                playerList[activePlayer].values[6] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_PLAYERVALUE15:
                            {
                                playerList[activePlayer].values[7] = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_STAGESTATE: stageMode = (StageModes)scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEACTIVELIST: activeStageList = (StageListNames)scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGELISTPOS: stageListPosition = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGETIMEENABLED: timeEnabled = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_STAGEMILLISECONDS: stageMilliseconds = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGESECONDS: stageSeconds = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEMINUTES: stageMinutes = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEACTNO: actID = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEPAUSEENABLED: pauseEnabled = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_STAGENEWXBOUNDARY1: newXBoundary1 = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGENEWXBOUNDARY2: newXBoundary2 = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGENEWYBOUNDARY1: newYBoundary1 = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGENEWYBOUNDARY2: newYBoundary2 = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEXBOUNDARY1:
                            if (xBoundary1 != scriptEng.operands[i])
                            {
                                xBoundary1 = scriptEng.operands[i];
                                newXBoundary1 = scriptEng.operands[i];
                            }
                            break;
                        case ScrVariable.VAR_STAGEXBOUNDARY2:
                            if (xBoundary2 != scriptEng.operands[i])
                            {
                                xBoundary2 = scriptEng.operands[i];
                                newXBoundary2 = scriptEng.operands[i];
                            }
                            break;
                        case ScrVariable.VAR_STAGEYBOUNDARY1:
                            if (yBoundary1 != scriptEng.operands[i])
                            {
                                yBoundary1 = scriptEng.operands[i];
                                newYBoundary1 = scriptEng.operands[i];
                            }
                            break;
                        case ScrVariable.VAR_STAGEYBOUNDARY2:
                            if (yBoundary2 != scriptEng.operands[i])
                            {
                                yBoundary2 = scriptEng.operands[i];
                                newYBoundary2 = scriptEng.operands[i];
                            }
                            break;
                        case ScrVariable.VAR_STAGEDEFORMATIONDATA0: bgDeformationData0[arrayVal] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEDEFORMATIONDATA1: bgDeformationData1[arrayVal] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEDEFORMATIONDATA2: bgDeformationData2[arrayVal] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEDEFORMATIONDATA3: bgDeformationData3[arrayVal] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEWATERLEVEL: waterLevel = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEACTIVELAYER: activeTileLayers[arrayVal] = (byte)scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEMIDPOINT: tLayerMidPoint = (byte)scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEPLAYERLISTPOS: playerListPos = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEACTIVEPLAYER:
                            activePlayer = scriptEng.operands[i];
                            if (activePlayer > activePlayerCount)
                                activePlayer = 0;
                            break;
                        case ScrVariable.VAR_SCREENCAMERAENABLED: cameraEnabled = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_SCREENCAMERATARGET: cameraTarget = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_SCREENCAMERASTYLE: cameraStyle = (CameraStyles)scriptEng.operands[i]; break;
                        case ScrVariable.VAR_SCREENDRAWLISTSIZE: drawListEntries[arrayVal].listSize = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_SCREENXOFFSET:
                            xScrollOffset = scriptEng.operands[i];
                            xScrollA = xScrollOffset;
                            xScrollB = SCREEN_XSIZE + xScrollOffset;
                            break;
                        case ScrVariable.VAR_SCREENYOFFSET:
                            yScrollOffsetPixels = scriptEng.operands[i];
                            yScrollA = yScrollOffsetPixels;
                            yScrollB = SCREEN_YSIZE + yScrollOffsetPixels;
                            break;
                        case ScrVariable.VAR_SCREENSHAKEX: cameraShakeX = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_SCREENSHAKEY: cameraShakeY = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_SCREENADJUSTCAMERAY: cameraAdjustY = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_MUSICVOLUME: SetMusicVolume(scriptEng.operands[i]); break;
                        case ScrVariable.VAR_KEYDOWNUP: keyDown[arrayVal].up = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYDOWNDOWN: keyDown[arrayVal].down = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYDOWNLEFT: keyDown[arrayVal].left = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYDOWNRIGHT: keyDown[arrayVal].right = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYDOWNBUTTONA: keyDown[arrayVal].A = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYDOWNBUTTONB: keyDown[arrayVal].B = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYDOWNBUTTONC: keyDown[arrayVal].C = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYDOWNSTART: keyDown[arrayVal].start = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYPRESSUP: keyPress[arrayVal].up = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYPRESSDOWN: keyPress[arrayVal].down = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYPRESSLEFT: keyPress[arrayVal].left = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYPRESSRIGHT: keyPress[arrayVal].right = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYPRESSBUTTONA: keyPress[arrayVal].A = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYPRESSBUTTONB: keyPress[arrayVal].B = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYPRESSBUTTONC: keyPress[arrayVal].C = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_KEYPRESSSTART: keyPress[arrayVal].start = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_MENU1SELECTION: gameMenu[0].selection1 = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_MENU2SELECTION: gameMenu[1].selection1 = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TILELAYERXSIZE: stageLayouts[arrayVal].xsize = (byte)scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TILELAYERYSIZE: stageLayouts[arrayVal].ysize = (byte)scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TILELAYERTYPE: stageLayouts[arrayVal].type = (TileLayerTypes)scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TILELAYERANGLE:
                            stageLayouts[arrayVal].angle = scriptEng.operands[i];
                            if (stageLayouts[arrayVal].angle < 0)
                                stageLayouts[arrayVal].angle += 0x200;
                            stageLayouts[arrayVal].angle &= 0x1FF;
                            break;
                        case ScrVariable.VAR_TILELAYERXPOS: stageLayouts[arrayVal].XPos = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TILELAYERYPOS: stageLayouts[arrayVal].YPos = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TILELAYERZPOS: stageLayouts[arrayVal].ZPos = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TILELAYERPARALLAXFACTOR: stageLayouts[arrayVal].parallaxFactor = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TILELAYERSCROLLSPEED: stageLayouts[arrayVal].scrollSpeed = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TILELAYERSCROLLPOS: stageLayouts[arrayVal].scrollPos = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_TILELAYERDEFORMATIONOFFSET:
                            stageLayouts[arrayVal].deformationOffset = scriptEng.operands[i];
                            stageLayouts[arrayVal].deformationOffset &= 0xFF;
                            break;
                        case ScrVariable.VAR_TILELAYERDEFORMATIONOFFSETW:
                            stageLayouts[arrayVal].deformationOffsetW = scriptEng.operands[i];
                            stageLayouts[arrayVal].deformationOffsetW &= 0xFF;
                            break;
                        case ScrVariable.VAR_HPARALLAXPARALLAXFACTOR: hParallax.parallaxFactor[arrayVal] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_HPARALLAXSCROLLSPEED: hParallax.scrollSpeed[arrayVal] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_HPARALLAXSCROLLPOS: hParallax.scrollPos[arrayVal] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_VPARALLAXPARALLAXFACTOR: vParallax.parallaxFactor[arrayVal] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_VPARALLAXSCROLLSPEED: vParallax.scrollSpeed[arrayVal] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_VPARALLAXSCROLLPOS: vParallax.scrollPos[arrayVal] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_3DSCENENOVERTICES: vertexCount = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_3DSCENENOFACES: faceCount = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_VERTEXBUFFERX: vertexBuffer[arrayVal].x = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_VERTEXBUFFERY: vertexBuffer[arrayVal].y = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_VERTEXBUFFERZ: vertexBuffer[arrayVal].z = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_VERTEXBUFFERU: vertexBuffer[arrayVal].u = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_VERTEXBUFFERV: vertexBuffer[arrayVal].v = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_FACEBUFFERA: faceBuffer[arrayVal].a = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_FACEBUFFERB: faceBuffer[arrayVal].b = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_FACEBUFFERC: faceBuffer[arrayVal].c = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_FACEBUFFERD: faceBuffer[arrayVal].d = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_FACEBUFFERFLAG: faceBuffer[arrayVal].flags = (FaceFlags)scriptEng.operands[i]; break;
                        case ScrVariable.VAR_FACEBUFFERCOLOR: faceBuffer[arrayVal].colour = Color.FromArgb(scriptEng.operands[i]); break;
                        case ScrVariable.VAR_3DSCENEPROJECTIONX: projectionX = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_3DSCENEPROJECTIONY: projectionY = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_ENGINESTATE: gameMode = (EngineStates)scriptEng.operands[i]; break;
                        case ScrVariable.VAR_STAGEDEBUGMODE: debugMode = scriptEng.operands[i] != 0; break;
                        case ScrVariable.VAR_SAVERAM: saveRAM[arrayVal] = scriptEng.operands[i]; break;
                        case ScrVariable.VAR_ENGINELANGUAGE: platform.language = (EngineLanguages)scriptEng.operands[i]; break;
                        case ScrVariable.VAR_OBJECTSPRITESHEET:
                            {
                                objectScriptList[objectEntityList[arrayVal].type].spriteSheetID = scriptEng.operands[i];
                                break;
                            }
                        case ScrVariable.VAR_ENGINESFXVOLUME:
                            sfxVolume = scriptEng.operands[i];
                            if (sfxVolume < 0)
                                sfxVolume = 0;
                            if (sfxVolume > MAX_VOLUME)
                                sfxVolume = MAX_VOLUME;
                            break;
                        case ScrVariable.VAR_ENGINEBGMVOLUME:
                            bgmVolume = scriptEng.operands[i];
                            if (bgmVolume < 0)
                                bgmVolume = 0;
                            if (bgmVolume > MAX_VOLUME)
                                bgmVolume = MAX_VOLUME;
                            break;
                        case ScrVariable.VAR_ENGINEHAPTICSENABLED: Engine.hapticsEnabled = scriptEng.operands[i] != 0; break;
                    }
                }
                else if ((ScriptVarTypes)opcodeType == ScriptVarTypes.SCRIPTVAR_INTCONST)
                { // int constant
                    scriptCodePtr++;
                }
                else if ((ScriptVarTypes)opcodeType == ScriptVarTypes.SCRIPTVAR_STRCONST)
                { // string constant
                    int strLen = scriptCode[scriptCodePtr++];
                    for (int c = 0; c < strLen; ++c)
                    {
                        switch (c % 4)
                        {
                            case 3: ++scriptCodePtr; break;
                            default: break;
                        }
                    }
                    scriptCodePtr++;
                }
            }
        }
    }

    static void RunFunction(
        ref bool running,
        ref int scriptCodeStart,
        ref int scriptCodePtr,
        ref int jumpTableStart,
        ref int numOps,
        ScriptSubs scriptSub,
        int opcode,
        ObjectScript scriptInfo,
        ref Entity entity,
        ref Player player
    )
    {
        Entity tempent;
        Player tempplayer;
        SpriteFrame spriteFrame;
        var chunkY = 0;

        // Functions
        switch ((ScrFunction)opcode)
        {
            default: break;
            case ScrFunction.FUNC_END: running = false; break;
            case ScrFunction.FUNC_EQUAL: scriptEng.operands[0] = scriptEng.operands[1]; break;
            case ScrFunction.FUNC_ADD: scriptEng.operands[0] += scriptEng.operands[1]; break;
            case ScrFunction.FUNC_SUB: scriptEng.operands[0] -= scriptEng.operands[1]; break;
            case ScrFunction.FUNC_INC: ++scriptEng.operands[0]; break;
            case ScrFunction.FUNC_DEC: --scriptEng.operands[0]; break;
            case ScrFunction.FUNC_MUL: scriptEng.operands[0] *= scriptEng.operands[1]; break;
            case ScrFunction.FUNC_DIV: scriptEng.operands[0] /= scriptEng.operands[1]; break;
            case ScrFunction.FUNC_SHR: scriptEng.operands[0] >>= scriptEng.operands[1]; break;
            case ScrFunction.FUNC_SHL: scriptEng.operands[0] <<= scriptEng.operands[1]; break;
            case ScrFunction.FUNC_AND: scriptEng.operands[0] &= scriptEng.operands[1]; break;
            case ScrFunction.FUNC_OR: scriptEng.operands[0] |= scriptEng.operands[1]; break;
            case ScrFunction.FUNC_XOR: scriptEng.operands[0] ^= scriptEng.operands[1]; break;
            case ScrFunction.FUNC_MOD: scriptEng.operands[0] %= scriptEng.operands[1]; break;
            case ScrFunction.FUNC_FLIPSIGN: scriptEng.operands[0] = -scriptEng.operands[0]; break;
            case ScrFunction.FUNC_CHECKEQUAL:
                scriptEng.checkResult = (scriptEng.operands[0] == scriptEng.operands[1]) ? 1 : 0;
                numOps = 0;
                break;
            case ScrFunction.FUNC_CHECKGREATER:
                scriptEng.checkResult = (scriptEng.operands[0] > scriptEng.operands[1]) ? 1 : 0;
                numOps = 0;
                break;
            case ScrFunction.FUNC_CHECKLOWER:
                scriptEng.checkResult = (scriptEng.operands[0] < scriptEng.operands[1]) ? 1 : 0;
                numOps = 0;
                break;
            case ScrFunction.FUNC_CHECKNOTEQUAL:
                scriptEng.checkResult = (scriptEng.operands[0] != scriptEng.operands[1]) ? 1 : 0;
                numOps = 0;
                break;
            case ScrFunction.FUNC_IFEQUAL:
                if (scriptEng.operands[1] != scriptEng.operands[2])
                    scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + scriptEng.operands[0]];
                jumpTableStack.Push(scriptEng.operands[0]);
                numOps = 0;
                break;
            case ScrFunction.FUNC_IFGREATER:
                if (scriptEng.operands[1] <= scriptEng.operands[2])
                    scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + scriptEng.operands[0]];
                jumpTableStack.Push(scriptEng.operands[0]);
                numOps = 0;
                break;
            case ScrFunction.FUNC_IFGREATEROREQUAL:
                if (scriptEng.operands[1] < scriptEng.operands[2])
                    scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + scriptEng.operands[0]];
                jumpTableStack.Push(scriptEng.operands[0]);
                numOps = 0;
                break;
            case ScrFunction.FUNC_IFLOWER:
                if (scriptEng.operands[1] >= scriptEng.operands[2])
                    scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + scriptEng.operands[0]];
                jumpTableStack.Push(scriptEng.operands[0]);
                numOps = 0;
                break;
            case ScrFunction.FUNC_IFLOWEROREQUAL:
                if (scriptEng.operands[1] > scriptEng.operands[2])
                    scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + scriptEng.operands[0]];
                jumpTableStack.Push(scriptEng.operands[0]);
                numOps = 0;
                break;
            case ScrFunction.FUNC_IFNOTEQUAL:
                if (scriptEng.operands[1] == scriptEng.operands[2])
                    scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + scriptEng.operands[0]];
                jumpTableStack.Push(scriptEng.operands[0]);
                numOps = 0;
                break;
            case ScrFunction.FUNC_ELSE:
                numOps = 0;
                scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + jumpTableStack.Pop() + 1];
                break;
            case ScrFunction.FUNC_ENDIF:
                numOps = 0;
                _ = jumpTableStack.Pop();
                break;
            case ScrFunction.FUNC_WEQUAL:
                if (scriptEng.operands[1] != scriptEng.operands[2])
                    scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + scriptEng.operands[0] + 1];
                else
                    jumpTableStack.Push(scriptEng.operands[0]);
                numOps = 0;
                break;
            case ScrFunction.FUNC_WGREATER:
                if (scriptEng.operands[1] <= scriptEng.operands[2])
                    scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + scriptEng.operands[0] + 1];
                else
                    jumpTableStack.Push(scriptEng.operands[0]);
                numOps = 0;
                break;
            case ScrFunction.FUNC_WGREATEROREQUAL:
                if (scriptEng.operands[1] < scriptEng.operands[2])
                    scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + scriptEng.operands[0] + 1];
                else
                    jumpTableStack.Push(scriptEng.operands[0]);
                numOps = 0;
                break;
            case ScrFunction.FUNC_WLOWER:
                if (scriptEng.operands[1] >= scriptEng.operands[2])
                    scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + scriptEng.operands[0] + 1];
                else
                    jumpTableStack.Push(scriptEng.operands[0]);
                numOps = 0;
                break;
            case ScrFunction.FUNC_WLOWEROREQUAL:
                if (scriptEng.operands[1] > scriptEng.operands[2])
                    scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + scriptEng.operands[0] + 1];
                else
                    jumpTableStack.Push(scriptEng.operands[0]);
                numOps = 0;
                break;
            case ScrFunction.FUNC_WNOTEQUAL:
                if (scriptEng.operands[1] == scriptEng.operands[2])
                    scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + scriptEng.operands[0] + 1];
                else
                    jumpTableStack.Push(scriptEng.operands[0]);
                numOps = 0;
                break;
            case ScrFunction.FUNC_LOOP:
                numOps = 0;
                scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + jumpTableStack.Pop()];
                break;
            case ScrFunction.FUNC_SWITCH:
                jumpTableStack.Push(scriptEng.operands[0]);
                if (scriptEng.operands[1] < jumpTable[jumpTableStart + scriptEng.operands[0]]
                    || scriptEng.operands[1] > jumpTable[jumpTableStart + scriptEng.operands[0] + 1])
                    scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + scriptEng.operands[0] + 2];
                else
                    scriptCodePtr = scriptCodeStart
                                    + jumpTable[jumpTableStart + scriptEng.operands[0] + 4
                                                    + (scriptEng.operands[1] - jumpTable[jumpTableStart + scriptEng.operands[0]])];
                numOps = 0;
                break;
            case ScrFunction.FUNC_BREAK:
                numOps = 0;
                scriptCodePtr = scriptCodeStart + jumpTable[jumpTableStart + jumpTableStack.Pop() + 3];
                break;
            case ScrFunction.FUNC_ENDSWITCH:
                numOps = 0;
                _ = jumpTableStack.Pop();
                break;
            case ScrFunction.FUNC_RAND: scriptEng.operands[0] = Random.Shared.Next() % scriptEng.operands[1]; break;
            case ScrFunction.FUNC_SIN: scriptEng.operands[0] = Sin512(scriptEng.operands[1]); break;
            case ScrFunction.FUNC_COS: scriptEng.operands[0] = Cos512(scriptEng.operands[1]); break;
            case ScrFunction.FUNC_SIN256: scriptEng.operands[0] = Sin256(scriptEng.operands[1]); break;
            case ScrFunction.FUNC_COS256: scriptEng.operands[0] = Cos256(scriptEng.operands[1]); break;
            case ScrFunction.FUNC_SINCHANGE: scriptEng.operands[0] = scriptEng.operands[3] + (Sin512(scriptEng.operands[1]) >> scriptEng.operands[2]) - scriptEng.operands[4]; break;
            case ScrFunction.FUNC_COSCHANGE: scriptEng.operands[0] = scriptEng.operands[3] + (Cos512(scriptEng.operands[1]) >> scriptEng.operands[2]) - scriptEng.operands[4]; break;
            case ScrFunction.FUNC_ATAN2: scriptEng.operands[0] = ArcTanLookup(scriptEng.operands[1], scriptEng.operands[2]); break;
            case ScrFunction.FUNC_INTERPOLATE: scriptEng.operands[0] = (scriptEng.operands[2] * (0x100 - scriptEng.operands[3]) + scriptEng.operands[3] * scriptEng.operands[1]) >> 8; break;
            case ScrFunction.FUNC_INTERPOLATEXY:
                scriptEng.operands[0] =
                    (scriptEng.operands[3] * (0x100 - scriptEng.operands[6]) >> 8) + ((scriptEng.operands[6] * scriptEng.operands[2]) >> 8);
                scriptEng.operands[1] =
                    (scriptEng.operands[5] * (0x100 - scriptEng.operands[6]) >> 8) + (scriptEng.operands[6] * scriptEng.operands[4] >> 8);
                break;
            case ScrFunction.FUNC_LOADSPRITESHEET:
                numOps = 0;
                objectScriptList[objectEntityList[objectLoop].type].spriteSheetID = Sprite.Add(scriptText);
                break;
            case ScrFunction.FUNC_REMOVESPRITESHEET:
                numOps = 0;
                Sprite.Remove(scriptText);
                break;
            case ScrFunction.FUNC_DRAWSPRITE:
                numOps = 0;
                spriteFrame = scriptFrames[scriptInfo.frameListOffset + scriptEng.operands[0]];
                DrawSprite(FixedPointToWhole(entity.XPos) - xScrollOffset + spriteFrame.pivotX,
                           FixedPointToWhole(entity.YPos) - yScrollOffsetPixels + spriteFrame.pivotY,
                           spriteFrame.width,
                           spriteFrame.height,
                           spriteFrame.sprX,
                           spriteFrame.sprY,
                           scriptInfo.spriteSheetID);
                break;
            case ScrFunction.FUNC_DRAWSPRITEXY:
                numOps = 0;
                spriteFrame = scriptFrames[scriptInfo.frameListOffset + scriptEng.operands[0]];
                DrawSprite(FixedPointToWhole(scriptEng.operands[1]) - xScrollOffset + spriteFrame.pivotX,
                           FixedPointToWhole(scriptEng.operands[2]) - yScrollOffsetPixels + spriteFrame.pivotY,
                           spriteFrame.width,
                           spriteFrame.height,
                           spriteFrame.sprX,
                           spriteFrame.sprY,
                           scriptInfo.spriteSheetID
                );
                break;
            case ScrFunction.FUNC_DRAWSPRITESCREENXY:
                numOps = 0;
                spriteFrame = scriptFrames[scriptInfo.frameListOffset + scriptEng.operands[0]];
                DrawSprite(scriptEng.operands[1] + spriteFrame.pivotX, scriptEng.operands[2] + spriteFrame.pivotY, spriteFrame.width,
                           spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, scriptInfo.spriteSheetID);
                break;
            case ScrFunction.FUNC_DRAWTINTRECT:
                numOps = 0;
                DrawTintRectangle(scriptEng.operands[0], scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]);
                break;
            case ScrFunction.FUNC_DRAWNUMBERS:
                {
                    numOps = 0;
                    int i = 10;
                    if (scriptEng.operands[6] != 0)
                    {
                        while (scriptEng.operands[4] > 0)
                        {
                            int frameID = scriptEng.operands[3] % i / (i / 10) + scriptEng.operands[0];
                            spriteFrame = scriptFrames[scriptInfo.frameListOffset + frameID];
                            DrawSprite(spriteFrame.pivotX + scriptEng.operands[1], spriteFrame.pivotY + scriptEng.operands[2], spriteFrame.width,
                                       spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, scriptInfo.spriteSheetID);
                            scriptEng.operands[1] -= scriptEng.operands[5];
                            i *= 10;
                            --scriptEng.operands[4];
                        }
                    }
                    else
                    {
                        int extra = 10;
                        if (scriptEng.operands[3] != 0)
                            extra = 10 * scriptEng.operands[3];
                        while (scriptEng.operands[4] > 0)
                        {
                            if (extra >= i)
                            {
                                int frameID = scriptEng.operands[3] % i / (i / 10) + scriptEng.operands[0];
                                spriteFrame = scriptFrames[scriptInfo.frameListOffset + frameID];
                                DrawSprite(spriteFrame.pivotX + scriptEng.operands[1], spriteFrame.pivotY + scriptEng.operands[2], spriteFrame.width,
                                           spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, scriptInfo.spriteSheetID);
                            }
                            scriptEng.operands[1] -= scriptEng.operands[5];
                            i *= 10;
                            --scriptEng.operands[4];
                        }
                    }
                    break;
                }
            case ScrFunction.FUNC_DRAWACTNAME:
                {
                    numOps = 0;
                    int charID = 0;

                    switch (scriptEng.operands[3])
                    {
                        default: break;

                        case 1: // Draw Word 1
                            charID = 0;

                            // Draw the first letter as a capital letter, the rest are lowercase (if scriptEng.operands[4] is true, otherwise they're all
                            // uppercase)
                            if (scriptEng.operands[4] == 1 && titleCardText[charID] != 0)
                            {
                                int character = titleCardText[charID];
                                if (character == ' ')
                                    character = 0;
                                if (character == '-')
                                    character = 0;
                                if (character >= '0' && character <= '9')
                                    character -= 22;
                                if (character > '9' && character < 'f')
                                    character -= 'A';

                                if (character <= -1)
                                {
                                    scriptEng.operands[1] += scriptEng.operands[5] + scriptEng.operands[6]; // spaceWidth + spacing
                                }
                                else
                                {
                                    character += scriptEng.operands[0];
                                    spriteFrame = scriptFrames[scriptInfo.frameListOffset + character];
                                    DrawSprite(scriptEng.operands[1] + spriteFrame.pivotX, scriptEng.operands[2] + spriteFrame.pivotY,
                                               spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, scriptInfo.spriteSheetID);
                                    scriptEng.operands[1] += spriteFrame.width + scriptEng.operands[6];
                                }

                                scriptEng.operands[0] += 26;
                                charID++;
                            }

                            while (titleCardText[charID] != 0 && titleCardText[charID] != '-')
                            {
                                int character = titleCardText[charID];
                                if (character == ' ')
                                    character = 0;
                                if (character == '-')
                                    character = 0;
                                if (character > '/' && character < ':')
                                    character -= 22;
                                if (character > '9' && character < 'f')
                                    character -= 'A';

                                if (character <= -1)
                                {
                                    scriptEng.operands[1] += scriptEng.operands[5] + scriptEng.operands[6]; // spaceWidth + spacing
                                }
                                else
                                {
                                    character += scriptEng.operands[0];
                                    spriteFrame = scriptFrames[scriptInfo.frameListOffset + character];
                                    DrawSprite(scriptEng.operands[1] + spriteFrame.pivotX, scriptEng.operands[2] + spriteFrame.pivotY,
                                               spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, scriptInfo.spriteSheetID);
                                    scriptEng.operands[1] += spriteFrame.width + scriptEng.operands[6];
                                }
                                charID++;
                            }
                            break;

                        case 2: // Draw Word 2
                            charID = titleCardWord2;

                            // Draw the first letter as a capital letter, the rest are lowercase (if scriptEng.operands[4] is true, otherwise they're all
                            // uppercase)
                            if (scriptEng.operands[4] == 1 && titleCardText[charID] != 0)
                            {
                                int character = titleCardText[charID];
                                if (character == ' ')
                                    character = 0;
                                if (character == '-')
                                    character = 0;
                                if (character >= '0' && character <= '9')
                                    character -= 22;
                                if (character > '9' && character < 'f')
                                    character -= 'A';

                                if (character <= -1)
                                {
                                    scriptEng.operands[1] += scriptEng.operands[5] + scriptEng.operands[6]; // spaceWidth + spacing
                                }
                                else
                                {
                                    character += scriptEng.operands[0];
                                    spriteFrame = scriptFrames[scriptInfo.frameListOffset + character];
                                    DrawSprite(scriptEng.operands[1] + spriteFrame.pivotX, scriptEng.operands[2] + spriteFrame.pivotY,
                                               spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, scriptInfo.spriteSheetID);
                                    scriptEng.operands[1] += spriteFrame.width + scriptEng.operands[6];
                                }
                                scriptEng.operands[0] += 26;
                                charID++;
                            }

                            while (titleCardText[charID] != 0)
                            {
                                int character = titleCardText[charID];
                                if (character == ' ')
                                    character = -1;
                                if (character == '-')
                                    character = 0;
                                if (character >= '0' && character <= '9')
                                    character -= 22;
                                if (character > '9' && character < 'f')
                                    character -= 'A';

                                if (character <= -1)
                                {
                                    scriptEng.operands[1] += scriptEng.operands[5] + scriptEng.operands[6]; // spaceWidth + spacing
                                }
                                else
                                {
                                    character += scriptEng.operands[0];
                                    spriteFrame = scriptFrames[scriptInfo.frameListOffset + character];
                                    DrawSprite(scriptEng.operands[1] + spriteFrame.pivotX, scriptEng.operands[2] + spriteFrame.pivotY,
                                               spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, scriptInfo.spriteSheetID);
                                    scriptEng.operands[1] += spriteFrame.width + scriptEng.operands[6];
                                }
                                charID++;
                            }
                            break;
                    }
                    break;
                }
            case ScrFunction.FUNC_DRAWMENU:
                numOps = 0;
                break;
            case ScrFunction.FUNC_SPRITEFRAME:
                numOps = 0;
                if (scriptSub == ScriptSubs.SUB_SETUP && scriptFrameCount < SPRITEFRAME_COUNT)
                {
                    scriptFrames[scriptFrameCount].pivotX = scriptEng.operands[0];
                    scriptFrames[scriptFrameCount].pivotY = scriptEng.operands[1];
                    scriptFrames[scriptFrameCount].width = scriptEng.operands[2];
                    scriptFrames[scriptFrameCount].height = scriptEng.operands[3];
                    scriptFrames[scriptFrameCount].sprX = scriptEng.operands[4];
                    scriptFrames[scriptFrameCount].sprY = scriptEng.operands[5];
                    ++scriptFrameCount;
                }
                break;
            case ScrFunction.FUNC_EDITFRAME:
                {
                    numOps = 0;
                    fixed (SpriteFrame* spriteFramePtr = &scriptFrames[scriptInfo.frameListOffset + scriptEng.operands[0]])
                    {
                        spriteFramePtr->pivotX = scriptEng.operands[1];
                        spriteFramePtr->pivotY = scriptEng.operands[2];
                        spriteFramePtr->width = scriptEng.operands[3];
                        spriteFramePtr->height = scriptEng.operands[4];
                        spriteFramePtr->sprX = scriptEng.operands[5];
                        spriteFramePtr->sprY = scriptEng.operands[6];

                        spriteFrame = *spriteFramePtr;
                    }
                }
                break;
            case ScrFunction.FUNC_LOADPALETTE:
                numOps = 0;
                LoadPalette(scriptText, scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3], scriptEng.operands[4]);
                break;
            case ScrFunction.FUNC_ROTATEPALETTE:
                numOps = 0;
                RotatePalette((byte)scriptEng.operands[0], (byte)scriptEng.operands[1], scriptEng.operands[2] != 0);
                break;
            case ScrFunction.FUNC_SETSCREENFADE:
                numOps = 0;
                SetFade((byte)scriptEng.operands[0], (byte)scriptEng.operands[1], (byte)scriptEng.operands[2], (ushort)scriptEng.operands[3]);
                break;
            case ScrFunction.FUNC_SETACTIVEPALETTE:
                numOps = 0;
                SetActivePalette((byte)scriptEng.operands[0], scriptEng.operands[1], scriptEng.operands[2]);
                break;
            case ScrFunction.FUNC_SETPALETTEFADE:
                numOps = 0;
                SetLimitedFade((byte)scriptEng.operands[0], (byte)scriptEng.operands[1], (byte)scriptEng.operands[2], (byte)scriptEng.operands[3], (ushort)scriptEng.operands[4],
                               scriptEng.operands[5], scriptEng.operands[6]);
                break;
            case ScrFunction.FUNC_COPYPALETTE:
                numOps = 0;
                CopyPalette((byte)scriptEng.operands[0], (byte)scriptEng.operands[1]);
                break;
            case ScrFunction.FUNC_CLEARSCREEN:
                numOps = 0;
                ClearScreen((byte)scriptEng.operands[0]);
                break;
            case ScrFunction.FUNC_DRAWSPRITEFX:
                numOps = 0;
                spriteFrame = scriptFrames[scriptInfo.frameListOffset + scriptEng.operands[0]];
                switch ((DrawFXFlags)scriptEng.operands[1])
                {
                    default: break;
                    case DrawFXFlags.FX_SCALE:
                        DrawSpriteScaled((FlipFlags)entity.direction,
                                         FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset,
                                         FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels, -spriteFrame.pivotX, -spriteFrame.pivotY,
                                         entity.scale, spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY,
                                         scriptInfo.spriteSheetID);
                        break;
                    case DrawFXFlags.FX_ROTATE:
                        DrawSpriteRotated((FlipFlags)entity.direction, FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset,
                                          FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels, -spriteFrame.pivotX, -spriteFrame.pivotY,
                                          spriteFrame.sprX, spriteFrame.sprY, spriteFrame.width, spriteFrame.height, entity.rotation,
                                          scriptInfo.spriteSheetID);
                        break;
                    case DrawFXFlags.FX_ROTOZOOM:
                        DrawSpriteRotozoom((FlipFlags)entity.direction, FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset,
                                           FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels, -spriteFrame.pivotX, -spriteFrame.pivotY,
                                           spriteFrame.sprX, spriteFrame.sprY, spriteFrame.width, spriteFrame.height, entity.rotation,
                                           entity.scale, scriptInfo.spriteSheetID);
                        break;
                    case DrawFXFlags.FX_INK:
                        switch ((InkFlags)entity.inkEffect)
                        {
                            case InkFlags.INK_NONE:
                                DrawSprite(FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset + spriteFrame.pivotX,
                                           FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels + spriteFrame.pivotY, spriteFrame.width,
                                           spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, scriptInfo.spriteSheetID);
                                break;
                            case InkFlags.INK_BLEND:
                                DrawBlendedSprite(FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset + spriteFrame.pivotX,
                                                  FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels + spriteFrame.pivotY, spriteFrame.width,
                                                  spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, scriptInfo.spriteSheetID);
                                break;
                            case InkFlags.INK_ALPHA:
                                DrawAlphaBlendedSprite(FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset + spriteFrame.pivotX,
                                                       FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels + spriteFrame.pivotY, spriteFrame.width,
                                                       spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, entity.alpha,
                                                       scriptInfo.spriteSheetID);
                                break;
                            case InkFlags.INK_ADD:
                                DrawAdditiveBlendedSprite(FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset + spriteFrame.pivotX,
                                                          FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels + spriteFrame.pivotY, spriteFrame.width,
                                                          spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, entity.alpha,
                                                          scriptInfo.spriteSheetID);
                                break;
                            case InkFlags.INK_SUB:
                                DrawSubtractiveBlendedSprite(FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset + spriteFrame.pivotX,
                                                             FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels + spriteFrame.pivotY, spriteFrame.width,
                                                             spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, entity.alpha,
                                                             scriptInfo.spriteSheetID);
                                break;
                        }
                        break;
                    case DrawFXFlags.FX_TINT:
                        if ((InkFlags)entity.inkEffect == InkFlags.INK_ALPHA)
                        {
                            DrawScaledTintMask(entity.direction, FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset,
                                               FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels, -spriteFrame.pivotX, -spriteFrame.pivotY,
                                               entity.scale, spriteFrame.width, spriteFrame.height, spriteFrame.sprX,
                                               spriteFrame.sprY, scriptInfo.spriteSheetID);
                        }
                        else
                        {
                            DrawSpriteScaled((FlipFlags)entity.direction,
                                             FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset,
                                             FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels, -spriteFrame.pivotX, -spriteFrame.pivotY,
                                             entity.scale, spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY,
                                             scriptInfo.spriteSheetID);
                        }
                        break;
                    case DrawFXFlags.FX_FLIP:
                        switch ((FlipFlags)entity.direction)
                        {
                            default:
                            case FlipFlags.FLIP_NONE:
                                DrawSpriteFlipped(FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset + spriteFrame.pivotX,
                                                  FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels + spriteFrame.pivotY, spriteFrame.width,
                                                  spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, FlipFlags.FLIP_NONE, scriptInfo.spriteSheetID);
                                break;
                            case FlipFlags.FLIP_X:
                                DrawSpriteFlipped(FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset - spriteFrame.width - spriteFrame.pivotX,
                                                  FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels + spriteFrame.pivotY, spriteFrame.width,
                                                  spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, FlipFlags.FLIP_X, scriptInfo.spriteSheetID);
                                break;
                            case FlipFlags.FLIP_Y:
                                DrawSpriteFlipped(FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset + spriteFrame.pivotX,
                                                  FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels - spriteFrame.height - spriteFrame.pivotY,
                                                  spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, FlipFlags.FLIP_Y,
                                                  scriptInfo.spriteSheetID);
                                break;
                            case FlipFlags.FLIP_XY:
                                DrawSpriteFlipped(FixedPointToWhole(scriptEng.operands[2]) - xScrollOffset - spriteFrame.width - spriteFrame.pivotX,
                                                  FixedPointToWhole(scriptEng.operands[3]) - yScrollOffsetPixels - spriteFrame.height - spriteFrame.pivotY,
                                                  spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, FlipFlags.FLIP_XY,
                                                  scriptInfo.spriteSheetID);
                                break;
                        }
                        break;
                }
                break;
            case ScrFunction.FUNC_DRAWSPRITESCREENFX:
                numOps = 0;
                spriteFrame = scriptFrames[scriptInfo.frameListOffset + scriptEng.operands[0]];
                switch ((DrawFXFlags)scriptEng.operands[1])
                {
                    default: break;
                    case DrawFXFlags.FX_SCALE:
                        DrawSpriteScaled((FlipFlags)entity.direction, scriptEng.operands[2], scriptEng.operands[3], -spriteFrame.pivotX, -spriteFrame.pivotY,
                                         entity.scale, spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY,
                                         scriptInfo.spriteSheetID);
                        break;
                    case DrawFXFlags.FX_ROTATE:
                        DrawSpriteRotated((FlipFlags)entity.direction, scriptEng.operands[2], scriptEng.operands[3], -spriteFrame.pivotX, -spriteFrame.pivotY,
                                          spriteFrame.sprX, spriteFrame.sprY, spriteFrame.width, spriteFrame.height, entity.rotation,
                                          scriptInfo.spriteSheetID);
                        break;
                    case DrawFXFlags.FX_ROTOZOOM:
                        DrawSpriteRotozoom((FlipFlags)entity.direction, scriptEng.operands[2], scriptEng.operands[3], -spriteFrame.pivotX,
                                           -spriteFrame.pivotY, spriteFrame.sprX, spriteFrame.sprY, spriteFrame.width, spriteFrame.height,
                                           entity.rotation, entity.scale, scriptInfo.spriteSheetID);
                        break;
                    case DrawFXFlags.FX_INK:
                        switch ((InkFlags)entity.inkEffect)
                        {
                            case InkFlags.INK_NONE:
                                DrawSprite(scriptEng.operands[2] + spriteFrame.pivotX, scriptEng.operands[3] + spriteFrame.pivotY,
                                           spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, scriptInfo.spriteSheetID);
                                break;
                            case InkFlags.INK_BLEND:
                                DrawBlendedSprite(scriptEng.operands[2] + spriteFrame.pivotX, scriptEng.operands[3] + spriteFrame.pivotY,
                                                  spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY,
                                                  scriptInfo.spriteSheetID);
                                break;
                            case InkFlags.INK_ALPHA:
                                DrawAlphaBlendedSprite(scriptEng.operands[2] + spriteFrame.pivotX, scriptEng.operands[3] + spriteFrame.pivotY,
                                                       spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, entity.alpha,
                                                       scriptInfo.spriteSheetID);
                                break;
                            case InkFlags.INK_ADD:
                                DrawAdditiveBlendedSprite(scriptEng.operands[2] + spriteFrame.pivotX, scriptEng.operands[3] + spriteFrame.pivotY,
                                                          spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY,
                                                          entity.alpha, scriptInfo.spriteSheetID);
                                break;
                            case InkFlags.INK_SUB:
                                DrawSubtractiveBlendedSprite(scriptEng.operands[2] + spriteFrame.pivotX, scriptEng.operands[3] + spriteFrame.pivotY,
                                                             spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY,
                                                             entity.alpha, scriptInfo.spriteSheetID);
                                break;
                        }
                        break;
                    case DrawFXFlags.FX_TINT:
                        if ((InkFlags)entity.inkEffect == InkFlags.INK_ALPHA)
                        {
                            DrawScaledTintMask(entity.direction, scriptEng.operands[2], scriptEng.operands[3], -spriteFrame.pivotX,
                                               -spriteFrame.pivotY, entity.scale, spriteFrame.width, spriteFrame.height,
                                               spriteFrame.sprX, spriteFrame.sprY, scriptInfo.spriteSheetID);
                        }
                        else
                        {
                            DrawSpriteScaled((FlipFlags)entity.direction, scriptEng.operands[2], scriptEng.operands[3], -spriteFrame.pivotX,
                                             -spriteFrame.pivotY, entity.scale, spriteFrame.width, spriteFrame.height,
                                             spriteFrame.sprX, spriteFrame.sprY, scriptInfo.spriteSheetID);
                        }
                        break;
                    case DrawFXFlags.FX_FLIP:
                        switch ((FlipFlags)entity.direction)
                        {
                            default:
                            case FlipFlags.FLIP_NONE:
                                DrawSpriteFlipped(scriptEng.operands[2] + spriteFrame.pivotX, scriptEng.operands[3] + spriteFrame.pivotY,
                                                  spriteFrame.width, spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, FlipFlags.FLIP_NONE,
                                                  scriptInfo.spriteSheetID);
                                break;
                            case FlipFlags.FLIP_X:
                                DrawSpriteFlipped(scriptEng.operands[2] - spriteFrame.width - spriteFrame.pivotX,
                                                  scriptEng.operands[3] + spriteFrame.pivotY, spriteFrame.width, spriteFrame.height,
                                                  spriteFrame.sprX, spriteFrame.sprY, FlipFlags.FLIP_X, scriptInfo.spriteSheetID);
                                break;
                            case FlipFlags.FLIP_Y:
                                DrawSpriteFlipped(scriptEng.operands[2] + spriteFrame.pivotX,
                                                  scriptEng.operands[3] - spriteFrame.height - spriteFrame.pivotY, spriteFrame.width,
                                                  spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, FlipFlags.FLIP_Y, scriptInfo.spriteSheetID);
                                break;
                            case FlipFlags.FLIP_XY:
                                DrawSpriteFlipped(scriptEng.operands[2] - spriteFrame.width - spriteFrame.pivotX,
                                                  scriptEng.operands[3] - spriteFrame.height - spriteFrame.pivotY, spriteFrame.width,
                                                  spriteFrame.height, spriteFrame.sprX, spriteFrame.sprY, FlipFlags.FLIP_XY, scriptInfo.spriteSheetID);
                                break;
                        }
                        break;
                }
                break;
            case ScrFunction.FUNC_LOADANIMATION:
                numOps = 0;
                objectScriptList[objectEntityList[objectLoop].type].animFile = AddAnimationFile(scriptText);
                break;
            case ScrFunction.FUNC_SETUPMENU:
                {
                    numOps = 0;
                    TextMenu menu = gameMenu[scriptEng.operands[0]];
                    SetupTextMenu(ref menu, scriptEng.operands[1]);
                    menu.selectionCount = (byte)scriptEng.operands[2];
                    menu.alignment = (byte)scriptEng.operands[3];
                    gameMenu[scriptEng.operands[0]] = menu;
                    break;
                }
            case ScrFunction.FUNC_ADDMENUENTRY:
                {
                    numOps = 0;
                    TextMenu menu = gameMenu[scriptEng.operands[0]];
                    menu.entryHighlight[menu.rowCount] = (byte)scriptEng.operands[2];
                    AddTextMenuEntry(ref menu, scriptText);
                    gameMenu[scriptEng.operands[0]] = menu;
                    break;
                }
            case ScrFunction.FUNC_EDITMENUENTRY:
                numOps = 0;
                break;
            case ScrFunction.FUNC_LOADSTAGE:
                numOps = 0;
                stageMode = StageModes.STAGEMODE_LOAD;
                break;
            case ScrFunction.FUNC_DRAWRECT:
                numOps = 0;
                DrawRectangle(scriptEng.operands[0], scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3], scriptEng.operands[4],
                              scriptEng.operands[5], scriptEng.operands[6], scriptEng.operands[7]);
                break;
            case ScrFunction.FUNC_RESETOBJECTENTITY:
                {
                    numOps = 0;
                    fixed (Entity* newEnt = &objectEntityList[scriptEng.operands[0]])
                    {
                        newEnt->type = (byte)scriptEng.operands[1];
                        newEnt->propertyValue = (byte)scriptEng.operands[2];
                        newEnt->XPos = scriptEng.operands[3];
                        newEnt->YPos = scriptEng.operands[4];
                        newEnt->direction = (byte)FlipFlags.FLIP_NONE;
                        newEnt->frame = 0;
                        newEnt->priority = (byte)ObjectPriority.PRIORITY_BOUNDS;
                        newEnt->rotation = 0;
                        newEnt->state = 0;
                        newEnt->drawOrder = 3;
                        newEnt->scale = 0x200;
                        newEnt->inkEffect = (byte)InkFlags.INK_NONE;
                        newEnt->values[0] = 0;
                        newEnt->values[1] = 0;
                        newEnt->values[2] = 0;
                        newEnt->values[3] = 0;
                        newEnt->values[4] = 0;
                        newEnt->values[5] = 0;
                        newEnt->values[6] = 0;
                        newEnt->values[7] = 0;
                    }
                    break;
                }
            case ScrFunction.FUNC_PLAYEROBJECTCOLLISION:
                numOps = 0;
                switch ((ObjectCollisionTypes)scriptEng.operands[0])
                {
                    default: break;
                    case ObjectCollisionTypes.C_TOUCH:
                        scriptEng.operands[5] = FixedPointToWhole(entity.XPos);
                        scriptEng.operands[6] = FixedPointToWhole(entity.YPos);
                        TouchCollision(scriptEng.operands[5] + scriptEng.operands[1], scriptEng.operands[6] + scriptEng.operands[2],
                                       scriptEng.operands[5] + scriptEng.operands[3], scriptEng.operands[6] + scriptEng.operands[4]);
                        break;
                    case ObjectCollisionTypes.C_BOX:
                        BoxCollision(entity.XPos + (scriptEng.operands[1] << 16), entity.YPos + (scriptEng.operands[2] << 16),
                                     entity.XPos + (scriptEng.operands[3] << 16), entity.YPos + (scriptEng.operands[4] << 16));
                        break;
                    case ObjectCollisionTypes.C_BOX2:
                        BoxCollision2(entity.XPos + (scriptEng.operands[1] << 16), entity.YPos + (scriptEng.operands[2] << 16),
                                        entity.XPos + (scriptEng.operands[3] << 16), entity.YPos + (scriptEng.operands[4] << 16));
                        break;
                    case ObjectCollisionTypes.C_PLATFORM:
                        scriptEng.checkResult = PlatformCollision(entity.XPos + (scriptEng.operands[1] << 16), entity.YPos + (scriptEng.operands[2] << 16),
                                                                    entity.XPos + (scriptEng.operands[3] << 16), entity.YPos + (scriptEng.operands[4] << 16));
                        break;
                }
                break;
            case ScrFunction.FUNC_CREATETEMPOBJECT:
                {
                    numOps = 0;
                    if (objectEntityList[scriptEng.arrayPosition[2]].type > 0 && ++scriptEng.arrayPosition[2] == ENTITY_COUNT)
                        scriptEng.arrayPosition[2] = TEMPENTITY_START;
                    fixed (Entity* temp = &objectEntityList[scriptEng.arrayPosition[2]])
                    {
                        temp->type = (byte)scriptEng.operands[0];
                        temp->propertyValue = (byte)scriptEng.operands[1];
                        temp->XPos = scriptEng.operands[2];
                        temp->YPos = scriptEng.operands[3];
                        temp->direction = (byte)FlipFlags.FLIP_NONE;
                        temp->frame = 0;
                        temp->priority = (byte)ObjectPriority.PRIORITY_ACTIVE;
                        temp->rotation = 0;
                        temp->state = 0;
                        temp->drawOrder = 3;
                        temp->scale = 512;
                        temp->inkEffect = (byte)InkFlags.INK_NONE;
                        temp->alpha = 0;
                        temp->animation = 0;
                        temp->prevAnimation = 0;
                        temp->animationSpeed = 0;
                        temp->animationTimer = 0;
                        temp->values[0] = 0;
                        temp->values[1] = 0;
                        temp->values[2] = 0;
                        temp->values[3] = 0;
                        temp->values[4] = 0;
                        temp->values[5] = 0;
                        temp->values[6] = 0;
                        temp->values[7] = 0;
                    }
                    break;
                }
            case ScrFunction.FUNC_BINDPLAYERTOOBJECT:
                {
                    numOps = 0;

                    playerList[scriptEng.operands[0]].animationFile = scriptInfo.animFile;
                    playerList[scriptEng.operands[0]].boundEntity = scriptEng.operands[1];
                    playerList[scriptEng.operands[0]].entityNo = scriptEng.operands[1];
                    break;
                }
            case ScrFunction.FUNC_PLAYERTILECOLLISION:
                numOps = 0;
                if (player.tileCollisions != 0)
                {
                    tempplayer = player;
                    ProcessPlayerTileCollisions(ref tempplayer);
                    player = tempplayer;
                }
                else
                {
                    player.XPos += player.XVelocity;
                    player.YPos += player.YVelocity;
                }
                break;
            case ScrFunction.FUNC_PROCESSPLAYERCONTROL:
                numOps = 0;
                tempplayer = player;
                ProcessPlayerControl(ref tempplayer);
                player = tempplayer;
                break;
            case ScrFunction.FUNC_PROCESSANIMATION:
                tempent = entity;
                ProcessObjectAnimation(ref scriptInfo, ref tempent);
                entity = tempent;
                numOps = 0;
                break;
            case ScrFunction.FUNC_DRAWOBJECTANIMATION:
                numOps = 0;
                tempent = entity;
                DrawObjectAnimation(ref scriptInfo, ref tempent, FixedPointToWhole(entity.XPos) - xScrollOffset, FixedPointToWhole(entity.YPos) - yScrollOffsetPixels);
                entity = tempent;
                break;
            case ScrFunction.FUNC_DRAWPLAYERANIMATION:
                numOps = 0;
                if (player.visible != 0)
                {
                    tempent = entity;
                    if (cameraEnabled == activePlayer)
                        DrawObjectAnimation(ref scriptInfo, ref tempent, player.screenXPos, player.screenYPos);
                    else
                        DrawObjectAnimation(ref scriptInfo, ref tempent, FixedPointToWhole(player.XPos) - xScrollOffset, FixedPointToWhole(player.YPos) - yScrollOffsetPixels);
                    entity = tempent;
                }
                break;
            case ScrFunction.FUNC_SETMUSICTRACK:
                numOps = 0;
                if (scriptEng.operands[2] <= 1)
                    SetMusicTrack(scriptText, (byte)scriptEng.operands[1], scriptEng.operands[2] != 0, 0);
                else
                    SetMusicTrack(scriptText, (byte)scriptEng.operands[1], true, (uint)scriptEng.operands[2]);
                break;
            case ScrFunction.FUNC_PLAYMUSIC:
                numOps = 0;
                PlayMusic(scriptEng.operands[0]);
                break;
            case ScrFunction.FUNC_STOPMUSIC:
                numOps = 0;
                StopMusic();
                break;
            case ScrFunction.FUNC_PLAYSFX:
                numOps = 0;
                PlaySFX(scriptEng.operands[0], scriptEng.operands[1] != 0);
                break;
            case ScrFunction.FUNC_STOPSFX:
                numOps = 0;
                StopSFX(scriptEng.operands[0]);
                break;
            case ScrFunction.FUNC_SETSFXATTRIBUTES:
                numOps = 0;
                SetSfxAttributes(scriptEng.operands[0], scriptEng.operands[1], (sbyte)scriptEng.operands[2]);
                break;
            case ScrFunction.FUNC_OBJECTTILECOLLISION:
                numOps = 0;
                switch ((CollisionSides)scriptEng.operands[0])
                {
                    default: break;
                    case CollisionSides.CSIDE_FLOOR: ObjectFloorCollision(scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                    case CollisionSides.CSIDE_LWALL: ObjectLWallCollision(scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                    case CollisionSides.CSIDE_RWALL: ObjectRWallCollision(scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                    case CollisionSides.CSIDE_ROOF: ObjectRoofCollision(scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                }
                break;
            case ScrFunction.FUNC_OBJECTTILEGRIP:
                numOps = 0;
                switch ((CollisionSides)scriptEng.operands[0])
                {
                    default: break;
                    case CollisionSides.CSIDE_FLOOR: ObjectFloorGrip(scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                    case CollisionSides.CSIDE_LWALL: ObjectLWallGrip(scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                    case CollisionSides.CSIDE_RWALL: ObjectRWallGrip(scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                    case CollisionSides.CSIDE_ROOF: ObjectRoofGrip(scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                }
                break;
            case ScrFunction.FUNC_LOADVIDEO:
                numOps = 0;
                PauseSound();
                if (!scriptText.EndsWith(".rsv"))
                    PlayVideoFile(scriptText); // not an rsv
                else
                    scriptInfo.spriteSheetID = Sprite.Add(scriptText);
                ResumeSound();
                break;
            case ScrFunction.FUNC_NEXTVIDEOFRAME:
                numOps = 0;
                /*
                    TODO: replace this entry with something more useful currently
                */
                break;
            case ScrFunction.FUNC_PLAYSTAGESFX:
                numOps = 0;
                PlaySFX(globalSFXCount + scriptEng.operands[0], scriptEng.operands[1] != 0);
                break;
            case ScrFunction.FUNC_STOPSTAGESFX:
                numOps = 0;
                StopSFX(globalSFXCount + scriptEng.operands[0]);
                break;
            case ScrFunction.FUNC_NOT: scriptEng.operands[0] = ~scriptEng.operands[0]; break;
            case ScrFunction.FUNC_DRAW3DSCENE:
                numOps = 0;
                TransformVertexBuffer();
                Draw3DScene(scriptInfo.spriteSheetID);
                break;
            case ScrFunction.FUNC_SETIDENTITYMATRIX:
                numOps = 0;
                switch ((MatrixTypes)scriptEng.operands[0])
                {
                    case MatrixTypes.MAT_WORLD: SetIdentityMatrix(ref matWorld); break;
                    case MatrixTypes.MAT_VIEW: SetIdentityMatrix(ref matView); break;
                    case MatrixTypes.MAT_TEMP: SetIdentityMatrix(ref matTemp); break;
                }
                break;
            case ScrFunction.FUNC_MATRIXMULTIPLY:
                numOps = 0;
                switch ((MatrixTypes)scriptEng.operands[0])
                {
                    case MatrixTypes.MAT_WORLD:
                        switch ((MatrixTypes)scriptEng.operands[1])
                        {
                            case MatrixTypes.MAT_WORLD: MatrixMultiply(ref matWorld, ref matWorld); break;
                            case MatrixTypes.MAT_VIEW: MatrixMultiply(ref matWorld, ref matView); break;
                            case MatrixTypes.MAT_TEMP: MatrixMultiply(ref matWorld, ref matTemp); break;
                        }
                        break;
                    case MatrixTypes.MAT_VIEW:
                        switch ((MatrixTypes)scriptEng.operands[1])
                        {
                            case MatrixTypes.MAT_WORLD: MatrixMultiply(ref matView, ref matWorld); break;
                            case MatrixTypes.MAT_VIEW: MatrixMultiply(ref matView, ref matView); break;
                            case MatrixTypes.MAT_TEMP: MatrixMultiply(ref matView, ref matTemp); break;
                        }
                        break;
                    case MatrixTypes.MAT_TEMP:
                        switch ((MatrixTypes)scriptEng.operands[1])
                        {
                            case MatrixTypes.MAT_WORLD: MatrixMultiply(ref matTemp, ref matWorld); break;
                            case MatrixTypes.MAT_VIEW: MatrixMultiply(ref matTemp, ref matView); break;
                            case MatrixTypes.MAT_TEMP: MatrixMultiply(ref matTemp, ref matTemp); break;
                        }
                        break;
                }
                break;
            case ScrFunction.FUNC_MATRIXTRANSLATEXYZ:
                numOps = 0;
                switch ((MatrixTypes)scriptEng.operands[0])
                {
                    case MatrixTypes.MAT_WORLD: MatrixTranslateXYZ(ref matWorld, scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                    case MatrixTypes.MAT_VIEW: MatrixTranslateXYZ(ref matView, scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                    case MatrixTypes.MAT_TEMP: MatrixTranslateXYZ(ref matTemp, scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                }
                break;
            case ScrFunction.FUNC_MATRIXSCALEXYZ:
                numOps = 0;
                switch ((MatrixTypes)scriptEng.operands[0])
                {
                    case MatrixTypes.MAT_WORLD: MatrixScaleXYZ(ref matWorld, scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                    case MatrixTypes.MAT_VIEW: MatrixScaleXYZ(ref matView, scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                    case MatrixTypes.MAT_TEMP: MatrixScaleXYZ(ref matTemp, scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                }
                break;
            case ScrFunction.FUNC_MATRIXROTATEX:
                numOps = 0;
                switch ((MatrixTypes)scriptEng.operands[0])
                {
                    case MatrixTypes.MAT_WORLD: MatrixRotateX(ref matWorld, scriptEng.operands[1]); break;
                    case MatrixTypes.MAT_VIEW: MatrixRotateX(ref matView, scriptEng.operands[1]); break;
                    case MatrixTypes.MAT_TEMP: MatrixRotateX(ref matTemp, scriptEng.operands[1]); break;
                }
                break;
            case ScrFunction.FUNC_MATRIXROTATEY:
                numOps = 0;
                switch ((MatrixTypes)scriptEng.operands[0])
                {
                    case MatrixTypes.MAT_WORLD: MatrixRotateY(ref matWorld, scriptEng.operands[1]); break;
                    case MatrixTypes.MAT_VIEW: MatrixRotateY(ref matView, scriptEng.operands[1]); break;
                    case MatrixTypes.MAT_TEMP: MatrixRotateY(ref matTemp, scriptEng.operands[1]); break;
                }
                break;
            case ScrFunction.FUNC_MATRIXROTATEZ:
                numOps = 0;
                switch ((MatrixTypes)scriptEng.operands[0])
                {
                    case MatrixTypes.MAT_WORLD: MatrixRotateZ(ref matWorld, scriptEng.operands[1]); break;
                    case MatrixTypes.MAT_VIEW: MatrixRotateZ(ref matView, scriptEng.operands[1]); break;
                    case MatrixTypes.MAT_TEMP: MatrixRotateZ(ref matTemp, scriptEng.operands[1]); break;
                }
                break;
            case ScrFunction.FUNC_MATRIXROTATEXYZ:
                numOps = 0;
                switch ((MatrixTypes)scriptEng.operands[0])
                {
                    case MatrixTypes.MAT_WORLD: MatrixRotateXYZ(ref matWorld, scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                    case MatrixTypes.MAT_VIEW: MatrixRotateXYZ(ref matView, scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                    case MatrixTypes.MAT_TEMP: MatrixRotateXYZ(ref matTemp, scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]); break;
                }
                break;
            case ScrFunction.FUNC_TRANSFORMVERTICES:
                numOps = 0;
                switch ((MatrixTypes)scriptEng.operands[0])
                {
                    case MatrixTypes.MAT_WORLD: TransformVerticies(ref matWorld, scriptEng.operands[1], scriptEng.operands[2]); break;
                    case MatrixTypes.MAT_VIEW: TransformVerticies(ref matView, scriptEng.operands[1], scriptEng.operands[2]); break;
                    case MatrixTypes.MAT_TEMP: TransformVerticies(ref matTemp, scriptEng.operands[1], scriptEng.operands[2]); break;
                }
                break;
            case ScrFunction.FUNC_CALLFUNCTION:
                {
                    numOps = 0;
                    functionStack.Push(scriptCodePtr);
                    functionStack.Push(jumpTableStart);
                    functionStack.Push(scriptCodeStart);
                    scriptCodeStart = scriptFunctionList[scriptEng.operands[0]].ptr.scriptCodePtr;
                    jumpTableStart = scriptFunctionList[scriptEng.operands[0]].ptr.jumpTablePtr;
                    scriptCodePtr = scriptCodeStart;
                }
                break;
            case ScrFunction.FUNC_ENDFUNCTION:
                numOps = 0;
                scriptCodeStart = functionStack.Pop();
                jumpTableStart = functionStack.Pop();
                scriptCodePtr = functionStack.Pop();
                break;
            case ScrFunction.FUNC_SETLAYERDEFORMATION:
                numOps = 0;
                SetLayerDeformation(scriptEng.operands[0], scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3], scriptEng.operands[4],
                                    scriptEng.operands[5]);
                break;
            case ScrFunction.FUNC_CHECKTOUCHRECT:
                numOps = 0; scriptEng.checkResult = -1;
                AddDebugHitbox((byte)DebugHitboxTypes.H_TYPE_FINGER, null, scriptEng.operands[0], scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]);
                for (int f = 0; f < touches; ++f)
                {
                    if (touchDown[f] != 0 && touchX[f] > scriptEng.operands[0] && touchX[f] < scriptEng.operands[2] && touchY[f] > scriptEng.operands[1]
                        && touchY[f] < scriptEng.operands[3])
                    {
                        scriptEng.checkResult = f;
                    }
                }
                break;
            case ScrFunction.FUNC_GETTILELAYERENTRY:
                chunkY = scriptEng.operands[3];
                if (chunkY < 0)
                {
                    chunkY = 0;
                }
                var tileOffset = scriptEng.operands[2] + 0x100 * chunkY;
                scriptEng.operands[0] = stageLayouts[scriptEng.operands[1]].tiles[tileOffset];
                break;
            case ScrFunction.FUNC_SETTILELAYERENTRY:
                stageLayouts[scriptEng.operands[1]].tiles[scriptEng.operands[2] + 0x100 * scriptEng.operands[3]] = (ushort)scriptEng.operands[0];
                break;
            case ScrFunction.FUNC_GETBIT: scriptEng.operands[0] = (scriptEng.operands[1] & (1 << scriptEng.operands[2])) >> scriptEng.operands[2]; break;
            case ScrFunction.FUNC_SETBIT:
                if (scriptEng.operands[2] <= 0)
                    scriptEng.operands[0] &= ~(1 << scriptEng.operands[1]);
                else
                    scriptEng.operands[0] |= 1 << scriptEng.operands[1];
                break;
            case ScrFunction.FUNC_PAUSEMUSIC:
                numOps = 0;
                PauseSound();
                break;
            case ScrFunction.FUNC_RESUMEMUSIC:
                numOps = 0;
                ResumeSound();
                break;
            case ScrFunction.FUNC_CLEARDRAWLIST:
                numOps = 0;
                drawListEntries[scriptEng.operands[0]].listSize = 0;
                break;
            case ScrFunction.FUNC_ADDDRAWLISTENTITYREF:
                {
                    numOps = 0;
                    drawListEntries[scriptEng.operands[0]].entityRefs[drawListEntries[scriptEng.operands[0]].listSize++] = scriptEng.operands[1];
                    break;
                }
            case ScrFunction.FUNC_GETDRAWLISTENTITYREF: scriptEng.operands[0] = drawListEntries[scriptEng.operands[1]].entityRefs[scriptEng.operands[2]]; break;
            case ScrFunction.FUNC_SETDRAWLISTENTITYREF:
                numOps = 0;
                drawListEntries[scriptEng.operands[1]].entityRefs[scriptEng.operands[2]] = scriptEng.operands[0];
                break;
            case ScrFunction.FUNC_GET16X16TILEINFO:
                {
                    scriptEng.operands[4] = scriptEng.operands[1] >> 7;
                    scriptEng.operands[5] = scriptEng.operands[2] >> 7;
                    chunkY = scriptEng.operands[5];
                    if (chunkY < 0)
                    {
                        chunkY = 0;
                    }
                    scriptEng.operands[6] = stageLayouts[0].tiles[scriptEng.operands[4] + (chunkY << 8)] << 6;
                    scriptEng.operands[6] += ((scriptEng.operands[1] & 0x7F) >> 4) + 8 * ((scriptEng.operands[2] & 0x7F) >> 4);
                    int index = tiles128x128.tileIndex[scriptEng.operands[6]];
                    switch ((TileInfo)scriptEng.operands[3])
                    {
                        case TileInfo.TILEINFO_INDEX: scriptEng.operands[0] = tiles128x128.tileIndex[scriptEng.operands[6]]; break;
                        case TileInfo.TILEINFO_DIRECTION: scriptEng.operands[0] = (int)tiles128x128.direction[scriptEng.operands[6]]; break;
                        case TileInfo.TILEINFO_VISUALPLANE: scriptEng.operands[0] = tiles128x128.visualPlane[scriptEng.operands[6]]; break;
                        case TileInfo.TILEINFO_SOLIDITYA: scriptEng.operands[0] = (int)tiles128x128.collisionFlags[0, scriptEng.operands[6]]; break;
                        case TileInfo.TILEINFO_SOLIDITYB: scriptEng.operands[0] = (int)tiles128x128.collisionFlags[1, scriptEng.operands[6]]; break;
                        case TileInfo.TILEINFO_FLAGSA: scriptEng.operands[0] = collisionMasks[0, index].flags; break;
                        case TileInfo.TILEINFO_ANGLEA: scriptEng.operands[0] = (int)collisionMasks[0, index].angles; break;
                        case TileInfo.TILEINFO_FLAGSB: scriptEng.operands[0] = collisionMasks[1, index].flags; break;
                        case TileInfo.TILEINFO_ANGLEB: scriptEng.operands[0] = (int)collisionMasks[1, index].angles; break;
                        default: break;
                    }
                    break;
                }
            case ScrFunction.FUNC_COPY16X16TILE:
                numOps = 0;
                Copy16x16Tile((ushort)scriptEng.operands[0], (ushort)scriptEng.operands[1]);
                break;
            case ScrFunction.FUNC_SET16X16TILEINFO:
                {
                    scriptEng.operands[4] = scriptEng.operands[1] >> 7;
                    scriptEng.operands[5] = scriptEng.operands[2] >> 7;
                    scriptEng.operands[6] = stageLayouts[0].tiles[scriptEng.operands[4] + (scriptEng.operands[5] << 8)] << 6;
                    scriptEng.operands[6] += ((scriptEng.operands[1] & 0x7F) >> 4) + 8 * ((scriptEng.operands[2] & 0x7F) >> 4);
                    switch ((TileInfo)scriptEng.operands[3])
                    {
                        case TileInfo.TILEINFO_INDEX:
                            tiles128x128.tileIndex[scriptEng.operands[6]] = (ushort)scriptEng.operands[0];
                            tiles128x128.gfxDataPos[scriptEng.operands[6]] = scriptEng.operands[0] << 8;
                            break;
                        case TileInfo.TILEINFO_DIRECTION: tiles128x128.direction[scriptEng.operands[6]] = (FlipFlags)scriptEng.operands[0]; break;
                        case TileInfo.TILEINFO_VISUALPLANE: tiles128x128.visualPlane[scriptEng.operands[6]] = (byte)scriptEng.operands[0]; break;
                        case TileInfo.TILEINFO_SOLIDITYA: tiles128x128.collisionFlags[0, scriptEng.operands[6]] = (CollisionSolidity)scriptEng.operands[0]; break;
                        case TileInfo.TILEINFO_SOLIDITYB: tiles128x128.collisionFlags[1, scriptEng.operands[6]] = (CollisionSolidity)scriptEng.operands[0]; break;
                        case TileInfo.TILEINFO_FLAGSA: collisionMasks[1, tiles128x128.tileIndex[scriptEng.operands[6]]].flags = (byte)scriptEng.operands[0]; break;
                        case TileInfo.TILEINFO_ANGLEA: collisionMasks[1, tiles128x128.tileIndex[scriptEng.operands[6]]].angles = (uint)scriptEng.operands[0]; break;
                        default: break;
                    }
                    break;
                }
            case ScrFunction.FUNC_GETANIMATIONBYNAME:
                {
                    AnimationFile animFile = scriptInfo.animFile;
                    scriptEng.operands[0] = -1;
                    int id = 0;
                    while (scriptEng.operands[0] == -1)
                    {
                        var anim = animationList[animFile.aniListOffset + id];
                        if (scriptText == anim.name)
                            scriptEng.operands[0] = id;
                        else if (id++ == animFile.animCount)
                            scriptEng.operands[0] = 0;
                    }
                    break;
                }
            case ScrFunction.FUNC_READSAVERAM:
                numOps = 0;
                scriptEng.checkResult = platform.ReadSaveRAMData() ? 1 : 0;
                break;
            case ScrFunction.FUNC_WRITESAVERAM:
                numOps = 0;
                scriptEng.checkResult = platform.WriteSaveRAMData() ? 1 : 0;
                break;
            case ScrFunction.FUNC_LOADTEXTFONT:
                numOps = 0;
                LoadFontFile(scriptText);
                break;
            case ScrFunction.FUNC_LOADTEXTFILE:
                {
                    numOps = 0;
                    TextMenu menu = gameMenu[scriptEng.operands[0]];
                    LoadTextFile(ref menu, scriptText, (byte)scriptEng.operands[2]);
                    gameMenu[scriptEng.operands[0]] = menu;
                    break;
                }
            case ScrFunction.FUNC_DRAWTEXT:
                {
                    numOps = 0;
                    textMenuSurfaceNo = scriptInfo.spriteSheetID;
                    TextMenu menu = gameMenu[scriptEng.operands[0]];
                    DrawBitmapText(ref menu, scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3], scriptEng.operands[4],
                                   scriptEng.operands[5], scriptEng.operands[6]);
                    break;
                }
            case ScrFunction.FUNC_GETTEXTINFO:
                {
                    TextMenu menu = gameMenu[scriptEng.operands[1]];
                    switch ((TextInfoTypes)scriptEng.operands[2])
                    {
                        case TextInfoTypes.TEXTINFO_TEXTDATA:
                            scriptEng.operands[0] = menu.textData[menu.entryStart[scriptEng.operands[3]] + scriptEng.operands[4]];
                            break;
                        case TextInfoTypes.TEXTINFO_TEXTSIZE: scriptEng.operands[0] = menu.entrySize[scriptEng.operands[3]]; break;
                        case TextInfoTypes.TEXTINFO_ROWCOUNT: scriptEng.operands[0] = menu.rowCount; break;
                    }
                    break;
                }
            case ScrFunction.FUNC_GETVERSIONNUMBER:
                {
                    numOps = 0;
                    TextMenu menu = gameMenu[scriptEng.operands[0]];
                    menu.entryHighlight[menu.rowCount] = (byte)scriptEng.operands[1];
                    AddTextMenuEntry(ref menu, EngineStuff.gameVersion);
                    gameMenu[scriptEng.operands[0]] = menu;
                    break;
                }
            case ScrFunction.FUNC_SETACHIEVEMENT:
                numOps = 0;
                platform.SetAchievement(scriptEng.operands[0], scriptEng.operands[1]);
                break;
            case ScrFunction.FUNC_SETLEADERBOARD:
                numOps = 0;
                platform.SetLeaderboard(scriptEng.operands[0], scriptEng.operands[1]);
                break;
            case ScrFunction.FUNC_LOADONLINEMENU:
                numOps = 0;
                switch ((OnlineMenuTypes)scriptEng.operands[0])
                {
                    default: break;
                    case OnlineMenuTypes.ONLINEMENU_ACHIEVEMENTS: platform.LoadAchievementsMenu(); break;
                    case OnlineMenuTypes.ONLINEMENU_LEADERBOARDS: platform.LoadLeaderboardsMenu(); break;
                }
                break;
            case ScrFunction.FUNC_ENGINECALLBACK:
                numOps = 0;
                Engine.Callback(scriptEng.operands[0]);
                break;
            case ScrFunction.FUNC_HAPTICEFFECT:
                numOps = 0;
                // params: scriptEng.operands[0], scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]
                if (scriptEng.operands[0] != -1)
                    QueueHapticEffect(scriptEng.operands[0]);
                else
                    PlayHaptics(scriptEng.operands[1], scriptEng.operands[2], scriptEng.operands[3]);
                break;
            case ScrFunction.FUNC_PRINT:
                platform.PrintLog(scriptText);
                break;
        }
    }
}