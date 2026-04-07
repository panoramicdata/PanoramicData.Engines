using Xunit;

namespace PanoramicData.Engines.Test;

public class TestEngine : Engine
{
	public TestEngine() : base("TestEngine")
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
}
