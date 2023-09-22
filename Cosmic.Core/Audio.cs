global using static Cosmic.Core.Audio;
using System;
using System.Runtime.InteropServices;
using Kaitai;

namespace Cosmic.Core;

public static unsafe class Audio
{
    public const int TRACK_COUNT = (0x10);
    public const int SFX_COUNT = (0x100);
    public const int CHANNEL_COUNT = (0x4);

    public const int MAX_VOLUME = (100);

    public const int STREAMFILE_COUNT = (2);

    public const int MIX_BUFFER_SAMPLES = (256);

    struct TrackInfo
    {
        public string fileName;
        public bool trackLoop;
        public uint loopPoint;
    }

    public struct StreamInfo
    {
        public nint stream;
        public short[] buffer => _buffer ??= new short[MIX_BUFFER_SAMPLES];
        short[] _buffer;
        public bool trackLoop;
        public uint loopPoint;
        public bool loaded;
    }

    public struct SFXInfo
    {
        public string name;
        public nint buffer;
        public long length;
        public bool loaded;
    }

    public struct ChannelInfo
    {
        public long sampleLength;
        public short* samplePtr;
        public int sfxID;
        public byte loopSFX;
        public sbyte pan;
    }

    public struct StreamFile
    {
        public byte[]? buffer;
        public int fileSize;
        public int filePos;
    }

    public enum MusicStatuses
    {
        MUSIC_STOPPED = 0,
        MUSIC_PLAYING = 1,
        MUSIC_PAUSED = 2,
        MUSIC_LOADING = 3,
        MUSIC_READY = 4,
    }

    static void FreeMusInfo()
    {
        platform.LockAudio();

        platform.FreeAudioStream();

        streamInfo[currentStreamIndex].stream = 0;
        streamFile[currentStreamIndex].buffer = null;

        platform.UnlockAudio();
    }

    public static void StopMusic()
    {
        musicStatus = MusicStatuses.MUSIC_STOPPED;
        FreeMusInfo();
    }

    public static void StopSFX(int sfx)
    {
        for (int i = 0; i < CHANNEL_COUNT; ++i)
        {
            if (sfxChannels[i].sfxID == sfx)
            {
                sfxChannels[i] = new()
                {
                    sfxID = -1
                };
            }
        }
    }

    public static void SetMusicVolume(int volume)
    {
        masterVolume = System.Math.Clamp(volume, 0, MAX_VOLUME);
    }

    public static bool PauseSound()
    {
        if (musicStatus == MusicStatuses.MUSIC_PLAYING)
        {
            musicStatus = MusicStatuses.MUSIC_PAUSED;
            return true;
        }
        return false;
    }

    public static void ResumeSound()
    {
        if (musicStatus == MusicStatuses.MUSIC_PAUSED)
            musicStatus = MusicStatuses.MUSIC_PLAYING;
    }

    public static void StopAllSfx()
    {
        for (int i = 0; i < CHANNEL_COUNT; ++i) sfxChannels[i].sfxID = -1;
    }

    static void ReleaseGlobalSfx()
    {
        StopAllSfx();
        for (int i = globalSFXCount - 1; i >= 0; --i)
        {
            if (sfxList[i].loaded)
            {
                sfxList[i].name = "";
                Marshal.FreeHGlobal((nint)sfxList[i].buffer);
                sfxList[i].length = 0;
                sfxList[i].loaded = false;
            }
        }
        globalSFXCount = 0;
    }

    public static void ReleaseStageSfx()
    {
        for (int i = stageSFXCount + globalSFXCount; i >= globalSFXCount; --i)
        {
            if (sfxList[i].loaded)
            {
                sfxList[i].name = "";
                Marshal.FreeHGlobal((nint)sfxList[i].buffer);
                sfxList[i].length = 0;
                sfxList[i].loaded = false;
            }
        }
        stageSFXCount = 0;
    }

    public static void ReleaseAudioDevice()
    {
        StopMusic();
        StopAllSfx();
        ReleaseStageSfx();
        ReleaseGlobalSfx();
    }

    public static int globalSFXCount = 0;
    public static int stageSFXCount = 0;

    public static int masterVolume = MAX_VOLUME;
    public static int trackID = -1;

    public static int sfxVolume = MAX_VOLUME;
    public static int bgmVolume = MAX_VOLUME;

    public static bool audioEnabled = false;

    static int nextChannelPos;
    public static MusicStatuses musicStatus;
    static readonly TrackInfo[] musicTracks = new TrackInfo[TRACK_COUNT];
    public static readonly SFXInfo[] sfxList = new SFXInfo[SFX_COUNT];

    public static readonly ChannelInfo[] sfxChannels = new ChannelInfo[CHANNEL_COUNT];

    public static int currentStreamIndex = 0;
    public static readonly StreamFile[] streamFile = new StreamFile[STREAMFILE_COUNT];
    public static readonly StreamInfo[] streamInfo = new StreamInfo[STREAMFILE_COUNT];
    public static int streamFilePtr;
    public static int streamInfoPtr;

    static int currentMusicTrack = -1;

    public static uint audioDevice;
    public static nint ogv_stream;

    public static int InitAudioPlayback()
    {
        StopAllSfx(); //"init"

        if (!platform.InitAudio())
        {
            return 0;
        }

        LoadGlobalSfx();

        return 1;
    }

    static void LoadGlobalSfx()
    {
        globalSFXCount = 0;

        if (platform.LoadFile("Data/Game/GameConfig.bin", out var info))
        {
            var gameConfig = new Cosmic.Formats.GameConfig(new KaitaiStream(info.BaseStream));

            // Read SFX
            globalSFXCount = gameConfig.NumGlobalSfxPaths;
            for (byte s = 0; s < globalSFXCount; ++s)
            {
                LoadSfx(gameConfig.GlobalSfxPaths[s].Contents, s);
            }

            info.Dispose();
        }

        // sfxDataPosStage = sfxDataPos;
        nextChannelPos = 0;
        for (int i = 0; i < CHANNEL_COUNT; ++i) sfxChannels[i].sfxID = -1;
    }

    static void LoadMusic()
    {
        currentStreamIndex++;
        currentStreamIndex %= STREAMFILE_COUNT;

        platform.LockAudio();

        if (streamFile[currentStreamIndex].fileSize > 0)
            FreeMusInfo();

        if (platform.LoadFile(musicTracks[currentMusicTrack].fileName, out var info))
        {
            streamFile[currentStreamIndex].filePos = 0;
            streamFile[currentStreamIndex].fileSize = (int)info.BaseStream.Length;
            streamFile[currentStreamIndex].buffer = new byte[streamFile[currentStreamIndex].fileSize];

            info.Read(streamFile[currentStreamIndex].buffer!, 0, streamFile[currentStreamIndex].fileSize);
            info.Dispose();

            streamInfo[currentStreamIndex].stream = platform.CreateAudioStream(streamFile[currentStreamIndex].buffer!);

            musicStatus = MusicStatuses.MUSIC_PLAYING;
            masterVolume = MAX_VOLUME;
            trackID = currentMusicTrack;
            streamInfo[currentStreamIndex].trackLoop = musicTracks[currentMusicTrack].trackLoop;
            streamInfo[currentStreamIndex].loopPoint = musicTracks[currentMusicTrack].loopPoint;
            streamInfo[currentStreamIndex].loaded = true;
            streamFilePtr = currentStreamIndex;
            streamInfoPtr = currentStreamIndex;
            currentMusicTrack = -1;
        }
        else
        {
            musicStatus = MusicStatuses.MUSIC_STOPPED;
        }
        platform.UnlockAudio();
    }

    public static void SetMusicTrack(string filePath, byte trackID, bool loop, uint loopPoint)
    {
        platform.LockAudio();
        musicTracks[trackID].fileName = "Data/Music/";
        musicTracks[trackID].fileName += filePath;
        musicTracks[trackID].trackLoop = loop;
        musicTracks[trackID].loopPoint = loopPoint;
        platform.UnlockAudio();
    }

    public static bool PlayMusic(int track)
    {
        if (!audioEnabled)
            return false;

        if (musicTracks[track].fileName != string.Empty)
        {
            if (musicStatus != MusicStatuses.MUSIC_LOADING)
            {
                currentMusicTrack = track;
                musicStatus = MusicStatuses.MUSIC_LOADING;
                LoadMusic();
                return true;
            }
            else
            {
                platform.PrintLog("WARNING music tried to play while music was loading!");
            }
        }
        else
        {
            StopMusic();
        }
        return false;
    }

    public static void LoadSfx(string filePath, byte sfxID)
    {
        if (!audioEnabled)
            return;

        var fullPath = "Data/SoundFX/";
        fullPath += filePath;

        if (platform.LoadFile(fullPath, out var info))
        {
            var sfx = new byte[info.BaseStream.Length];
            info.Read(sfx, 0, sfx.Length);

            info.Dispose();

            var convertedBuffer = platform.GetConvertedAudioWAV(sfx);

            platform.LockAudio();
            sfxList[sfxID].name = filePath;
            sfxList[sfxID].buffer = Marshal.AllocHGlobal(convertedBuffer.Length);
            Marshal.Copy(convertedBuffer, 0, sfxList[sfxID].buffer, convertedBuffer.Length);
            sfxList[sfxID].length = convertedBuffer.Length / sizeof(short);
            sfxList[sfxID].loaded = true;
            platform.UnlockAudio();
        }
    }

    public static void PlaySFX(int sfx, bool loop)
    {
        platform.LockAudio();
        int sfxChannelID = nextChannelPos++;
        for (int c = 0; c < CHANNEL_COUNT; ++c)
        {
            if (sfxChannels[c].sfxID == sfx)
            {
                sfxChannelID = c;
                break;
            }
        }

        fixed (ChannelInfo* sfxInfo = &sfxChannels[sfxChannelID])
        {
            sfxInfo->sfxID = sfx;
            sfxInfo->samplePtr = (short*)sfxList[sfx].buffer;
            sfxInfo->sampleLength = sfxList[sfx].length;
            sfxInfo->loopSFX = (byte)(loop ? 1 : 0);
            sfxInfo->pan = 0;
        }
        if (nextChannelPos == CHANNEL_COUNT)
            nextChannelPos = 0;
        platform.UnlockAudio();
    }

    public static void SetSfxAttributes(int sfx, int loopCount, sbyte pan)
    {
        platform.LockAudio();
        int sfxChannel = -1;
        for (int i = 0; i < CHANNEL_COUNT; ++i)
        {
            if (sfxChannels[i].sfxID == sfx || sfxChannels[i].sfxID == -1)
            {
                sfxChannel = i;
                break;
            }
        }
        if (sfxChannel == -1)
            return; // wasn't found

        fixed (ChannelInfo* sfxInfo = &sfxChannels[sfxChannel])
        {
            sfxInfo->samplePtr = (short*)sfxList[sfx].buffer;
            sfxInfo->sampleLength = sfxList[sfx].length;
            sfxInfo->loopSFX = (byte)(loopCount == -1 ? sfxInfo->loopSFX : loopCount);
            sfxInfo->pan = pan;
            sfxInfo->sfxID = sfx;
        }
        platform.UnlockAudio();
    }
}