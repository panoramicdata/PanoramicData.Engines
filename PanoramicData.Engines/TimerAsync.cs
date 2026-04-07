using System;
using System.Threading;
using System.Threading.Tasks;

namespace PanoramicData.Engines;

/// <summary>
/// Event args wrapping an exception from a scheduled action.
/// </summary>
public class TimerAsyncErrorEventArgs : EventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TimerAsyncErrorEventArgs"/> class.
	/// </summary>
	/// <param name="exception">The exception that occurred.</param>
	public TimerAsyncErrorEventArgs(Exception exception)
	{
		Exception = exception;
	}

	/// <summary>
	/// Gets the exception that occurred.
	/// </summary>
	public Exception Exception { get; }
}

/// <summary>
/// <para>
/// Async-friendly Timer implementation.
/// Provides a mechanism for executing an async method on
/// a thread pool thread at specified intervals.
/// </para>
/// <para>This class cannot be inherited.</para>
/// </summary>
public sealed class TimerAsync : IDisposable
{
	private readonly Func<CancellationToken, Task> _scheduledAction;
	private readonly TimeSpan _dueTime;
	private readonly TimeSpan _period;
	private CancellationTokenSource? _cancellationSource;
	private Task? _scheduledTask;
	private readonly SemaphoreSlim _semaphore;
	private bool _disposed;
	private readonly bool _canStartNextActionBeforePreviousIsCompleted;

	/// <summary>
	/// Occurs when an error is raised in the scheduled action
	/// </summary>
	public event EventHandler<TimerAsyncErrorEventArgs>? OnError;

	/// <summary>
	/// Gets the running status of the TimerAsync instance.
	/// </summary>
	public bool IsRunning { get; private set; }

	/// <summary>
	/// Initializes a new instance of the TimerAsync.
	/// </summary>
	/// <param name="scheduledAction">A delegate representing a method to be executed.</param>
	/// <param name="dueTime">The amount of time to delay before scheduledAction is invoked for the first time.</param>
	/// <param name="period">The time interval between invocations of the scheduledAction.</param>
	public TimerAsync(Func<CancellationToken, Task> scheduledAction, TimeSpan dueTime, TimeSpan period)
		: this(scheduledAction, dueTime, period, false)
	{

	}

	/// <summary>
	/// Initializes a new instance of the TimerAsync.
	/// </summary>
	/// <param name="scheduledAction">A delegate representing a method to be executed.</param>
	/// <param name="dueTime">The amount of time to delay before scheduledAction is invoked for the first time.</param>
	/// <param name="period">The time interval between invocations of the scheduledAction.</param>
	/// <param name="canStartNextActionBeforePreviousIsCompleted">
	///   Whether or not the interval starts at the end of the previous scheduled action or at precise points in time.
	/// </param>
	public TimerAsync(Func<CancellationToken, Task> scheduledAction, TimeSpan dueTime, TimeSpan period, bool canStartNextActionBeforePreviousIsCompleted)
	{
		_scheduledAction = scheduledAction ?? throw new ArgumentNullException(nameof(scheduledAction));

		if (dueTime < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(dueTime), "due time must be equal or greater than zero");
		}

		_dueTime = dueTime;

		if (period < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(period), "period must be equal or greater than zero");
		}

		_period = period;

		_canStartNextActionBeforePreviousIsCompleted = canStartNextActionBeforePreviousIsCompleted;

		_semaphore = new SemaphoreSlim(1);
	}


	/// <summary>
	/// Starts the TimerAsync.
	/// </summary>
	public void Start()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		_semaphore.Wait();

		try
		{
			if (IsRunning)
			{
				return;
			}

			_cancellationSource = new CancellationTokenSource();
			_scheduledTask = RunScheduledAction();
			IsRunning = true;
		}
		finally
		{
			_semaphore.Release();
		}
	}

	/// <summary>
	/// Stops the TimerAsync.
	/// </summary>
	/// <returns>A task that completes when the timer is stopped.</returns>
	public async Task Stop()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await _semaphore.WaitAsync().ConfigureAwait(false);

		try
		{
			if (!IsRunning)
			{
				return;
			}

			await _cancellationSource!.CancelAsync().ConfigureAwait(false);

			await _scheduledTask!.ConfigureAwait(false);
		}
		finally
		{
			IsRunning = false;
			_semaphore.Release();
		}
	}

	private Task RunScheduledAction() => Task.Run(async () =>
									{
										try
										{
											await Task.Delay(_dueTime, _cancellationSource!.Token).ConfigureAwait(false);

											while (true)
											{
												if (_canStartNextActionBeforePreviousIsCompleted)
												{
#pragma warning disable 4014
													_scheduledAction(_cancellationSource.Token);
#pragma warning restore 4014
												}
												else
												{
													await _scheduledAction(_cancellationSource.Token).ConfigureAwait(false);
												}

												await Task.Delay(_period, _cancellationSource.Token).ConfigureAwait(false);
											}
										}
										catch (OperationCanceledException) when (_cancellationSource!.IsCancellationRequested)
										{
											// Expected cancellation - no error to report
										}
#pragma warning disable CA1031 // Background timer must catch all exceptions to invoke the error handler
										catch (Exception ex) when (!_cancellationSource!.IsCancellationRequested)
#pragma warning restore CA1031
										{
											try
											{
												OnError?.Invoke(this, new TimerAsyncErrorEventArgs(ex));
											}
#pragma warning disable CA1031 // Error handler must not throw
											catch
											{
												// ignored
											}
#pragma warning restore CA1031
										}
										finally
										{
											IsRunning = false;
										}
									}, _cancellationSource!.Token);

	private void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		// NOTE: release unmanaged resources here

		if (disposing)
		{
			_cancellationSource?.Dispose();
			_semaphore?.Dispose();
		}

		_disposed = true;
	}

	/// <summary>
	/// Releases all resources used by the current instance of TimerAsync.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}