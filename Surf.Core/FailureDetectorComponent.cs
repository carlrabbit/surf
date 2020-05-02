using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Surf.Core
{
    /// <summary>
    /// The failure detection component is the main protocol component as all other messages are piggy bagged by it's messages.
    /// </summary>
    public class FailureDetectorComponent
    {
        private readonly StateAndConfigurationComponent _state;
        private readonly TransportComponent _transport;
        private readonly MembershipComponent _members;
        private readonly DisseminationComponent _gossip;

        public FailureDetectorComponent(StateAndConfigurationComponent state,
            TransportComponent transport,
            MembershipComponent members,
            DisseminationComponent gossip)
        {
            _state = state;
            _transport = transport;
            _members = members;
            _gossip = gossip;

            _transport.SetFDC(this);
        }

        private readonly AsyncReaderWriterLock _rwLock = new AsyncReaderWriterLock();

        private long _currentPeriod = -1;
        private readonly Stopwatch _currentSW = new Stopwatch();
        private Task _timeout = null;
        private CancellationTokenSource _currentToken;
        /// <summary>
        /// Starts a new protocol period by sending an initial ping to a random member
        /// 
        /// </summary>
        public async Task DoProtocolPeriod()
        {
            _currentPeriod = _state.IncreaseProtocolPeriod();

            //pick member
            if (await _members.MemberCountAsync() == 0)
            {
                return;
            }

            var m = await _members.NextRandomMemberAsync().ConfigureAwait(false);

            // wait on response for X time 
            if (_timeout != null)
            {
                _currentToken.Cancel();
            }


            var pingMsg = new Proto.UdpMessage()
            {
                Ping = new Proto.Ping()
            };

            pingMsg.Ping.Gossip.AddRange(await _gossip.FetchNextAsync(6));

            // send ping
            await _transport.SendMessageAsync(pingMsg, m);

            _currentToken = new CancellationTokenSource();
            _timeout = Task.Delay(await _state.GetCurrentPingTimeoutAsync().ConfigureAwait(false), _currentToken.Token)
                .ContinueWith(async (t, _) =>
                {
                    await _members.RemoveMemberAsync(new Member()).ConfigureAwait(false);

                }, null, TaskContinuationOptions.OnlyOnRanToCompletion);


            // TODO: Start before or after? 
            _currentSW.Restart();

        }

        public async Task OnAck(Proto.Ack ack, Member fromMember)
        {
            // check if ack is actually from the current protocol period
            // and from the pinged member

            // check if the ack was received in 
            if (_timeout.IsCompletedSuccessfully)
            {
                return;
            }

            _currentSW.Stop();
            var elapsed = _currentSW.ElapsedMilliseconds;
            // Console.WriteLine(elapsed);
            await _state.UpdateAverageRoundTripTimeAsync(elapsed);
        }

        public async Task OnPing(Proto.Ping ping, Member fromMember)
        {
            var ackMessage = new Proto.UdpMessage()
            {
                Ack = new Proto.Ack()
                {
                }
            };

            await _transport.SendMessageAsync(ackMessage, toMember: fromMember);

            foreach (var g in ping.Gossip)
            {
                switch (g.MessageTypeCase)
                {
                    case Proto.Gossip.MessageTypeOneofCase.MemberJoined:
                        await _members.AddMemberAsync(new Member() { Address = (int)g.MemberJoined.Member.Port });
                        break;
                    default:
                        break;
                }
                await _gossip.AddAsync(g);
            }
        }
    }
}
