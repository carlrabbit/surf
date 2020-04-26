using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Data;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Google.Protobuf;
using System.IO;
using System.Collections.Generic;
using Surf.Core;
using System.Text.Json;
using System.Linq;

namespace Surf.Kestrel
{

    public class SurfTcpConnectionHandler : ConnectionHandler
    {

        public override Task OnConnectedAsync(ConnectionContext connection)
        {
            throw new System.NotImplementedException();
        }
    }

    public interface ISurf { }

    public class SurfS : ISurf
    {

        public SurfS()
        {
            StartMember(6666, null);
            StartMember(6667, 6666);

            int port = 6668;

            for (int i = 0; i < 20; i++, port++)
            {
                StartMember(port, joinPort: 6666);
            }
            for (int i = 0; i < 20; i++, port++)
            {
                StartMember(port, joinPort: 6667);
            }

            // Task.Run(async () =>
            // {
            //     var cli = new UdpClient();
            //     while (true)
            //     {
            //         var ping = new Surf.Proto.UdpMessage()
            //         {
            //             Ping = new Proto.Ping()
            //             {
            //                 Gossip = {
            //                     new Surf.Proto.Gossip {
            //                         MemberJoined= new Proto.MemberJoined() {
            //                             Member= new Proto.MemberAddress() {
            //                                 Port=7777,
            //                                 V6=ByteString.CopyFrom(IPAddress.IPv6Loopback.GetAddressBytes())
            //                             }
            //                         }
            //                     }
            //                 }
            //             }
            //             // Ts = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
            //             // Nodes = { new Surf.Proto.NodeAddress() { Port = 33, V6 = ByteString.CopyFrom(IPAddress.IPv6Loopback.GetAddressBytes()) } }
            //         };

            //         Console.WriteLine($" Size is {ping.CalculateSize()}");
            //         byte[] msg = null;
            //         using (var x = new MemoryStream())
            //         {
            //             ping.WriteTo(x);
            //             msg = x.ToArray();
            //         }

            //         await cli.SendAsync(msg, msg.Length, new System.Net.IPEndPoint(IPAddress.Loopback, 2223));
            //         await cli.SendAsync(new byte[] { 21, 32, 3 }, 3, new System.Net.IPEndPoint(IPAddress.Loopback, 2223));
            //         await Task.Delay(TimeSpan.FromSeconds(1));
            //     }
            // });
        }

        public void StartMember(int port, int? joinPort)
        {
            var self = new Member()
            {
                Address = port
            };

            var cli = new UdpClient(new IPEndPoint(IPAddress.IPv6Loopback, port));

            var mC = new MembershipComponent();
            var gl = new DisseminationComponent(mC);

            // start error component
            Task.Run(async () =>
            {
                while (true)
                {
                    Console.WriteLine($"A: {port}: {await mC.MemberCountAsync()}/{await gl.StackCount()}");// + JsonSerializer.Serialize(l));
                    if (await mC.MemberCountAsync() > 0)
                    {
                        //exlude self
                        var randomEl = await mC.NextRandomMember();

                        var ping = new Proto.UdpMessage()
                        {
                            Ping = new Proto.Ping()
                        };
                        ping.Ping.Gossip.AddRange(await gl.FetchNextAsync(4));

                        await cli.SendAsync(ping.ToByteArray(), ping.CalculateSize(), new IPEndPoint(IPAddress.IPv6Loopback, randomEl.Address));
                    }

                    await Task.Delay(500);
                }
            });

            // listen for events
            Task.Run(async () =>
            {

                await gl.AddAsync(new Proto.Gossip()
                {
                    MemberJoined = new Proto.MemberJoined()
                    {
                        Member = new Proto.MemberAddress()
                        {
                            V6 = ByteString.CopyFrom(IPAddress.Loopback.GetAddressBytes()),
                            Port = (uint)self.Address
                        }
                    }
                });

                if (joinPort.HasValue)
                {
                    var ping = new Proto.UdpMessage()
                    {
                        Ping = new Proto.Ping()
                    };
                    ping.Ping.Gossip.AddRange(await gl.FetchNextAsync(4));
                    await cli.SendAsync(ping.ToByteArray(), ping.CalculateSize(), new IPEndPoint(IPAddress.IPv6Loopback, joinPort.Value));
                }

                while (true)
                {
                    var r = await cli.ReceiveAsync();

                    try
                    {
                        var udpEnvelope = Surf.Proto.UdpMessage.Parser.ParseFrom(r.Buffer);
                        var requester = new Surf.Core.Member() { Address = r.RemoteEndPoint.Port };

                        switch (udpEnvelope.TypeCase)
                        {
                            case Proto.UdpMessage.TypeOneofCase.Ping:
                                var p = udpEnvelope.Ping;

                                var ack = new Proto.Ack();
                                var ackEnv = new Proto.UdpMessage()
                                {
                                    Ack = ack
                                };
                                await cli.SendAsync(ack.ToByteArray(), ack.CalculateSize(), r.RemoteEndPoint);
                                //sendAck

                                foreach (var m in p.Gossip)
                                {
                                    switch (m.MessageTypeCase)
                                    {
                                        case Proto.Gossip.MessageTypeOneofCase.MemberJoined:
                                            var join = m.MemberJoined;

                                            if (await mC.AddMemberAsync(new Member()
                                            {
                                                Address = (int)join.Member.Port
                                            }))
                                            {
                                                await gl.AddAsync(m).ConfigureAwait(false);
                                            }

                                            break;
                                        default: break;
                                    }
                                }

                                break;
                            default:
                                break;
                        }
                    }
                    catch (Google.Protobuf.InvalidProtocolBufferException)
                    {

                    }
                }
            });

        }
    }

}
