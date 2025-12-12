cmake_minimum_required(VERSION 4.1)

file(GLOB FFMPEG_CONFIGS LIST_DIRECTORIES true "${CMAKE_CURRENT_SOURCE_DIR}/../ffmpeg-*/config.h")
if (!FFMPEG_CONFIGS)
    message(FATAL_ERROR "COUND NOT FIND FFMPEG BUILD DIRECTORY")
endif()

list(POP_BACK FFMPEG_CONFIGS c)
get_filename_component(FFMPEG_BUILD_DIR ${c} DIRECTORY)
message(STATUS "Using found FFMPEG directory: ${FFMPEG_BUILD_DIR}")

find_library(LIB_AVFILTER
        NAMES avfilter
        HINTS "${FFMPEG_BUILD_DIR}/libavfilter"
)
find_library(LIB_SWRESAMPLE
        NAMES swresample
        HINTS "${FFMPEG_BUILD_DIR}/libswresample"
)
find_library(LIB_AVFORMAT
        NAMES avformat
        HINTS "${FFMPEG_BUILD_DIR}/libavformat"
)
find_library(LIB_AVCODEC
        NAMES avcodec
        HINTS "${FFMPEG_BUILD_DIR}/libavcodec"
)
find_library(LIB_AVUTIL
        NAMES avutil
        HINTS "${FFMPEG_BUILD_DIR}/libavutil"
)
find_library(LIB_FDK_AAC
        NAMES fdk-aac
        HINTS "../libfdk/lib"
)

if (!LIB_AVFILTER)
    message(FATAL_ERROR "COUND NOT FIND LIBAVFILTER")
elseif (!LIB_SWRESAMPLE)
    message(FATAL_ERROR "COUND NOT FIND LIBSWRESAMPLE")
elseif (!LIB_AVFORMAT)
    message(FATAL_ERROR "COUND NOT FIND LIBAVFORMAT")
elseif (!LIB_AVCODEC)
    message(FATAL_ERROR "COUND NOT FIND LIBAVCODEC")
elseif (!LIB_AVUTIL)
    message(FATAL_ERROR "COUND NOT FIND LIBAVUTIL")
elseif (!LIB_FDK_AAC)
    message(FATAL_ERROR "COUND NOT FIND LIBFDK-AAC")
endif()

project(ffmpegaac C)

set(CMAKE_C_STANDARD 17)
set(CMAKE_THREAD_PREFER_PTHREAD TRUE)
set(THREADS_PREFER_PTHREAD_FLAG TRUE)
find_package(Threads REQUIRED)
add_library(ffmpegaac SHARED
        AacDecoder.c
        AacEncoder.c
)

target_include_directories(ffmpegaac PRIVATE
        ${FFMPEG_BUILD_DIR}
)

set_target_properties(ffmpegaac PROPERTIES POSITION_INDEPENDENT_CODE TRUE)

set (CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} -Wl,-Bsymbolic")
target_link_libraries(ffmpegaac Threads::Threads c ${LIB_AVFILTER} ${LIB_SWRESAMPLE} ${LIB_AVFORMAT} ${LIB_AVCODEC} ${LIB_AVUTIL} ${LIB_FDK_AAC} m rt)
