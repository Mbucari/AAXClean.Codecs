#include "ffmpegaac.h"

#include <libswresample/swresample_internal.h>

const int sampleFreqTable[13] =
{
96000,
88200,
64000,
48000,
44100,
32000,
24000,
22050,
16000,
12000,
11025,
8000,
7350
};


int32_t decode_audio(AVCodecContext* pAVContext, AVPacket* pavPacket, uint8_t* pCompressedAudio, uint32_t cbInBufferSize, AVFrame* pDecodedFrame)
{
    int ret;

    pavPacket->size = cbInBufferSize; //input buffer size
    pavPacket->data = pCompressedAudio; // the input bufferra

    /* send the packet with the compressed data to the decoder */
    ret = avcodec_send_packet(pAVContext, pavPacket);
    if (ret < 0)
        return ret;

    ret = avcodec_receive_frame(pAVContext, pDecodedFrame);

    return ret;
}

int32_t aacDecoder_ReceiveDecodedFrame(PAacDecoder config, uint8_t* outBuff0, uint8_t* outBuff1, int32_t numSamples) {
    
    int32_t required_size = swr_get_out_samples(config->swr_ctx, config->frame->nb_samples);

    if (!outBuff0 && !numSamples)
        return required_size;
    else if (!config->frame->nb_samples || required_size < numSamples)
        return -1;
    else {

        uint8_t* convertedData[2] = { outBuff0 , outBuff1 };
        int decoded = swr_convert(config->swr_ctx,
            convertedData, numSamples,
            config->frame->data, config->frame->nb_samples);

        if (decoded < 0)
            return -1;
        else return decoded;
    }
}

int32_t aacDecoder_DecodeFlush(PAacDecoder config, uint8_t* outBuff0, uint8_t* outBuff1, uint32_t cbOutBuff)
{
    if (!config || !config->context || !config->swr_ctx)
        return ERR_INVALID_HANDLE;

    int ret;
    uint8_t* convertedData[2] = { outBuff0 , outBuff1 };

    //Null frame flushes buffer
    ret = swr_convert(config->swr_ctx, convertedData, cbOutBuff, NULL, 0);
    return ret;
}

int32_t aacDecoder_DecodeFrame(PAacDecoder config, uint8_t* pCompressedAudio, uint32_t cbInBufferSize)
{
    if (!config || !config->context)
        return ERR_INVALID_HANDLE; 
 
    int ret = 0;

    ret = decode_audio(config->context, config->packet, pCompressedAudio, cbInBufferSize, config->frame);    
    return ret;
}

PVOID aacDecoder_Open(PAacDecoderOptions decoder_options)
{
    PAacDecoder pdec = NULL;
    AVCodec* codec;
    int ret = 0;

    if (!decoder_options) {
        ret = ERR_AAC_CODEC_NOT_FOUND;
        goto failed;
    }

    if (decoder_options->sample_fmt != AV_SAMPLE_FMT_S16 && decoder_options->sample_fmt != AV_SAMPLE_FMT_FLT && decoder_options->sample_fmt != AV_SAMPLE_FMT_FLTP) {
        ret = ERR_AAC_CODEC_NOT_FOUND;
        goto failed;
    }

    /*Initialize the decoder context*/
    pdec = malloc(sizeof(AacDecoder));
    if (!pdec) {
        ret = ERR_ALLOC_FAIL;
        goto failed;
    }

    pdec->context = NULL;
    pdec->swr_ctx = NULL;
    pdec->packet = NULL;
    pdec->frame = NULL;

    codec = avcodec_find_decoder(AV_CODEC_ID_AAC);

    if (!codec) {
        ret = ERR_AAC_CODEC_NOT_FOUND;
        goto failed;
    }

    /*Initialize the codec context*/
    pdec->context = avcodec_alloc_context3(codec);
    if (!pdec->context) {
        ret = ERR_ALLOC_FAIL;
        goto failed;
    }

    pdec->context->extradata_size = decoder_options->asc_size;
    pdec->context->extradata = malloc(pdec->context->extradata_size);

    if (!pdec->context->extradata) {
        ret = ERR_ALLOC_FAIL;
        goto failed;
    }

    memcpy(pdec->context->extradata, decoder_options->ASC, pdec->context->extradata_size);

    uint8_t b1 = (uint8_t)*decoder_options->ASC;
    uint8_t b2 = (uint8_t) * (decoder_options->ASC + 1);
    int sample_rate_index = ((b1 & 7) << 1) | (b2 >> 7);
    if (sample_rate_index < 0 || sample_rate_index > 12) {
        ret = ERR_ASC_INVALID;
        goto failed;
    }

    int input_sample_rate = sampleFreqTable[sample_rate_index];

    if (avcodec_open2(pdec->context, pdec->context->codec, NULL) != 0) {
        ret = ERR_AAC_CODEC_OPEN_FAIL;
        goto failed;
    }

    /*Initialize the resampler*/

    AVChannelLayout newLayout = decoder_options->channels == 2 ? (AVChannelLayout)AV_CHANNEL_LAYOUT_STEREO : (AVChannelLayout)AV_CHANNEL_LAYOUT_MONO;

    if (swr_alloc_set_opts2(
        &pdec->swr_ctx,
        &newLayout, decoder_options->sample_fmt, decoder_options->sample_rate,
        &pdec->context->ch_layout, pdec->context->sample_fmt, input_sample_rate, 0, NULL) < 0) {
        ret = ERR_SWR_INIT_FAIL;
        goto failed;
    }

    if (swr_init(pdec->swr_ctx) < 0) {
        ret = ERR_SWR_INIT_FAIL;
        goto failed;
    }

    pdec->frame = av_frame_alloc();
    if (!pdec->frame) {
        ret = ERR_ALLOC_FAIL;
        goto failed;
    }

    pdec->packet = av_packet_alloc();

    if (!pdec->packet) {
        ret = ERR_ALLOC_FAIL;
        goto failed;
    }

    return pdec;

failed:
    aacDecoder_Close(pdec);
    return ret;
}


int32_t aacDecoder_Close(PAacDecoder pdec)
{
    if (pdec) {
        if (pdec->context) {
            avcodec_close(pdec->context);
            av_free(pdec->context);
        }
        if (pdec->swr_ctx) {
            swr_close(pdec->swr_ctx);
            swr_free(&pdec->swr_ctx);
        }
        if (pdec->frame) {
            av_frame_free(&pdec->frame);
        }
        if (pdec->packet) {
            av_packet_free(&pdec->packet);
        }
        free(pdec);
    }
    return ERR_SUCCESS;
}