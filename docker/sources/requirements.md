<!-- 
-- +==== BEGIN AAXClean.Codecs =================+
-- LOGO: 
-- +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
-- |A|A|X|C|l|e|a|n|.|C|o|d|e|c|s|
-- +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
-- 
-- name: Bubble
-- source https://patorjk.com/software/
-- taag/#p=display&f=Digital&
-- t=AAXClean.Codecs&x=none&v=4&h=4
-- &w=80&we=false
-- /STOP
-- PROJECT: AAXClean.Codecs
-- FILE: requirements.md
-- CREATION DATE: 13-12-2025
-- LAST Modified: 13:30:39 13-12-2025
-- DESCRIPTION: 
-- Converts and filters AAC audio from AAXClean. Supports multiple codecs (AAC-LC, E-AC-3, HE-AAC, etc.) and platforms (Windows, macOS, Linux). Provides NuGet integration and APIs for audio conversion, silence detection, and multipart processing.
-- /STOP
-- COPYRIGHT: (c) AAXClean.Codecs
-- PURPOSE: This is the overview of the issue I was trying to solve.
-- // AR
-- +==== END AAXClean.Codecs =================+
-->

# Content from issue 8

This issue is to track progress towards making build systems for ffmpegaac, the native library which decodes aac/e-ac-3, downsamples audio, and encodes aac.

Required platforms:

* Windows x64
* Linux x64
* Linux arm64
* macOS x64
* macOS arm64
* Would be nice:
* Windows arm64

[@HenraL](https://github.com/HenraL)

could you give me details about the dependencies and their versions that you used?

The dependencies are whatever is required to build ffmpeg. My [build script](https://github.com/Mbucari/AAXClean.Codecs/blob/master/src/ffmpegaac/build_aaxclean_libs.sh) specs the following to install from the package manager:

```bash
gcc make perl pkg-config yasm nasm autoconf libtool
```

I know that nasm minimum version is nasm-2.13, and I believe that was the only package that I had to compile myself because the apt-get version was too low on Ubuntu 18.

For ffmpegaac to build, you first need to build [fdk-aac](https://github.com/mstorsjo/fdk-aac) (I build from the master).
Here are the build commands
for libfdk-aac. I install it to a folder named `libfdk` in the same directory as the `ffmpeegaac` and `ffmpeg-x.x.x` folders.

```bash
FDK_INSTALL="libfdk"
./autogen.sh
./configure --prefix=$FDK_INSTALL --enable-shared=no --enable-static=yes
make -j$NUM_CPUS CFLAGS="-g0 -fPIC -O2 -Werror" CXXFLAGS="-g0 -fPIC -O2 -Werror"
make install
```

You then need to build ffmpeg (I try to use the latest version. I just used 8.0.1 successfully). with the following commands:

```bash
export PKG_CONFIG_PATH=$FDK_INSTALL/lib/pkgconfig
./configure --disable-swscale --disable-avdevice --disable-doc --disable-v4l2-m2m --disable-vaapi --disable-vdpau --disable-network --disable-libxcb --disable-libxcb-xfixes --disable-libxcb-shape --disable-zlib --disable-iconv --disable-alsa --disable-shared --enable-static --disable-doc --disable-symver --disable-programs --disable-debug --enable-pic --disable-everything --enable-decoder=pcm_f32le --enable-decoder=pcm_s16le --enable-encoder=pcm_f32le --enable-encoder=pcm_s16le --enable-demuxer=pcm_f32le --enable-demuxer=pcm_s16le --enable-muxer=pcm_f32le --enable-muxer=pcm_s16le --enable-filter=aresample --enable-filter=asetnsamples --enable-libfdk_aac --enable-nonfree --enable-decoder=libfdk_aac --enable-encoder=libfdk_aac --enable-decoder=eac3
make
```

Then, you need to build ffmpegaac. I've been using the following commands. Though now I have a cmake file for it.

```bash
gcc -fPIC -c AacDecoder.c -c AacEncoder.c -I$FFMPEG_MAIN -I./
gcc -pthread -shared -fPIC -Wl,-Bsymbolic -Wl,--no-undefined -Wl,-soname,ffmpegaac.so.2 -o ffmpegaac.so AacEncoder.o AacDecoder.o -L$FFMPEG_MAIN/libavutil -L$FFMPEG_MAIN/libswscale -L$FFMPEG_MAIN/libswresample -L$FFMPEG_MAIN/libavcodec -L$FFMPEG_MAIN/libavformat -L$FFMPEG_MAIN/libavfilter -L$FFMPEG_MAIN/libavdevice -L$FDK_INSTALL/lib -lc -lavfilter -lswresample -lavformat -lavcodec -lavutil -lfdk-aac -lm -lrt
```

The cmake requires the following directory structure:

```bash
├── ffmpegaac
│   ├── AacDecoder.c
│   ├── AacEncoder.c
│   ├── CMakeLists.txt
│   ├── ffmpegaac.h
├── libfdk
│   ├── include
│   │   └── fdk-aac
│   │       ├── aacdecoder_lib.h
│   │       ├── aacenc_lib.h
│   │       ├── FDK_audio.h
│   │       ├── genericStds.h
│   │       ├── machine_type.h
│   │       └── syslib_channelMapDescr.h
│   └── lib
│       ├── libfdk-aac.a
│       ├── libfdk-aac.la
│       └── pkgconfig
│           └── fdk-aac.pc
└── ffmpeg-x.x.x
    ├── config.h
    ├── libavcodec
    │   ├── avcodec.h
    │   ├── mpeg4audio_sample_rates.h
    │   ├── ac3tab.h
    │   └── libavcodec.a
    ├── libavfilter
    │   └── libavfilter.a
    ├── libavformat
    │   └── libavformat.a
    ├── libavutil
    │   ├── frame.h
    │   ├── mem.h
    │   ├── samplefmt.h
    │   └── libavutil.a
    └── libswresample
        ├── swresample.h
        └── libswresample.a
```
