using Microsoft.Extensions.Logging;
using System.Reflection;
using Xunit;

namespace PanoramicData.Engines.Test;

public class TestTimerEngine : AsyncTimerEngine
{
	public TestTimerEngine(TimeSpan interval, ILoggerFactory? loggerFactory = null)
		: base("TestTimerEngine", interval, loggerFactory)
	{
	}

	public TestTimerEngine(TimeSpan interval) : base("TestTimerEngine", interval)
	{
	}

	public Func<Task> OnTick { get; set; } = () => Task.CompletedTask;
	public int PreStartupCount { get; private set; }
	public int PostShutdownCount { get; private set; }
	public long ElapsedMs => ProcessingElapsedTimeMs;

	public void DisposeManaged(bool disposing) => Dispose(disposing);

	protected override Task TimerEngineMethod() => OnTick();

	protected override Task PreStartup()
	{
		PreStartupCount++;
		return Task.CompletedTask;
	}

	protected override Task PostShutdown()
	{
		PostShutdownCount++;
		return Task.CompletedTask;
	}
}

public class AsyncTimerEngineTests
{
	private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(10);

	private static Task WaitForExecutions(TestTimerEngine engine, int count)
		=> TimerAsyncTests.WaitUntil(() => engine.ExecutionCount >= count);

	[Fact]
	public async Task StartAndStop_RunsHooksAndExecutesTimerMethod()
	{
		using var engine = new TestTimerEngine(Fast);

		await engine.StartAsync();
		await WaitForExecutions(engine, 2);
		await engine.StopAsync();

		Assert.Equal(1, engine.PreStartupCount);
		Assert.Equal(1, engine.PostShutdownCount);
		Assert.True(engine.ExecutionCount >= 2);
		Assert.Equal(EngineState.Stopped, engine.EngineState);
	}

	[Fact]
	public async Task Restart_ResetsExecutionCount()
	{
		using var engine = new TestTimerEngine(Fast);
		await engine.StartAsync();
		await WaitForExecutions(engine, 1);

		await engine.RestartAsync();

		Assert.Equal(2, engine.PreStartupCount);
		Assert.Equal(1, engine.PostShutdownCount);
		await engine.StopAsync();
	}

	[Fact]
	public async Task TimerMethod_ThrowsWithoutLogger_IsSwallowed()
	{
		using var engine = new TestTimerEngine(Fast)
		{
			OnTick = () => throw new InvalidOperationException("boom")
		};

		await engine.StartAsync();
		await WaitForExecutions(engine, 2);
		await engine.StopAsync();

		Assert.Equal(EngineState.Stopped, engine.EngineState);
	}

	[Fact]
	public async Task TimerMethod_Throws_LogsWarningAndKeepsRunning()
	{
		var factory = new RecordingLoggerFactory();
		using var engine = new TestTimerEngine(Fast, factory)
		{
			OnTick = () => throw new InvalidOperationException("boom")
		};

		await engine.StartAsync();
		await WaitForExecutions(engine, 2);
		await engine.StopAsync();

		Assert.True(factory.Logger.Contains(LogLevel.Warning, "InvalidOperationException"));
		Assert.True(factory.Logger.Contains(LogLevel.Warning, "boom"));
	}

	[Fact]
	public async Task TimerMethod_ThrowsAggregateExceptionWithLogger_LogsEachInnerException()
	{
		var factory = new RecordingLoggerFactory();
		using var engine = new TestTimerEngine(Fast, factory)
		{
			OnTick = () => throw new AggregateException(new ArgumentException("first"), new InvalidOperationException("second"))
		};

		await engine.StartAsync();
		await WaitForExecutions(engine, 1);
		await engine.StopAsync();

		Assert.True(factory.Logger.Contains(LogLevel.Warning, "ArgumentException"));
		Assert.True(factory.Logger.Contains(LogLevel.Warning, "second"));
	}

	[Fact]
	public async Task TimerMethod_ThrowsAggregateExceptionWithoutLogger_IsSwallowed()
	{
		using var engine = new TestTimerEngine(Fast)
		{
			OnTick = () => throw new AggregateException(new ArgumentException("first"))
		};

		await engine.StartAsync();
		await WaitForExecutions(engine, 1);
		await engine.StopAsync();

		Assert.Equal(EngineState.Stopped, engine.EngineState);
	}

	[Fact]
	public async Task TimerMethod_ThrowsCancellationWithoutCancelRequested_IsLogged()
	{
		var factory = new RecordingLoggerFactory();
		using var engine = new TestTimerEngine(Fast, factory)
		{
			OnTick = () => throw new OperationCanceledException("not cancelled")
		};

		await engine.StartAsync();
		await WaitForExecutions(engine, 1);
		await engine.StopAsync();

		Assert.True(factory.Logger.Contains(LogLevel.Warning, "OperationCanceledException"));
	}

	[Fact]
	public async Task StopWhileTimerMethodCancelled_DoesNotLogOrRestartTimer()
	{
		var factory = new RecordingLoggerFactory();
		var entered = new TaskCompletionSource();
		var release = new TaskCompletionSource();
		using var engine = new TestTimerEngine(TimeSpan.FromMilliseconds(10), factory)
		{
			OnTick = async () =>
			{
				entered.TrySetResult();
				await release.Task;
				throw new OperationCanceledException();
			}
		};
		await engine.StartAsync();
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

		var stopping = engine.StopAsync();
		await TimerAsyncTests.WaitUntil(() => engine.EngineState == EngineState.Stopping);
		await Task.Delay(100, TestContext.Current.CancellationToken);
		release.SetResult();
		await stopping.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

		Assert.Equal(EngineState.Stopped, engine.EngineState);
		Assert.False(factory.Logger.Contains(LogLevel.Warning, "OperationCanceledException"));
	}

	[Fact]
	public async Task TimerMethod_WhenAlreadyExecuting_Throws()
	{
		using var engine = new TestTimerEngine(Fast);
		SetExecuting(engine, true);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeTimerMethod(engine));

		Assert.Contains("already executing", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Shutdown_WaitsForTimerMethodFlagToClear()
	{
		using var engine = new TestTimerEngine(TimeSpan.FromMinutes(5));
		await engine.StartAsync();
		SetExecuting(engine, true);

		var stopping = engine.StopAsync();
		await Task.Delay(100, TestContext.Current.CancellationToken);
		Assert.False(stopping.IsCompleted);
		SetExecuting(engine, false);
		await stopping.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

		Assert.Equal(EngineState.Stopped, engine.EngineState);
		Assert.Equal(1, engine.PostShutdownCount);
	}

	[Fact]
	public void Dispose_CanBeCalledRepeatedly_AndWithoutDisposing()
	{
		var engine = new TestTimerEngine(Fast);

		engine.DisposeManaged(false);
		engine.Dispose();
		engine.Dispose();
	}

	private static void SetExecuting(AsyncTimerEngine engine, bool value)
		=> typeof(AsyncTimerEngine)
			.GetProperty("IsTimerEngineMethodExecuting", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(engine, value);

	private static Task InvokeTimerMethod(AsyncTimerEngine engine)
		=> (Task)typeof(AsyncTimerEngine)
			.GetMethod("TimerMethod", BindingFlags.Instance | BindingFlags.NonPublic)!
			.Invoke(engine, [CancellationToken.None])!;
}
