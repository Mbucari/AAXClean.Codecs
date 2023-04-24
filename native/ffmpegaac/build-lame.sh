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

echo "Download and build lame-3.100"

wget -O lame-3.100.tar.gz --no-check-certificate https://cfhcable.dl.sourceforge.net/project/lame/lame/3.100/lame-3.100.tar.gz
tar -xf lame-3.100.tar.gz
cd lame-3.100
./configure --enable-nasm --disable-frontend --host="arm"
make CFLAGS='-fPIC -O3' CPPFLAGS="-DNDEBUG"
cd libmp3lame/.libs
gcc -shared -fPIC -Wl,-v -Wl,--whole-archive libmp3lame.a -o libmp3lame.so  -Wl,--no-whole-archive -lm
mv libmp3lame.so ${THIS_DIR}
cd ${THIS_DIR}

rm -r lame-3.100
rm lame-3.100.tar.gz

ldd -v libmp3lame.so

echo "Done building libmp3lame.so"
