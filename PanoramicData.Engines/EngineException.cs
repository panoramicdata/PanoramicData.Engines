using System;

namespace PanoramicData.Engines;

/// <summary>
/// Represents errors that occur during engine operations.
/// </summary>
public class EngineException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EngineException"/> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="exception">The inner exception.</param>
	public EngineException(string message, Exception exception) : base(message, exception)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EngineException"/> class.
	/// </summary>
	public EngineException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EngineException"/> class with a message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public EngineException(string message) : base(message)
	{
	}
}
