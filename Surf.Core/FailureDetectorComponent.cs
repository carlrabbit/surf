using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
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

        private int _currentPeriod = -1;

        private readonly Stopwatch _currentSW = new Stopwatch();

        private int _currentMemberAlive;
        private int _currentPingTimedOut;

        /// <summary>
        /// Starts a new protocol period by sending an initial ping to a random member
        /// 
        /// </summary>
        public async Task DoProtocolPeriod()
        {
            Interlocked.Exchange(ref _currentMemberAlive, 0);
            Interlocked.Exchange(ref _currentPingTimedOut, 0);
            _currentPeriod = _state.IncreaseProtocolPeriod();

            Member m = await _members.NextRandomMemberAsync().ConfigureAwait(false);

            // TODO: Start before or after? 
            _currentSW.Restart();

            await Task.WhenAll(
               SendPingAsync(m),
               MarkMemberAsFailedAsync(m),
               EndProtocolPeriodAsync()
           );

        }

        private async Task MarkMemberAsFailedAsync(Member m)//, CancellationToken ct)
        {
            //pick member
            if (await _members.MemberCountAsync() == 0)
            {
                return;
            }

            await Task.Delay(await _state.GetCurrentPingTimeoutAsync());

            if (_currentMemberAlive == 1)
            {
                return;
            }

            Interlocked.Exchange(ref _currentPingTimedOut, 1);

            var memberRemoved = await _members.RemoveMemberAsync(m).ConfigureAwait(false);

            if (memberRemoved)
            {
                await _gossip.AddAsync(new Proto.GossipEnvelope()
                {
                    MemberFailed = new Proto.MemberFailedForMe()
                    {
                        Member = new Proto.MemberAddress()
                        {
                            V6 = ByteString.CopyFrom(IPAddress.Loopback.GetAddressBytes()),
                            Port = m.Address
                        }
                    }
                }).ConfigureAwait(false);
            }
        }

        private async Task EndProtocolPeriodAsync()
        {
            await Task.Delay(await _state.GetProtocolPeriodAsync());
        }

        private async Task SendPingAsync(Member m)
        {

            //pick member
            if (await _members.MemberCountAsync() == 0)
            {
                return;
            }

            // send 
            Proto.MessageEnvelope pingMsg = new Proto.MessageEnvelope()
            {
                Ping = new Proto.Ping()
                {
                    LocalTime = _currentPeriod
                }
            };

            //TODO: Add goosip number constant
            pingMsg.Ping.Gossip.AddRange(await _gossip.FetchNextAsync(6).ConfigureAwait(false));

            // send ping
            await _transport.SendMessageAsync(pingMsg, m);
        }

        public async Task OnAck(Proto.Ack ack, Member fromMember)
        {
            // check if ack is actually from the current protocol period
            if (ack.LocalTime != _currentPeriod)
            {
                return;
            }

            if (_currentPingTimedOut == 1)
            {
                return;
            }

            Interlocked.Exchange(ref _currentMemberAlive, 1);
            _currentSW.Stop();
            var elapsed = _currentSW.ElapsedMilliseconds;
            // Console.WriteLine(elapsed);
            await _state.UpdateAverageRoundTripTimeAsync(elapsed);
        }

        public async Task OnPing(Proto.Ping ping, Member fromMember)
        {
            var ackMessage = new Proto.MessageEnvelope()
            {
                Ack = new Proto.Ack()
                {
                    LocalTime = ping.LocalTime
                }
            };

            // acknowledge back as soon as possible
            await _transport.SendMessageAsync(ackMessage, toMember: fromMember);

            // check if the member who pinged is new and start gossiping about it if so
            var isMemberNew = await _members.AddMemberAsync(fromMember);
            if (isMemberNew)
            {
                await _gossip.AddAsync(new Proto.GossipEnvelope()
                {
                    MemberJoined = new Proto.MemberJoinedMe()
                    {
                        Member = new Proto.MemberAddress()
                        {
                            V6 = ByteString.CopyFrom(IPAddress.Loopback.GetAddressBytes()),
                            Port = fromMember.Address
                        }
                    }
                });
            }

            // react to gossip from member who pinged 
            foreach (var g in ping.Gossip)
            {
                switch (g.MessageTypeCase)
                {
                    case Proto.GossipEnvelope.MessageTypeOneofCase.MemberJoined:
                        bool newMember = await _members.AddMemberAsync(new Member()
                        {
                            Address = (int)g.MemberJoined.Member.Port
                        });

                        if (newMember)
                        {
                            await _gossip.AddAsync(new Proto.GossipEnvelope()
                            {
                                MemberJoined = new Proto.MemberJoinedMe()
                                {
                                    Member = g.MemberJoined.Member
                                }
                            });
                        }

                        break;
                    case Proto.GossipEnvelope.MessageTypeOneofCase.MemberFailed:
                        bool memberRemoved = await _members.RemoveMemberAsync(new Member()
                        {
                            Address = (int)g.MemberFailed.Member.Port
                        });

                        if (memberRemoved)
                        {
                            await _gossip.AddAsync(new Proto.GossipEnvelope()
                            {
                                MemberFailed = new Proto.MemberFailedForMe()
                                {
                                    Member = g.MemberFailed.Member
                                }
                            });
                        }
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
