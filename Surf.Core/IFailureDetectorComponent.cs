using System.Threading.Tasks;
using Surf.Proto;

namespace Surf.Core
{
    public interface IFailureDetectorComponent
    {
        Task DoProtocolPeriod();
        Task HandleMessage(MessageEnvelope message, Member fromMember);
    }
}
