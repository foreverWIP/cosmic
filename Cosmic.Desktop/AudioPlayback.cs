using System;
using System.IO;
using System.Runtime.InteropServices;
using Cosmic.Core;
using NVorbis;
using static Cosmic.Core.Audio;
using static Cosmic.Core.Video;
using static SDL2.SDL;
using SDL_AudioFormat = System.UInt16;

namespace Cosmic.Desktop;

sealed partial class DesktopCosmicPlatform : ICosmicPlatform
{
    static SDL_AudioSpec audioDeviceFormat = new();

    [LibraryImport("SDL2")]
    private static partial int SDL_AudioStreamFlush(nint stream);

    const int SDL_AUDIOCVT_MAX_FILTERS = 9;

    [StructLayout(
        LayoutKind.Sequential,
        Pack = 0,
        Size =
        sizeof(int) +
        sizeof(SDL_AudioFormat) +
        sizeof(SDL_AudioFormat) +
        sizeof(double) +
        sizeof(ulong) +
        sizeof(int) +
        sizeof(int) +
        sizeof(int) +
        sizeof(double) +
        sizeof(ulong) * (SDL_AUDIOCVT_MAX_FILTERS + 1) +
        sizeof(int)
    )]
    unsafe struct SDL_AudioCVT
    {
        public int needed;                 /**< Set to 1 if conversion possible */
        public SDL_AudioFormat src_format; /**< Source audio format */
        public SDL_AudioFormat dst_format; /**< Target audio format */
        public double rate_incr;           /**< Rate conversion increment */
        public byte* buf;                 /**< Buffer to hold entire audio data */
        public int len;                    /**< Length of original audio buffer */
        public int len_cvt;                /**< Length of converted audio buffer */
        public int len_mult;               /**< buffer must be len*len_mult big */
        public double len_ratio;           /**< Given len, final size is len*len_ratio */

        // we don't have fixed-size nint buffers yet, so we pad arbitrarily instead...
        // fixed nint filters[SDL_AUDIOCVT_MAX_FILTERS + 1]; /**< NULL-terminated list of filter functions */
        // int filter_index;           /**< Current audio conversion function */
        // fixed byte _padding[sizeof(ulong) * (SDL_AUDIOCVT_MAX_FILTERS + 1) + sizeof(int)];
    }

    [LibraryImport("SDL2")]
    private static unsafe partial int SDL_BuildAudioCVT(
        nint cvt,
        SDL_AudioFormat src_format,
        byte src_channels,
        int src_rate,
        SDL_AudioFormat dst_format,
        byte dst_channels,
        int dst_rate
    );

    [LibraryImport("SDL2")]
    private static unsafe partial int SDL_ConvertAudio(nint cvt);

    const int TARGET_AUDIO_FREQUENCY = (44100);
    static readonly int TARGET_AUDIO_FORMAT = (AUDIO_S16SYS);
    const int AUDIO_SAMPLES = (0x800);
    const int TARGET_AUDIO_CHANNELS = (2);

    public bool InitAudio()
    {
        _ = SDL_Init(SDL_INIT_AUDIO);
        SDL_AudioSpec want = new()
        {
            freq = TARGET_AUDIO_FREQUENCY,
            format = (ushort)TARGET_AUDIO_FORMAT,
            samples = AUDIO_SAMPLES,
            channels = TARGET_AUDIO_CHANNELS,
            callback = ProcessAudioPlayback
        };

        if ((audioDevice = SDL_OpenAudioDevice(0, 0, ref want, out audioDeviceFormat, (int)SDL_AUDIO_ALLOW_FREQUENCY_CHANGE)) > 0)
        {
            audioEnabled = true;
            SDL_PauseAudioDevice(audioDevice, 0);
            PrintLog($"Opened audio device: {audioDevice}");
        }
        else
        {
            PrintLog($"Unable to open audio device: {SDL_GetError()}");
            audioEnabled = false;
            return false; // no audio but game wont crash now
        }

        // Init video sound stuff
        // TODO: Unfortunately, we're assuming that video sound is stereo at 48000Hz.
        // This is true of every .ogv file in the game (the Steam version, at least),
        // but it would be nice to make this dynamic. Unfortunately, THEORAPLAY's API
        // makes this awkward.
        ogv_stream = SDL_NewAudioStream(AUDIO_F32SYS, 2, 48000, audioDeviceFormat.format, audioDeviceFormat.channels, audioDeviceFormat.freq);
        if (ogv_stream == 0)
        {
            PrintLog($"Failed to create stream: {SDL_GetError()}");
            SDL_CloseAudioDevice(audioDevice);
            audioEnabled = false;
            return false; // no audio but game wont crash now
        }

        return true;
    }

    unsafe void ProcessAudioPlayback(nint userdata, nint stream, int len)
    {
        // (void)userdata; // Unused

        if (!audioEnabled)
            return;

        short* output_buffer = (short*)stream;

        var samples_remaining = (long)len / sizeof(short);
        var mix_buffer = stackalloc int[MIX_BUFFER_SAMPLES];
        var buffer = stackalloc short[MIX_BUFFER_SAMPLES];
        var shortBuf = stackalloc short[MIX_BUFFER_SAMPLES];
        var managedFloats = stackalloc float[MIX_BUFFER_SAMPLES * 2];
        var vidBuffer = stackalloc short[MIX_BUFFER_SAMPLES];
        while (samples_remaining != 0)
        {
            NativeMemory.Clear(mix_buffer, MIX_BUFFER_SAMPLES * sizeof(int));
            long samples_to_do = (samples_remaining < MIX_BUFFER_SAMPLES) ? samples_remaining : MIX_BUFFER_SAMPLES;

            // Process music being played by a video
            if (videoPlaying == 1)
            {
                // Fetch THEORAPLAY audio packets, and shove them into the SDL Audio Stream
                long bytes_to_do = samples_to_do * sizeof(short);

                videoMutex.WaitOne();
                DoVideoAudioSamples(samples_to_do, in ogv_stream);
                videoMutex.ReleaseMutex();


                // If we need more samples, assume we've reached the end of the file,
                // and flush the audio stream so we can get more. If we were wrong, and
                // there's still more file left, then there will be a gap in the audio. Sorry.
                if (SDL_AudioStreamAvailable(ogv_stream) < bytes_to_do)
                {
                    _ = SDL_AudioStreamFlush(ogv_stream);
                    videoAudioStreamEmpty = true;
                }
                else
                {
                    videoAudioStreamEmpty = false;
                }

                // Fetch the converted audio data, which is ready for mixing.
                int get = SDL_AudioStreamGet(ogv_stream, (nint)vidBuffer, (int)bytes_to_do);

                // Mix the converted audio data into the final output
                if (get != -1)
                    ProcessAudioMixing(mix_buffer, vidBuffer, get / sizeof(short), bgmVolume, 0);
            }
            else
            {
                // Mix music
                ProcessMusicStream(mix_buffer, (nint)(samples_to_do * sizeof(short)), shortBuf, managedFloats);

                SDL_AudioStreamClear(ogv_stream); // Prevent leftover audio from playing at the start of the next video
            }

            // Mix SFX
            for (byte i = 0; i < CHANNEL_COUNT; ++i)
            {
                fixed (ChannelInfo* sfx = &sfxChannels[i])
                {
                    if (sfx == null)
                        continue;

                    if (sfx->sfxID < 0)
                        continue;

                    if (sfx->samplePtr != null)
                    {
                        long samples_done = 0;
                        while (samples_done != samples_to_do)
                        {
                            long sampleLen = (sfx->sampleLength < samples_to_do - samples_done) ? sfx->sampleLength : samples_to_do - samples_done;
                            NativeMemory.Copy(sfx->samplePtr, &buffer[samples_done], (nuint)(sampleLen * sizeof(short)));

                            samples_done += sampleLen;
                            sfx->samplePtr += sampleLen;
                            sfx->sampleLength -= sampleLen;

                            if (sfx->sampleLength == 0)
                            {
                                if (sfx->loopSFX != 0)
                                {
                                    sfx->samplePtr = (short*)sfxList[sfx->sfxID].buffer;
                                    sfx->sampleLength = sfxList[sfx->sfxID].length;
                                }
                                else
                                {
                                    *sfx = new();
                                    sfx->sfxID = -1;
                                    break;
                                }
                            }
                        }

                        ProcessAudioMixing(mix_buffer, buffer, (int)samples_done, sfxVolume, sfx->pan);
                    }
                }
            }

            // Clamp mixed samples back to 16-bit and write them to the output buffer
            for (long i = 0; i < MIX_BUFFER_SAMPLES; ++i)
            {
                const short max_audioval = ((1 << (16 - 1)) - 1);
                const short min_audioval = -(1 << (16 - 1));

                int sample = mix_buffer[i];

                if (sample > max_audioval)
                    *output_buffer++ = max_audioval;
                else if (sample < min_audioval)
                    *output_buffer++ = min_audioval;
                else
                    *output_buffer++ = (short)sample;
            }

            samples_remaining -= samples_to_do;
        }
    }

    static void ADJUST_VOLUME(ref int s, int v) { s = s * v / MAX_VOLUME; }

    unsafe void ProcessAudioMixing(int* dst, short* src, int len, int volume, sbyte pan)
    {
        if (volume == 0)
            return;

        if (volume > MAX_VOLUME)
            volume = MAX_VOLUME;

        float panL = 0;
        float panR = 0;
        int i = 0;

        if (pan < 0)
        {
            panR = 1.0f - System.MathF.Abs(pan / 100.0f);
            panL = 1.0f;
        }
        else if (pan > 0)
        {
            panL = 1.0f - System.MathF.Abs(pan / 100.0f);
            panR = 1.0f;
        }

        while (len-- != 0)
        {
            int sample = *src++;
            ADJUST_VOLUME(ref sample, volume);

            if (pan != 0)
            {
                if ((i % 2) != 0)
                {
                    sample = (int)(sample * panR);
                }
                else
                {
                    sample = (int)(sample * panL);
                }
            }

            *dst++ += sample;

            i++;
        }
    }

    readonly VorbisReader[] vorbs = new VorbisReader[STREAMFILE_COUNT];

    unsafe void ProcessMusicStream(int* stream, nint bytes_wanted, short* shortBuf, float* managedFloats)
    {
        if (streamFile[streamFilePtr].buffer == null || streamInfo[streamInfoPtr].buffer == null)
            return;
        if (streamFile[streamFilePtr].fileSize == 0)
            return;
        switch (musicStatus)
        {
            case MusicStatuses.MUSIC_READY:
            case MusicStatuses.MUSIC_PLAYING:
                {
                    while (musicStatus == MusicStatuses.MUSIC_PLAYING && SDL_AudioStreamAvailable(streamInfo[streamInfoPtr].stream) < bytes_wanted)
                    {
                        // We need more samples: get some
                        long bytes_read = vorbs[streamInfoPtr].ReadSamples(new Span<float>(managedFloats, MIX_BUFFER_SAMPLES)) * sizeof(short);
                        for (var i = 0; i < MIX_BUFFER_SAMPLES; i++)
                        {
                            shortBuf[i] = (short)(managedFloats[i] * short.MaxValue);
                        }
                        if (bytes_read == 0)
                        {
                            // We've reached the end of the file
                            if (streamInfo[streamInfoPtr].trackLoop)
                            {
                                vorbs[streamInfoPtr].SamplePosition = streamInfo[streamInfoPtr].loopPoint;
                                continue;
                            }
                            else
                            {
                                musicStatus = MusicStatuses.MUSIC_STOPPED;
                                break;
                            }
                        }

                        if (musicStatus != MusicStatuses.MUSIC_PLAYING || SDL_AudioStreamPut(streamInfo[streamInfoPtr].stream, (nint)shortBuf, (int)bytes_read) == -1)
                            return;
                    }

                    // Now that we know there are enough samples, read them and mix them
                    {
                        int bytes_done = SDL_AudioStreamGet(streamInfo[streamInfoPtr].stream, (nint)shortBuf, (int)bytes_wanted);
                        if (bytes_done == -1)
                        {
                            return;
                        }
                        if (bytes_done != 0)
                        {
                            ProcessAudioMixing(stream, shortBuf, bytes_done / sizeof(short), (bgmVolume * masterVolume) / MAX_VOLUME, 0);
                        }
                    }
                    break;
                }
            default:
                break;
        }
    }

    public nint CreateAudioStream(byte[] oggFileBytes)
    {
        vorbs[currentStreamIndex] = new VorbisReader(new MemoryStream(oggFileBytes), true);
        var ret = SDL_NewAudioStream(
            AUDIO_S16,
            (byte)vorbs[currentStreamIndex].Channels,
            (int)vorbs[currentStreamIndex].SampleRate,
            audioDeviceFormat.format,
            audioDeviceFormat.channels,
            audioDeviceFormat.freq
        );
        if (ret == 0)
        {
            PrintLog($"Failed to create stream: {SDL_GetError()}");
        }
        return ret;
    }

    public byte[] GetConvertedAudioWAV(byte[] fileBytes)
    {
        byte[] convertedBuffer;
        unsafe
        {
            SDL_AudioCVT wavCvt = new();
            fixed (byte* fileBytesPtr = fileBytes)
            {
                var rw = SDL_RWFromMem((nint)fileBytesPtr, fileBytes.Length);
                SDL_LoadWAV_RW(rw, 0, out var wav, out var audioBuf, out var audioLen);
                _ = SDL_BuildAudioCVT((nint)(&wavCvt), wav.format, wav.channels, wav.freq, audioDeviceFormat.format, audioDeviceFormat.channels, audioDeviceFormat.freq);
                // lol
                var workBuffer = new byte[fileBytes.Length * 8];
                var sampleSizeBytes = GetBytesInFormat(wav.format);
                Array.Copy(fileBytes, fileBytes.Length - audioLen, workBuffer, 0, audioLen);
                fixed (byte* convertedBufferPtr = workBuffer)
                {
                    wavCvt.buf = convertedBufferPtr;
                    wavCvt.len = (int)audioLen;
                    SDL_ConvertAudio((nint)(&wavCvt));
                    SDL_FreeWAV(audioBuf);
                }
                SDL_RWclose(rw);
                convertedBuffer = new byte[wavCvt.len_cvt];
                Array.Copy(workBuffer, convertedBuffer, convertedBuffer.Length);
            }
        }
        return convertedBuffer;
    }

    byte GetBytesInFormat(SDL_AudioFormat format)
    {
        return format switch
        {
            AUDIO_S8 => 1,
            AUDIO_U8 => 1,
            AUDIO_S16LSB => 2,
            AUDIO_S16MSB => 2,
            AUDIO_U16LSB => 2,
            AUDIO_U16MSB => 2,
            AUDIO_F32LSB => 4,
            AUDIO_F32MSB => 4,
            AUDIO_S32LSB => 4,
            AUDIO_S32MSB => 4,
            _ => throw new InvalidOperationException("what")
        };
    }

    public void LockAudio()
    {
        SDL_LockAudio();
    }

    public void UnlockAudio()
    {
        SDL_UnlockAudio();
    }

    public void FreeAudioStream()
    {
        if (streamInfo[currentStreamIndex].stream != 0)
            SDL_FreeAudioStream(streamInfo[currentStreamIndex].stream);
        vorbs[currentStreamIndex]?.Dispose();
    }

    public void SetupVideoBuffer(int width, int height)
    {
        renderer.SetupVideoBuffer(videoInfo, width, height);
    }

    public void CloseVideoBuffer()
    {
        renderer.CloseVideoBuffer(ref videoInfo);
    }

    public void DoVideoAudioSamples(long samples, in nint outputBuffer)
    {
        renderer.DoVideoAudioSamples(videoInfo, samples, outputBuffer);
    }

    public void DoVideoFrames(int frames)
    {
        renderer.DoVideoFrames(videoInfo, frames);
    }

    public void StopVideoPlayback()
    {
        renderer.StopVideoPlayback(ref videoInfo);
    }

    public bool IsVideoFinished()
    {
        return renderer.IsVideoFinished(videoInfo);
    }
}