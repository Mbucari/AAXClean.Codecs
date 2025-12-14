@echo off
@REM 
@REM +==== BEGIN AAXClean.Codecs =================+
@REM LOGO: 
@REM +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
@REM |A|A|X|C|l|e|a|n|.|C|o|d|e|c|s|
@REM +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
@REM
@REM name: Bubble
@REM source https://patorjk.com/software/
@REM taag/#p=display&f=Digital&
@REM t=AAXClean.Codecs&x=none&v=4&h=4
@REM &w=80&we=false
@REM /STOP
@REM PROJECT: AAXClean.Codecs
@REM FILE: rebuild_from_scratch.bat
@REM CREATION DATE: 26-11-2025
@REM LAST Modified: 10:3:21 14-12-2025
@REM DESCRIPTION: 
@REM Converts and filters AAC audio from AAXClean. Supports multiple codecs (AAC-LC, E-AC-3, HE-AAC, etc.) and platforms (Windows, macOS, Linux). Provides NuGet integration and APIs for audio conversion, silence detection, and multipart processing.
@REM /STOP
@REM COPYRIGHT: (c) AAXClean.Codecs
@REM PURPOSE: Complete Docker environment rebuild script - stops containers, removes all volumes and cached data, then rebuilds from scratch (Windows version)
@REM // AR
@REM +==== END AAXClean.Codecs =================+
@REM 
@REM rebuild_from_scratch.bat - Windows batch equivalent of rebuild_from_scratch.sh
@REM /**
@REM  * @file rebuild_from_scratch.bat
@REM  * @brief Nuclear option: Complete Docker stack rebuild with full cleanup (Windows).
@REM  *
@REM  * This script performs a complete teardown and rebuild of the Docker environment:
@REM  *   1. Stops all running containers
@REM  *   2. Runs docker system prune to remove all unused containers, networks, images, and volumes
@REM  *   3. Forcefully removes ALL Docker volumes (including those not managed by this project)
@REM  *   4. Rebuilds the Docker image from the Dockerfile
@REM  *   5. Optionally runs the rebuilt Docker container
@REM  *
@REM  * WARNING: This script is destructive and will delete ALL Docker volumes on your system,
@REM  * not just those related to this project. Use with extreme caution.
@REM  *
@REM  * Usage:
@REM  *   docker\utils\rebuild_from_scratch.bat
@REM  *
@REM  * Notes:
@REM  *  - This script should be run from the repository root
@REM  *  - Administrative privileges may be required depending on Docker setup
@REM  *  - This is useful when the Docker environment is corrupted or you need a clean slate
@REM  *  - Consider using start_compose.bat for normal operations instead
@REM  */

SETLOCAL ENABLEDELAYEDEXPANSION

SET DOCKERFILE_PATH=docker/Dockerfile.linux
SET IMAGE_NAME=hanralatalliard/aaxclean-codecs
SET CONTAINER_NAME=aaxclean-codecs-container

echo Checking Docker availability...
where docker >nul 2>&1
IF ERRORLEVEL 1 (
  echo Error: docker is not installed or not in PATH.
  EXIT /B 1
)

IF NOT EXIST "docker" (

IF NOT EXIST "%DOCKERFILE_PATH%" (
  echo Error: %DOCKERFILE_PATH% not found. Please ensure you are running this script from the correct directory.
  EXIT /B 1
)

echo.
echo WARNING: This script will DELETE ALL Docker volumes on your system!
echo This is a destructive operation that cannot be undone.
echo.
SET /P CONFIRM="Are you sure you want to continue? (yes/no): "
IF /I NOT "%CONFIRM%"=="yes" (
  echo Operation cancelled.
  EXIT /B 0
)

echo.
echo Stopping any running Docker containers...
FOR /F "tokens=*" %%C IN ('docker ps -q') DO (
  echo Stopping container: %%C
  docker stop %%C >nul 2>&1
)

echo.
echo Cleaning up Docker resources...
echo Running system prune (this may take a while)...
docker system prune -fa --volumes

echo.
echo Removing all Docker volumes...
FOR /F "tokens=*" %%V IN ('docker volume ls -q') DO (
  echo Removing volume: %%V
  docker volume rm -f "%%V" 2>nul
)

echo.
echo Building Docker image from specified Dockerfile...
docker build -f "%DOCKERFILE_PATH%" -t "%IMAGE_NAME%" .

echo.
echo Docker image build complete.
echo.
SET /P START_NOW="Do you wish to run the Docker container now? (y/n): "
IF /I "%START_NOW%"=="y" (
  echo Running Docker container...
  docker run -it --name "%CONTAINER_NAME%" "%IMAGE_NAME%" /bin/bash
) ELSE (
  echo Docker image build complete. You can run it later with 'docker run -it --name "%CONTAINER_NAME%" "%IMAGE_NAME%" /bin/bash'.
)

ENDLOCAL
