#!/bin/bash
# 
# +==== BEGIN AAXClean.Codecs =================+
# LOGO: 
# +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
# |A|A|X|C|l|e|a|n|.|C|o|d|e|c|s|
# +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
# 
# name: Bubble
# source https://patorjk.com/software/
# taag/#p=display&f=Digital&
# t=AAXClean.Codecs&x=none&v=4&h=4
# &w=80&we=false
# /STOP
# PROJECT: AAXClean.Codecs
# FILE: build_aaxclean_libs.sh
# CREATION DATE: 13-12-2025
# LAST Modified: 13:31:31 13-12-2025
# DESCRIPTION: 
# Converts and filters AAC audio from AAXClean. Supports multiple codecs (AAC-LC, E-AC-3, HE-AAC, etc.) and platforms (Windows, macOS, Linux). Provides NuGet integration and APIs for audio conversion, silence detection, and multipart processing.
# /STOP
# COPYRIGHT: (c) AAXClean.Codecs
# PURPOSE: This is the refence bash script that I used to try and create the docker container.
# // AR
# +==== END AAXClean.Codecs =================+
# 

OS=$(uname -s)
ARCH=$(uname -m)
if [[ ! "$ARCH" =~ ^(x86_64|arm64|aarch64)$ ]]; then
  echo "Unknown architecture: $ARCH"
  exit 1
fi

THIS_DIR=$(pwd)
FFMPEG_VER="8.0.1"
LAME_VER="3.100"

download_file() {
  FILENAME=$1
  URL=$2
  if [ -f "$FILENAME" ]; then
    return
  fi
  if command -v wget &> /dev/null; then
    echo "Downloading $FILENAME"
    wget -O $FILENAME --no-check-certificate $URL &> /dev/null;
  elif command -v curl &> /dev/null; then
    echo "Downloading $FILENAME"
    curl -k -o $FILENAME -L0 $URL &> /dev/null;
  else
    echo "could not download $FILENAME"
    exit 1
  fi
}

echo "Installing $OS dependencies"
if [ $OS = Linux ]; then
  if ! command -v apt-get &> /dev/null; then
    sudo dnf install gcc make perl pkg-config yasm nasm autoconf libtool
  else
    sudo apt-get install gcc make perl pkg-config yasm nasm autoconf libtool
  fi
  LIB_EXTENSION=so
  NUM_CPUS=$(nproc)
elif [ $OS = Darwin ]; then
  if ! command -v brew &> /dev/null; then
    echo "Installing homebrew"
    /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
  fi
  #xcode-select --install && brew install gcc perl pkg-config yasm
  LIB_EXTENSION=dylib
  NUM_CPUS=$(sysctl -n hw.physicalcpu)
elif [[ $OS =~ MINGW64.* ]]; then
  OS=Windows
  #pacman -S gcc make perl pkg-config yasm autoconf libtool unzip
  LIB_EXTENSION=dll
  NUM_CPUS=$(nproc)
  ARCH=x64
elif [[ $OS =~ MINGW32.* ]]; then
  OS=Windows
  #pacman -S gcc make perl pkg-config yasm autoconf libtool unzip
  LIB_EXTENSION=dll
  NUM_CPUS=$(nproc)
  ARCH=x86
else
  echo "Unknown OS: '$OS'"
  exit 1
fi

FDK_NAME=fdk-aac-master
FDK_MAIN=$THIS_DIR/$FDK_NAME
FDK_INSTALL=$THIS_DIR/libfdk
if [ ! -d "$FDK_MAIN" ]; then
  download_file $FDK_NAME.zip "https://github.com/mstorsjo/fdk-aac/archive/refs/heads/master.zip"
  echo "Extracting $FDK_NAME.zip"
  unzip -o ./$FDK_NAME.zip
  rm $FDK_NAME.zip
fi

if ! [ -f "$FDK_INSTALL/lib/libfdk-aac.a" ]; then
  echo "Building $FDK_NAME"
  cd $FDK_MAIN
  ./autogen.sh &> /dev/null;
  ./configure --prefix=$FDK_INSTALL --enable-shared=no --enable-static=yes &> /dev/null;
  make -j$NUM_CPUS CFLAGS="-g0 -fPIC -O2 -Werror" CXXFLAGS="-g0 -fPIC -O2 -Werror" &> /dev/null;
  make install &> /dev/null;
  cd $THIS_DIR
fi


FFMPEG_NAME=ffmpeg-$FFMPEG_VER
FFMPEG_MAIN=$THIS_DIR/$FFMPEG_NAME
if [ ! -d "$FFMPEG_MAIN" ]; then
  download_file $FFMPEG_NAME.tar.xz "https://ffmpeg.org/releases/$FFMPEG_NAME.tar.xz"
  echo "Extracting $FFMPEG_NAME.tar.xz"
  tar -xf ./$FFMPEG_NAME.tar.xz
  rm $FFMPEG_NAME.tar.xz
fi

if ! [ -f "$FFMPEG_MAIN/libavcodec/libavcodec.a" ]; then
  echo "Building $FFMPEG_NAME"
  cd $FFMPEG_MAIN
  export PKG_CONFIG_PATH=$FDK_INSTALL/lib/pkgconfig
  ./configure --disable-swscale --disable-avdevice --disable-doc --disable-v4l2-m2m --disable-vaapi --disable-vdpau --disable-network --disable-libxcb --disable-libxcb-xfixes --disable-libxcb-shape --disable-zlib --disable-iconv --disable-alsa --disable-shared --enable-static --disable-doc --disable-symver --disable-programs --disable-debug --enable-pic --disable-everything --enable-decoder=pcm_f32le --enable-decoder=pcm_s16le --enable-encoder=pcm_f32le --enable-encoder=pcm_s16le --enable-demuxer=pcm_f32le --enable-demuxer=pcm_s16le --enable-muxer=pcm_f32le --enable-muxer=pcm_s16le --enable-filter=aresample --enable-filter=asetnsamples --enable-libfdk_aac --enable-nonfree --enable-decoder=libfdk_aac --enable-encoder=libfdk_aac --enable-decoder=eac3
  make
  cd $THIS_DIR
fi

FFMPEGAAC_MAIN=$THIS_DIR/ffmpegaac
if [ ! -d "$FFMPEGAAC_MAIN" ]; then
  mkdir $FFMPEGAAC_MAIN
  cd $FFMPEGAAC_MAIN
  FFMPEGAAC_BASE_URL="https://raw.githubusercontent.com/Mbucari/AAXClean.Codecs/refs/heads/master/src/ffmpegaac";
  download_file AacDecoder.c "$FFMPEGAAC_BASE_URL/AacDecoder.c"
  download_file AacEncoder.c "$FFMPEGAAC_BASE_URL/AacEncoder.c"
  download_file ffmpegaac.h "$FFMPEGAAC_BASE_URL/ffmpegaac.h"
  cd $THIS_DIR
fi

if ! [ -f "ffmpegaac.$LIB_EXTENSION" ]; then
  echo "Building ffmpegaac"
  cd $FFMPEGAAC_MAIN
  if [ $OS = Darwin ]; then
    gcc -fPIC -v -c AacDecoder.c -c AacEncoder.c -I$FFMPEG_MAIN -I./ 1> /dev/null;
    gcc -dynamiclib -shared -static -fPIC -Wl,-v -o ffmpegaac.dylib AacEncoder.o AacDecoder.o -L$FFMPEG_MAIN/libavutil -L$FFMPEG_MAIN/libswscale -L$FFMPEG_MAIN/libswresample -L$FFMPEG_MAIN/libavcodec -L$FFMPEG_MAIN/libavformat -L$FFMPEG_MAIN/libavfilter -L$FFMPEG_MAIN/libavdevice -L$FDK_INSTALL/lib -lc -lavfilter -lswresample -lavformat -lavcodec -lavutil -lfdk-aac -lm -framework VideoToolbox -framework CoreFoundation -framework CoreMedia -framework CoreVideo -framework CoreServices 1> /dev/null;
  elif [ $OS = Linux ]; then
    gcc -fPIC -c AacDecoder.c -c AacEncoder.c -I$FFMPEG_MAIN -I./ 1> /dev/null;
    gcc -pthread -shared -fPIC -Wl,-Bsymbolic -Wl,--no-undefined -Wl,-soname,ffmpegaac.so.2 -o ffmpegaac.so AacEncoder.o AacDecoder.o -L$FFMPEG_MAIN/libavutil -L$FFMPEG_MAIN/libswscale -L$FFMPEG_MAIN/libswresample -L$FFMPEG_MAIN/libavcodec -L$FFMPEG_MAIN/libavformat -L$FFMPEG_MAIN/libavfilter -L$FFMPEG_MAIN/libavdevice -L$FDK_INSTALL/lib -lc -lavfilter -lswresample -lavformat -lavcodec -lavutil -lfdk-aac -lm -lrt 1> /dev/null;
  else
    gcc -static -fPIC -Wno-error=incompatible-pointer-types -Wno-error=int-conversion -c AacDecoder.c -c AacEncoder.c -I$FFMPEG_MAIN -I./ 1> /dev/null;
    gcc -shared -static -fPIC -o ffmpegaac.dll AacEncoder.o AacDecoder.o -L$FFMPEG_MAIN/libavutil -L$FFMPEG_MAIN/libswscale -L$FFMPEG_MAIN/libswresample -L$FFMPEG_MAIN/libavcodec -L$FFMPEG_MAIN/libavformat -L$FFMPEG_MAIN/libavfilter -L$FFMPEG_MAIN/libavdevice -L$FDK_INSTALL/lib -lavfilter -lswresample -lavformat -lavcodec -lavutil -lfdk-aac -lbcrypt
  fi
  mv ffmpegaac.$LIB_EXTENSION $THIS_DIR/ffmpegaac.$LIB_EXTENSION
  cd $THIS_DIR
fi

LAME_NAME=lame-$LAME_VER
LAME_MAIN=$THIS_DIR/$LAME_NAME
LAME_LIB_DIR=$LAME_MAIN/libmp3lame/.libs
if [ ! -d "$LAME_MAIN" ]; then
  download_file $LAME_NAME.tar.gz "https://cfhcable.dl.sourceforge.net/project/lame/lame/$LAME_VER/$LAME_NAME.tar.gz"
  echo "Extracting $LAME_NAME.tar.gz"
  tar -xf ./$LAME_NAME.tar.gz &> /dev/null;
  rm $LAME_NAME.tar.gz &> /dev/null;
fi

if ! [ -f "$LAME_LIB_DIR/libmp3lame.a" ]; then
  if [ $ARCH = "arm64" ]; then
    LAME_ARCH=arm
  elif [ $ARCH = "x64" ]; then  
    LAME_ARCH="x86_64"
  else
    LAME_ARCH=$ARCH
  fi
  echo "Building $LAME_NAME-$LAME_ARCH"
  cd $LAME_MAIN
  ./configure --disable-decoder --disable-frontend --host="$LAME_ARCH"
  make CFLAGS='-fPIC -O3' CPPFLAGS="-DNDEBUG"
fi

cd $LAME_LIB_DIR
if [ $OS = Darwin ]; then
  gcc -dynamiclib -shared -fPIC -Wl,-v,-force_load libmp3lame.a -o libmp3lame.dylib &> /dev/null;
else
  gcc -shared -fPIC -Wl,-v -Wl,--whole-archive libmp3lame.a -o libmp3lame.$LIB_EXTENSION -Wl,--no-whole-archive -lm
fi
mv libmp3lame.$LIB_EXTENSION $THIS_DIR/libmp3lame.$LIB_EXTENSION

cd $THIS_DIR

tar -cvzf aaxclean_native_libs_${OS}_$ARCH.tar.gz libmp3lame.$LIB_EXTENSION ffmpegaac.$LIB_EXTENSION

echo "Done!"
