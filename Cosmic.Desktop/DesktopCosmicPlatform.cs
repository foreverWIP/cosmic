using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text;
using Cosmic.Core;
using Cosmic.Graphics;
using static Cosmic.Core.Drawing;
using static Cosmic.Core.EngineStuff;
using static Cosmic.Core.Input;
using static Cosmic.Core.Stage;
using static SDL2.SDL;
using static Theorafile;

namespace Cosmic.Desktop;

sealed partial class DesktopCosmicPlatform : ICosmicPlatform
{
    public GamePlatformID GAMEPLATFORMID
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return GamePlatformID.Windows;
            }
            if (OperatingSystem.IsMacOS())
            {
                return GamePlatformID.MacOS;
            }
            if (OperatingSystem.IsLinux())
            {
                return GamePlatformID.Linux;
            }
            throw new InvalidOperationException("unsupported platform");
        }
    }

    public bool enginePaused { get; set; }
    byte focusState = 0;
    public bool running { get; set; }
    int gameSpeed = 1;
    public void SetGameSpeed(byte speed)
    {
        gameSpeed = speed;
    }
    bool errorStop = false;
    bool inputDisplay = false;
    public bool gameDebugMode { get; private set; } =
#if DEBUG
        true
#else
        false
#endif
    ;
    public StageListNames startList { get; set; } = (StageListNames)(-1);
    public int startStage { get; set; } = -1;
    int fastForwardSpeed = 8;
    public EngineLanguages language { get; set; } = EngineLanguages.English;
    bool startFullScreen = false;
    bool borderless = false;
    public bool VSync { get; private set; }
    public int refreshRate { get; set; } = 60; // user-picked screen update rate
    int screenRefreshRate = 60; // hardware screen update rate
    int dimLimit = 0;
    public bool onlineActive { get; set; } = true;
    int gameViewScale = 2;
    bool render2x = false;
    int BaseRenderWidth => SCREEN_XSIZE * (render2x ? 2 : 1);
    int BaseRenderHeight => SCREEN_YSIZE * (render2x ? 2 : 1);
    int disableFocusPause = 0;
    int disableFocusPause_Config = 0;

#pragma warning disable CS8618
    public DesktopCosmicPlatform()
    {
        // Support for extra controller types SDL doesn't recognise
        var buffer = GamePath + "/controllerdb.txt";

        if (File.Exists(buffer))
        {
            int nummaps = SDL2.SDL.SDL_GameControllerAddMappingsFromFile(buffer);
            if (nummaps >= 0)
                PrintLog($"loaded {buffer} controller mappings from '{nummaps}'");
        }
    }
#pragma warning restore CS8618

    public void Run()
    {
        while (running)
        {
            renderer.VSync = VSync;
            while (IsWaitingForVSync()) ;
            running = ProcessEvents();

            // Focus Checks
            if (((disableFocusPause + 1) & 2) == 0)
            {
                if (!Engine.hasFocus)
                {
                    if ((focusState & 1) == 0)
                        focusState = (byte)(Audio.PauseSound() ? 3 : 1);
                }
                else if (focusState != 0)
                {
                    if ((focusState & 2) != 0)
                        Audio.ResumeSound();
                    focusState = 0;
                }
            }

            if ((focusState & 1) == 0 && !errorStop)
            {
                for (int s = 0; s < gameSpeed; ++s)
                {
                    if (!enginePaused)
                    {
                        Engine.RunFrame();
                    }
                }
            }

            FlipScreen();

            Engine.message = EngineMessages.MESSAGE_NONE;

            int hapticID = GetHapticEffectNum();
            if (hapticID >= 0)
            {
                // PlayHaptics(hapticID);
            }
            else if (hapticID == (int)DefaultHapticIDs.HAPTIC_STOP)
            {
                // StopHaptics();
            }
        }

        renderer.StopVideoPlayback(ref videoInfo);
        Audio.ReleaseAudioDevice();
        ReleaseRenderDevice();
        WriteSettings();

        Exit();
    }

    public long GetMillisecondsSinceStartup()
    {
        return SDL2.SDL.SDL_GetTicks();
    }

    ulong _targetFreqCached = ulong.MaxValue;
    ulong TargetFreq
    {
        get
        {
            if (_targetFreqCached == ulong.MaxValue)
            {
                _targetFreqCached = SDL2.SDL.SDL_GetPerformanceFrequency() / (ulong)refreshRate;
            }
            return _targetFreqCached;
        }
    }
    ulong curTicks = 0;
    ulong prevTicks = 0;

    public bool IsWaitingForVSync()
    {
        if (VSync)
        {
            return false;
        }
        curTicks = SDL2.SDL.SDL_GetPerformanceCounter();
        return curTicks < prevTicks + TargetFreq;
    }

    public bool ProcessEvents()
    {
        prevTicks = curTicks;
        renderer.UpdateGUI((curTicks - prevTicks) / (float)TargetFreq, ref inputDisplay);

        return gameMode != EngineStates.ENGINE_EXITGAME;
    }

    void ProcessEvent(ref SDL_Event sdlEvents)
    {
        // Main Events
        switch (sdlEvents.type)
        {
            case SDL_EventType.SDL_CONTROLLERDEVICEADDED: ControllerInit((byte)sdlEvents.cdevice.which); break;
            case SDL_EventType.SDL_CONTROLLERDEVICEREMOVED: ControllerClose((byte)sdlEvents.cdevice.which); break;

            case SDL_EventType.SDL_FINGERMOTION:
            case SDL_EventType.SDL_FINGERDOWN:
            case SDL_EventType.SDL_FINGERUP:
                {
                    int count = SDL2.SDL.SDL_GetNumTouchFingers(sdlEvents.tfinger.touchId);
                    touches = 0;
                    unsafe
                    {
                        for (int i = 0; i < count; i++)
                        {
                            SDL2.SDL.SDL_Finger* finger = (SDL2.SDL.SDL_Finger*)SDL2.SDL.SDL_GetTouchFinger(sdlEvents.tfinger.touchId, i);
                            if (finger != null)
                            {
                                touchDown[touches] = 1;
                                touchX[touches] = (int)(finger->x * SCREEN_XSIZE * gameViewScale);
                                touchY[touches] = (int)(finger->y * SCREEN_YSIZE * gameViewScale);
                                touches++;
                            }
                        }
                    }
                    break;
                }
        }
    }

    public void Exit()
    {
        renderer.CloseEngineWindow();
    }

    public void InitError()
    {
        errorStop = true;
    }

    public void ClearError()
    {
        errorStop = false;
    }

    public bool ErrorStop => errorStop;

    public bool LoadFile(string filePath, [NotNullWhen(true)] out BinaryReader? reader)
    {
        if (File.Exists(filePath))
        {
            reader = new BinaryReader(File.OpenRead(filePath));
            return true;
        }
        reader = null;
        return false;
    }

    public void PrintLog(string msg)
    {
#if DEBUG
        Console.WriteLine(msg);
#endif

        string pathBuffer = GamePath + "/log.txt";
        msg += '\n';

        File.AppendAllText(pathBuffer, msg);
    }

    Renderer.VideoInfo videoInfo = new();

    public bool LoadVideoFile(string filePath, out int videoWidth, out int videoHeight, out double fps)
    {
        videoWidth = 0;
        videoHeight = 0;
        fps = 0;

        th_pixel_fmt fmt;
        tf_fopen(filePath, out videoInfo.theora);
        tf_videoinfo(
            videoInfo.theora,
            out videoInfo.yWidth,
            out videoInfo.yHeight,
            out videoInfo.fps,
            out fmt
        );
        if (fmt == Theorafile.th_pixel_fmt.TH_PF_420)
        {
            videoInfo.uvWidth = videoInfo.yWidth / 2;
            videoInfo.uvHeight = videoInfo.yHeight / 2;
        }
        else if (fmt == Theorafile.th_pixel_fmt.TH_PF_422)
        {
            videoInfo.uvWidth = videoInfo.yWidth / 2;
            videoInfo.uvHeight = videoInfo.yHeight;
        }
        else if (fmt == Theorafile.th_pixel_fmt.TH_PF_444)
        {
            videoInfo.uvWidth = videoInfo.yWidth;
            videoInfo.uvHeight = videoInfo.yHeight;
        }
        else
        {
            throw new NotSupportedException(
                "Unrecognized YUV format!"
            );
        }

        videoWidth = videoInfo.yWidth;
        videoHeight = videoInfo.yHeight;
        fps = videoInfo.fps;

        return true;
    }

    public void OnFocusLost()
    {
        if (((disableFocusPause + 1) & 1) == 0)
            Engine.message = EngineMessages.MESSAGE_LOSTFOCUS;
        Engine.hasFocus = false;
    }
}