#!/bin/bash

_self="${0##*/}"
FILE=$1; shift
HOST=$1; shift

validHosts=("armv6" "armv7" "armv7s" "arm64" "arm64e" "arm64_32" "i386" "x86_64" "x86_64h" "armv6m" "armv7k" "armv7m" "armv7em")

print_hosts() {
    echo "valid hosts are:"	
	for item in ${validHosts[@]}
	do
		echo "    $item"
	done
}


if [ ! -f ${FILE} ]
then
  echo "The file \"$FILE\" does not exist."
  exit
fi

HOST_IN_LIST=0
for item in ${validHosts[@]}
do
	if [[ ${item} == ${HOST} ]]; then
		HOST_IN_LIST=1
		break;
	fi
done

if [[ ${HOST_IN_LIST} == 0 ]]
then
  echo "This script must be called with the target platform."
  print_hosts
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
brew_install "nasm"

FILENAME="$(basename -- $FILE)"
FOLDER_MAIN=${FILENAME:0:${#FILENAME}-7}
THIS_DIR=$(pwd)

echo "Extracting $FILENAME"
tar -xf ${FILE}

cd ${FOLDER_MAIN}

echo "Configuring LAME"
./configure --enable-nasm --disable-frontend --host="$HOST"

echo "Building LAME"
make CFLAGS='-fPIC -O3' CPPFLAGS="-DNDEBUG"

cd "libmp3lame/.libs"

echo "Building libmp3lame.dynlib"
gcc-12 -dynamiclib -shared -fPIC -Wl,-v,-force_load libmp3lame.a -o libmp3lame.dylib
mv libmp3lame.dylib ${THIS_DIR}/libmp3lame.dylib

cd ${THIS_DIR}

echo "Cleaning up...";
rm -r ${FOLDER_MAIN}

echo "Done!"