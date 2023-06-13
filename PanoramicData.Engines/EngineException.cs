namespace PanoramicData.Engines;

public class EngineException : Exception
{
	public EngineException(string message, Exception exception) : base(message, exception)
	{
	}

	public EngineException()
	{
	}

	public EngineException(string message) : base(message)
	{
	}

	protected EngineException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
	{
	}
}
