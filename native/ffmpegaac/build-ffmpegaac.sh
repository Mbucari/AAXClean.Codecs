#!/bin/bash

vercomp () {
    if [[ $1 == $2 ]]
    then
        return 0
    fi
    local IFS=.
    local i ver1=($1) ver2=($2)
    # fill empty fields in ver1 with zeros
    for ((i=${#ver1[@]}; i<${#ver2[@]}; i++))
    do
        ver1[i]=0
    done
    for ((i=0; i<${#ver1[@]}; i++))
    do
        if [[ -z ${ver2[i]} ]]
        then
            # fill empty fields in ver2 with zeros
            ver2[i]=0
        fi
        if ((10#${ver1[i]} > 10#${ver2[i]}))
        then
            return 1
        fi
        if ((10#${ver1[i]} < 10#${ver2[i]}))
        then
            return 2
        fi
    done
    return 0
}

echo "Installing dependencies"

if ! command -v apt-get &> /dev/null
then
	sudo dnf install gcc make perl pkg-config yasm
else
	sudo apt-get install gcc make perl pkg-config yasm
fi

THIS_DIR=$(pwd)
FFMPEG_VER="5.1.3"
NASM_VER=$(nasm -v | sed -rn "s/NASM version ([0-9]+\.[0-9]+).*$/\1/p")

vercomp $NASM_VER "2.12"
if [[ $? == 1 ]]
then

 echo "NASM version ${NASM_VER} already installed"

else
  
  NASM_VER="2.16.01"
  echo "Download, Make, Install nasm-${NASM_VER}"

  wget -O nasm-${NASM_VER}.tar.bz2 --no-check-certificate https://www.nasm.us/pub/nasm/releasebuilds/${NASM_VER}/nasm-${NASM_VER}.tar.bz2
  tar -xf nasm-${NASM_VER}.tar.bz2
  cd nasm-${NASM_VER}
  ./configure
  make
  sudo make install
  cd ${THIS_DIR}
  rm -r nasm-${NASM_VER}
  rm nasm-${NASM_VER}.tar.bz2

fi

echo "Download and build ffmpeg-${FFMPEG_VER}"

wget -O ffmpeg-${FFMPEG_VER}.tar.xz --no-check-certificate https://ffmpeg.org/releases/ffmpeg-${FFMPEG_VER}.tar.xz
tar -xf ./ffmpeg-${FFMPEG_VER}.tar.xz
FOLDER_MAIN=./ffmpeg-${FFMPEG_VER}
cd ${FOLDER_MAIN}
./configure --disable-swscale --disable-avdevice --disable-doc --disable-v4l2-m2m --disable-vaapi --disable-vdpau --disable-network --disable-libxcb --disable-libxcb-xfixes --disable-libxcb-shape --disable-zlib --disable-iconv --disable-alsa --disable-shared --enable-static --disable-doc --disable-symver --disable-programs --disable-debug --enable-pic --disable-everything --enable-decoder=aac --enable-decoder=pcm_f32le --enable-decoder=pcm_s16le --enable-encoder=aac --enable-encoder=pcm_f32le --enable-encoder=pcm_s16le --enable-parser=aac --enable-demuxer=aac --enable-demuxer=pcm_f32le --enable-demuxer=pcm_s16le --enable-muxer=pcm_f32le --enable-muxer=pcm_s16le --enable-filter=aresample --enable-filter=asetnsamples
make
cd ${THIS_DIR}
rm ffmpeg-${FFMPEG_VER}.tar.xz


echo "Build ffmpegaac"

gcc -fPIC -c aacDecoder.c -c aacEncoder.c -I${FOLDER_MAIN} -I./
gcc -pthread -shared -fPIC -Wl,-Bsymbolic -Wl,-soname,ffmpegaac.so.1 -o ffmpegaac.so aacEncoder.o aacDecoder.o -L${FOLDER_MAIN}/libavutil -L${FOLDER_MAIN}/libswscale -L${FOLDER_MAIN}/libswresample -L${FOLDER_MAIN}/libavcodec -L${FOLDER_MAIN}/libavformat -L${FOLDER_MAIN}/libavfilter -L${FOLDER_MAIN}/libavdevice -lc -lavfilter -lswresample -lavformat -lavcodec -lavutil -lm -lrt


rm -r ffmpeg-${FFMPEG_VER}
rm aacEncoder.o
rm aacDecoder.o

ldd -v ffmpegaac.so

echo "Done building ffmpegaac.so"
