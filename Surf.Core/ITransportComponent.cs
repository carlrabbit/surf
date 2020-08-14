using System;
using System.Threading;
using System.Threading.Tasks;
using Surf.Proto;

namespace Surf.Core
{
    public interface ITransportComponent
    {
        Task ListenAsync(CancellationToken token = default);
        void RegisterFailureDetectorComponent(IFailureDetectorComponent fdc);
        Task SendMessageAsync(MessageEnvelope msg, Member toMember);
    }
}
