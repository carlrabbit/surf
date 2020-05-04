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
        }

        public void StartMember(int port, int? joinPort)
        {
            var self = new Member()
            {
                Address = port
            };

            var scC = new StateAndConfigurationComponent();
            var tc = new TransportComponent(scC, port);
            var mC = new MembershipComponent(scC);
            var gl = new DisseminationComponent(scC);
            var fdc = new FailureDetectorComponent(scC, tc, mC, gl);

            // listen for events
            Task.Run(async () =>
            {
                await gl.AddAsync(new Proto.GossipEnvelope()
                {
                    MemberJoined = new Proto.MemberJoinedMe()
                    {
                        Member = new Proto.MemberAddress()
                        {
                            V6 = ByteString.CopyFrom(IPAddress.Loopback.GetAddressBytes()),
                            Port = self.Address
                        }
                    }
                });

                if (joinPort.HasValue)
                {
                    await mC.AddMemberAsync(new Member() { Address = joinPort.Value });
                    //await cli.SendAsync(ping.ToByteArray(), ping.CalculateSize(), new IPEndPoint(IPAddress.IPv6Loopback, joinPort.Value));
                }

                await tc.ListenAsync();
            });
            // start error component
            Task.Run(async () =>
            {
                while (true)
                {
                    Console.WriteLine($"A: {port}: {await mC.MemberCountAsync()}/{await gl.StackCount()}");// + JsonSerializer.Serialize(l));

                    await fdc.DoProtocolPeriod();

                    await Task.Delay(1000);
                }
            });
        }
    }

}
