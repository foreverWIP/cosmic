using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Cosmic.Core;

static unsafe class ScriptParsing
{
    public enum ScrFunction : byte
    {
        FUNC_END,
        FUNC_EQUAL,
        FUNC_ADD,
        FUNC_SUB,
        FUNC_INC,
        FUNC_DEC,
        FUNC_MUL,
        FUNC_DIV,
        FUNC_SHR,
        FUNC_SHL,
        FUNC_AND,
        FUNC_OR,
        FUNC_XOR,
        FUNC_MOD,
        FUNC_FLIPSIGN,
        FUNC_CHECKEQUAL,
        FUNC_CHECKGREATER,
        FUNC_CHECKLOWER,
        FUNC_CHECKNOTEQUAL,
        FUNC_IFEQUAL,
        FUNC_IFGREATER,
        FUNC_IFGREATEROREQUAL,
        FUNC_IFLOWER,
        FUNC_IFLOWEROREQUAL,
        FUNC_IFNOTEQUAL,
        FUNC_ELSE,
        FUNC_ENDIF,
        FUNC_WEQUAL,
        FUNC_WGREATER,
        FUNC_WGREATEROREQUAL,
        FUNC_WLOWER,
        FUNC_WLOWEROREQUAL,
        FUNC_WNOTEQUAL,
        FUNC_LOOP,
        FUNC_SWITCH,
        FUNC_BREAK,
        FUNC_ENDSWITCH,
        FUNC_RAND,
        FUNC_SIN,
        FUNC_COS,
        FUNC_SIN256,
        FUNC_COS256,
        FUNC_SINCHANGE,
        FUNC_COSCHANGE,
        FUNC_ATAN2,
        FUNC_INTERPOLATE,
        FUNC_INTERPOLATEXY,
        FUNC_LOADSPRITESHEET,
        FUNC_REMOVESPRITESHEET,
        FUNC_DRAWSPRITE,
        FUNC_DRAWSPRITEXY,
        FUNC_DRAWSPRITESCREENXY,
        FUNC_DRAWTINTRECT,
        FUNC_DRAWNUMBERS,
        FUNC_DRAWACTNAME,
        FUNC_DRAWMENU,
        FUNC_SPRITEFRAME,
        FUNC_EDITFRAME,
        FUNC_LOADPALETTE,
        FUNC_ROTATEPALETTE,
        FUNC_SETSCREENFADE,
        FUNC_SETACTIVEPALETTE,
        FUNC_SETPALETTEFADE,
        FUNC_COPYPALETTE,
        FUNC_CLEARSCREEN,
        FUNC_DRAWSPRITEFX,
        FUNC_DRAWSPRITESCREENFX,
        FUNC_LOADANIMATION,
        FUNC_SETUPMENU,
        FUNC_ADDMENUENTRY,
        FUNC_EDITMENUENTRY,
        FUNC_LOADSTAGE,
        FUNC_DRAWRECT,
        FUNC_RESETOBJECTENTITY,
        FUNC_PLAYEROBJECTCOLLISION,
        FUNC_CREATETEMPOBJECT,
        FUNC_BINDPLAYERTOOBJECT,
        FUNC_PLAYERTILECOLLISION,
        FUNC_PROCESSPLAYERCONTROL,
        FUNC_PROCESSANIMATION,
        FUNC_DRAWOBJECTANIMATION,
        FUNC_DRAWPLAYERANIMATION,
        FUNC_SETMUSICTRACK,
        FUNC_PLAYMUSIC,
        FUNC_STOPMUSIC,
        FUNC_PLAYSFX,
        FUNC_STOPSFX,
        FUNC_SETSFXATTRIBUTES,
        FUNC_OBJECTTILECOLLISION,
        FUNC_OBJECTTILEGRIP,
        FUNC_LOADVIDEO,
        FUNC_NEXTVIDEOFRAME,
        FUNC_PLAYSTAGESFX,
        FUNC_STOPSTAGESFX,
        FUNC_NOT,
        FUNC_DRAW3DSCENE,
        FUNC_SETIDENTITYMATRIX,
        FUNC_MATRIXMULTIPLY,
        FUNC_MATRIXTRANSLATEXYZ,
        FUNC_MATRIXSCALEXYZ,
        FUNC_MATRIXROTATEX,
        FUNC_MATRIXROTATEY,
        FUNC_MATRIXROTATEZ,
        FUNC_MATRIXROTATEXYZ,
        FUNC_TRANSFORMVERTICES,
        FUNC_CALLFUNCTION,
        FUNC_ENDFUNCTION,
        FUNC_SETLAYERDEFORMATION,
        FUNC_CHECKTOUCHRECT,
        FUNC_GETTILELAYERENTRY,
        FUNC_SETTILELAYERENTRY,
        FUNC_GETBIT,
        FUNC_SETBIT,
        FUNC_PAUSEMUSIC,
        FUNC_RESUMEMUSIC,
        FUNC_CLEARDRAWLIST,
        FUNC_ADDDRAWLISTENTITYREF,
        FUNC_GETDRAWLISTENTITYREF,
        FUNC_SETDRAWLISTENTITYREF,
        FUNC_GET16X16TILEINFO,
        FUNC_COPY16X16TILE,
        FUNC_SET16X16TILEINFO,
        FUNC_GETANIMATIONBYNAME,
        FUNC_READSAVERAM,
        FUNC_WRITESAVERAM,
        FUNC_LOADTEXTFONT,
        FUNC_LOADTEXTFILE,
        FUNC_DRAWTEXT,
        FUNC_GETTEXTINFO,
        FUNC_GETVERSIONNUMBER,
        FUNC_SETACHIEVEMENT,
        FUNC_SETLEADERBOARD,
        FUNC_LOADONLINEMENU,
        FUNC_ENGINECALLBACK,
        FUNC_HAPTICEFFECT,
        FUNC_PRINT,
        FUNC_MAX_CNT
    }

    internal record ScrFunctionInfo(
        string Name,
        byte NumOps
    );

    internal static readonly ScrFunctionInfo[] functionInfoLookup = new ScrFunctionInfo[]
    {
        new("End", 0),
        new("Equal", 2),
        new("Add", 2),
        new("Sub", 2),
        new("Inc", 1),
        new("Dec", 1),
        new("Mul", 2),
        new("Div", 2),
        new("ShR", 2),
        new("ShL", 2),
        new("And", 2),
        new("Or", 2),
        new("Xor", 2),
        new("Mod", 2),
        new("FlipSign", 1),
        new("CheckEqual", 2),
        new("CheckGreater", 2),
        new("CheckLower", 2),
        new("CheckNotEqual", 2),
        new("IfEqual", 3),
        new("IfGreater", 3),
        new("IfGreaterOrEqual", 3),
        new("IfLower", 3),
        new("IfLowerOrEqual", 3),
        new("IfNotEqual", 3),
        new("else", 0),
        new("endif", 0),
        new("WEqual", 3),
        new("WGreater", 3),
        new("WGreaterOrEqual", 3),
        new("WLower", 3),
        new("WLowerOrEqual", 3),
        new("WNotEqual", 3),
        new("loop", 0),
        new("switch", 2),
        new("break", 0),
        new("endswitch", 0),
        new("Rand", 2),
        new("Sin", 2),
        new("Cos", 2),
        new("Sin256", 2),
        new("Cos256", 2),
        new("SinChange", 5),
        new("CosChange", 5),
        new("ATan2", 3),
        new("Interpolate", 4),
        new("InterpolateXY", 7),
        new("LoadSpriteSheet", 1),
        new("RemoveSpriteSheet", 1),
        new("DrawSprite", 1),
        new("DrawSpriteXY", 3),
        new("DrawSpriteScreenXY", 3),
        new("DrawTintRect", 4),
        new("DrawNumbers", 7),
        new("DrawActName", 7),
        new("DrawMenu", 3),
        new("SpriteFrame", 6),
        new("EditFrame", 7),
        new("LoadPalette", 5),
        new("RotatePalette", 3),
        new("SetScreenFade", 4),
        new("SetActivePalette", 3),
        new("SetPaletteFade", 7),
        new("CopyPalette", 2),
        new("ClearScreen", 1),
        new("DrawSpriteFX", 4),
        new("DrawSpriteScreenFX", 4),
        new("LoadAnimation", 1),
        new("SetupMenu", 4),
        new("AddMenuEntry", 3),
        new("EditMenuEntry", 4),
        new("LoadStage", 0),
        new("DrawRect", 8),
        new("ResetObjectEntity", 5),
        new("PlayerObjectCollision", 5),
        new("CreateTempObject", 4),
        new("BindPlayerToObject", 2),
        new("PlayerTileCollision", 0),
        new("ProcessPlayerControl", 0),
        new("ProcessAnimation", 0),
        new("DrawObjectAnimation", 0),
        new("DrawPlayerAnimation", 0),
        new("SetMusicTrack", 3),
        new("PlayMusic", 1),
        new("StopMusic", 0),
        new("PlaySFX", 2),
        new("StopSFX", 1),
        new("SetSfxAttributes", 3),
        new("ObjectTileCollision", 4),
        new("ObjectTileGrip", 4),
        new("LoadVideo", 1),
        new("NextVideoFrame", 0),
        new("PlayStageSfx", 2),
        new("StopStageSfx", 1),
        new("Not", 1),
        new("Draw3DScene", 0),
        new("SetIdentityMatrix", 1),
        new("MatrixMultiply", 2),
        new("MatrixTranslateXYZ", 4),
        new("MatrixScaleXYZ", 4),
        new("MatrixRotateX", 2),
        new("MatrixRotateY", 2),
        new("MatrixRotateZ", 2),
        new("MatrixRotateXYZ", 4),
        new("TransformVertices", 3),
        new("CallFunction", 1),
        new("EndFunction", 0),
        new("SetLayerDeformation", 6),
        new("CheckTouchRect", 4),
        new("GetTileLayerEntry", 4),
        new("SetTileLayerEntry", 4),
        new("GetBit", 3),
        new("SetBit", 3),
        new("PauseMusic", 0),
        new("ResumeMusic", 0),
        new("ClearDrawList", 1),
        new("AddDrawListEntityRef", 2),
        new("GetDrawListEntityRef", 3),
        new("SetDrawListEntityRef", 3),
        new("Get16x16TileInfo", 4),
        new("Copy16x16Tile", 2),
        new("Set16x16TileInfo", 4),
        new("GetAnimationByName", 2),
        new("ReadSaveRAM", 0),
        new("WriteSaveRAM", 0),
        new("LoadTextFont", 1),
        new("LoadTextFile", 3),
        new("DrawText", 7),
        new("GetTextInfo", 5),
        new("GetVersionNumber", 2),
        new("SetAchievement", 2),
        new("SetLeaderboard", 2),
        new("LoadOnlineMenu", 1),
        new("EngineCallback", 1),
        new("HapticEffect", 4),
        new("Print", 1),
    };

    public static readonly AliasInfo[] defaultAliases = new AliasInfo[] {
        new AliasInfo("true", "1"),
        new AliasInfo("false", "0"),
        new AliasInfo("FX_SCALE", "0"),
        new AliasInfo("FX_ROTATE", "1"),
        new AliasInfo("FX_ROTOZOOM", "2"),
        new AliasInfo("FX_INK", "3"),
        new AliasInfo("PRESENTATION_STAGE", "0"),
        new AliasInfo("REGULAR_STAGE", "1"),
        new AliasInfo("SPECIAL_STAGE", "2"),
        new AliasInfo("BONUS_STAGE", "3"),
        new AliasInfo("MENU_1", "0"),
        new AliasInfo("MENU_2", "1"),
        new AliasInfo("C_TOUCH", "0"),
        new AliasInfo("C_BOX", "1"),
        new AliasInfo("C_BOX2", "2"),
        new AliasInfo("C_PLATFORM", "3"),
        new AliasInfo("MAT_WORLD", "0"),
        new AliasInfo("MAT_VIEW", "1"),
        new AliasInfo("MAT_TEMP", "2"),
        new AliasInfo("FX_FLIP", "5"),
        new AliasInfo("FACING_LEFT", "1"),
        new AliasInfo("FACING_RIGHT", "0"),
        new AliasInfo("STAGE_PAUSED", "2"),
        new AliasInfo("STAGE_RUNNING", "1"),
        new AliasInfo("RESET_GAME", "2"),
        new AliasInfo("RETRO_WIN", "0"),
        new AliasInfo("RETRO_OSX", "1"),
        new AliasInfo("RETRO_XBOX_360", "2"),
        new AliasInfo("RETRO_PS3", "3"),
        new AliasInfo("RETRO_IOS", "4"),
        new AliasInfo("RETRO_ANDROID", "5"),
        new AliasInfo("RETRO_WP7", "6")
    };

    internal static readonly string[] scriptOperators = new string[] { "=",  "+=", "-=", "++", "--", "*=", "/=", ">>=", "<<=", "&=",
                                                 "|=", "^=", "%=", "==", ">",  ">=", "<",  "<=",  "!=" };

    public enum ScriptVarTypes { SCRIPTVAR_VAR = 1, SCRIPTVAR_INTCONST = 2, SCRIPTVAR_STRCONST = 3 }
    public enum ScriptVarArrTypes { VARARR_NONE = 0, VARARR_ARRAY = 1, VARARR_ENTNOPLUS1 = 2, VARARR_ENTNOMINUS1 = 3 }

    enum ScriptReadModes { READMODE_NORMAL = 0, READMODE_STRING = 1, READMODE_COMMENTLINE = 2, READMODE_ENDLINE = 3, READMODE_EOF = 4 }
    enum ScriptParseModes
    {
        PARSEMODE_SCOPELESS = 0,
        PARSEMODE_PLATFORMSKIP = 1,
        PARSEMODE_FUNCTION = 2,
        PARSEMODE_SWITCHREAD = 3,
        PARSEMODE_ERROR = 0xFF
    }

    public static readonly List<AliasInfo> aliases = new();

    static void ConvertIfWhileStatement(ref string text)
    {
        var dest = string.Empty;
        int compareOp = -1;
        int strPos = 0;
        int destStrPos = 0;

        if (text.StartsWith("if"))
        {
            for (int i = 0; i < 6; ++i)
            {
                destStrPos = text.IndexOf(scriptOperators[i + (int)ScrFunction.FUNC_MOD]);
                if (destStrPos > -1)
                {
                    strPos = destStrPos;
                    compareOp = i;
                }
            }

            if (compareOp > -1)
            {
                text = text.Replace(scriptOperators[compareOp + (int)ScrFunction.FUNC_MOD], ",");

                dest = $"{functionInfoLookup[compareOp + (int)ScrFunction.FUNC_IFEQUAL].Name}({jumpTablePos - jumpTableOffset},";

                destStrPos = dest.Length;
                for (int i = 2; i < text.Length; ++i)
                {
                    if (text[i] != '=' && text[i] != '(' && text[i] != ')')
                    {
                        destStrPos++;
                        dest += text[i];
                    }
                }

                dest += ")";
                text = dest;

                jumpTableStack.Push(jumpTablePos);
                AssignJumpTable(jumpTablePos++, -1);
                AssignJumpTable(jumpTablePos++, 0);
            }
        }
        else if (text.StartsWith("while"))
        {
            for (int i = 0; i < 6; ++i)
            {
                destStrPos = text.IndexOf(scriptOperators[i + (int)ScrFunction.FUNC_MOD]);
                if (destStrPos > -1)
                {
                    strPos = destStrPos;
                    compareOp = i;
                }
            }

            if (compareOp > -1)
            {
                text = text.Replace(scriptOperators[compareOp + (int)ScrFunction.FUNC_MOD], ",");

                dest = $"{functionInfoLookup[compareOp + (int)ScrFunction.FUNC_WEQUAL].Name}({jumpTablePos - jumpTableOffset},";

                destStrPos = dest.Length;
                for (int i = 5; i < text.Length; ++i)
                {
                    if (text[i] != '=' && text[i] != '(' && text[i] != ')')
                    {
                        destStrPos++;
                        dest += text[i];
                    }
                }

                dest += ")";
                text = dest;

                jumpTableStack.Push(jumpTablePos);
                AssignJumpTable(jumpTablePos++, scriptCodePos - scriptCodeOffset);
                AssignJumpTable(jumpTablePos++, 0);
            }
        }
    }

    static bool ConvertSwitchStatement(ref string text)
    {
        if (!text.StartsWith("switch"))
            return false;

        var switchText = string.Empty;
        switchText = $"switch({jumpTablePos - jumpTableOffset},";
        int pos = switchText.Length;
        for (int i = 6; i < text.Length; ++i)
        {
            if (text[i] != '=' && text[i] != '(' && text[i] != ')')
            {
                pos++;
                switchText += text[i];
            }
        }
        switchText += ")";
        text = switchText;

        jumpTableStack.Push(jumpTablePos);
        AssignJumpTable(jumpTablePos++, 0x10000);
        AssignJumpTable(jumpTablePos++, -0x10000);
        AssignJumpTable(jumpTablePos++, -1);
        AssignJumpTable(jumpTablePos++, 0);

        return true;
    }

    static void ConvertArithmeticSyntax(ref string text)
    {
        int token = 0;
        int findID = 0;
        var dest = string.Empty;

        for (int i = (int)ScrFunction.FUNC_EQUAL; i <= (int)ScrFunction.FUNC_MOD; ++i)
        {
            findID = text.IndexOf(scriptOperators[i - 1]);
            if (findID > -1)
            {
                token = i;
            }
        }

        if (token > 0)
        {
            var split = text.Split(scriptOperators[token - 1], StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 2)
            {
                dest = $"{functionInfoLookup[token].Name}({split[0]},{split[1]})";
            }
            else
            {
                dest = $"{functionInfoLookup[token].Name}({split[0]})";
            }
            text = dest;
        }
    }

    static bool TryParseInt(string number, ref int value)
    {
        if (int.TryParse(number, NumberStyles.Integer, null, out var result))
        {
            value = result;
            return true;
        }
        else
        {
            var negative = false;
            if (number.StartsWith('-'))
            {
                number = number[1..];
                negative = true;
            }
            if (number.StartsWith("0x"))
            {
                number = number[2..];
            }
            if (int.TryParse(number, NumberStyles.HexNumber, null, out result))
            {
                if (negative)
                {
                    result = -result;
                }
                value = result;
                return true;
            }
            return false;
        }
    }

    static bool ReadSwitchCase(string text)
    {
        var caseText = string.Empty;
        if (text.StartsWith("case"))
        {
            int textPos = 4;
            while (textPos < text.Length)
            {
                if (text[textPos] != ':')
                    caseText += text[textPos];
                ++textPos;
            }
            for (int a = 0; a < aliases.Count; ++a)
            {
                if (caseText == aliases[a].name)
                    caseText = aliases[a].value;
            }

            int val = 0;

            int jPos = jumpTableStack.Peek();
            int jOffset = jPos + 4;
            if (TryParseInt(caseText, ref val))
                AssignJumpTable(val - jumpTable[jPos] + jOffset, scriptCodePos - scriptCodeOffset);
            return true;
        }
        else if (text.StartsWith("default"))
        {
            int jumpTablepos = jumpTableStack.Peek();
            AssignJumpTable(jumpTablepos + 2, scriptCodePos - scriptCodeOffset);
            int cnt = System.Math.Abs(jumpTable[jumpTablepos + 1] - jumpTable[jumpTablepos]) + 1;

            int jOffset = jumpTablepos + 4;
            for (int i = 0; i < cnt; ++i)
            {
                if (jumpTable[jOffset + i] < 0)
                    AssignJumpTable(jOffset + i, scriptCodePos - scriptCodeOffset);
            }
            return true;
        }

        return false;
    }

    static void CheckAliasText(string text)
    {
        if (!text.StartsWith("#alias"))
            return;

        int textPos = 6;
        int aliasStrPos = 0;
        int parseMode = 0;

        var aliasInfo = new AliasInfo();

        while (parseMode < 2)
        {
            if (parseMode != 0)
            {
                if (parseMode == 1)
                {
                    if (textPos < text.Length)
                    {
                        aliasInfo.name += text[textPos];
                        aliasStrPos++;
                    }
                    else
                    {
                        aliasStrPos = 0;
                        ++parseMode;
                    }
                }
            }
            else if (text[textPos] == ':')
            {
                aliasStrPos = 0;
                parseMode = 1;
            }
            else
            {
                aliasInfo.value += text[textPos];
                aliasStrPos++;
            }
            ++textPos;
        }

        aliases.Add(aliasInfo);
    }

    static void CopyAliasStr(ref string dest, string text, bool arrayIndex)
    {
        int textPos = 0;
        int destPos = 0;
        bool arrayValue = false;
        if (arrayIndex)
        {
            while (textPos < text.Length)
            {
                if (arrayValue)
                {
                    if (text[textPos] == ']')
                    {
                        arrayValue = false;
                    }
                    else
                    {
                        if (destPos == 0)
                        {
                            dest = string.Empty;
                        }
                        dest += text[textPos];
                        destPos++;
                    }
                    ++textPos;
                }
                else
                {
                    if (text[textPos] == '[')
                        arrayValue = true;
                    ++textPos;
                }
            }
        }
        else
        {
            while (textPos < text.Length)
            {
                if (arrayValue)
                {
                    if (text[textPos] == ']')
                        arrayValue = false;
                    ++textPos;
                }
                else
                {
                    if (text[textPos] == '[')
                    {
                        arrayValue = true;
                    }
                    else
                    {
                        if (destPos == 0)
                        {
                            dest = string.Empty;
                        }
                        dest += text[textPos];
                        destPos++;
                    }
                    ++textPos;
                }
            }
        }
    }

    static void ConvertFunctionText(string text, int lineID)
    {
        var arrayStr = string.Empty;
        var funcName = string.Empty;
        int opcode = 0;
        int opcodeSize = 0;
        int textPos = 0;
        int namePos;
        for (namePos = 0; namePos < text.Length && text[namePos] != '('; ++namePos) funcName += text[namePos];
        for (int i = 0; i < functionInfoLookup.Length; ++i)
        {
            if (funcName == functionInfoLookup[i].Name)
            {
                opcode = i;
                opcodeSize = functionInfoLookup[i].NumOps;
                textPos = functionInfoLookup[i].Name.Length;
                i = functionInfoLookup.Length;
            }
        }

        if (opcode <= 0)
        {
            platform.PrintLog($"Script parsing failed: opcode {funcName} not found on line {lineID}");
            gameMode = EngineStates.ENGINE_SCRIPTERROR;
        }
        else
        {
            scriptCode[scriptCodePos++] = opcode;
            if ((ScrFunction)opcode == ScrFunction.FUNC_ELSE)
            {
                AssignJumpTable(jumpTableStack.Peek(), scriptCodePos - scriptCodeOffset);
                goto NameDecided;
            }
            if ((ScrFunction)opcode == ScrFunction.FUNC_ENDIF)
            {
                int jPos = jumpTableStack.Peek();
                AssignJumpTable(jPos + 1, scriptCodePos - scriptCodeOffset);
                if (jumpTable[jPos] == -1)
                    AssignJumpTable(jPos, scriptCodePos - scriptCodeOffset - 1);
                _ = jumpTableStack.Pop();
                goto NameDecided;
            }
            if ((ScrFunction)opcode == ScrFunction.FUNC_ENDSWITCH)
            {
                int jPos = jumpTableStack.Peek();
                AssignJumpTable(jPos + 3, scriptCodePos - scriptCodeOffset);
                if (jumpTable[jPos + 2] == -1)
                {
                    AssignJumpTable(jPos + 2, scriptCodePos - scriptCodeOffset - 1);
                    int caseCnt = System.Math.Abs(jumpTable[jPos + 1] - jumpTable[jPos]) + 1;

                    int jOffset = jPos + 4;
                    for (int c = 0; c < caseCnt; ++c)
                    {
                        if (jumpTable[jOffset + c] < 0)
                            AssignJumpTable(jOffset + c, jumpTable[jPos + 2]);
                    }
                }
                _ = jumpTableStack.Pop();
                goto NameDecided;
            }
            if ((ScrFunction)opcode == ScrFunction.FUNC_LOOP)
            {
                AssignJumpTable(jumpTableStack.Pop() + 1, scriptCodePos - scriptCodeOffset);
            }

        NameDecided:

            if (opcodeSize > 0)
            {
                var operands = text[(textPos + 1)..^1].Split(',', opcodeSize, StringSplitOptions.TrimEntries);

                for (int i = 0; i < opcodeSize; ++i)
                {
                    funcName = string.Empty;
                    arrayStr = string.Empty;

                    var split = operands[i].Split('[', ']');
                    if (operands[i].Contains('['))
                    {
                        funcName = split[0] + split[2];
                        arrayStr = split[1];
                    }
                    else
                    {
                        funcName = split[0];
                    }

                    // Eg: TempValue0 = FX_SCALE
                    for (int a = 0; a < aliases.Count; ++a)
                    {
                        if (funcName == aliases[a].name)
                        {
                            CopyAliasStr(ref funcName, aliases[a].value, false);
                            if (aliases[a].value.Contains('['))
                                CopyAliasStr(ref arrayStr, aliases[a].value, true);
                        }
                    }

                    // Eg: TempValue0 = Game.Variable
                    for (int v = 0; v < Engine.globalVariableNames.Count; ++v)
                    {
                        if (funcName == Engine.globalVariableNames[v])
                        {
                            funcName = "Global";
                            arrayStr = v.ToString();
                            goto ConvertedOperand;
                        }
                    }

                    // Eg: TempValue0 = Function1
                    for (int f = 0; f < scriptFunctionList.Count; ++f)
                    {
                        if (funcName == scriptFunctionList[f].name)
                        {
                            funcName = f.ToString();
                            goto ConvertedOperand;
                        }
                    }

                    // Eg: TempValue0 = TypeName[PlayerObject]
                    if (funcName == "TypeName")
                    {
                        funcName = "0";

                        for (int o = 0; o < OBJECT_COUNT; ++o)
                        {
                            if (arrayStr == typeNames[o])
                            {
                                funcName = o.ToString();
                            }
                        }
                    }

                ConvertedOperand:

                    int constant = 0;
                    if (TryParseInt(funcName, ref constant))
                    {
                        scriptCode[scriptCodePos++] = (int)ScriptVarTypes.SCRIPTVAR_INTCONST;
                        scriptCode[scriptCodePos++] = constant;
                    }
                    else if (funcName[0] == '"')
                    {
                        scriptCode[scriptCodePos++] = (int)ScriptVarTypes.SCRIPTVAR_STRCONST;
                        scriptCode[scriptCodePos++] = funcName.Length - 2;

                        int scriptTextPos = 1;
                        var arrayStrPos = 0;
                        while (scriptTextPos > -1)
                        {
                            switch (arrayStrPos)
                            {
                                case 0:
                                    scriptCode[scriptCodePos] = funcName[scriptTextPos] << 24;
                                    ++arrayStrPos;
                                    break;

                                case 1:
                                    scriptCode[scriptCodePos] += funcName[scriptTextPos] << 16;
                                    ++arrayStrPos;
                                    break;

                                case 2:
                                    scriptCode[scriptCodePos] += funcName[scriptTextPos] << 8;
                                    ++arrayStrPos;
                                    break;

                                case 3:
                                    scriptCode[scriptCodePos++] += funcName[scriptTextPos];
                                    arrayStrPos = 0;
                                    break;

                                default: break;
                            }

                            if (funcName[scriptTextPos] == '"')
                            {
                                if (arrayStrPos > 0)
                                    ++scriptCodePos;
                                scriptTextPos = -1;
                            }
                            else
                            {
                                scriptTextPos++;
                            }
                        }
                    }
                    else
                    {
                        scriptCode[scriptCodePos++] = (int)ScriptVarTypes.SCRIPTVAR_VAR;
                        if (arrayStr != string.Empty)
                        {
                            scriptCode[scriptCodePos] = (int)ScriptVarArrTypes.VARARR_ARRAY;

                            if (arrayStr[0] == '+')
                                scriptCode[scriptCodePos] = (int)ScriptVarArrTypes.VARARR_ENTNOPLUS1;

                            if (arrayStr[0] == '-')
                                scriptCode[scriptCodePos] = (int)ScriptVarArrTypes.VARARR_ENTNOMINUS1;

                            ++scriptCodePos;

                            if (arrayStr[0] == '-' || arrayStr[0] == '+')
                            {
                                arrayStr = arrayStr[1..];
                            }

                            if (TryParseInt(arrayStr, ref constant))
                            {
                                scriptCode[scriptCodePos++] = 0;
                                scriptCode[scriptCodePos++] = constant;
                            }
                            else
                            {
                                if (arrayStr == "ArrayPos0")
                                    constant = 0;
                                if (arrayStr == "ArrayPos1")
                                    constant = 1;
                                if (arrayStr == "TempObjectPos")
                                    constant = 2;

                                scriptCode[scriptCodePos++] = 1;
                                scriptCode[scriptCodePos++] = constant;
                            }
                        }
                        else
                        {
                            scriptCode[scriptCodePos++] = (int)ScriptVarArrTypes.VARARR_NONE;
                        }

                        constant = -1;
                        for (int iLocal = 0; iLocal < ScriptVarNames.names.Length; ++iLocal)
                        {
                            if (funcName == ScriptVarNames.names[iLocal])
                                constant = iLocal;
                        }

                        if (constant == -1 && gameMode != EngineStates.ENGINE_SCRIPTERROR)
                        {
                            platform.PrintLog($"Script parsing failed: operand {funcName} not found on line {lineID}");
                            gameMode = EngineStates.ENGINE_SCRIPTERROR;
                            constant = 0;
                        }

                        scriptCode[scriptCodePos++] = constant;
                    }
                }
            }
        }
    }

    static void CheckCaseNumber(string text)
    {
        text += '\0';
        if (!text.StartsWith("case"))
            return;

        var caseString = string.Empty;
        char caseChar = text[4];

        int textPos = 5;
        while (caseChar != 0)
        {
            if (caseChar != ':')
                caseString += caseChar;
            caseChar = text[textPos++];
        }

        for (int a = 0; a < aliases.Count; ++a)
        {
            if (aliases[a].name == caseString)
            {
                caseString = aliases[a].value;
                break;
            }
        }

        int caseID = 0;
        if (TryParseInt(caseString, ref caseID))
        {
            int stackValue = jumpTableStack.Peek();
            if (caseID < jumpTable[stackValue])
                AssignJumpTable(stackValue, caseID);
            stackValue++;
            if (caseID > jumpTable[stackValue])
                AssignJumpTable(stackValue, caseID);
        }
    }

    public static void ParseScriptFile(string scriptName, int scriptID)
    {
        jumpTableStack.Clear();
        lineID = 0;
        aliases.Clear();
        aliases.AddRange(defaultAliases);

        var scriptPath = string.Empty;
        scriptPath = "Scripts/";
        scriptPath += scriptName;
        if (platform.LoadFile(scriptPath, out var info))
        {
            using var reader = new BinaryReader(info.BaseStream);

            int readMode = (int)ScriptReadModes.READMODE_NORMAL;
            int parseMode = (int)ScriptParseModes.PARSEMODE_SCOPELESS;
            char prevChar = '\0';
            char curChar = '\0';
            int switchDeep = 0;
            var infoReadPos = 0L;

            var scriptTextBuilder = new StringBuilder();

            while (readMode < (int)ScriptReadModes.READMODE_EOF)
            {
                int textPos = 0;
                scriptText = string.Empty;
                readMode = (int)ScriptReadModes.READMODE_NORMAL;
                scriptTextBuilder.Clear();
                while (readMode < (int)ScriptReadModes.READMODE_ENDLINE)
                {
                    prevChar = curChar;
                    curChar = (char)reader.ReadByte();
                    if (readMode == (int)ScriptReadModes.READMODE_STRING)
                    {
                        if (curChar == '\t' || curChar == '\r' || curChar == '\n' || curChar == ';' || readMode >= (int)ScriptReadModes.READMODE_COMMENTLINE)
                        {
                            if ((curChar == '\n' && prevChar != '\r') || (curChar == '\n' && prevChar == '\r'))
                            {
                                readMode = (int)ScriptReadModes.READMODE_ENDLINE;
                            }
                        }
                        else if (curChar != '/' || textPos <= 0)
                        {
                            scriptTextBuilder.Append(curChar);
                            textPos++;
                            if (curChar == '"')
                            {
                                readMode = (int)ScriptReadModes.READMODE_NORMAL;
                            }
                        }
                        else if (curChar == '/' && prevChar == '/')
                        {
                            readMode = (int)ScriptReadModes.READMODE_COMMENTLINE;
                            textPos--;
                            scriptTextBuilder.Remove(scriptTextBuilder.Length - 1, 1);
                        }
                        else
                        {
                            textPos++;
                            scriptTextBuilder.Append(curChar);
                        }
                    }
                    else if (curChar == ' ' || curChar == '\t' || curChar == '\r' || curChar == '\n' || curChar == ';'
                             || readMode >= (int)ScriptReadModes.READMODE_COMMENTLINE)
                    {
                        if ((curChar == '\n' && prevChar != '\r') || (curChar == '\n' && prevChar == '\r'))
                        {
                            readMode = (int)ScriptReadModes.READMODE_ENDLINE;
                        }
                    }
                    else if (curChar != '/' || textPos <= 0)
                    {
                        textPos++;
                        scriptTextBuilder.Append(curChar);
                        if (curChar == '"' && readMode == 0)
                        {
                            readMode = (int)ScriptReadModes.READMODE_STRING;
                        }
                    }
                    else if (curChar == '/' && prevChar == '/')
                    {
                        readMode = (int)ScriptReadModes.READMODE_COMMENTLINE;
                        textPos--;
                        scriptTextBuilder.Remove(scriptTextBuilder.Length - 1, 1);
                    }
                    else
                    {
                        textPos++;
                        scriptTextBuilder.Append(curChar);
                    }
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                    {
                        readMode = (int)ScriptReadModes.READMODE_EOF;
                    }
                }

                scriptText = scriptTextBuilder.ToString();

                switch (parseMode)
                {
                    case (int)ScriptParseModes.PARSEMODE_SCOPELESS:
                        ++lineID;
                        CheckAliasText(scriptText);
                        if (scriptText == "subObjectMain")
                        {
                            parseMode = (int)ScriptParseModes.PARSEMODE_FUNCTION;
                            objectScriptList[scriptID].subMain.scriptCodePtr = scriptCodePos;
                            objectScriptList[scriptID].subMain.jumpTablePtr = jumpTablePos;
                            scriptCodeOffset = scriptCodePos;
                            jumpTableOffset = jumpTablePos;
                        }
                        if (scriptText == "subObjectPlayerInteraction")
                        {
                            parseMode = (int)ScriptParseModes.PARSEMODE_FUNCTION;
                            objectScriptList[scriptID].subPlayerInteraction.scriptCodePtr = scriptCodePos;
                            objectScriptList[scriptID].subPlayerInteraction.jumpTablePtr = jumpTablePos;
                            scriptCodeOffset = scriptCodePos;
                            jumpTableOffset = jumpTablePos;
                        }
                        if (scriptText == "subObjectDraw")
                        {
                            parseMode = (int)ScriptParseModes.PARSEMODE_FUNCTION;
                            objectScriptList[scriptID].subDraw.scriptCodePtr = scriptCodePos;
                            objectScriptList[scriptID].subDraw.jumpTablePtr = jumpTablePos;
                            scriptCodeOffset = scriptCodePos;
                            jumpTableOffset = jumpTablePos;
                        }
                        if (scriptText == "subObjectStartup")
                        {
                            parseMode = (int)ScriptParseModes.PARSEMODE_FUNCTION;
                            objectScriptList[scriptID].subStartup.scriptCodePtr = scriptCodePos;
                            objectScriptList[scriptID].subStartup.jumpTablePtr = jumpTablePos;
                            scriptCodeOffset = scriptCodePos;
                            jumpTableOffset = jumpTablePos;
                        }

                        if (scriptText.StartsWith("function"))
                        {
                            var funcName = string.Empty;
                            for (textPos = 8; textPos < scriptText.Length; ++textPos) funcName += scriptText[textPos];

                            int funcID = -1;
                            for (int f = 0; f < scriptFunctionList.Count; ++f)
                            {
                                if (funcName == scriptFunctionList[f].name)
                                    funcID = f;
                            }

                            if (funcID <= -1)
                            {
                                var scriptFunction = new ScriptFunction();
                                scriptFunction.name = funcName;
                                scriptFunction.ptr.scriptCodePtr = scriptCodePos;
                                scriptFunction.ptr.jumpTablePtr = jumpTablePos;
                                scriptFunctionList.Add(scriptFunction);
                                scriptCodeOffset = scriptCodePos;
                                jumpTableOffset = jumpTablePos;
                                parseMode = (int)ScriptParseModes.PARSEMODE_FUNCTION;
                            }
                            else
                            {
                                var scriptFunction = scriptFunctionList[funcID];
                                scriptFunction.name = funcName;
                                scriptFunction.ptr.scriptCodePtr = scriptCodePos;
                                scriptFunction.ptr.jumpTablePtr = jumpTablePos;
                                scriptFunctionList[funcID] = scriptFunction;
                                scriptCodeOffset = scriptCodePos;
                                jumpTableOffset = jumpTablePos;
                                parseMode = (int)ScriptParseModes.PARSEMODE_FUNCTION;
                            }
                        }
                        else if (scriptText.StartsWith("#function"))
                        {
                            var funcName = string.Empty;
                            for (textPos = 9; textPos < scriptText.Length; ++textPos) funcName += scriptText[textPos];

                            int funcID = -1;
                            for (int f = 0; f < scriptFunctionList.Count; ++f)
                            {
                                if (funcName == scriptFunctionList[f].name)
                                    funcID = f;
                            }

                            if (funcID == -1)
                            {
                                scriptFunctionList.Add(new() { name = funcName });
                            }

                            parseMode = (int)ScriptParseModes.PARSEMODE_SCOPELESS;
                        }
                        break;

                    case (int)ScriptParseModes.PARSEMODE_PLATFORMSKIP:
                        ++lineID;

                        if (scriptText.StartsWith("#endplatform"))
                            parseMode = (int)ScriptParseModes.PARSEMODE_FUNCTION;
                        break;

                    case (int)ScriptParseModes.PARSEMODE_FUNCTION:
                        ++lineID;

                        if (scriptText != string.Empty)
                        {
                            if (scriptText == "endsub")
                            {
                                scriptCode[scriptCodePos++] = (int)ScrFunction.FUNC_END;
                                parseMode = (int)ScriptParseModes.PARSEMODE_SCOPELESS;
                            }
                            else if (scriptText == "endfunction")
                            {
                                scriptCode[scriptCodePos++] = (int)ScrFunction.FUNC_ENDFUNCTION;
                                parseMode = (int)ScriptParseModes.PARSEMODE_SCOPELESS;
                            }
                            else if (scriptText.StartsWith("#platform:"))
                            {
                                if (!scriptText.Contains(gamePlatform)
                                    && !scriptText.Contains(Engine.gameRenderType)
                                    && !scriptText.Contains("Use_Haptics")
                                    && !scriptText.Contains(Engine.releaseType)
                                    && !scriptText.Contains("Use_Decomp")
                                )
                                { // if NONE of these checks succeeded, then we skip everything until "end platform"
                                    parseMode = (int)ScriptParseModes.PARSEMODE_PLATFORMSKIP;
                                }
                            }
                            else if (!scriptText.Contains("#endplatform"))
                            {
                                ConvertIfWhileStatement(ref scriptText);

                                if (ConvertSwitchStatement(ref scriptText))
                                {
                                    parseMode = (int)ScriptParseModes.PARSEMODE_SWITCHREAD;
                                    infoReadPos = reader.BaseStream.Position;
                                    switchDeep = 0;
                                }

                                ConvertArithmeticSyntax(ref scriptText);

                                if (!ReadSwitchCase(scriptText))
                                {
                                    ConvertFunctionText(scriptText, lineID);

                                    if (gameMode == EngineStates.ENGINE_SCRIPTERROR)
                                    {
                                        platform.PrintLog($"Script parsing failed: error in {scriptName}");
                                        parseMode = (int)ScriptParseModes.PARSEMODE_ERROR;
                                    }
                                }
                            }
                        }
                        break;

                    case (int)ScriptParseModes.PARSEMODE_SWITCHREAD:
                        if (scriptText.StartsWith("switch"))
                            ++switchDeep;

                        if (switchDeep != 0)
                        {
                            if (scriptText.StartsWith("endswitch"))
                                --switchDeep;
                        }
                        else if (scriptText.StartsWith("endswitch"))
                        {
                            reader.BaseStream.Seek(infoReadPos, SeekOrigin.Begin);
                            parseMode = (int)ScriptParseModes.PARSEMODE_FUNCTION;
                            int jPos = jumpTableStack.Peek();
                            switchDeep = System.Math.Abs(jumpTable[jPos + 1] - jumpTable[jPos]) + 1;
                            for (textPos = 0; textPos < switchDeep; ++textPos) AssignJumpTable(jumpTablePos++, -1);
                        }
                        else
                        {
                            CheckCaseNumber(scriptText);
                        }
                        break;

                    default: break;
                }
            }

            info.Dispose();
        }
    }

    static void AssignJumpTable(int index, int value)
    {
        jumpTable[index] = value;
    }
}