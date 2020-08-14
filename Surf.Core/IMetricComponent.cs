using System.Threading.Tasks;

namespace Surf.Core
{
    public interface IMetricComponent
    {
        Task<string> Dump();
        void TrackMeanRoundtripTime(double milliSeconds);
    }
}
