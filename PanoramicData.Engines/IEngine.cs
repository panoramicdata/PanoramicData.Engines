using System.Threading.Tasks;

namespace PanoramicData.Engines
{
	public interface IEngine
	{
		Task StartAsync();
		Task StopAsync();
	}
}
