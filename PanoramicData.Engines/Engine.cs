using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace PanoramicData.Engines
{
	public abstract class Engine : IEngine
	{
		private EngineState _engineState;
		private readonly ILogger<Engine> _logger;

		protected Engine(
			string name,
			ILoggerFactory loggerFactory = null
			)
		{
			Name = name;
			_logger = loggerFactory?.CreateLogger<Engine>();
			_engineState = EngineState.Stopped;
		}

		public EngineState EngineState
		{
			get => _engineState;
			private set
			{
				_engineState = value;
				_logger?.LogInformation(PrettyPrint(value.ToString()));
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
			switch (EngineState)
			{
				case EngineState.Started:
					EngineState = EngineState.Stopping;
					break;
				default:
					var message = PrettyPrint($"Cannot stop the engine when it is {EngineState}");
					_logger?.LogError(message);
					throw new InvalidOperationException(message);
			}
			await Shutdown().ConfigureAwait(false);
			EngineState = EngineState.Stopped;
		}

		protected string PrettyPrint(string message) => $"{Name}: {message}";

		/// <summary>
		///    Called to restart the engine
		/// </summary>
		// ReSharper disable once MemberCanBeProtected.Global
		public async Task RestartAsync()
		{
			_logger?.LogInformation(PrettyPrint("Restarting"));
			await StopAsync().ConfigureAwait(false);
			await StartAsync().ConfigureAwait(false);
			_logger?.LogInformation(PrettyPrint("Restart complete"));
		}

		/// <summary>
		///    Called when stopping the engine
		/// </summary>
		protected abstract Task Shutdown();

		/// <summary>
		///    Called when starting the engine
		/// </summary>
		protected abstract Task Startup();
	}
}