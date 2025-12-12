#include "ffmpegaac.h"

int32_t AacEncoder_EncodeFlush(PAacEncoder config) {

    int32_t ret;

    if (config->current_frame_nb_samples) {
        //Send last partial frame
        ret = avcodec_send_frame(config->context, config->frame);

        if (ret < 0)
            return ret;
    }

    config->current_frame_nb_samples = 0;
    //Flush the encoder
    ret = avcodec_send_frame(config->context, NULL);

    if (ret == 0 || ret == AVERROR_EOF)
        return 0;
    else return ret;
}

int32_t AacEncoder_ReceiveEncodedFrame(PAacEncoder config, uint8_t* outBuff, int32_t cbOutBuff) {

    int32_t ret;

    if (!outBuff && !cbOutBuff) {
        //Called with null, so receive packet and report size;
        ret = avcodec_receive_packet(config->context, config->packet);
        if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF)
            return 0;
        else if (ret < 0)
            return -1;
        else
            return config->packet->size;
    }
    else if (!outBuff || config->packet->size > cbOutBuff) {
        //Buffer is too small. Tell caller how big it needs to be.
        return config->packet->size;
    }
    else
    {
        memcpy(
            outBuff,
            config->packet->data,
            config->packet->size);

        return 0;
    }
}

int32_t AacEncoder_EncodeFrame(PAacEncoder config, uint8_t* pDecodedAudio0, uint8_t* pDecodedAudio1, int32_t nbSamples) {

    int32_t i, ret;
    int32_t nb_available_samples = nbSamples + config->current_frame_nb_samples;
    int32_t remain_to_fill = AAC_FRAME_SIZE - config->current_frame_nb_samples;
    int32_t to_copy = min(nbSamples, remain_to_fill);

    uint8_t* inputBuff[2] = { pDecodedAudio0 , pDecodedAudio1 };
    const int32_t num_planes = pDecodedAudio1 ? 2 : 1;
	const int32_t bytes_per_sample = config->sample_size * config->context->ch_layout.nb_channels / num_planes;

    //Copy audio into the frame buffer from where we left off last time
    for (i = 0; i < num_planes; i++) {
        memcpy(
            config->frame->data[i] + config->current_frame_nb_samples * bytes_per_sample,
            inputBuff[i],
            to_copy * bytes_per_sample);
    }

    config->current_frame_nb_samples += to_copy;

    //Tell the caller how many more samples we need before we can encode a frame.
    if (config->current_frame_nb_samples < AAC_FRAME_SIZE)
        return AAC_FRAME_SIZE - config->current_frame_nb_samples;

    ret = avcodec_send_frame(config->context, config->frame);
    if (ret < 0)
        return ret;

    config->current_frame_nb_samples = 0;
    nb_available_samples -= AAC_FRAME_SIZE;

    //Copy the rest of the partial frame to the beginning of the frame buffer
    if (nb_available_samples > 0) {

        for (i = 0; i < num_planes; i++) {
            memcpy(
                config->frame->data[i],
                inputBuff[i] + to_copy * bytes_per_sample,
                nb_available_samples * bytes_per_sample);
        }

        config->current_frame_nb_samples = nb_available_samples;
    }

    return 0;
}

int32_t AacEncoder_Close(PAacEncoder config) {

    if (config) {
        if (config->context) {
            avcodec_free_context(&config->context);
        }
        if (config->packet) {
            av_packet_free(&config->packet);
        }
        if (config->frame) {
            av_frame_free(&config->frame);
        }
        free(config);
    }
    return ERR_SUCCESS;
}

EXPORT int32_t AacEncoder_GetExtraData(PAacEncoder config, uint8_t* ascBuffer, int32_t* pSize) {

    int32_t ret;

    if (!ascBuffer || !pSize || *pSize < config->context->extradata_size) {
        ret = config->context->extradata_size;
        goto failed;
    }
     memcpy(
		ascBuffer,
        config->context->extradata,
		config->context->extradata_size);

     *pSize = config->context->extradata_size;
     return ERR_SUCCESS;

failed:
    if(pSize)
        *pSize = 0;
    return ret;
}

PVOID AacEncoder_Open(PAacEncoderOptions encoder_options) {

    PAacEncoder penc = NULL;
    const AVCodec* codec;
    intptr_t ret = 0;

    if (!encoder_options) {
        ret = -1;
        goto failed;
    }

    /*Initialize the encoder context*/
    penc = malloc(sizeof(AacEncoder));
    if (!penc) {
        ret = ERR_ALLOC_FAIL;
        goto failed;
    }

    penc->context = NULL;
    penc->packet = NULL;
    penc->frame = NULL;
    penc->current_frame_nb_samples = 0;

    codec = avcodec_find_encoder(AV_CODEC_ID_AAC);

    if (!codec) {
        ret = ERR_AAC_CODEC_NOT_FOUND;
        goto failed;
    }

    // Ffmpeg native aac encoder only supports AV_SAMPLE_FMT_FLTP
    // FDK aac encoder only supports AV_SAMPLE_FMT_S16
    if (strcmp(codec->name, "libfdk_aac") == 0 && encoder_options->sample_fmt == AV_SAMPLE_FMT_S16) {
        penc->sample_size = sizeof(uint16_t);
    } else if (strcmp(codec->name, "aac") == 0 && encoder_options->sample_fmt == AV_SAMPLE_FMT_FLTP) {
        penc->sample_size = sizeof(float_t);
    } else {
        ret = -1;
        goto failed;
    }

    /*Initialize the codec context*/
    penc->context = avcodec_alloc_context3(codec);
    if (!penc->context) {
        ret = ERR_ALLOC_FAIL;
        goto failed;
    }

    //maximum bitrate is 6144 * channels / 1024.0 * sample_rate
    //put sample parameters
    penc->context->bit_rate = encoder_options->bit_rate;
    penc->context->sample_rate = encoder_options->sample_rate;
    penc->context->sample_fmt = encoder_options->sample_fmt;
    penc->context->global_quality = encoder_options->global_quality;
    penc->context->flags |= AV_CODEC_FLAG_GLOBAL_HEADER;
    penc->context->ch_layout = encoder_options->channels == 2 ? (AVChannelLayout)AV_CHANNEL_LAYOUT_STEREO : (AVChannelLayout)AV_CHANNEL_LAYOUT_MONO;

    ret = avcodec_open2(penc->context, codec, NULL);
    if (ret < 0)
        goto failed;

    penc->packet = av_packet_alloc();
    if (!penc->packet) {
        ret = -1;
        goto failed;
    }

    penc->frame = av_frame_alloc();
    if (!penc->frame) {
        ret = -1;
        goto failed;
    }

    penc->frame->nb_samples = penc->context->frame_size;
    penc->frame->format = penc->context->sample_fmt;

    ret = av_channel_layout_copy(&penc->frame->ch_layout, &penc->context->ch_layout);
    if (ret < 0)
        goto failed;

    ret = av_frame_get_buffer(penc->frame, 0);
    if (ret < 0)
        goto failed;

    ret = av_frame_make_writable(penc->frame);
    if (ret < 0)
        goto failed;

    return penc;

failed:

    AacEncoder_Close(penc);

    return (void*)ret;
}
