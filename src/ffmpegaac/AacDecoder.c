#include "ffmpegaac.h"

#include <libswresample/swresample_internal.h>

static int32_t decode_audio(AVCodecContext* pAVContext, AVPacket* pavPacket, AVFrame* pDecodedFrame)
{
    int32_t ret;

    /* send the packet with the compressed data to the decoder */
    ret = avcodec_send_packet(pAVContext, pavPacket);
    if (ret < 0)
        return ret;

    ret = avcodec_receive_frame(pAVContext, pDecodedFrame);
    return ret;
}

int32_t Decoder_ReceiveDecodedFrame(PAacDecoder config, uint8_t* outBuff0, uint8_t* outBuff1, int32_t numSamples) {
    
    int32_t required_size = swr_get_out_samples(config->swr_ctx, config->nb_samples);

    if (!outBuff0 && !numSamples)
        return required_size;
    else if (!config->nb_samples || required_size < numSamples)
        return -1;
    else {

        uint8_t* convertedData[2] = { outBuff0 , outBuff1 };
        int32_t decoded = swr_convert(config->swr_ctx,
            convertedData, numSamples,
            config->data, config->nb_samples);

        if (config->use_temp_buffer) {
			for (int32_t i = 0; i < AV_NUM_DATA_POINTERS && config->data[i]; i++) {
				if (config->data[i]) {
					free(config->data[i]);
					config->data[i] = NULL;
				}
			}
        }

        if (decoded < 0)
            return -1;
        else return decoded;
    }
}

int32_t Decoder_DecodeFlush(PAacDecoder config, uint8_t* outBuff0, uint8_t* outBuff1, uint32_t cbOutBuff)
{
    if (!config || !config->context || !config->swr_ctx)
        return ERR_INVALID_HANDLE;

    int32_t ret;
    uint8_t* convertedData[2] = { outBuff0 , outBuff1 };

    //Null frame flushes buffer
    ret = swr_convert(config->swr_ctx, convertedData, cbOutBuff, NULL, 0);
    return ret;
}

int32_t Decoder_DecodeFrame(PAacDecoder config, uint8_t* pCompressedAudio, uint32_t cbInBufferSize, int32_t nbSamples)
{
    if (!config || !config->context)
        return ERR_INVALID_HANDLE; 
 
    int32_t ret;

    config->packet->size = cbInBufferSize; //input buffer size
    config->packet->data = pCompressedAudio; // the input buffer
	config->packet->duration = nbSamples; //number of samples in the decoded frame

    ret = decode_audio(config->context, config->packet, config->frame);
	if (ret == 0) {
        if (config->frame->nb_samples >= nbSamples) {
            config->use_temp_buffer = 0;
            for (int i = 0; i < AV_NUM_DATA_POINTERS && config->frame->data[i]; i++) {
                config->data[i] = config->frame->data[i];
            }
        }
        else {
            //The frame is supposed to be longer, so pad the end with silence
            int32_t size = av_get_bytes_per_sample(config->swr_ctx->in_sample_fmt) * config->swr_ctx->in_ch_layout.nb_channels;
            config->use_temp_buffer = 1;
            for (int i = 0; i < AV_NUM_DATA_POINTERS && config->frame->data[i]; i++) {
				size_t sampleBytes = size * nbSamples;
				config->data[i] = malloc(sampleBytes);
				if (!config->data[i]) {
					ret = ERR_ALLOC_FAIL;
                }
                else {
                    memset(config->data[i], 0, sampleBytes);
                    memcpy(config->data[i], config->frame->data[i], config->frame->linesize[i]);
                }
            }
        }
        config->nb_samples = nbSamples;
	}
    return ret;
}

static int32_t parse_asc(uint8_t *asc, AVChannelLayout* pIn_layout, uint32_t* pIn_sample_rate) {

    int32_t ret = 0;
    uint8_t sample_index;
    uint8_t channel_layout;
    const uint8_t AOT_ESCAPE = 0x1F;
    const uint32_t sampleFreqTable[13] =
    { 96000, 88200, 64000, 48000, 44100, 32000, 24000,
        22050, 16000, 12000, 11025, 8000, 7350 };

    if (*asc >> 3 == AOT_ESCAPE) {
        sample_index = (*(asc + 1) >> 1) & 0x1F;
        channel_layout = ((*(asc + 1) & 1) << 3) | (*(asc + 1) >> 5);
    }
    else {
        sample_index = ((*asc & 7) << 1) | (*(asc + 1) >> 7);
        channel_layout = (*(asc + 1) >> 3) & 0xF;
    }

    if (sample_index < 0 || sample_index >= sizeof(sampleFreqTable) / sizeof(uint32_t)
        || channel_layout > 2) {
        ret = ERR_ASC_INVALID;
        goto end;
    }

    *pIn_sample_rate = sampleFreqTable[sample_index];
    *pIn_layout
        = channel_layout == 2
        ? (AVChannelLayout)AV_CHANNEL_LAYOUT_STEREO
        : (AVChannelLayout)AV_CHANNEL_LAYOUT_MONO;

end:
    return ret;
}

static int32_t init_swr(PAacDecoder pdec, POutputOptions pOptions, AVChannelLayout* pIn_layout, int32_t in_sample_rate) {

    int32_t ret = 0;
    int32_t out_sample_fmt;
    AVChannelLayout out_layout;

    if (pOptions->out_channels < 1 || pOptions->out_channels > 2) {
        ret = ERR_SWR_OUTPUT_CHANNELS_UNSUPPORTED;
        goto failed;
    }

    out_layout
        = pOptions->out_channels == 2
        ? (AVChannelLayout)AV_CHANNEL_LAYOUT_STEREO
        : (AVChannelLayout)AV_CHANNEL_LAYOUT_MONO;

    out_sample_fmt = pOptions->out_sample_fmt;
    if (out_sample_fmt != AV_SAMPLE_FMT_S16 && out_sample_fmt != AV_SAMPLE_FMT_FLT && out_sample_fmt != AV_SAMPLE_FMT_FLTP) {
        ret = ERR_SWR_OUTPUT_FORMAT_UNSUPPORTED;
        goto failed;
    }
    
    if (swr_alloc_set_opts2(
        &pdec->swr_ctx,
        &out_layout, pOptions->out_sample_fmt, pOptions->out_sample_rate,
        pIn_layout, pdec->context->sample_fmt, in_sample_rate, 0, NULL) < 0) {
        ret = ERR_SWR_INIT_FAIL;
        goto failed;
    }

    if (swr_init(pdec->swr_ctx) < 0) {
        ret = ERR_SWR_INIT_FAIL;
        goto failed;
    }

    return ret;

failed:
    if (pdec->swr_ctx) {
        swr_close(pdec->swr_ctx);
        swr_free(&pdec->swr_ctx);
    }
	return ret;
}

static int32_t init_frame_packet(PAacDecoder pdec) {

    int32_t ret = 0;

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

    return ret;

failed:
    if (pdec->frame) {
        av_frame_free(&pdec->frame);
    }
    if (pdec->packet) {
        av_packet_free(&pdec->packet);
    }
    return ret;
}

static int32_t init_decoder_context(PAacDecoder* ppdec, enum AVCodecID id) {

    int32_t ret = 0;
    const AVCodec* codec;
    PAacDecoder pdec = NULL;


    pdec = malloc(sizeof(AacDecoder));
    if (!pdec) {
        ret = ERR_ALLOC_FAIL;
        *ppdec = NULL;
        goto failed;
    }
	*ppdec = pdec;

    pdec->context = NULL;
    pdec->swr_ctx = NULL;
    pdec->packet = NULL;
    pdec->frame = NULL;
    for (int32_t i = 0; i < AV_NUM_DATA_POINTERS; i++) {
        pdec->data[i] = NULL;
    }

    codec = avcodec_find_decoder(id);

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

    return ret;

failed:
    if (pdec) {
        if (pdec->context) {
            avcodec_free_context(&pdec->context);
            av_free(pdec->context);
        }
        free(pdec);
    }
    return ret;
}

int32_t Decoder_Close(PAacDecoder pdec)
{
    if (pdec) {
        if (pdec->context) {
            avcodec_free_context(&pdec->context);
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

PVOID Decoder_OpenAac(PAacDecoderOptions decoder_options)
{
    int32_t ret = 0;
    int32_t out_sample_fmt;
    AVChannelLayout in_layout;
    int32_t in_sample_rate;
    PAacDecoder pdec = NULL;

    if (!decoder_options || !decoder_options->ASC || decoder_options->asc_size < 2) {
        ret = ERR_AAC_CODEC_NOT_FOUND;
        goto failed;
    }

    if ((ret = init_decoder_context(&pdec, AV_CODEC_ID_AAC)) != 0) {
        goto failed;
    }

    /* Copy ASC to AVCodecConbtext.extradata and open the codec*/
    pdec->context->extradata_size = decoder_options->asc_size;
    pdec->context->extradata = av_malloc(pdec->context->extradata_size);
    if (!pdec->context->extradata) {
        ret = ERR_ALLOC_FAIL;
        goto failed;
    }

    memcpy(pdec->context->extradata, decoder_options->ASC, pdec->context->extradata_size);
    if (avcodec_open2(pdec->context, pdec->context->codec, NULL) != 0) {
        ret = ERR_AAC_CODEC_OPEN_FAIL;
        goto failed;
    }

    /* parse the ASC to get source channel config and sample rate */
    if ((ret = parse_asc(decoder_options->ASC, &in_layout, &in_sample_rate)) != 0) {
        goto failed;
    }

    if ((ret = init_swr(pdec, &decoder_options->output_options, &in_layout, in_sample_rate)) != 0) {
        goto failed;
    }

    if ((ret = init_frame_packet(pdec)) != 0) {
        goto failed;
    }

    return pdec;

failed:
    Decoder_Close(pdec);
    return ret;
}

#include <libavcodec/ac3tab.h>
#include <libavcodec/ac3_channel_layout_tab.h>

PVOID Decoder_OpenEC3(PEC3DecoderOptions decoder_options) {
    
    uint8_t nb_channels;
    uint16_t channel_layout;
    AVChannelLayout in_layout;
    PAacDecoder pdec = NULL;
    int32_t ret = 0;

    if (!decoder_options) {
        ret = ERR_AAC_CODEC_NOT_FOUND;
        goto failed;
    }

    if (decoder_options->in_audio_coding_mode > 7) {
        ret = ERR_AAC_CODEC_NOT_FOUND;
        goto failed;
    }

    nb_channels = ff_ac3_channels_tab[decoder_options->in_audio_coding_mode];
	channel_layout = ff_ac3_channel_layout_tab[decoder_options->in_audio_coding_mode];
    if (decoder_options->in_subwoofer) {
        channel_layout |= AV_CH_LOW_FREQUENCY;
        nb_channels++;
    }

    in_layout = (AVChannelLayout)AV_CHANNEL_LAYOUT_MASK(nb_channels, channel_layout);
    /*Initialize the decoder context*/
	if ((ret = init_decoder_context(&pdec, AV_CODEC_ID_EAC3)) != 0) {
		goto failed;
	}
    
    if (avcodec_open2(pdec->context, pdec->context->codec, NULL) != 0) {
        ret = ERR_AAC_CODEC_OPEN_FAIL;
        goto failed;
    }

	if ((ret = init_swr(pdec, &decoder_options->output_options, &in_layout, decoder_options->in_sample_rate)) != 0) {
		goto failed;
	}

	if ((ret = init_frame_packet(pdec)) != 0) {
		goto failed;
	}

    return pdec;

failed:
    Decoder_Close(pdec);
    return ret;
}
