using System.Threading.Tasks;

namespace Surf.Core
{
    public interface IMembershipComponent
    {
        Task<bool> AddMemberAsync(Member member);
        Task<int> GetMemberCountAsync();
        Task<Member?> PickRandomMemberForPingAsync();
        Task<Member?> PickRandomMemberPingReqAsync();
        Task<bool> RemoveMemberAsync(Member member);
    }
}
