#!/bin/bash

FILE=$1; shift
_self="${0##*/}"

if [ ! -f "$FILE" ]
then
  echo "The file \"$FILE\" does not exist."
  exit
fi

brew_install() {
    echo -e "Installing $1"
    if brew list $1 &>/dev/null; then
        echo "${1} is already installed"
    else
        brew install $1 && echo "$1 is installed"
    fi
}

brew_install "gcc"
brew_install "make"
brew_install "perl"
brew_install "pkg-config"
brew_install "yasm"


FILENAME="$(basename -- $FILE)"
FOLDER_MAIN=${FILENAME:0:${#FILENAME}-7}
THIS_DIR=$(pwd)

echo "Extracting $FILENAME"
tar -xf ${FILE}

cd ${FOLDER_MAIN}

echo "Configuring FFMpeg"
./configure --disable-shared --enable-static --disable-doc --disable-symver --disable-programs --disable-debug --enable-pic --disable-everything --enable-decoder=aac --enable-decoder=pcm_f32le --enable-decoder=pcm_s16le --enable-encoder=aac --enable-encoder=pcm_f32le --enable-encoder=pcm_s16le --enable-parser=aac --enable-demuxer=aac --enable-demuxer=pcm_f32le --enable-demuxer=pcm_s16le --enable-muxer=pcm_f32le --enable-muxer=pcm_s16le --enable-filter=aresample --enable-filter=asetnsamples

echo "Building FFMpeg"
make

cd ${THIS_DIR}

LIBAVUTIL="./$FOLDER_MAIN/libavutil"
LIBSWSCALE="./$FOLDER_MAIN/libswscale"
LIBSWRESAMPLE="./$FOLDER_MAIN/libswresample"
LIBAVCODEC="./$FOLDER_MAIN/libavcodec"
LIBAVFORMAT="./$FOLDER_MAIN/libavformat"
LIBAVFILTER="./$FOLDER_MAIN/libavfilter"
LIBAVDEVICE="./$FOLDER_MAIN/libavdevice"

echo "Building ffmpegaac.dylib"
echo "Foldermain: [$FOLDER_MAIN]"

gcc-12 -fPIC -v -c aacDecoder.c -c aacEncoder.c -I"./$FOLDER_MAIN" -I./

gcc-12 -dynamiclib -shared -fPIC -Wl,-v -o ffmpegaac.dylib aacEncoder.o aacDecoder.o -L${LIBAVUTIL} -L${LIBSWSCALE} -L${LIBSWRESAMPLE} -L${LIBAVCODEC} -L${LIBAVFORMAT} -L${LIBAVFILTER} -L${LIBAVDEVICE} -lc -lswresample -lavformat -lavcodec -lavutil -lm -liconv -framework VideoToolbox -framework CoreFoundation -framework CoreMedia -framework CoreVideo -framework CoreServices

echo "Cleaning up...";
rm aacEncoder.o
rm aacDecoder.o
rm -r ${FOLDER_MAIN}

echo "Done!"