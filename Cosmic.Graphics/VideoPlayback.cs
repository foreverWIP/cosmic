using System;
using System.Runtime.InteropServices;
using Cosmic.Core;
using static Cosmic.Core.Palette;
using static Cosmic.Core.Video;
using static SDL2.SDL;
using static Theorafile;

namespace Cosmic.Graphics;

partial class Renderer
{
    public struct VideoInfo
    {
        public int yWidth, yHeight;
        public int uvWidth, uvHeight;
        public double fps;
        public int audioChannels;
        public int audioSampleRate;
        public nint theora;
    }

    public void SetupVideoBuffer(VideoInfo videoInfo, int width, int height)
    {
        videoBuffer = Marshal.AllocHGlobal(videoInfo.yWidth * videoInfo.yHeight * sizeof(int));

        SetupVideoResources(videoInfo);

    }

    public void CloseVideoBuffer(ref VideoInfo videoInfo)
    {
        if (videoPlaying == 1)
        {
            FreeVideoResources();

            if (videoBuffer != 0)
            {
                Marshal.FreeHGlobal(videoBuffer);
            }

            if (videoInfo.theora != 0)
            {
                _ = tf_close(ref videoInfo.theora);
            }
        }
    }

    nint videoBuffer;

    static readonly float[] managedFloats = new float[Audio.MIX_BUFFER_SAMPLES * 2];

    public void DoVideoAudioSamples(VideoInfo videoInfo, long samples_to_do, in nint outputBuffer)
    {
        unsafe
        {
            fixed (float* fixedFloats = managedFloats)
            {
                platform.LockAudio();
                _ = tf_readaudio(videoInfo.theora, (nint)fixedFloats, (int)samples_to_do * 2);
                long bytes_to_do = samples_to_do * sizeof(short);
                _ = SDL_AudioStreamPut(outputBuffer, (nint)fixedFloats, (int)bytes_to_do * 4);
                platform.UnlockAudio();
            }
        }
    }

    public unsafe void DoVideoFrames(VideoInfo videoInfo, int frames)
    {
        _ = tf_readvideo(videoInfo.theora, videoBuffer, frames);

        int half_w = videoInfo.yWidth / 2;
        byte* y = (byte*)videoBuffer;
        byte* u = y + (videoInfo.yWidth * videoInfo.yHeight);
        byte* v = u + (half_w * (videoInfo.yHeight / 2));

        UpdateVideoResources((nint)y, (nint)u, (nint)v, videoInfo);
    }

    public void StopVideoPlayback(ref VideoInfo videoInfo)
    {
        if (videoPlaying == 1)
        {
            // `videoPlaying` and `videoDecoder` are read by
            // the audio thread, so lock it to prevent a race
            // condition that results in invalid memory accesses.
            SDL_LockAudio();

            if (videoSkipped && fadeMode >= 0xFF)
                fadeMode = 0;

            vidCurrentTicks = 0;
            videoMutex.WaitOne();
            CloseVideoBuffer(ref videoInfo);
            videoMutex.ReleaseMutex();
            videoInfo = new();
            videoPlaying = 0;

            SDL_UnlockAudio();
        }
    }

    public bool IsVideoFinished(VideoInfo videoInfo)
    {
        return videoInfo.theora != 0 && tf_eos(videoInfo.theora) != 0;
    }
}