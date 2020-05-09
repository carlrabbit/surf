using System.Threading.Tasks;

namespace Surf.Core
{
    public interface IProtocolStateComponent
    {
        Task<double> GetAverageRoundTripTimeAsync();
        Task<double> GetChatterLifeTimeAsync();
        Task<int> GetCurrentPingTimeoutAsync();
        Task<int> GetErrorCycleNumberAsync();
        Task<int> GetProtocolPeriodNumberAsync();
        Member GetSelf();
        int IncreaseProtocolPeriodNumber();
        Task UpdateAverageRoundTripTimeAsync(long measuredMilliseconds);
        Task UpdateMemberCountAsync(int activeMembers);
    }
}
