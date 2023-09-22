using System;
using System.IO;
using Cosmic.Core;
using static Cosmic.Core.Audio;
using static Cosmic.Core.EngineStuff;
using static Cosmic.Core.Input;
using static Cosmic.Core.Stage;

namespace Cosmic.Desktop;

sealed partial class DesktopCosmicPlatform : ICosmicPlatform
{
    const int ACHIEVEMENT_COUNT = (0x40);
    const int LEADERBOARD_COUNT = (0x80);

    struct Achievement
    {
        public string name;
        public int status;
    }

    struct LeaderboardEntry
    {
        public int score;
    }

    public void LoadAchievementsMenu() { ReadUserdata(); }
    public void LoadLeaderboardsMenu() { ReadUserdata(); }

    readonly Achievement[] achievements = new Achievement[ACHIEVEMENT_COUNT];
    readonly LeaderboardEntry[] leaderboards = new LeaderboardEntry[LEADERBOARD_COUNT];

    bool useSGame = false;
    readonly string savePath = string.Empty;

    public string GamePath =>
#if DEBUG
        "./";
#else
        "./"; // Path.GetDirectoryName(System.AppContext.BaseDirectory)!;
#endif

    public bool ReadSaveRAMData()
    {
        useSGame = false;
        var buffer = $"{GamePath}/SData.bin";

        saveRAM[33] = bgmVolume;
        saveRAM[34] = sfxVolume;

        if (!File.Exists(buffer))
        {
            buffer = $"{GamePath}/{savePath}SGame.bin";

            if (!File.Exists(buffer))
                return false;
            useSGame = true;
        }
        using var saveFile = File.OpenRead(buffer);
        using var binaryReader = new BinaryReader(saveFile);
        for (var i = 0; i < saveRAM.Length; i++)
        {
            saveRAM[i] = binaryReader.ReadInt32();
        }

        return true;
    }

    public bool WriteSaveRAMData()
    {
        string buffer;
        if (!useSGame)
        {
            buffer = $"{GamePath}/SData.bin";
        }
        else
        {
            buffer = $"{GamePath}/SGame.bin";
        }

        try
        {
            using var saveFile = File.OpenWrite(buffer);
            using var binaryWriter = new BinaryWriter(saveFile);

            saveRAM[33] = bgmVolume;
            saveRAM[34] = sfxVolume;

            for (var i = 0; i < saveRAM.Length; i++)
            {
                binaryWriter.Write(saveRAM[i]);
            }
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    static bool TryGetValue(IniFile ini, string category, string valueName, ref bool value)
    {
        if (ini[category][valueName].Value != null)
        {
            value = ini[category][valueName].ToBool();
            return true;
        }
        return false;
    }
    static bool TryGetValue(IniFile ini, string category, string valueName, ref int value)
    {
        if (ini[category][valueName].Value != null)
        {
            value = ini[category][valueName].ToInt();
            return true;
        }
        return false;
    }

    static bool TryGetValue(IniFile ini, string category, string valueName, ref float value)
    {
        if (ini[category][valueName].Value != null)
        {
            value = (float)ini[category][valueName].ToDouble();
            return true;
        }
        return false;
    }

    static bool TryGetValue(IniFile ini, string category, string valueName, ref double value)
    {
        if (ini[category][valueName].Value != null)
        {
            value = ini[category][valueName].ToDouble();
            return true;
        }
        return false;
    }

    static bool TryGetValue(IniFile ini, string category, string valueName, ref string value)
    {
        if (ini[category][valueName].Value != null)
        {
            value = ini[category][valueName].ToString();
            return true;
        }
        return false;
    }

    public void InitUserdata()
    {
        var buffer = GamePath + "/settings.ini";

        var keyMappings = GetDefaultKeyboardMappings();
        var contMappings = GetDefaultControllerMappings();

        var ini = new IniFile();

        if (!File.Exists(buffer))
        {
            ini["Dev"]["GameDebugMode"] = (gameDebugMode = false);
            ini["Dev"]["StartingCategory"] = ((int)(startList = StageListNames.STAGELIST_PRESENTATION));
            ini["Dev"]["StartingScene"] = (startStage = 0);
            ini["Dev"]["FastForwardSpeed"] = (fastForwardSpeed = 8);

            ini["Game"]["Language"] = ((int)(language = EngineLanguages.English));
            ini["Game"]["DisableFocusPause"] = (disableFocusPause = 0);
            disableFocusPause_Config = disableFocusPause;

            ini["Window"]["Borderless"] = (borderless = false);
            ini["Window"]["VSync"] = (VSync = false);
            ini["Window"]["RefreshRate"] = (refreshRate = 60);
            ini["Window"]["DimLimit"] = (dimLimit = 300);
            dimLimit *= refreshRate;

            ini["Window"]["DefaultGameViewScale"] = (gameViewScale = 2);
            ini["Window"]["Render2x"] = (render2x = false);

            ini["Audio"]["BGMVolume"] = (bgmVolume / (float)MAX_VOLUME);
            ini["Audio"]["SFXVolume"] = (sfxVolume / (float)MAX_VOLUME);

            ini["Keyboard 1"].Comment = "Keyboard Mappings for P1 (Based on: https://github.com/libsdl-org/sdlwiki/blob/main/SDL2/SDLScancodeLookup.mediawiki)";
            ini["Keyboard 1"]["Up"] = ((int)(inputDevice[(int)InputButtons.INPUT_UP].keyMappings = keyMappings[(int)InputButtons.INPUT_UP]));
            ini["Keyboard 1"]["Down"] = ((int)(inputDevice[(int)InputButtons.INPUT_DOWN].keyMappings = keyMappings[(int)InputButtons.INPUT_DOWN]));
            ini["Keyboard 1"]["Left"] = ((int)(inputDevice[(int)InputButtons.INPUT_LEFT].keyMappings = keyMappings[(int)InputButtons.INPUT_LEFT]));
            ini["Keyboard 1"]["Right"] = ((int)(inputDevice[(int)InputButtons.INPUT_RIGHT].keyMappings = keyMappings[(int)InputButtons.INPUT_RIGHT]));
            ini["Keyboard 1"]["A"] = ((int)(inputDevice[(int)InputButtons.INPUT_BUTTONA].keyMappings = keyMappings[(int)InputButtons.INPUT_BUTTONA]));
            ini["Keyboard 1"]["B"] = ((int)(inputDevice[(int)InputButtons.INPUT_BUTTONB].keyMappings = keyMappings[(int)InputButtons.INPUT_BUTTONB]));
            ini["Keyboard 1"]["C"] = ((int)(inputDevice[(int)InputButtons.INPUT_BUTTONC].keyMappings = keyMappings[(int)InputButtons.INPUT_BUTTONC]));
            ini["Keyboard 1"]["Start"] = ((int)(inputDevice[(int)InputButtons.INPUT_START].keyMappings = keyMappings[(int)InputButtons.INPUT_START]));

            ini["Controller 1"].Comment = "Controller Mappings for P1 (Based on: https://github.com/libsdl-org/sdlwiki/blob/main/SDL2/SDL_GameControllerButton.mediawiki)";
            ini["Controller 1"]["Up"] = ((int)(inputDevice[(int)InputButtons.INPUT_UP].contMappings = contMappings[(int)InputButtons.INPUT_UP]));
            ini["Controller 1"]["Down"] = ((int)(inputDevice[(int)InputButtons.INPUT_DOWN].contMappings = contMappings[(int)InputButtons.INPUT_DOWN]));
            ini["Controller 1"]["Left"] = ((int)(inputDevice[(int)InputButtons.INPUT_LEFT].contMappings = contMappings[(int)InputButtons.INPUT_LEFT]));
            ini["Controller 1"]["Right"] = ((int)(inputDevice[(int)InputButtons.INPUT_RIGHT].contMappings = contMappings[(int)InputButtons.INPUT_RIGHT]));
            ini["Controller 1"]["A"] = ((int)(inputDevice[(int)InputButtons.INPUT_BUTTONA].contMappings = contMappings[(int)InputButtons.INPUT_BUTTONA]));
            ini["Controller 1"]["B"] = ((int)(inputDevice[(int)InputButtons.INPUT_BUTTONB].contMappings = contMappings[(int)InputButtons.INPUT_BUTTONB]));
            ini["Controller 1"]["C"] = ((int)(inputDevice[(int)InputButtons.INPUT_BUTTONC].contMappings = contMappings[(int)InputButtons.INPUT_BUTTONC]));
            ini["Controller 1"]["Start"] = ((int)(inputDevice[(int)InputButtons.INPUT_START].contMappings = contMappings[(int)InputButtons.INPUT_START]));

            ini["Controller 1"]["LStickDeadzone"] = (LSTICK_DEADZONE = 0.3f);
            ini["Controller 1"]["RStickDeadzone"] = (RSTICK_DEADZONE = 0.3f);
            ini["Controller 1"]["LTriggerDeadzone"] = (LTRIGGER_DEADZONE = 0.3f);
            ini["Controller 1"]["RTriggerDeadzone"] = (RTRIGGER_DEADZONE = 0.3f);

            ini.Save(buffer);
        }
        else
        {
            ini.Load(buffer);

            var gameDebugModeTmp = false;
            // if (ini.GetBool("Dev", "GameDebugMode", ref gameDebugModeTmp) == 0)
            if (!TryGetValue(ini, "Dev", "GameDebugMode", ref gameDebugModeTmp))
                gameDebugMode = false;
            else
                gameDebugMode = gameDebugModeTmp;
            var startListTmp = 0;
            if (!TryGetValue(ini, "Dev", "StartingCategory", ref startListTmp))
                startList = 0;
            else
                startList = (StageListNames)startListTmp;
            var startStageTmp = 0;
            if (!TryGetValue(ini, "Dev", "StartingScene", ref startStageTmp))
                startStage = 0;
            else
                startStage = startStageTmp;
            if (!TryGetValue(ini, "Dev", "FastForwardSpeed", ref fastForwardSpeed))
                fastForwardSpeed = 8;

            var languageTmp = 0;
            if (!TryGetValue(ini, "Game", "Language", ref languageTmp))
                language = EngineLanguages.English;
            else
                language = (EngineLanguages)languageTmp;

            if (!TryGetValue(ini, "Game", "DisableFocusPause", ref disableFocusPause))
                disableFocusPause = 0;
            disableFocusPause_Config = disableFocusPause;

            if (!TryGetValue(ini, "Window", "Borderless", ref borderless))
                borderless = false;
            var tmpVSync = VSync;
            if (!TryGetValue(ini, "Window", "VSync", ref tmpVSync))
                VSync = false;
            else
                VSync = tmpVSync;
            var refreshRateTmp = 0;
            if (!TryGetValue(ini, "Window", "RefreshRate", ref refreshRateTmp))
                refreshRate = 60;
            else
                refreshRate = refreshRateTmp;
            if (!TryGetValue(ini, "Window", "DimLimit", ref dimLimit))
                dimLimit = 300; // 5 mins
            if (dimLimit >= 0)
                dimLimit *= refreshRate;

            var defaultGameViewScaleTmp = gameViewScale;
            if (!TryGetValue(ini, "Window", "DefaultGameViewScale", ref defaultGameViewScaleTmp))
                defaultGameViewScaleTmp = 2;
            gameViewScale = defaultGameViewScaleTmp;
            if (!TryGetValue(ini, "Window", "Render2x", ref render2x))
                render2x = false;

            float bv = 0, sv = 0;
            if (!TryGetValue(ini, "Audio", "BGMVolume", ref bv))
                bv = 1.0f;
            if (!TryGetValue(ini, "Audio", "SFXVolume", ref sv))
                sv = 1.0f;

            bgmVolume = (int)(bv * MAX_VOLUME);
            sfxVolume = (int)(sv * MAX_VOLUME);

            if (bgmVolume > MAX_VOLUME)
                bgmVolume = MAX_VOLUME;
            if (bgmVolume < 0)
                bgmVolume = 0;

            if (sfxVolume > MAX_VOLUME)
                sfxVolume = MAX_VOLUME;
            if (sfxVolume < 0)
                sfxVolume = 0;

            if (!TryGetValue(ini, "Keyboard 1", "Up", ref inputDevice[(int)InputButtons.INPUT_UP].keyMappings))
                inputDevice[0].keyMappings = keyMappings[(int)InputButtons.INPUT_UP];
            if (!TryGetValue(ini, "Keyboard 1", "Down", ref inputDevice[(int)InputButtons.INPUT_DOWN].keyMappings))
                inputDevice[1].keyMappings = keyMappings[(int)InputButtons.INPUT_DOWN];
            if (!TryGetValue(ini, "Keyboard 1", "Left", ref inputDevice[(int)InputButtons.INPUT_LEFT].keyMappings))
                inputDevice[2].keyMappings = keyMappings[(int)InputButtons.INPUT_LEFT];
            if (!TryGetValue(ini, "Keyboard 1", "Right", ref inputDevice[(int)InputButtons.INPUT_RIGHT].keyMappings))
                inputDevice[3].keyMappings = keyMappings[(int)InputButtons.INPUT_RIGHT];
            if (!TryGetValue(ini, "Keyboard 1", "A", ref inputDevice[(int)InputButtons.INPUT_BUTTONA].keyMappings))
                inputDevice[4].keyMappings = keyMappings[(int)InputButtons.INPUT_BUTTONA];
            if (!TryGetValue(ini, "Keyboard 1", "B", ref inputDevice[(int)InputButtons.INPUT_BUTTONB].keyMappings))
                inputDevice[5].keyMappings = keyMappings[(int)InputButtons.INPUT_BUTTONB];
            if (!TryGetValue(ini, "Keyboard 1", "C", ref inputDevice[(int)InputButtons.INPUT_BUTTONC].keyMappings))
                inputDevice[6].keyMappings = keyMappings[(int)InputButtons.INPUT_BUTTONC];
            if (!TryGetValue(ini, "Keyboard 1", "Start", ref inputDevice[(int)InputButtons.INPUT_START].keyMappings))
                inputDevice[7].keyMappings = keyMappings[(int)InputButtons.INPUT_START];

            if (!TryGetValue(ini, "Controller 1", "Up", ref inputDevice[(int)InputButtons.INPUT_UP].contMappings))
                inputDevice[0].contMappings = contMappings[(int)InputButtons.INPUT_UP];
            if (!TryGetValue(ini, "Controller 1", "Down", ref inputDevice[(int)InputButtons.INPUT_DOWN].contMappings))
                inputDevice[1].contMappings = contMappings[(int)InputButtons.INPUT_DOWN];
            if (!TryGetValue(ini, "Controller 1", "Left", ref inputDevice[(int)InputButtons.INPUT_LEFT].contMappings))
                inputDevice[2].contMappings = contMappings[(int)InputButtons.INPUT_LEFT];
            if (!TryGetValue(ini, "Controller 1", "Right", ref inputDevice[(int)InputButtons.INPUT_RIGHT].contMappings))
                inputDevice[3].contMappings = contMappings[(int)InputButtons.INPUT_RIGHT];
            if (!TryGetValue(ini, "Controller 1", "A", ref inputDevice[(int)InputButtons.INPUT_BUTTONA].contMappings))
                inputDevice[4].contMappings = contMappings[(int)InputButtons.INPUT_BUTTONA];
            if (!TryGetValue(ini, "Controller 1", "B", ref inputDevice[(int)InputButtons.INPUT_BUTTONB].contMappings))
                inputDevice[5].contMappings = contMappings[(int)InputButtons.INPUT_BUTTONB];
            if (!TryGetValue(ini, "Controller 1", "C", ref inputDevice[(int)InputButtons.INPUT_BUTTONC].contMappings))
                inputDevice[6].contMappings = contMappings[(int)InputButtons.INPUT_BUTTONC];
            if (!TryGetValue(ini, "Controller 1", "Start", ref inputDevice[(int)InputButtons.INPUT_START].contMappings))
                inputDevice[7].contMappings = contMappings[(int)InputButtons.INPUT_START];

            if (!TryGetValue(ini, "Controller 1", "LStickDeadzone", ref LSTICK_DEADZONE))
                LSTICK_DEADZONE = 0.3f;
            if (!TryGetValue(ini, "Controller 1", "RStickDeadzone", ref RSTICK_DEADZONE))
                RSTICK_DEADZONE = 0.3f;
            if (!TryGetValue(ini, "Controller 1", "LTriggerDeadzone", ref LTRIGGER_DEADZONE))
                LTRIGGER_DEADZONE = 0.3f;
            if (!TryGetValue(ini, "Controller 1", "RTriggerDeadzone", ref RTRIGGER_DEADZONE))
                RTRIGGER_DEADZONE = 0.3f;
        }

        buffer = GamePath + "/Udata.bin";
        if (File.Exists(buffer))
        {
            ReadUserdata();
        }
        else
        {
            WriteUserdata();
        }

        if (LoadFile("Game/Achievements.txt", out var reader))
        {
            using (var textReader = new StreamReader(reader.BaseStream))
            {
                var achievementNameNum = 0;
                var name = string.Empty;
                while ((name = textReader.ReadLine()) != null)
                {
                    achievements[achievementNameNum++].name = name;
                }
            }

            reader.Dispose();
        }
    }

    public void WriteSettings()
    {
        var ini = new IniFile();

        ini["Dev"].SetValueComment("GameDebugMode", "Enable this flag to activate debugging features for the game");
        ini["Dev"]["GameDebugMode"] = gameDebugMode;
        ini["Dev"].SetValueComment("StartingCategory", "Sets the starting category ID");
        ini["Dev"]["StartingCategory"] = (int)startList;
        ini["Dev"].SetValueComment("StartingScene", "Sets the starting scene ID");
        ini["Dev"]["StartingScene"] = (startStage);
        ini["Dev"].SetValueComment("FastForwardSpeed", "Determines how fast the game will be when fastforwarding is active");
        ini["Dev"]["FastForwardSpeed"] = (fastForwardSpeed);

        ini["Game"].SetValueComment("Language", "Sets the game language (0 = EN, 1 = FR, 2 = IT, 3 = DE, 4 = ES, 5 = JP)");
        ini["Game"]["Language"] = ((int)language);
        ini["Game"].SetValueComment("DisableFocusPause", "Handles pausing behaviour when focus is lost\n; 0 = Game focus enabled, engine focus enabled\n; 1 = Game focus enabled, engine focus disabled\n; 2 = Game focus disabled, engine focus disabled");
        ini["Game"]["DisableFocusPause"] = (disableFocusPause_Config);

        ini["Window"].SetValueComment("Borderless", "Determines if the window will be borderless or not");
        ini["Window"]["Borderless"] = (borderless);
        ini["Window"].SetValueComment("VSync", "Determines if VSync will be active or not (not recommended as the engine is built around running at 60 FPS)");
        ini["Window"]["VSync"] = (VSync);
        ini["Window"].SetValueComment("RefreshRate", "Determines the target FPS");
        ini["Window"]["RefreshRate"] = (refreshRate);
        ini["Window"].SetValueComment("DimLimit", "Determines the dim timer in seconds, set to -1 to disable dimming");
        ini["Window"]["DimLimit"] = (dimLimit >= 0 ? dimLimit / refreshRate : -1);
        ini["Window"].SetValueComment("DefaultGameViewScale", "On startup, the window will display the game at\n; its base resolution multiplied by this value");
        ini["Window"]["DefaultGameViewScale"] = (gameViewScale);
        ini["Window"]["Render2x"] = render2x;

        ini["Audio"]["BGMVolume"] = (bgmVolume / (float)MAX_VOLUME);
        ini["Audio"]["SFXVolume"] = (sfxVolume / (float)MAX_VOLUME);

        ini["Keyboard 1"].Comment = ("Keyboard Mappings for P1 (Based on: https://github.com/libsdl-org/sdlwiki/blob/main/SDLScancodeLookup.mediawiki)");
        ini["Keyboard 1"]["Up"] = ((int)inputDevice[(int)InputButtons.INPUT_UP].keyMappings);
        ini["Keyboard 1"]["Down"] = ((int)inputDevice[(int)InputButtons.INPUT_DOWN].keyMappings);
        ini["Keyboard 1"]["Left"] = ((int)inputDevice[(int)InputButtons.INPUT_LEFT].keyMappings);
        ini["Keyboard 1"]["Right"] = ((int)inputDevice[(int)InputButtons.INPUT_RIGHT].keyMappings);
        ini["Keyboard 1"]["A"] = ((int)inputDevice[(int)InputButtons.INPUT_BUTTONA].keyMappings);
        ini["Keyboard 1"]["B"] = ((int)inputDevice[(int)InputButtons.INPUT_BUTTONB].keyMappings);
        ini["Keyboard 1"]["C"] = ((int)inputDevice[(int)InputButtons.INPUT_BUTTONC].keyMappings);
        ini["Keyboard 1"]["Start"] = ((int)inputDevice[(int)InputButtons.INPUT_START].keyMappings);

        ini["Controller 1"].Comment =
            "Controller Mappings for P1 (Based on: https://github.com/libsdl-org/sdlwiki/blob/main/SDL_GameControllerButton.mediawiki)" + Environment.NewLine +
            "Extra buttons can be mapped with the following IDs:" + Environment.NewLine +
            "CONTROLLER_BUTTON_ZL             = 16" + Environment.NewLine +
            "CONTROLLER_BUTTON_ZR             = 17" + Environment.NewLine +
            "CONTROLLER_BUTTON_LSTICK_UP      = 18" + Environment.NewLine +
            "CONTROLLER_BUTTON_LSTICK_DOWN    = 19" + Environment.NewLine +
            "CONTROLLER_BUTTON_LSTICK_LEFT    = 20" + Environment.NewLine +
            "CONTROLLER_BUTTON_LSTICK_RIGHT   = 21" + Environment.NewLine +
            "CONTROLLER_BUTTON_RSTICK_UP      = 22" + Environment.NewLine +
            "CONTROLLER_BUTTON_RSTICK_DOWN    = 23" + Environment.NewLine +
            "CONTROLLER_BUTTON_RSTICK_LEFT    = 24" + Environment.NewLine +
            "CONTROLLER_BUTTON_RSTICK_RIGHT   = 25";
        ini["Controller 1"]["Up"] = ((int)inputDevice[(int)InputButtons.INPUT_UP].contMappings);
        ini["Controller 1"]["Down"] = ((int)inputDevice[(int)InputButtons.INPUT_DOWN].contMappings);
        ini["Controller 1"]["Left"] = ((int)inputDevice[(int)InputButtons.INPUT_LEFT].contMappings);
        ini["Controller 1"]["Right"] = ((int)inputDevice[(int)InputButtons.INPUT_RIGHT].contMappings);
        ini["Controller 1"]["A"] = ((int)inputDevice[(int)InputButtons.INPUT_BUTTONA].contMappings);
        ini["Controller 1"]["B"] = ((int)inputDevice[(int)InputButtons.INPUT_BUTTONB].contMappings);
        ini["Controller 1"]["C"] = ((int)inputDevice[(int)InputButtons.INPUT_BUTTONC].contMappings);
        ini["Controller 1"]["Start"] = ((int)inputDevice[(int)InputButtons.INPUT_START].contMappings);

        ini["Controller 1"].SetValueComment("LStickDeadzone", "Deadzones, 0.0-1.0");
        ini["Controller 1"]["LStickDeadzone"] = (LSTICK_DEADZONE);
        ini["Controller 1"]["RStickDeadzone"] = (RSTICK_DEADZONE);
        ini["Controller 1"]["LTriggerDeadzone"] = (LTRIGGER_DEADZONE);
        ini["Controller 1"]["RTriggerDeadzone"] = (RTRIGGER_DEADZONE);

        var buffer = GamePath + "/settings.ini";

        ini.Save(buffer);
    }

    void ReadUserdata()
    {
        var buffer = GamePath + "/UData.bin";

        using var userFile = File.OpenRead(buffer);
        using var binaryReader = new BinaryReader(userFile);

        for (int a = 0; a < ACHIEVEMENT_COUNT; ++a)
        {
            achievements[a].status = binaryReader.ReadInt32();
        }
        for (int l = 0; l < LEADERBOARD_COUNT; ++l)
        {
            leaderboards[l].score = binaryReader.ReadInt32();
            if (leaderboards[l].score == 0)
                leaderboards[l].score = 0x7FFFFFF;
        }

        if (onlineActive)
        {
            // Load from online
        }
    }

    void WriteUserdata()
    {
        var buffer = $"{GamePath}/UData.bin";

        using var userFile = File.OpenWrite(buffer);
        using var binaryWriter = new BinaryWriter(userFile);

        for (int a = 0; a < ACHIEVEMENT_COUNT; ++a)
        {
            binaryWriter.Write(achievements[a].status);
        }
        for (int l = 0; l < LEADERBOARD_COUNT; ++l)
        {
            binaryWriter.Write(leaderboards[l].score);
        }

        if (onlineActive)
        {
            // Load from online
        }
    }

    void AwardAchievement(int id, int status)
    {
        if (id < 0 || id >= ACHIEVEMENT_COUNT)
            return;

        if (status != achievements[id].status)
            PrintLog($"Achieved achievement: {achievements[id].name} ({status})!");

        achievements[id].status = status;

        if (onlineActive)
        {
            // Set Achievement online
        }
        WriteUserdata();
    }

    public void SetAchievement(int achievementID, int achievementDone)
    {
        if (!Engine.trialMode && !debugMode)
        {
            AwardAchievement(achievementID, achievementDone);
        }
    }

    public void SetLeaderboard(int leaderboardID, int result)
    {
        if (!Engine.trialMode && !debugMode)
        {
            if (result < leaderboards[leaderboardID].score)
            {
                PrintLog($"Set leaderboard ({leaderboardID}) value to {result}");
                leaderboards[leaderboardID].score = result;
                WriteUserdata();
            }
            else
            {
                PrintLog($"Attempted to set leaderboard ({leaderboardID}) value to {result}... but score was already {leaderboards[leaderboardID].score}!");
            }
        }
    }
}