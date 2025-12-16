using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AAXClean.Codecs.Interop;

public enum LogLevel
{
    Quiet  = -8,
    Panic =  0,
    Fatal =  8,
    Error  = 16,
    Warning = 24,
    Info = 32,
    Verbose = 40,
    Debug = 48,
    Trace = 56
}

public delegate void LogEventHandler(LogLevel level, string message);
public static unsafe class NativeLogging
{
    private static LogEventHandler? _messageLogged;
    public static event LogEventHandler? MessageLogged
    {
        add
        {
            _messageLogged += value;
            SetLogCallback(&MessageLogger);
        }
        remove
        {
            _messageLogged -= value;
            if (_messageLogged == null)
                SetLogCallback(null);
        }
    }

    [UnmanagedCallersOnly]
    private static void MessageLogger(int code, byte* pMessage, int messageSize)
    {
        if (_messageLogged is not null)
        {
            var message = Encoding.UTF8.GetString(pMessage,  messageSize - 1);
            _messageLogged((LogLevel)code, message);
        }
    }
    
    private const string libname = "aaxcleannative";
    [DllImport(libname, CallingConvention = CallingConvention.StdCall)]
    private static extern void SetLogCallback(delegate* unmanaged<int,byte*,int,void> messageDelegate);
}