using UnityEngine;

namespace LegacyTextureLoader;

public class Logger
{
    private static readonly string ModName = "LegacyTextureLoader";

    public static void Log(object message)
    {
        Debug.Log($"[{ModName}] [INFO]: {message.ToString()}");
    }

    public static void LogWarn(object obj)
    {
        Debug.LogWarning($"[{ModName}] [WARN]: {obj}");
    }

    public static void LogError(object message)
    {
        Debug.LogError($"[{ModName}] [ERROR]: {message.ToString()}");
    }
}