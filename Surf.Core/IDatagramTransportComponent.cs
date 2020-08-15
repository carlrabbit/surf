using System;
using System.Threading;
using System.Threading.Tasks;
using Surf.Proto;

namespace Surf.Core
{
    public interface IDatagramTransportComponent
    {
        /// <summary>
        /// Todo, replace handler with IFailureDetectorComponent
        /// </summary>
        Task ListenAsync(Func<MessageEnvelope, Member, CancellationToken, Task> handler, CancellationToken token = default);
        Task SendMessageAsync(MessageEnvelope msg, Member toMember);
    }
}
