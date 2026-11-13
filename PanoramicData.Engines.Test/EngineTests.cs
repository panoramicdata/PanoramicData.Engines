using Xunit;

namespace PanoramicData.Engines.Test;

public class TestEngine : Engine
{
	public TestEngine() : base("TestEngine")
	{
	}

	public TestEngine(Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory) : base("TestEngine", loggerFactory)
	{
	}

	public int StartCount { get; private set; }
	public int StopCount { get; private set; }

	protected override Task Startup()
	{
		StartCount++;
		return Task.CompletedTask;
	}

	protected override Task Shutdown()
	{
		StopCount++;
		return Task.CompletedTask;
	}
}

public class EngineTests
{
	[Fact]
	public async Task StartAsync_SetsStateToStarted()
	{
		var engine = new TestEngine();
		Assert.Equal(EngineState.Stopped, engine.EngineState);

		await engine.StartAsync();

		Assert.Equal(EngineState.Started, engine.EngineState);
		Assert.Equal(1, engine.StartCount);
	}

	[Fact]
	public async Task StopAsync_SetsStateToStopped()
	{
		var engine = new TestEngine();
		await engine.StartAsync();

		await engine.StopAsync();

		Assert.Equal(EngineState.Stopped, engine.EngineState);
		Assert.Equal(1, engine.StopCount);
	}

	[Fact]
	public async Task RestartAsync_StopsAndStarts()
	{
		var engine = new TestEngine();
		await engine.StartAsync();

		await engine.RestartAsync();

		Assert.Equal(EngineState.Started, engine.EngineState);
		Assert.Equal(2, engine.StartCount);
		Assert.Equal(1, engine.StopCount);
	}

	[Fact]
	public async Task StartAsync_WhenAlreadyStarted_Throws()
	{
		var engine = new TestEngine();
		await engine.StartAsync();

		await Assert.ThrowsAsync<InvalidOperationException>(engine.StartAsync);
	}

	[Fact]
	public async Task StopAsync_WhenAlreadyStopped_Throws()
	{
		var engine = new TestEngine();

		await Assert.ThrowsAsync<InvalidOperationException>(engine.StopAsync);
	}

	[Fact]
	public async Task RestartAsync_WhenStopped_Throws()
	{
		var engine = new TestEngine();

		await Assert.ThrowsAsync<InvalidOperationException>(engine.RestartAsync);
	}

	[Fact]
	public async Task StartAsync_WithNullLoggerFactory_Works()
	{
		var engine = new TestEngine(null);

		await engine.StartAsync();
		await engine.RestartAsync();
		await engine.StopAsync();

		Assert.Equal(EngineState.Stopped, engine.EngineState);
	}

	[Fact]
	public async Task Lifecycle_WithLogger_LogsStateChangesAndRestart()
	{
		var factory = new RecordingLoggerFactory();
		var engine = new TestEngine(factory);

		await engine.StartAsync();
		await engine.RestartAsync();
		await engine.StopAsync();

		Assert.True(factory.Logger.Contains(Microsoft.Extensions.Logging.LogLevel.Information, "TestEngine: Starting"));
		Assert.True(factory.Logger.Contains(Microsoft.Extensions.Logging.LogLevel.Information, "TestEngine: Started"));
		Assert.True(factory.Logger.Contains(Microsoft.Extensions.Logging.LogLevel.Information, "TestEngine: Stopping"));
		Assert.True(factory.Logger.Contains(Microsoft.Extensions.Logging.LogLevel.Information, "TestEngine: Stopped"));
		Assert.True(factory.Logger.Contains(Microsoft.Extensions.Logging.LogLevel.Information, "TestEngine: Restarting"));
		Assert.True(factory.Logger.Contains(Microsoft.Extensions.Logging.LogLevel.Information, "TestEngine: Restart complete"));
	}

	[Fact]
	public async Task StopAsync_WhenStoppedWithLogger_LogsError()
	{
		var factory = new RecordingLoggerFactory();
		var engine = new TestEngine(factory);

		await Assert.ThrowsAsync<InvalidOperationException>(engine.StopAsync);

		Assert.True(factory.Logger.Contains(Microsoft.Extensions.Logging.LogLevel.Error, "Cannot stop the engine when it is Stopped"));
	}
}

public class EngineExceptionTests
{
	[Fact]
	public void DefaultConstructor_CreatesException()
	{
		var exception = new EngineException();

		Assert.NotNull(exception.Message);
		Assert.Null(exception.InnerException);
	}

	[Fact]
	public void MessageConstructor_SetsMessage()
	{
		var exception = new EngineException("boom");

		Assert.Equal("boom", exception.Message);
	}

	[Fact]
	public void MessageAndInnerConstructor_SetsBoth()
	{
		var inner = new InvalidOperationException("inner");

		var exception = new EngineException("boom", inner);

		Assert.Equal("boom", exception.Message);
		Assert.Same(inner, exception.InnerException);
	}
}

public class EngineStateTests
{
	[Theory]
	[InlineData(EngineState.Unknown, 0)]
	[InlineData(EngineState.Starting, 1)]
	[InlineData(EngineState.Started, 2)]
	[InlineData(EngineState.Stopping, 3)]
	[InlineData(EngineState.Stopped, 4)]
	public void Values_HaveStableNumbers(EngineState state, int expected)
		=> Assert.Equal(expected, (int)state);
}
