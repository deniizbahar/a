using Serilog.Events;

public enum ApplicationType
{
    mywebapipool,
    MockService,
    ControlService
}

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

public static class LogLevelConverter
{
    public static LogEventLevel ToSerilogLevel(LogLevel logLevel)
    {
        switch (logLevel)
        {
            case LogLevel.Trace:
                return LogEventLevel.Verbose;
            case LogLevel.Debug:
                return LogEventLevel.Debug;
            case LogLevel.Information:
                return LogEventLevel.Information;
            case LogLevel.Warning:
                return LogEventLevel.Warning;
            case LogLevel.Error:
                return LogEventLevel.Error;
            case LogLevel.Critical:
                return LogEventLevel.Fatal;
            default:
                return LogEventLevel.Information;
        }
    }
}

public class ServiceConfigDto
{
    public ApplicationType Application { get; set; }
    public LogLevel LogLevel { get; set; }
    public int? MonitoringFrequency { get; set; } // ControlService için null olabilir
}
