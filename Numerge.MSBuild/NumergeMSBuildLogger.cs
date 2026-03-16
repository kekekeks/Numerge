using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Numerge.MSBuild;

public class NumergeMSBuildLogger(TaskLoggingHelper log) : INumergeLogger
{
    public void Log(NumergeLogLevel level, string message)
    {
        switch (level)
        {
            case NumergeLogLevel.Info:
                log.LogMessage(MessageImportance.Low, message);
                break;
            case NumergeLogLevel.Warning:
                log.LogWarning(message);
                break;
            case NumergeLogLevel.Error:
                log.LogError(message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }
    }
}