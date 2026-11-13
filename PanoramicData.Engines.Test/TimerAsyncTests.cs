using Xunit;

namespace PanoramicData.Engines.Test;

public class TimerAsyncTests
{
	private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(10);
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

	[Fact]
	public void Constructor_NullAction_Throws()
		=> Assert.Throws<ArgumentNullException>(() => new TimerAsync(null!, Fast, Fast));

	[Fact]
	public void Constructor_NegativeDueTime_Throws()
		=> Assert.Throws<ArgumentOutOfRangeException>(() => new TimerAsync(_ => Task.CompletedTask, TimeSpan.FromSeconds(-1), Fast));

	[Fact]
	public void Constructor_NegativePeriod_Throws()
		=> Assert.Throws<ArgumentOutOfRangeException>(() => new TimerAsync(_ => Task.CompletedTask, Fast, TimeSpan.FromSeconds(-1)));

	[Fact]
	public void ErrorEventArgs_ExposesException()
	{
		var exception = new InvalidOperationException("x");

		Assert.Same(exception, new TimerAsyncErrorEventArgs(exception).Exception);
	}

	[Fact]
	public async Task StartAndStop_RunsActionRepeatedly()
	{
		var count = 0;
		var reached = new TaskCompletionSource();
		using var timer = new TimerAsync(_ =>
		{
			if (Interlocked.Increment(ref count) >= 3)
			{
				reached.TrySetResult();
			}

			return Task.CompletedTask;
		}, TimeSpan.Zero, Fast);
		Assert.False(timer.IsRunning);

		timer.Start();
		Assert.True(timer.IsRunning);
		await reached.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
		await timer.Stop();

		Assert.False(timer.IsRunning);
	}

	[Fact]
	public async Task Start_WhenAlreadyRunning_IsNoOp()
	{
		using var timer = new TimerAsync(_ => Task.CompletedTask, TimeSpan.Zero, Fast);

		timer.Start();
		timer.Start();

		Assert.True(timer.IsRunning);
		await timer.Stop();
	}

	[Fact]
	public async Task Stop_WhenNotRunning_IsNoOp()
	{
		using var timer = new TimerAsync(_ => Task.CompletedTask, TimeSpan.Zero, Fast);

		await timer.Stop();

		Assert.False(timer.IsRunning);
	}

	[Fact]
	public async Task CanStartNextActionBeforePreviousIsCompleted_RunsActionsOverlapping()
	{
		var running = 0;
		var maxConcurrent = 0;
		var overlapped = new TaskCompletionSource();
		using var timer = new TimerAsync(async ct =>
		{
			var now = Interlocked.Increment(ref running);
			InterlockedMax(ref maxConcurrent, now);
			if (now >= 2)
			{
				overlapped.TrySetResult();
			}

			try
			{
				await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
			}
			catch (OperationCanceledException)
			{
				// Timer stopped
			}
			finally
			{
				Interlocked.Decrement(ref running);
			}
		}, TimeSpan.Zero, Fast, canStartNextActionBeforePreviousIsCompleted: true);

		timer.Start();
		await overlapped.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
		await timer.Stop();

		Assert.True(maxConcurrent >= 2);
	}

	[Fact]
	public async Task ActionThrows_RaisesOnErrorAndStopsRunning()
	{
		var raised = new TaskCompletionSource<Exception>();
		using var timer = new TimerAsync(_ => throw new InvalidOperationException("boom"), TimeSpan.Zero, Fast);
		timer.OnError += (_, e) => raised.TrySetResult(e.Exception);

		timer.Start();
		var exception = await raised.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
		await WaitUntil(() => !timer.IsRunning);

		Assert.Equal("boom", exception.Message);
		await timer.Stop();
	}

	[Fact]
	public async Task ActionThrows_WithNoSubscriber_IsSwallowed()
	{
		var invoked = new TaskCompletionSource();
		using var timer = new TimerAsync(_ =>
		{
			invoked.TrySetResult();
			throw new InvalidOperationException("boom");
		}, TimeSpan.Zero, Fast);

		timer.Start();
		await invoked.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
		await WaitUntil(() => !timer.IsRunning);

		Assert.False(timer.IsRunning);
	}

	[Fact]
	public async Task OnErrorHandlerThrows_IsSwallowed()
	{
		var handlerCalled = new TaskCompletionSource();
		using var timer = new TimerAsync(_ => throw new InvalidOperationException("boom"), TimeSpan.Zero, Fast);
		timer.OnError += (_, _) =>
		{
			handlerCalled.TrySetResult();
			throw new InvalidOperationException("handler");
		};

		timer.Start();
		await handlerCalled.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
		await WaitUntil(() => !timer.IsRunning);

		Assert.False(timer.IsRunning);
	}

	[Fact]
	public async Task Stop_DuringDueTimeDelay_CancelsWithoutError()
	{
		var errored = false;
		using var timer = new TimerAsync(_ => Task.CompletedTask, TimeSpan.FromMinutes(5), Fast);
		timer.OnError += (_, _) => errored = true;

		timer.Start();
		await timer.Stop();

		Assert.False(errored);
		Assert.False(timer.IsRunning);
	}

	[Fact]
	public async Task Stop_WhileActionAwaitingToken_CancelsWithoutError()
	{
		var errored = false;
		var started = new TaskCompletionSource();
		using var timer = new TimerAsync(async ct =>
		{
			started.TrySetResult();
			await Task.Delay(Timeout, ct);
		}, TimeSpan.Zero, Fast);
		timer.OnError += (_, _) => errored = true;

		timer.Start();
		await started.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
		await timer.Stop();

		Assert.False(errored);
	}

	[Fact]
	public async Task Dispose_WhenCalledTwice_IsSafe()
	{
		var timer = new TimerAsync(_ => Task.CompletedTask, TimeSpan.Zero, Fast);
		timer.Start();
		await timer.Stop();

		timer.Dispose();
		timer.Dispose();
	}

	[Fact]
	public void Dispose_WithoutStarting_IsSafe()
	{
		var timer = new TimerAsync(_ => Task.CompletedTask, TimeSpan.Zero, Fast);

		timer.Dispose();
	}

	[Fact]
	public void Start_AfterDispose_Throws()
	{
		var timer = new TimerAsync(_ => Task.CompletedTask, TimeSpan.Zero, Fast);
		timer.Dispose();

		Assert.Throws<ObjectDisposedException>(timer.Start);
	}

	[Fact]
	public async Task Stop_AfterDispose_Throws()
	{
		var timer = new TimerAsync(_ => Task.CompletedTask, TimeSpan.Zero, Fast);
		timer.Dispose();

		await Assert.ThrowsAsync<ObjectDisposedException>(timer.Stop);
	}

	private static void InterlockedMax(ref int location, int value)
	{
		int current;
		do
		{
			current = Volatile.Read(ref location);
			if (value <= current)
			{
				return;
			}
		}
		while (Interlocked.CompareExchange(ref location, value, current) != current);
	}

	internal static async Task WaitUntil(Func<bool> condition)
	{
		var deadline = DateTime.UtcNow + Timeout;
		while (!condition())
		{
			Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for condition");
			await Task.Delay(10, TestContext.Current.CancellationToken);
		}
	}
}
