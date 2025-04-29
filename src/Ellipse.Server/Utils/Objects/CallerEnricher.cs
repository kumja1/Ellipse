using System.Diagnostics;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace Ellipse.Server.Utils.Objects;

public sealed class CallerEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        StackTrace trace = new StackTrace();
        StackFrame? frame = trace.GetFrame(2);
        MethodBase? method = frame?.GetMethod();

        if (method == null)
            return;

        logEvent.AddOrUpdateProperty(factory.CreateProperty("Caller", method.Name));
    }
}
