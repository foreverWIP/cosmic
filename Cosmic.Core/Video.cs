global using static Cosmic.Core.Video;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
namespace Cosmic.Core;

public static class Video
{
    static int videoWidth = 0;
    static int videoHeight = 0;
    public static float videoAR { get; private set; } = 0;

    public static Mutex videoMutex = new();

    public static int videoPlaying = 0;
    static int vidFrameMS = 0;
    static int vidBaseticks = 0;
    public static uint vidCurrentTicks = 0;

    public static bool videoSkipped = false;
    public static bool videoAudioStreamEmpty = false;

    public record struct VideoFile(
        Color[] Palette,
        List<byte[]> Frames
    );

    public static void PlayVideoFile(string filePath)
    {
        var pathBuffer = string.Empty;
        int len = filePath.Length;

        pathBuffer = "videos/";
        pathBuffer += filePath;
        pathBuffer += ".ogv";

        var filepath = platform.GamePath + "/" + pathBuffer;

        // if (platform.LoadVideoFile(filepath, out videoWidth, out videoHeight, out var fps))
        if (platform.LoadVideoFile(filepath, out videoWidth, out videoHeight, out var fps))
        {
            platform.PrintLog($"Loaded File '{filepath}'!");

            // commit video Aspect Ratio.
            videoAR = (float)videoWidth / (float)videoHeight;

            platform.SetupVideoBuffer(videoWidth, videoHeight);
            vidBaseticks = (int)platform.GetMillisecondsSinceStartup();
            vidFrameMS = (int)((fps == 0.0) ? 0 : ((uint)(1000.0 / fps)));
            videoPlaying = 1; // playing ogv
            trackID = TRACK_COUNT - 1;

            videoSkipped = false;
            gameMode = EngineStates.ENGINE_VIDEOWAIT;
        }
        else
        {
            platform.PrintLog($"Couldn't find file '{filepath}'!");
        }
    }

    public static int ProcessVideo()
    {
        if (videoPlaying == 1)
        {
            CheckKeyPress(ref keyPress[0], 0xFF);

            if (videoSkipped && fadeMode < 0xFF)
            {
                fadeMode += 8;
            }

            if (anyPress[0] || touches > 0)
            {
                if (!videoSkipped)
                    fadeMode = 0;

                videoSkipped = true;
            }

            if ((videoAudioStreamEmpty && platform.IsVideoFinished()) || (videoSkipped && fadeMode >= 0xFF))
            {
                platform.StopVideoPlayback();
                ResumeSound();
                return 1; // video finished
            }

            // Don't pause or it'll go wild
            if (videoPlaying == 1)
            {
                uint now = (uint)(platform.GetMillisecondsSinceStartup() - vidBaseticks);

                // Play video frames when it's time.
                if (vidCurrentTicks <= now)
                {
                    if (vidFrameMS != 0 && ((now - vidCurrentTicks) >= vidFrameMS))
                    {
                        // Skip frames to catch up, but keep track of the last one+
                        //  in case we catch up to a series of dupe frames, which
                        //  means we'd have to draw that final frame and then wait for
                        //  more.

                        var frameCount = 0;
                        do
                        {
                            frameCount++;
                            vidCurrentTicks += (uint)vidFrameMS;
                        }
                        while ((now - vidCurrentTicks) >= vidFrameMS);
                        videoMutex.WaitOne();
                        platform.DoVideoFrames(frameCount);
                        videoMutex.ReleaseMutex();
                    }
                }

                return 2; // its playing as expected
            }
        }

        return 0; // its not even initialised
    }
}