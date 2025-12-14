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
# FILE: rebuild_from_scratch.bat
# CREATION DATE: 16-10-2025
# LAST Modified: 9:58:47 14-12-2025
# DESCRIPTION: 
# Converts and filters AAC audio from AAXClean. Supports multiple codecs (AAC-LC, E-AC-3, HE-AAC, etc.) and platforms (Windows, macOS, Linux). Provides NuGet integration and APIs for audio conversion, silence detection, and multipart processing.
# /STOP
# COPYRIGHT: (c) AAXClean.Codecs
# PURPOSE: Complete Docker environment rebuild script - stops containers, removes all volumes and cached data, then rebuilds from scratch (Windows version)
# // AR
# +==== END AAXClean.Codecs =================+
# 
#
# @file rebuild_from_scratch.sh
# @brief Nuclear option: Complete Docker rebuild with full cleanup (Linux/macOS).
#
# This script performs a complete teardown and rebuild of the Docker environment:
#   1. Stops all running containers
#   2. Runs docker system prune to remove all unused containers, networks, images, and volumes
#   3. Forcefully removes ALL Docker volumes (including those not managed by this project)
#   4. Rebuilds the Docker image from the Dockerfile
#   5. Optionally runs the Docker container
#
# WARNING: This script is destructive and will delete ALL Docker volumes on your system,
# not just those related to this project. Use with extreme caution.
#
# @author Cat Feeder
# @date 2025-10-16
#
# Usage:
#   ./docker/utils/rebuild_from_scratch.sh
#
# Notes:
#  - This script should be run from the repository root
#  - It will try to run Docker without sudo first, falling back to sudo if needed
#  - The Dockerfile is expected at `./docker/Dockerfile.linux`
#  - This is useful when the Docker environment is corrupted or you need a clean slate
#  - Consider using start_compose.sh for normal operations instead

set -euo pipefail

DOCKERFILE="docker/Dockerfile.linux"
IMAGE_NAME="hanralatalliard/aaxclean-codecs"
CONTAINER_NAME="aaxclean-codecs-container"

echo "Checking docker availability..."

if ! command -v docker > /dev/null 2>&1; then
  echo "Error: docker is not installed or not in PATH." >&2
  exit 1
fi

if ! check_docker_no_sudo; then
	if command -v sudo > /dev/null 2>&1 && sudo -n true 2>/dev/null; then
		if sudo docker info > /dev/null 2>&1; then
			SUDO="sudo "
			echo "Using sudo for docker commands"
		fi
	else
		if command -v sudo > /dev/null 2>&1 && sudo docker info > /dev/null 2>&1; then
			SUDO="sudo "
			echo "Using sudo for docker commands (password may be prompted)"
		else
			echo "Warning: current user cannot access docker daemon and sudo docker failed or is unavailable." >&2
			echo "You may need to add your user to the 'docker' group or run this script with appropriate privileges." >&2
		fi
	fi
fi

COMPOSE_FOLDER="./docker"
if [ ! -d "$COMPOSE_FOLDER" ]; then
	echo "Error: $COMPOSE_FOLDER folder not found. Please ensure you are running this script from the correct directory." >&2
	exit 1
fi

echo "Stopping any existing docker containers"
eval ${SUDO} docker stop $(eval ${SUDO} docker ps -q) || true
echo "Cleaning up any docker ressources"
echo "Running a system prune"
eval ${SUDO} docker system prune -fa --volumes
echo "Removing any volumes"
ALL_CONTAINERS_VOLUMES=$(eval ${SUDO} docker volume ls -q)
echo "Located volumes: $ALL_CONTAINERS_VOLUMES"
echo "Removing volumes..."
for volume in $ALL_CONTAINERS_VOLUMES; do
    echo "Removing volume: $volume"
    eval ${SUDO} docker volume rm -f "$volume" 
done
echo "Building docker compose"
eval ${SUDO} docker build -f ${DOCKERFILE} -t ${IMAGE_NAME} .
echo "Do you wish to start the docker container now? (y/n)"
read -r START_NOW
if [[ "$START_NOW" == "y" || "$START_NOW" == "Y" ]]; then
    echo "Starting docker compose"
    eval ${SUDO} docker run -it --name ${CONTAINER_NAME} ${IMAGE_NAME} /bin/bash
else
    echo "Docker compose build complete. You can start it later with the '${SUDO} docker run -it --name ${CONTAINER_NAME} ${IMAGE_NAME} /bin/bash' command."
fi
