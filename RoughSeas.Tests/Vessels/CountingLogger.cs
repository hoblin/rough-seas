using Vintagestory.API.Common;

namespace RoughSeas.Tests.Vessels;

internal class CountingLogger : LoggerBase
{
    public int Warnings { get; private set; }

    protected override void LogImpl(EnumLogType logType, string format, params object[] args)
    {
        if (logType == EnumLogType.Warning) Warnings++;
    }
}
