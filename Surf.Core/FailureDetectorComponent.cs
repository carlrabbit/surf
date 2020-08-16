using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Nito.AsyncEx;
using Surf.Proto;

namespace Surf.Core
{
    /// <summary>
    /// The failure detection component is the main protocol component as all other messages are piggy bagged by it's messages.
    /// </summary>
    public class FailureDetectorComponent : IFailureDetectorComponent
    {
        private readonly IProtocolStateComponent _state;
        private readonly IDatagramTransportComponent _transport;
        private readonly IMembershipComponent _members;
        private readonly DisseminationComponent _gossip;
        private readonly ITimeProvider _tp;

        public FailureDetectorComponent(IProtocolStateComponent state,
            IDatagramTransportComponent transport,
            IMembershipComponent members,
            DisseminationComponent gossip,
            ITimeProvider tp)
        {
            _state = state;
            _transport = transport;
            _members = members;
            _gossip = gossip;
            _tp = tp;
        }

        private int _currentProtocolPeriod = -1;

        private object? _currentSW = null;

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
            Interlocked.Exchange(ref _currentProtocolPeriod, _state.IncreaseProtocolPeriodNumber());

            var m = await _members.PickRandomMemberForPingAsync().ConfigureAwait(false);

            if (m == null)
            {
                return;
            }

            // TODO: Start before or after? 
            _currentSW = _tp.NowForDiff();

            var e = new AsyncCountdownEvent(3);

            await SendPingAsync(m, e);

            await _tp.ExecuteAfter(await _state.GetCurrentPingTimeoutAsync(), default,
                                (_) => SuspectMemberToBeDead(m, e));

            await _tp.ExecuteAfter(await _state.GetProtocolPeriodNumberAsync(), default,
                 (_) => EndProtocolPeriodAsync(m, e));


            await e.WaitAsync();
        }

        private async Task SuspectMemberToBeDead(Member m, AsyncCountdownEvent s)//, CancellationToken ct)
        {
            try
            {
                if (_currentMemberAlive == 1)
                {
                    return;
                }

                Interlocked.Exchange(ref _currentPingTimedOut, 1);

                //send ping request to k members
                //TODO: don't send more than one ping req to an individual member (and add a random pick without side effects)
                var randomMembersToPingReq = new List<Member>(4/*k*/);

                for (var i = 0; i < 4/*k*/; i++)
                {
                    var randomMember = await _members.PickRandomMemberPingReqAsync();
                    if (randomMember == null) return;

                    randomMembersToPingReq.Append(randomMember);

                    await _transport.SendMessageAsync(new Proto.MessageEnvelope()
                    {
                        PingReq = new Proto.PingReq()
                        {
                            FromMember = Member.ToProto(_state.GetSelf()),
                            ToMember = Member.ToProto(m),
                            LocalTime = _currentProtocolPeriod
                        }
                    }, randomMember);
                }
            }
            finally
            {
                s.Signal();
            }
        }

        private async Task EndProtocolPeriodAsync(Member m, AsyncCountdownEvent s)
        {
            try
            {
                if (_currentMemberAlive == 0)
                {
                    var memberRemoved = await _members.RemoveMemberAsync(m).ConfigureAwait(false);

                    if (memberRemoved)
                    {
                        await _gossip.AddAsync(new Proto.GossipEnvelope()
                        {
                            MemberFailed = new Proto.MemberFailedForMe()
                            {
                                Member = Member.ToProto(m)
                            }
                        }).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                s.Signal();
            }
        }

        private async Task SendPingAsync(Member m, AsyncCountdownEvent s)
        {
            try
            {
                // send a ping 
                var pingMsg = new Proto.MessageEnvelope()
                {
                    Ping = new Proto.Ping()
                    {
                        LocalTime = _currentProtocolPeriod
                    }
                };

                pingMsg.Ping.Gossip.AddRange(await _gossip.FetchNextAsync(6).ConfigureAwait(false));

                // send ping
                await _transport.SendMessageAsync(pingMsg, m);
            }
            finally
            {
                s.Signal();
            }
        }

        public async Task HandleMessage(Proto.MessageEnvelope message, Member fromMember)
        {
            switch (message.TypeCase)
            {
                case Proto.MessageEnvelope.TypeOneofCase.Ping:
                    await OnPing(message.Ping, fromMember);
                    break;
                case Proto.MessageEnvelope.TypeOneofCase.Ack:
                    await OnAck(message.Ack, fromMember);
                    break;
                case Proto.MessageEnvelope.TypeOneofCase.PingReq:
                    await OnPingReq(message.PingReq, fromMember);
                    break;
                case Proto.MessageEnvelope.TypeOneofCase.AckReq:
                    await OnAckReq(message.AckReq, fromMember);
                    break;
                default:
                    break;
            }
        }

        private async Task OnAck(Proto.Ack ack, Member _)
        {
            // check if ack is actually from the current protocol period
            if (ack.LocalTime != _currentProtocolPeriod)
            {
                return;
            }

            if (_currentPingTimedOut == 1)
            {
                return;
            }

            Interlocked.Exchange(ref _currentMemberAlive, 1);

            if (_currentSW is null)
            {
                throw new Exception("_currentSW should be initialized");
            }

            var elapsed = _tp.Diff(_currentSW);

            // Console.WriteLine(elapsed);
            await _state.UpdateAverageRoundTripTimeAsync(elapsed);
        }

        private async Task OnAckReq(Proto.AckReq ackReq, Member _)
        {
            //if ping request is addressed to the current member node, mark _current ping target as alive
            if (ackReq.ToMember.Port == (_state.GetSelf()).Port)
            {
                //if local time matches
                if (ackReq.LocalTime != _currentProtocolPeriod)
                {
                    return;
                }
                Interlocked.Exchange(ref _currentMemberAlive, 1);
            }
            //else forward the AckReq
            else
            {
                await _transport.SendMessageAsync(new Proto.MessageEnvelope()
                {
                    AckReq = ackReq
                }, Member.FromProto(ackReq.ToMember));
            }
        }

        private async Task OnPingReq(Proto.PingReq pr, Member fromMember)
        {
            //if ping request is addressed to the current member node, send a ackReq
            if (pr.ToMember.Port == (_state.GetSelf()).Port)
            {
                await _transport.SendMessageAsync(new Proto.MessageEnvelope()
                {
                    AckReq = new Proto.AckReq()
                    {
                        FromMember = Member.ToProto(_state.GetSelf()),
                        ToMember = pr.FromMember,
                        LocalTime = pr.LocalTime
                    }
                }, fromMember);
            }
            //else create ping to target member
            else
            {
                await _transport.SendMessageAsync(new Proto.MessageEnvelope()
                {
                    PingReq = pr
                }, Member.FromProto(pr.ToMember));
            }
        }


        private async Task OnPing(Proto.Ping ping, Member fromMember)
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
                        Member = Member.ToProto(fromMember)
                    }
                });
            }

            // react to gossip from member who pinged 
            foreach (var g in ping.Gossip)
            {
                switch (g.MessageTypeCase)
                {
                    case Proto.GossipEnvelope.MessageTypeOneofCase.MemberJoined:
                        // TODO: Ignore events basic gossip about ourselves
                        if (g.MemberJoined.Member.Port == _state.GetSelf().Port)
                        {
                            continue;
                        }

                        var isNewMember = await _members.AddMemberAsync(Member.FromProto(g.MemberJoined.Member));

                        if (isNewMember)
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
                        // TODO: Ignore events basic gossip about ourselves
                        //       In this case without suspicion mechanism, there is no reaction to this as well
                        //       According to swim
                        if (g.MemberFailed.Member.Port == _state.GetSelf().Port)
                        {
                            continue;
                        }

                        var memberWasRemoved = await _members.RemoveMemberAsync(Member.FromProto(g.MemberFailed.Member));

                        if (memberWasRemoved)
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
