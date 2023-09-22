global using static Cosmic.Core.EngineStuff;
using System;
using System.Collections.Generic;
using Kaitai;

namespace Cosmic.Core;

public static unsafe class EngineStuff
{
    public const string CosmicID = "Cosmic Engine pre-alpha";
    internal static ICosmicPlatform platform;

    public enum GamePlatformTypes
    {
        Standard,
        Mobile,
    }

    public static GamePlatformTypes GamePlatform
    {
        get
        {
            if (platform.GAMEPLATFORMID == GamePlatformID.Android || platform.GAMEPLATFORMID == GamePlatformID.Ios)
            {
                return GamePlatformTypes.Mobile;
            }
            else
            {
                return GamePlatformTypes.Standard;
            }
        }
    }

    public enum GamePlatformID
    {
        Windows,
        MacOS,
        Xbox360,
        Ps3,
        Ios,
        Android,
        Wp7,
        Vita,
        Uwp,
        Linux,
    }

    public enum EngineLanguages { English = 0, French = 1, Italian = 2, German = 3, Spanish = 4, Japanese = 5 }

    public enum EngineStates
    {
        ENGINE_DEVMENU = 0,
        ENGINE_MAINGAME = 1,
        ENGINE_INITDEVMENU = 2,
        ENGINE_EXITGAME = 3,
        ENGINE_SCRIPTERROR = 4,
        ENGINE_ENTER_HIRESMODE = 5,
        ENGINE_EXIT_HIRESMODE = 6,
        ENGINE_PAUSE = 7,
        ENGINE_WAIT = 8,
        ENGINE_VIDEOWAIT = 9,
    }

    public enum EngineMessages
    {
        MESSAGE_NONE = 0,
        MESSAGE_MESSAGE_1 = 1,
        MESSAGE_LOSTFOCUS = 2,
        MESSAGE_YES_SELECTED = 3, // Used for old android confirmation popups
        MESSAGE_NO_SELECTED = 4, // Used for old android confirmation popups
    }

    enum ScriptCallbacks
    {
        CALLBACK_DISPLAYLOGOS = 0,
        CALLBACK_PRESS_START = 1,
        CALLBACK_TIMEATTACK_NOTIFY_ENTER = 2,
        CALLBACK_TIMEATTACK_NOTIFY_EXIT = 3,
        CALLBACK_FINISHGAME_NOTIFY = 4,
        CALLBACK_RETURNSTORE_SELECTED = 5,
        CALLBACK_RESTART_SELECTED = 6,
        CALLBACK_EXIT_SELECTED = 7,
        CALLBACK_BUY_FULL_GAME_SELECTED = 8,
        CALLBACK_TERMS_SELECTED = 9,
        CALLBACK_PRIVACY_SELECTED = 10,
        CALLBACK_TRIAL_ENDED = 11,
        CALLBACK_SETTINGS_SELECTED = 12,
        CALLBACK_PAUSE_REQUESTED = 13,
        CALLBACK_FULL_VERSION_ONLY = 14,
        CALLBACK_STAFF_CREDITS = 15,
        CALLBACK_MOREGAMES = 16,
        CALLBACK_SHOWREMOVEADS = 20,
        CALLBACK_AGEGATE = 100,

        // Sonic Origins Notify Callbacks
        NOTIFY_DEATH_EVENT = 128,
        NOTIFY_TOUCH_SIGNPOST = 129,
        NOTIFY_HUD_ENABLE = 130,
        NOTIFY_ADD_COIN = 131,
        NOTIFY_KILL_ENEMY = 132,
        NOTIFY_SAVESLOT_SELECT = 133,
        NOTIFY_FUTURE_PAST = 134,
        NOTIFY_GOTO_FUTURE_PAST = 135,
        NOTIFY_BOSS_END = 136,
        NOTIFY_SPECIAL_END = 137,
        NOTIFY_DEBUGPRINT = 138,
        NOTIFY_KILL_BOSS = 139,
        NOTIFY_TOUCH_EMERALD = 140,
        NOTIFY_STATS_ENEMY = 141,
        NOTIFY_STATS_CHARA_ACTION = 142,
        NOTIFY_STATS_RING = 143,
        NOTIFY_STATS_MOVIE = 144,
        NOTIFY_STATS_PARAM_1 = 145,
        NOTIFY_STATS_PARAM_2 = 146,
        NOTIFY_CHARACTER_SELECT = 147,
        NOTIFY_SPECIAL_RETRY = 148,
        NOTIFY_TOUCH_CHECKPOINT = 149,
        NOTIFY_ACT_FINISH = 150,
        NOTIFY_1P_VS_SELECT = 151,
        NOTIFY_CONTROLLER_SUPPORT = 152,
        NOTIFY_STAGE_RETRY = 153,
        NOTIFY_SOUND_TRACK = 154,
        NOTIFY_GOOD_ENDING = 155,
        NOTIFY_BACK_TO_MAINMENU = 156,
        NOTIFY_LEVEL_SELECT_MENU = 157,
        NOTIFY_PLAYER_SET = 158,
        NOTIFY_EXTRAS_MODE = 159,
        NOTIFY_SPIN_DASH_TYPE = 160,
        NOTIFY_TIME_OVER = 161,

        // Sega Forever stuff
        // Mod CBs start at about 1000
        CALLBACK_SHOWMENU_2 = 997,
        CALLBACK_SHOWHELPCENTER = 998,
        CALLBACK_CHANGEADSTYPE = 999,
        CALLBACK_NONE_1000 = 1000,
        CALLBACK_NONE_1001 = 1001,
        CALLBACK_NONE_1006 = 1002,
        CALLBACK_ONSHOWINTERSTITIAL = 1003,
        CALLBACK_ONSHOWBANNER = 1004,
        CALLBACK_ONSHOWBANNER_PAUSESTART = 1005,
        CALLBACK_ONHIDEBANNER = 1006,
        CALLBACK_REMOVEADSBUTTON_FADEOUT = 1007,
        CALLBACK_REMOVEADSBUTTON_FADEIN = 1008,
        CALLBACK_ONSHOWINTERSTITIAL_2 = 1009,
        CALLBACK_ONSHOWINTERSTITIAL_3 = 1010,
        CALLBACK_ONSHOWINTERSTITIAL_4 = 1011,
        CALLBACK_ONVISIBLEGRIDBTN_1 = 1012,
        CALLBACK_ONVISIBLEGRIDBTN_0 = 1013,
        CALLBACK_ONSHOWINTERSTITIAL_PAUSEDURATION = 1014,
        CALLBACK_SHOWCOUNTDOWNMENU = 1015,
        CALLBACK_ONVISIBLEMAINMENU_1 = 1016,
        CALLBACK_ONVISIBLEMAINMENU_0 = 1017,
    }

    public enum RenderTypes
    {
        RENDER_SW = 0,
        RENDER_HW = 1,
    }

    public enum BytecodeFormat
    {
        BYTECODE_MOBILE = 0,
        BYTECODE_PC = 1,
    }

    // General Defines
    public const int SCREEN_YSIZE = (240);
    public const int SCREEN_CENTERY = (SCREEN_YSIZE / 2);

    public class CosmicEngine
    {
        public CosmicEngine(ICosmicPlatform platform)
        {
            EngineStuff.platform = platform;
        }

        public void Init()
        {
            platform.InitUserdata();

            gameMode = EngineStates.ENGINE_EXITGAME;
            platform.running = false;
            if (LoadGameConfig("Data/Game/GameConfig.bin"))
            {
                if (platform.InitRenderDevice())
                {
                    if (InitAudioPlayback() != 0)
                    {
                        InitFirstStage();
                        ClearScriptData();
                        platform.running = true;
                        gameMode = EngineStates.ENGINE_MAINGAME;
                    }
                }
            }

            if (GamePlatform == GamePlatformTypes.Mobile)
                gamePlatform = "Mobile";
            else
                gamePlatform = "Standard";
        }

        public BytecodeFormat bytecodeMode = BytecodeFormat.BYTECODE_MOBILE;

        public EngineMessages message = 0;

        public bool trialMode = false;

        public bool hapticsEnabled = true;

        public int dimTimer = 0;
        public float dimPercent = 1.0f;

        public bool showPaletteOverlay = false;

        public void RunFrame()
        {
            platform.ProcessInput();

            shouldUpdateChunks = false;
            platform.ClearDrawLists();
            switch (gameMode)
            {
                case EngineStates.ENGINE_INITDEVMENU: // hack for now until scripts start getting modified
                case EngineStates.ENGINE_DEVMENU:
                    stageMode = StageModes.STAGEMODE_LOAD;
                    gameMode = EngineStates.ENGINE_MAINGAME;
                    goto DevMenuFallthrough;
                case EngineStates.ENGINE_MAINGAME:
                DevMenuFallthrough:
                    ProcessStage();
                    break;

                case EngineStates.ENGINE_EXITGAME: platform.running = false; break;

                case EngineStates.ENGINE_SCRIPTERROR:
                    LoadGameConfig("Data/Game/GameConfig.bin");
                    platform.InitError();
                    ResetCurrentStageFolder();
                    break;

                case EngineStates.ENGINE_VIDEOWAIT:
                    if (ProcessVideo() == 1)
                    {
                        gameMode = EngineStates.ENGINE_MAINGAME;
                    }
                    break;

                default: break;
            }
            if (shouldUpdateChunks)
            {
                platform.UpdateHWChunks();
            }
        }

        bool LoadGameConfig(string filePath)
        {
            globalVariables.Clear();

            if (platform.LoadFile(filePath, out var info))
            {
                var gameConfig = new Cosmic.Formats.GameConfig(new KaitaiStream(info.BaseStream));

                gameWindowText = gameConfig.GameName.Contents;

                gameDescriptionText = gameConfig.GameDescription.Contents;

                for (var i = 0; i < gameConfig.NumPlayerNames; i++)
                {
                    playerNames[i] = gameConfig.PlayerNames[i].Contents;
                }

                foreach (var globalVarDef in gameConfig.GlobalVars)
                {
                    globalVariableNames.Add(globalVarDef.Name.Contents);
                    globalVariables.Add(globalVarDef.DefaultValue);
                }

                SetGlobalVariableByName("Options.DevMenuFlag", 0);
                if (platform.gameDebugMode)
                {
                    SetGlobalVariableByName("Options.DevMenuFlag", 1);
                }

                SetGlobalVariableByName("Engine.PlatformId", (int)platform.GAMEPLATFORMID);
                SetGlobalVariableByName("Engine.DeviceType", (int)GamePlatform);

                stageListCount[(int)StageListNames.STAGELIST_PRESENTATION] = gameConfig.NumStagesPresentation;
                for (var i = 0; i < gameConfig.NumStagesPresentation; i++)
                {
                    stageList[(int)StageListNames.STAGELIST_PRESENTATION, i].name = gameConfig.StagesPresentation[i].Name.Contents;
                    stageList[(int)StageListNames.STAGELIST_PRESENTATION, i].id = gameConfig.StagesPresentation[i].ShortName.Contents;
                    stageList[(int)StageListNames.STAGELIST_PRESENTATION, i].folder = gameConfig.StagesPresentation[i].Folder.Contents;
                    stageList[(int)StageListNames.STAGELIST_PRESENTATION, i].highlighted = gameConfig.StagesPresentation[i].Highlighted != 0;
                }

                stageListCount[(int)StageListNames.STAGELIST_REGULAR] = gameConfig.NumStagesRegular;
                for (var i = 0; i < gameConfig.NumStagesRegular; i++)
                {
                    stageList[(int)StageListNames.STAGELIST_REGULAR, i].name = gameConfig.StagesRegular[i].Name.Contents;
                    stageList[(int)StageListNames.STAGELIST_REGULAR, i].id = gameConfig.StagesRegular[i].ShortName.Contents;
                    stageList[(int)StageListNames.STAGELIST_REGULAR, i].folder = gameConfig.StagesRegular[i].Folder.Contents;
                    stageList[(int)StageListNames.STAGELIST_REGULAR, i].highlighted = gameConfig.StagesRegular[i].Highlighted != 0;
                }

                stageListCount[(int)StageListNames.STAGELIST_SPECIAL] = gameConfig.NumStagesSpecial;
                for (var i = 0; i < gameConfig.NumStagesSpecial; i++)
                {
                    stageList[(int)StageListNames.STAGELIST_SPECIAL, i].name = gameConfig.StagesSpecial[i].Name.Contents;
                    stageList[(int)StageListNames.STAGELIST_SPECIAL, i].id = gameConfig.StagesSpecial[i].ShortName.Contents;
                    stageList[(int)StageListNames.STAGELIST_SPECIAL, i].folder = gameConfig.StagesSpecial[i].Folder.Contents;
                    stageList[(int)StageListNames.STAGELIST_SPECIAL, i].highlighted = gameConfig.StagesSpecial[i].Highlighted != 0;
                }

                stageListCount[(int)StageListNames.STAGELIST_BONUS] = gameConfig.NumStagesBonus;
                for (var i = 0; i < gameConfig.NumStagesBonus; i++)
                {
                    stageList[(int)StageListNames.STAGELIST_BONUS, i].name = gameConfig.StagesBonus[i].Name.Contents;
                    stageList[(int)StageListNames.STAGELIST_BONUS, i].id = gameConfig.StagesBonus[i].ShortName.Contents;
                    stageList[(int)StageListNames.STAGELIST_BONUS, i].folder = gameConfig.StagesBonus[i].Folder.Contents;
                    stageList[(int)StageListNames.STAGELIST_BONUS, i].highlighted = gameConfig.StagesBonus[i].Highlighted != 0;
                }

                info.Dispose();

                return true;
            }

            return false;
        }

        public bool hasFocus = true;

        public void Callback(int callbackID)
        {
            // Sonic Origins Params
            int notifyParam1 = GetGlobalVariableByName("game.callbackParam0");

            switch ((ScriptCallbacks)callbackID)
            {
                case ScriptCallbacks.CALLBACK_DISPLAYLOGOS: // Display Logos, Called immediately
                    if (activeStageList != 0)
                    {
                        gameMode = (EngineStates)7;
                    }
                    platform.PrintLog("Callback: Display Logos");
                    break;
                case ScriptCallbacks.CALLBACK_PRESS_START: // Called when "Press Start" is activated, PC = NONE
                    if (activeStageList != 0)
                    {
                        gameMode = (EngineStates)7;
                    }
                    platform.PrintLog("Callback: Press Start");
                    break;
                case ScriptCallbacks.CALLBACK_RETURNSTORE_SELECTED:
                    gameMode = EngineStates.ENGINE_EXITGAME;
                    platform.PrintLog("Callback: Return To Store Selected");
                    break;
                case ScriptCallbacks.CALLBACK_RESTART_SELECTED:
                    platform.PrintLog("Callback: Restart Selected");
                    stageMode = StageModes.STAGEMODE_LOAD;
                    break;
                case ScriptCallbacks.CALLBACK_EXIT_SELECTED:
                    // gameMode = ENGINE_EXITGAME;
                    platform.PrintLog("Callback: Exit Selected");
                    if (bytecodeMode == BytecodeFormat.BYTECODE_PC)
                    {
                        platform.running = false;
                    }
                    else
                    {
                        activeStageList = 0;
                        stageListPosition = 0;
                        stageMode = StageModes.STAGEMODE_LOAD;
                    }
                    break;
                case ScriptCallbacks.CALLBACK_BUY_FULL_GAME_SELECTED: //, Mobile = Buy Full Game Selected (Trial Mode Only)
                    gameMode = EngineStates.ENGINE_EXITGAME;
                    platform.PrintLog("Callback: Buy Full Game Selected");
                    break;
                case ScriptCallbacks.CALLBACK_TERMS_SELECTED: // PC = How to play, Mobile = Full Game Only Screen
                                                              // PC doesn't have hi res mode
                    if (bytecodeMode == BytecodeFormat.BYTECODE_PC)
                    {
                        for (int s = 0; s < stageListCount[(int)StageListNames.STAGELIST_PRESENTATION]; ++s)
                        {
                            if ("HELP" == stageList[(int)StageListNames.STAGELIST_PRESENTATION, s].name)
                            {
                                activeStageList = StageListNames.STAGELIST_PRESENTATION;
                                stageListPosition = s;
                                stageMode = StageModes.STAGEMODE_LOAD;
                            }
                        }
                    }
                    platform.PrintLog("Callback: PC = How to play Menu, Mobile = Terms & Conditions Screen");
                    break;
                case ScriptCallbacks.CALLBACK_STAFF_CREDITS:    // PC = Staff Credits, Mobile = Privacy
                    if (bytecodeMode == BytecodeFormat.BYTECODE_PC)
                    {
                        for (int s = 0; s < stageListCount[(int)StageListNames.STAGELIST_PRESENTATION]; ++s)
                        {
                            if ("CREDITS" == stageList[(int)StageListNames.STAGELIST_PRESENTATION, s].name)
                            {
                                activeStageList = StageListNames.STAGELIST_PRESENTATION;
                                stageListPosition = s;
                                stageMode = StageModes.STAGEMODE_LOAD;
                            }
                        }
                        platform.PrintLog("Callback: Staff Credits Requested");
                    }
                    else
                    {
                        // Go to this URL http://www.sega.com/legal/privacy_mobile.php
                        platform.PrintLog("Callback: Privacy Requested");
                    }
                    break;

                case ScriptCallbacks.CALLBACK_AGEGATE:
                    platform.PrintLog("Callback: Age Gate");
                    // Newer versions of the game wont continue without this
                    // Thanks to Sappharad for pointing this out
                    SetGlobalVariableByName("HaveLoadAllGDPRValue", 1);
                    break;

                // Sonic Origins
                case ScriptCallbacks.NOTIFY_FUTURE_PAST:
                    platform.PrintLog($"NOTIFY: FuturePast() -> {notifyParam1}");
                    objectEntityList[objectLoop].state++;
                    break;
                case ScriptCallbacks.NOTIFY_CHARACTER_SELECT:
                    platform.PrintLog($"NOTIFY: CharacterSelect() -> {notifyParam1}");
                    SetGlobalVariableByName("game.callbackResult", 1);
                    SetGlobalVariableByName("game.continueFlag", 0);
                    break;
            }
        }

        public static readonly string[] gameRenderTypes = { "SW_Rendering", "HW_Rendering" };

        public readonly string gameRenderType = gameRenderTypes[(int)RenderTypes.RENDER_SW];
        public readonly string releaseType = "Use_Standalone";

        public uint framesSinceSceneLoad = 0;

        public readonly List<int> globalVariables = new();
        public readonly List<string> globalVariableNames = new();

        public static string[] GetPlayerNames()
        {
            var ret = new string[playerNames.Length];
            playerNames.CopyTo(ret, 0);
            return ret;
        }
        public static void SetPlayer(int playerIndex)
        {
            playerListPos = playerIndex;
        }

        public void RestartGame()
        {
            activeStageList = 0;
            stageListPosition = 0;
            stageMode = StageModes.STAGEMODE_LOAD;
            gameMode = EngineStates.ENGINE_MAINGAME;
        }

        public void RestartScene()
        {
            ResetCurrentStageFolder(); // reload all assets & scripts
            stageMode = StageModes.STAGEMODE_LOAD;
        }
    }

    public static string gameWindowText = string.Empty;
    public static string gameDescriptionText = string.Empty;
    public const string gameVersion = "0.0.0";
    public static string gamePlatform;
    public static EngineStates gameMode = EngineStates.ENGINE_MAINGAME;

    public static readonly int[] saveRAM = new int[0x2000];

    public static CosmicEngine Engine;

    public enum OnlineMenuTypes
    {
        ONLINEMENU_ACHIEVEMENTS = 0,
        ONLINEMENU_LEADERBOARDS = 1,
    }
}