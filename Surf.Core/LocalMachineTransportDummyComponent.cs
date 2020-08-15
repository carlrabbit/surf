using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Surf.Proto;
using Nito.AsyncEx;
using Nito.Collections;
using System;
using System.Threading.Channels;

namespace Surf.Core
{
    /// <summary>
    /// This transport component fakes a network transport and allows to keep 
    /// a global in-process list of available members.
    /// 
    /// TODO: enhance to simulate timeouts and network failures
    /// TODO: enhance to simulate network partitions
    /// </summary>
    public class LocalMachineTransportDummyComponent : IDatagramTransportComponent
    {
        private static readonly AsyncReaderWriterLock s_rwLock = new AsyncReaderWriterLock();
        private static readonly Dictionary<string, LocalMachineTransportDummyComponent> s_memberLookup = new Dictionary<string, LocalMachineTransportDummyComponent>();

        private readonly IProtocolStateComponent _state;
        private readonly ITimeProvider _tp;

        private Func<MessageEnvelope, Member, CancellationToken, Task>? _handler = null;

        public LocalMachineTransportDummyComponent(IProtocolStateComponent state, ITimeProvider tp)
        {
            _state = state;
            _tp = tp;
        }

        public async Task SendMessageAsync(Proto.MessageEnvelope msg, Member toMember)
        {
            // Simulate some network delay
            // await _tp.TaskDelay(10);
            await _tp.ExecuteAfter(10, default, async (_) =>
            {
                LocalMachineTransportDummyComponent? target = null;

                using (await s_rwLock.ReaderLockAsync())
                {
                    if (s_memberLookup.ContainsKey(CalculateLookupHash(toMember)))
                    {
                        target = s_memberLookup[CalculateLookupHash(toMember)];
                    }
                    else
                    {
                        return;
                    }
                }

                // Pretend udp behaviour, so no error occurs if target is not reachable
                await target.ReceiveMessage(msg, _state.GetSelf());
            });
        }

        private async Task ReceiveMessage(MessageEnvelope msg, Member fromMember)
        {
            await _handler(msg, fromMember, CancellationToken.None);
        }

        public async Task ListenAsync(Func<MessageEnvelope, Member, CancellationToken, Task> handler, CancellationToken token = default)
        {

            var old = Interlocked.Exchange(ref _handler, handler);
            if (old != null)
            {
                throw new Exception("Cannot start two listeners.");
            }

            using (await s_rwLock.WriterLockAsync())
            {
                s_memberLookup.Add(CalculateLookupHash(_state.GetSelf()), this);
            }

            try
            {
                // Sleep until token is canceled
                await new CancellationTokenTaskSource<object>(token).Task;
            }
            catch (TaskCanceledException) { }
            finally
            {
                using (await s_rwLock.WriterLockAsync())
                {
                    s_memberLookup.Remove(CalculateLookupHash(_state.GetSelf()));
                }
            }

        }

        private static string CalculateLookupHash(Member member)
        {
            return $"{member.Address}:{member.Port}";
        }
    }
}
