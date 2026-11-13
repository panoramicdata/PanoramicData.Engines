using Microsoft.Extensions.Logging;

namespace PanoramicData.Engines.Test;

public sealed class RecordingLogger : ILogger
{
	public List<(LogLevel Level, string Message)> Entries { get; } = [];

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		lock (Entries)
		{
			Entries.Add((logLevel, formatter(state, exception)));
		}
	}

	public bool Contains(LogLevel level, string text)
	{
		lock (Entries)
		{
			return Entries.Any(e => e.Level == level && e.Message.Contains(text, StringComparison.Ordinal));
		}
	}
}

public sealed class RecordingLoggerFactory : ILoggerFactory
{
	public RecordingLogger Logger { get; } = new();

	public void AddProvider(ILoggerProvider provider)
	{
		// Providers are not used
	}

	public ILogger CreateLogger(string categoryName) => Logger;

	public void Dispose()
	{
		// Nothing to release
	}
}
