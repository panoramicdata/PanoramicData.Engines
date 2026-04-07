using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PanoramicData.Engines;

/// <summary>
/// An engine that executes work on a timer interval.
/// </summary>
public abstract partial class AsyncTimerEngine : Engine, IDisposable
{
	private readonly TimerAsync _timer;
	private readonly ILogger? _logger;

	/// <summary>
	/// Gets the number of times the timer method has executed.
	/// </summary>
	public int ExecutionCount { get; private set; }

	/// <summary>
	/// Gets or sets the elapsed processing time in milliseconds.
	/// </summary>
	protected long ProcessingElapsedTimeMs { get; set; }

	/// <summary>
	/// A timer based engine
	/// </summary>
	/// <param name="name">Engine name</param>
	/// <param name="interval">The delay after execution completes before the next execution is attempted.</param>
	protected AsyncTimerEngine(string name, TimeSpan interval)
		: this(name, interval, default(ILoggerFactory?))
	{

	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncTimerEngine"/> class with logging support.
	/// </summary>
	/// <param name="name">Engine name.</param>
	/// <param name="interval">The delay after execution completes before the next execution is attempted.</param>
	/// <param name="loggerFactory">Optional logger factory.</param>
	protected AsyncTimerEngine(string name, TimeSpan interval, ILoggerFactory? loggerFactory)
		: base(name, loggerFactory)
	{
		_timer = new TimerAsync(TimerMethod, TimeSpan.FromSeconds(1), interval, false);
		_logger = loggerFactory?.CreateLogger<AsyncTimerEngine>();
		ProcessingElapsedTimeMs = 0;
	}


	/// <summary>
	/// Whether the TimerEngineMethod is executing
	/// </summary>
	private bool IsTimerEngineMethodExecuting { get; set; }

	/// <inheritdoc />
	protected sealed override async Task Startup()
	{
		// Do pre-startup in derived classes
		await PreStartup().ConfigureAwait(false);

		ExecutionCount = 0;
		_timer.Start();
	}

	/// <inheritdoc />
	protected sealed override async Task Shutdown()
	{
		await _timer.Stop().ConfigureAwait(false);
		while (IsTimerEngineMethodExecuting)
		{
			await Task.Delay(1000).ConfigureAwait(false);
		}

		// Do post-shutdown in derived classes
		await PostShutdown().ConfigureAwait(false);
	}

	/// <summary>
	/// This is the engine method which does the work when the timer fires.
	/// </summary>
	protected abstract Task TimerEngineMethod();

	/// <summary>
	/// This can be used for any startup in inheriting classes
	/// </summary>
	protected abstract Task PreStartup();

	/// <summary>
	/// This can be used for any startup in inheriting classes
	/// </summary>
	protected abstract Task PostShutdown();

	/// <summary>
	/// Timer callback that invokes the engine method.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task TimerMethod(CancellationToken cancellationToken)
	{
		if (IsTimerEngineMethodExecuting)
		{
			throw new InvalidOperationException($"{Name}: Can't execute engine method as it's already executing.");
		}

		IsTimerEngineMethodExecuting = true;

		try
		{
			ExecutionCount++;
			ProcessingElapsedTimeMs = 0;
			await TimerEngineMethod().ConfigureAwait(false);
		}
		catch (AggregateException aggregateException)
		{
			foreach (var ex in aggregateException.Flatten().InnerExceptions)
			{
				if (_logger is not null)
				{
					LogTimerWarning(_logger, Name, ex.GetType().ToString(), ex.ToString());
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Expected during shutdown — do not log
		}
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
		{
			if (_logger is not null)
			{
				LogTimerWarning(_logger, Name, ex.GetType().ToString(), ex.ToString());
			}
		}
		finally
		{
			IsTimerEngineMethodExecuting = false;
			if (EngineState != EngineState.Stopping)
			{
				_timer.Start();
			}
		}
	}

	/// <summary>
	/// Releases all resources used by the engine.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases resources used by the engine.
	/// </summary>
	/// <param name="disposing">True to release managed resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!disposing)
		{
			return;
		}

		_timer?.Dispose();
	}

	[LoggerMessage(Level = LogLevel.Warning, Message = "{EngineName}: Exception of type {ExceptionType} thrown in timer method: {ExceptionDetails}")]
	private static partial void LogTimerWarning(ILogger logger, string engineName, string exceptionType, string exceptionDetails);
}