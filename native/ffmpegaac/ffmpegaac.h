#pragma once
#include "config.h"

#include <stdlib.h>
#include <stdint.h>

#include <libavutil/mem.h>
#include <libavutil/frame.h>
#include <libavcodec/avcodec.h>

#include <libavutil/samplefmt.h>
#include <libswresample/swresample.h>

#define AAC_FRAME_SIZE 1024

typedef void* PVOID;

#if !defined(min)
#define min(a,b) (((a) < (b)) ? (a) : (b))
#endif

#if defined(_MSC_VER)
//  Microsoft 
#define EXPORT __declspec(dllexport)
#elif defined(__GNUC__)
//  GCC
#define EXPORT __attribute__((visibility("default")))
#else
//  do nothing and hope for the best?
#define EXPORT
#pragma warning Unknown dynamic link import/export semantics.
#endif


typedef struct _AacDecoder {
    AVCodecContext* context;
    SwrContext* swr_ctx;
    AVPacket* packet;
    AVFrame* frame;
}AacDecoder, * PAacDecoder;

typedef struct _AacDecoderOptions {
    int32_t asc_size;
    int32_t sample_rate;
    int32_t channels;
    int32_t sample_fmt;
    uint8_t* ASC;
}AacDecoderOptions, * PAacDecoderOptions;

typedef struct _AacEncoder {
    AVCodecContext* context;
    AVPacket* packet;
    AVFrame* frame;
    int32_t current_frame_nb_samples;
}AacEncoder, * PAacEncoder;

typedef struct _AacEncoderOptions {
    int64_t bit_rate;
    int32_t global_quality;
    int32_t sample_rate;
    int32_t channels;
    int32_t sample_fmt;
}AacEncoderOptions, * PAacEncoderOptions;


#define ERR_SUCCESS 0
#define ERR_INVALID_HANDLE -1
#define ERR_BUFF_HANDLE_INVALID -2
#define ERR_BUFF_TOO_SMALL -3
#define ERR_ALLOC_FAIL -4
#define ERR_ASC_INVALID -5
#define ERR_AAC_CODEC_NOT_FOUND -6
#define ERR_AAC_CODEC_OPEN_FAIL -7
#define ERR_SWR_INIT_FAIL -8
#define ERR_AVPACKET_INIT_FAIL -9
#define ERR_AAC_DECODE_FAIL -10

/**
* Open an AAC-LC audio encoder instance. Only supports AV_SAMPLE_FMT_FLTP
audio in mono or stereo.
*  
* @param encoder_options options for encoding the audio.
* 
* @return handle to the encoder instance, otherwise a negative error code.
*/
EXPORT PVOID aacEncoder_Open(PAacEncoderOptions encoder_options);

EXPORT int32_t aacEncoder_Close(PAacEncoder config);

/**
* Send AV_SAMPLE_FMT_FLTP audio samples to the encoder up to AAC_FRAME_SIZE samples.
* 
* @param config encoder handle
* 
* @param pDecodedAudio0 a pointer to channel 0 of the of AV_SAMPLE_FMT_FLTP audio.
* 
* @param pDecodedAudio1 if stereo, a pointer to channel 1 of the AV_SAMPLE_FMT_FLTP audio.
* 
* @param nbSamples the number of audio samples being sent to the encoder
* 
* @return 0 if a full frame was sent to the encoder. If the encoder needs more
samples before sending a full frame, returns the number of samples needed.
Otherwise a negative error code.
*/
EXPORT int32_t aacEncoder_EncodeFrame(PAacEncoder config, uint8_t* pDecodedAudio0, uint8_t* pDecodedAudio1, int32_t nbSamples);

/**
* Receive and AAC-encoded audio frame. Must call first with outBuff null and
cbOutBuff 0 to retrieve the size of the encoded frame. Call repeatedly, first
with NULL/0 then with an empty buffer to drain the encoder buffer.
* 
* @param config encoder handle
* 
* @param outBuff The buffer to receive the encoded frame. Can be null.
* 
* @param cbOutBuff The size, in bytes, of outBuff. 
* 
* @return If outBuff is null and cbOutBuff is 0, the size of the encoded frame,
otherwise 0 if success or a negative error code.
* 
*/
EXPORT int32_t aacEncoder_ReceiveEncodedFrame(PAacEncoder config, uint8_t* outBuff, int32_t cbOutBuff);
/**
* Flush all data in the encoder buffer and signal the end of encoding.
* 
* @param config encoder handle
* 
* @return 0 is fuccess, otherwise a negative error code.
*/
EXPORT int32_t aacEncoder_EncodeFlush(PAacEncoder config);

/**
* Open an AAC-LC audio decoder instance.
*
* @param decoder_options options for decoding the audio.
*
* @return handle to the decoder instance
*/
EXPORT PVOID aacDecoder_Open(PAacDecoderOptions decoder_options);
EXPORT int32_t aacDecoder_Close(PAacDecoder config);

/**
* Send a frame of AAC-LC audio to the decoder.
* 
* @note Only 1 frame may be sent to the decoder before calling aacDecoder_ReceiveDecodedFrame
* 
* @param config decoder handle.
* 
* @param pCompressedAudio A pointer to a buffer containing the AAC-encoded audio frame.
* 
* @param cbInBufferSize The size, in bytes, of the AAC audio frame.
* 
* @return 0 is success, otherwise a negative error code.
* 
*/
EXPORT int32_t aacDecoder_DecodeFrame(PAacDecoder config, uint8_t* pCompressedAudio, uint32_t cbInBufferSize);
/**
* Receive a decoded audio frame. Must call first with outBuff0 null and cbOutBuff 0
to retrieve the size of the decoded frame. Call repeatedly, first with NULL/0 then
with an empty buffer to drain the encoder buffer.
*
* @param config decoder handle
*
* @param outBuff0 pointer to the buffer to receive the decoded frame. For packet
audio, this buffer receives the full frame (both stereo and mono). For planar audio,
this buffer receives the channel 0 audio. 
*
* @param outBuff1 pointer to the buffer to receive channel 1 of the decoded planar
audio. Unused for packet audio.
* 
* @param numSamples The size of the buffer, in number of audio samples.
*
* @return If outBuff0 is null and numSamples is 0, the maximum size (in number of
audio samples) of the decoded frame. Otherwise the number of samples actually
decoded if positive, or a negative error code.
*
*/
EXPORT int32_t aacDecoder_ReceiveDecodedFrame(PAacDecoder config, uint8_t* outBuff0, uint8_t* outBuff1, int32_t numSamples);
/**
* Flush all data in the decoder buffer and signal the end of decoding. Call aacDecoder_ReceiveDecodedFrame()
with NULL/0 to get the maximum size of the buffer needed to drain the decoder.
*
* @param config decoder handle
* 
* @param outBuff0 pointer to the buffer to receive the decoded frame. For packet
audio, this buffer receives the full frame (both stereo and mono). For planar audio,
this buffer receives the channel 0 audio.
*
* @param outBuff1 pointer to the buffer to receive channel 1 of the decoded planar
audio. Unused for packet audio.
*
* @return the number of samples decoded, otherwise a negative error code.
*/
EXPORT int32_t aacDecoder_DecodeFlush(PAacDecoder config, uint8_t* outBuff0, uint8_t* outBuff1, uint32_t cbOutBuff);
