﻿using System;
using ManagedBass;
using ManagedBass.DirectX8;
using ManagedBass.Fx;
using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    public static class BassHelpers
    {
        public const int PLAYBACK_BUFFER_LENGTH = 75;
        public const double PLAYBACK_BUFFER_DESYNC = PLAYBACK_BUFFER_LENGTH / 1000.0;

        public const float REVERB_VOLUME_MULTIPLIER = 0.80f;

        public const int FADE_TIME_MILLISECONDS = 1000;

        public const int REVERB_SLIDE_IN_MILLISECONDS = 300;
        public const int REVERB_SLIDE_OUT_MILLISECONDS = 500;

        public const EffectType REVERB_TYPE = EffectType.Freeverb;

        /*
         * From Bass documentation (http://bass.radio42.com/help/html/4c663bda-2751-c2c3-eaf2-770b846b6652.htm)
         * "With a ratio of 4:1, when the (time averaged) input level is 4 dB over the threshold, the output signal level will be 1 dB over the threshold."
         * "[Additionally,] with any threshold/ratio combination, you could calculate the gain for a 0dB peak like this: fGain=fThreshold*(1/fRatio-1)"
         * 
         * The intention of the gain is to normalize 0dB signals back to 0dB after compression.
         * However, we only want the compressors to handle "clipping" situations (audio that exceeds 0dB).
         * So we set the gain and thresholds both to zero - which still follows the formula.
         * We can then set the ratio to whatever we want.
         * 
         * Note: you don't want to apply a negative gain as the gain value effects ALL audio, not just the part that got compressed.
         * We don't want to make quiet parts even quieter.
         */
        public static readonly CompressorParameters CompressorParams = new()
        {
            fGain = 0f, fThreshold = 0, fAttack = 10f, fRelease = 100f, fRatio = 8,
        };
        
        public static readonly PeakEQParameters LowEqParams = new()
        {
            fBandwidth = 1.25f, fCenter = 250.0f, fGain = -12f
        };

        public static readonly PeakEQParameters MidEqParams = new()
        {
            fBandwidth = 1.25f, fCenter = 2300.0f, fGain = 2.25f
        };

        public static readonly PeakEQParameters HighEqParams = new()
        {
            fBandwidth = 0.75f, fCenter = 6000.0f, fGain = 2.25f
        };

        public static readonly DXReverbParameters DXReverbParams = new()
        {
            fInGain = -5f, fReverbMix = 0f, fReverbTime = 1000.0f, fHighFreqRTRatio = 0.001f
        };

        public static readonly ReverbParameters FreeverbParams = new()
        {
            fDryMix = 0.5f, fWetMix = 1.0f, fRoomSize = 0.8f, fDamp = 0.5f, fWidth = 1.0f, lMode = 0
        };

        public static int FXAddParameters(int streamHandle, EffectType type, IEffectParameter parameters,
            int priority = 0)
        {
            int fxHandle = Bass.ChannelSetFX(streamHandle, type, priority);
            if (fxHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create effects handle for {0}: {1}", type, Bass.LastError);
                return 0;
            }

            if (!Bass.FXSetParameters(fxHandle, parameters))
            {
                YargLogger.LogFormatError("Failed to apply effects parameters for {0}: {1}", type, Bass.LastError);
                Bass.ChannelRemoveFX(streamHandle, fxHandle);
                return 0;
            }

            return fxHandle;
        }

        public static int FXAddParameters<T>(int streamHandle, EffectType type, T parameters, int priority = 0)
            where T : unmanaged, IEffectParameter
        {
            int fxHandle = Bass.ChannelSetFX(streamHandle, type, priority);
            if (fxHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create effects handle: {0}", Bass.LastError);
                return 0;
            }

            if (!FXSetParameters(fxHandle, parameters))
            {
                YargLogger.LogFormatError("Failed to apply effects parameters: {0}", Bass.LastError);
                Bass.ChannelRemoveFX(streamHandle, fxHandle);
                return 0;
            }

            return fxHandle;
        }

        public static unsafe bool FXSetParameters<T>(int Handle, T Parameters)
            where T : unmanaged, IEffectParameter
        {
            return Bass.FXSetParameters(Handle, (IntPtr) (void*) &Parameters);
        }

        public static int AddCompressorToChannel(int handle)
        {
            return FXAddParameters(handle, EffectType.Compressor, CompressorParams);
        }

        public static int AddReverbToChannel(int handle)
        {
            IEffectParameter reverbParams = REVERB_TYPE switch
            {
                EffectType.DXReverb => DXReverbParams,
                EffectType.Freeverb => FreeverbParams,
                _ => throw new ArgumentOutOfRangeException()
            };

            return FXAddParameters(handle, REVERB_TYPE, reverbParams);
        }

        public static int AddEqToChannel(int handle, IEffectParameter eqParams)
        {
            return FXAddParameters(handle, EffectType.PeakEQ, eqParams);
        }

        public static unsafe bool ApplyGain(float gain, IntPtr buffer, int length)
        {
            var sampleBuffer = new Span<float>((void*) buffer, length / sizeof(float));

            foreach (ref float sample in sampleBuffer)
            {
                sample *= gain;
            }

            return true;
        }
    }
}