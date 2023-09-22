using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Cosmic.Core;

public interface ICosmicPlatform : IDisposable
{
    public GamePlatformID GAMEPLATFORMID { get; }
    bool gameDebugMode { get; }
    bool enginePaused { get; set; }
    StageListNames startList { get; protected set; }
    int startStage { get; protected set; }
    EngineLanguages language { get; set; }
    int refreshRate { get; protected set; }
    bool onlineActive { get; protected set; }

    void InitError();
    void ClearError();
    bool ErrorStop { get; }

    string GamePath { get; }

    void InitUserdata();
    bool ReadSaveRAMData();
    bool WriteSaveRAMData();
    void WriteSettings();
    void SetAchievement(int achievementID, int achievementDone);
    void SetLeaderboard(int leaderboardID, int result);
    void LoadAchievementsMenu();
    void LoadLeaderboardsMenu();
    bool LoadFile(string filePath, [NotNullWhen(true)] out BinaryReader? reader);
    void PrintLog(string msg);

    long GetMillisecondsSinceStartup();
    void Run();
    void SetGameSpeed(byte frameMultiplier);
    bool running { get; set; }
    void Exit();

    void OnFocusLost();

    bool InitRenderDevice();
    void ReleaseRenderDevice();
    void FlipScreen();
    void UpdatePrevFramebuffer();
    void SubmitDrawList(uint drawFacesCount, DrawVertexFace[] drawFacesList, DrawBlendMode blendMode);
    void ClearDrawLists();
    void UpdateHWSurface(int index, int x, int y, int w, int h, byte[] data);
    void UpdateHWChunks();
    void UpdatePalettes();
    void UpdatePaletteIndices();

    bool InitAudio();
    nint CreateAudioStream(byte[] oggFileBytes);
    byte[] GetConvertedAudioWAV(byte[] fileBytes);
    void LockAudio();
    void UnlockAudio();
    void FreeAudioStream();

    void SetupVideoBuffer(int width, int height);
    void CloseVideoBuffer();
    bool LoadVideoFile(string filePath, out int videoWidth, out int videoHeight, out double fps);
    void DoVideoAudioSamples(long samples, in nint outputBuffer);
    void DoVideoFrames(int frames);
    void StopVideoPlayback();
    bool IsVideoFinished();

    int[] GetDefaultKeyboardMappings();
    int[] GetDefaultControllerMappings();
    void ControllerInit(byte controllerID);
    void ControllerClose(byte controllerID);
    void ProcessInput();

    void IDisposable.Dispose()
    {
        GC.SuppressFinalize(this);
    }
}