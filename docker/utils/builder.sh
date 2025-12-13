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
# FILE: builder.sh
# CREATION DATE: 13-12-2025
# LAST Modified: 16:21:54 13-12-2025
# DESCRIPTION: 
# Converts and filters AAC audio from AAXClean. Supports multiple codecs (AAC-LC, E-AC-3, HE-AAC, etc.) and platforms (Windows, macOS, Linux). Provides NuGet integration and APIs for audio conversion, silence detection, and multipart processing.
# /STOP
# COPYRIGHT: (c) AAXClean.Codecs
# PURPOSE: This is the file that is in charge of building and publishing the docker image to my docker hub account.
# // AR
# +==== END AAXClean.Codecs =================+
# 

# Define image name
DOCKER_HUB_IMAGE="hanralatalliard/aaxclean-codecs"

# Check if Docker can be run without sudo
if ! docker info &>/dev/null; then
  DOCKER_CMD="sudo docker"
else
  DOCKER_CMD="docker"
fi

# Ensure buildx is set up
if ! $DOCKER_CMD buildx inspect multiarch-builder &>/dev/null; then
  $DOCKER_CMD buildx create --name multiarch-builder --use
  $DOCKER_CMD buildx inspect --bootstrap
fi

# Get the current date in YYYY-MM-DD format
CURRENT_DATE=$(date +%Y-%m-%d)

# Build and push multi-architecture images with multiple tags
$DOCKER_CMD buildx build \
  --platform linux/amd64,linux/arm64,linux/arm/v7 \
  -t $DOCKER_HUB_IMAGE:latest \
  -t $DOCKER_HUB_IMAGE:ubuntu_18.04 \
  -t $DOCKER_HUB_IMAGE:ubuntu_18.04-$CURRENT_DATE \
  -f ./Dockerfile.linux \
  --push .
