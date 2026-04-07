using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace PanoramicData.Engines;

/// <summary>
/// Base class for engines that can be started, stopped, and restarted.
/// </summary>
public abstract partial class Engine : IEngine
{
	private EngineState _engineState;
	private readonly ILogger<Engine>? _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="Engine"/> class.
	/// </summary>
	/// <param name="name">The engine name.</param>
	protected Engine(string name)
	{
		Name = name;
		_logger = null;
		_engineState = EngineState.Stopped;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Engine"/> class with logging support.
	/// </summary>
	/// <param name="name">The engine name.</param>
	/// <param name="loggerFactory">Optional logger factory.</param>
	protected Engine(string name, ILoggerFactory? loggerFactory)
	{
		Name = name;
		_logger = loggerFactory?.CreateLogger<Engine>();
		_engineState = EngineState.Stopped;
	}

	/// <summary>
	/// Gets the current state of the engine.
	/// </summary>
	public EngineState EngineState
	{
		get => _engineState;
		private set
		{
			_engineState = value;
			if (_logger is not null)
			{
				LogEngineStateChanged(_logger, Name, value);
			}
		}
	}

	/// <summary>
	///    The engine name
	/// </summary>
	protected string Name { get; }

	/// <summary>
	///    Called to start the engine
	/// </summary>
	public async Task StartAsync()
	{
		if (EngineState == EngineState.Stopped)
		{
			EngineState = EngineState.Starting;
		}
		else
		{
			throw new InvalidOperationException($"{Name}: Cannot start when it is {EngineState}");
		}

		await Startup().ConfigureAwait(false);
		EngineState = EngineState.Started;

	}

	/// <summary>
	///    Called to stop the engine
	/// </summary>
	public async Task StopAsync()
	{

		if (EngineState == EngineState.Started)
		{
			EngineState = EngineState.Stopping;
		}
		else
		{
			var message = $"{Name}: Cannot stop the engine when it is {EngineState}";
			if (_logger is not null)
			{
				LogEngineError(_logger, message);
			}

			throw new InvalidOperationException(message);
		}

		await Shutdown().ConfigureAwait(false);
		EngineState = EngineState.Stopped;
	}

	/// <summary>
	///    Called to restart the engine
	/// </summary>
	// ReSharper disable once MemberCanBeProtected.Global
	public async Task RestartAsync()
	{
		if (_logger is not null)
		{
			LogEngineInfo(_logger, Name, "Restarting");
		}

		await StopAsync().ConfigureAwait(false);
		await StartAsync().ConfigureAwait(false);

		if (_logger is not null)
		{
			LogEngineInfo(_logger, Name, "Restart complete");
		}
	}

	/// <summary>
	///    Called when stopping the engine
	/// </summary>
	protected abstract Task Shutdown();

	/// <summary>
	///    Called when starting the engine
	/// </summary>
	protected abstract Task Startup();

	[LoggerMessage(Level = LogLevel.Information, Message = "{EngineName}: {EngineState}")]
	private static partial void LogEngineStateChanged(ILogger logger, string engineName, EngineState engineState);

	[LoggerMessage(Level = LogLevel.Information, Message = "{EngineName}: {Message}")]
	private static partial void LogEngineInfo(ILogger logger, string engineName, string message);

	[LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
	private static partial void LogEngineError(ILogger logger, string message);
}