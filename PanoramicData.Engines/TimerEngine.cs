using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PanoramicData.Engines
{
	public abstract class AsyncTimerEngine : Engine, IDisposable
	{
		private readonly TimerAsync _timer;
		private readonly ILogger _logger;
		public int ExecutionCount { get; private set; }
		protected long ProcessingElapsedTimeMs { private get; set; }

		/// <summary>
		/// A timer based engine
		/// </summary>
		/// <param name="name">Engine name</param>
		/// <param name="interval">The delay after execution completes before the next execution is attempted.</param>
		protected AsyncTimerEngine(string name, TimeSpan interval)
			: this(name, interval, default)
		{

		}

		protected AsyncTimerEngine(string name, TimeSpan interval, ILoggerFactory loggerFactory)
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

		protected sealed override async Task Startup()
		{
			// Do pre-startup in derived classes
			await PreStartup().ConfigureAwait(false);

			ExecutionCount = 0;
			_timer.Start();
		}

		protected sealed override async Task Shutdown()
		{
			await _timer.Stop().ConfigureAwait(false);
			while (IsTimerEngineMethodExecuting)
			{
				Thread.Sleep(1000);
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
		/// Starts the engine
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async Task TimerMethod(CancellationToken cancellationToken)
		{
			if (IsTimerEngineMethodExecuting)
			{
				throw new InvalidOperationException(PrettyPrint("Can't execute engine method as it's already executing."));
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
					_logger?.LogWarning(PrettyPrint($"Exception (from aggregate exception) of type {ex.GetType()} thrown in timer method: {ex}"));
				}
			}
			catch (Exception ex)
			{
				_logger?.LogWarning(PrettyPrint($"Exception of type {ex.GetType()} thrown in timer method: {ex}"));
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

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (!disposing)
			{
				return;
			}

			_timer?.Dispose();
		}
	}
}