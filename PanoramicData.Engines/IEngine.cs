using System.Threading.Tasks;

namespace PanoramicData.Engines;

/// <summary>
/// Defines an engine that can be started and stopped.
/// </summary>
public interface IEngine
{
	/// <summary>
	/// Starts the engine.
	/// </summary>
	Task StartAsync();

	/// <summary>
	/// Stops the engine.
	/// </summary>
	Task StopAsync();
}
