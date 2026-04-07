namespace PanoramicData.Engines;

/// <summary>
/// Represents the possible states of an engine.
/// </summary>
public enum EngineState
{
	/// <summary>Unknown state.</summary>
	Unknown = 0,
	/// <summary>The engine is starting.</summary>
	Starting = 1,
	/// <summary>The engine has started.</summary>
	Started = 2,
	/// <summary>The engine is stopping.</summary>
	Stopping = 3,
	/// <summary>The engine has stopped.</summary>
	Stopped = 4,
}