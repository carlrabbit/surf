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
            int[] seedNodes = new int[] { 13332, 13333 };

            for (int i = 0; i < seedNodes.Length; i++)
            {
                StartMember(seedNodes[i], i == 0 ? seedNodes[0] - 1 : seedNodes[i - 1]);
            }


            int nextPort = seedNodes.Max() + 1;
            //   Task.Delay(100).Wait();
            for (int i = 0; i < 10; i++, nextPort++)
            {
                StartMember(nextPort, joinPort: seedNodes[nextPort % seedNodes.Length]);
                Task.Delay(1000).Wait();
            }
        }

        public void StartMember(int port, int? joinPort)
        {
            var self = new Member()
            {
                Address = port
            };

            var scC = new StateAndConfigurationComponent(port);
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
                }
            });
        }
    }

}
