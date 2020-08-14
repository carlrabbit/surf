using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Surf.Proto;
using Nito.AsyncEx;
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
    public class LocalMachineTransportDummyComponent : ITransportComponent
    {
        private static readonly AsyncReaderWriterLock s_rwLock = new AsyncReaderWriterLock();
        private static readonly Dictionary<string, LocalMachineTransportDummyComponent> s_memberLookup = new Dictionary<string, LocalMachineTransportDummyComponent>();

        private readonly IProtocolStateComponent _state;
        private readonly ITimeProvider _tp;
        private IFailureDetectorComponent? _fdc = null; //TEMP CODE

        public LocalMachineTransportDummyComponent(IProtocolStateComponent state, ITimeProvider tp)
        {
            _state = state;
            _tp = tp;
        }

        public void RegisterFailureDetectorComponent(IFailureDetectorComponent fdc)
        {
            Interlocked.Exchange(ref _fdc, fdc);
        }

        public async Task SendMessageAsync(Proto.MessageEnvelope msg, Member toMember)
        {
            // Simulate some network delay
            await _tp.TaskDelay(10);

            LocalMachineTransportDummyComponent? target = null;

            using (await s_rwLock.ReaderLockAsync())
            {
                if (s_memberLookup.ContainsKey(CalculateLookup(toMember)))
                {
                    target = s_memberLookup[CalculateLookup(toMember)];
                }
                else
                {
                    return;
                }
            }

            // Pretend udp behaviour, so no error occurs if target is not reachable
            await target.ReceiveMessage(msg, _state.GetSelf());
        }

        private async Task ReceiveMessage(MessageEnvelope msg, Member fromMember)
        {
            await _fdc!.HandleMessage(msg, fromMember);
        }

        public async Task ListenAsync(CancellationToken token = default)
        {
            using (await s_rwLock.WriterLockAsync())
            {
                s_memberLookup.Add(CalculateLookup(_state.GetSelf()), this);
            }

            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    using (await s_rwLock.WriterLockAsync())
                    {
                        s_memberLookup.Remove(CalculateLookup(_state.GetSelf()));
                    }
                    return;
                }
                await _tp.TaskDelay(1000);
            }
        }

        private static string CalculateLookup(Member member)
        {
            return $"{member.Address}:{member.Port}";
        }
    }
}
