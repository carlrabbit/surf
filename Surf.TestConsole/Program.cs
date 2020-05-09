using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Surf.Core;

namespace Surf.TestConsole
{
    internal class Program
    {
        private static void Main(string[] _)
        {
            var rng = new Random();

            var members = new List<CancellationTokenSource>();
            Console.WriteLine("Starting nodes");
            var seedNodes = new int[] { 6666, 6667, 6668 };

            for (var i = 0; i < seedNodes.Length; i++)
            {
                StartMember(seedNodes[i], i == 0 ? (int?)null : seedNodes[i - 1]);
            }


            var nextPort = seedNodes.Max() + 1;
            for (var i = 0; i < 3; i++, nextPort++)
            {
                members.Add(StartMember(nextPort, joinPort: seedNodes[nextPort % seedNodes.Length]));
            }

            var stop = false;
            while (!stop)
            {
                switch (Console.ReadKey().Key)
                {
                    case ConsoleKey.Q:
                        stop = true;
                        break;
                    case ConsoleKey.F:
                        var index = rng.Next(members.Count);
                        members[index].Cancel();
                        members.RemoveAt(index);
                        break;
                    case ConsoleKey.N:
                        members.Add(StartMember(nextPort, joinPort: seedNodes[nextPort % seedNodes.Length]));
                        nextPort++;
                        break;
                    default: break;
                }
            }
            Console.WriteLine();
            Console.WriteLine("exti");
        }

        public static CancellationTokenSource StartMember(int port, int? joinPort)
        {
            var tokenSource = new CancellationTokenSource();
            var self = new Member()
            {
                Port = port
            };

            var metricComponent = new MetricComponent();
            var scC = new ProtocolStateComponent(port, metricComponent);

            var tc = new TransportComponent(scC);
            var mC = new MembershipComponent(scC);
            var gl = new DisseminationComponent(scC);
            var fdc = new FailureDetectorComponent(scC, tc, mC, gl);

            // listen for events
            var t1 = Task.Run(async () =>
            {
                await gl.AddAsync(new Proto.GossipEnvelope()
                {
                    MemberJoined = new Proto.MemberJoinedMe()
                    {
                        Member = new Proto.MemberAddress()
                        {
                            V6 = ByteString.CopyFrom(IPAddress.Loopback.GetAddressBytes()),
                            Port = self.Port
                        }
                    }
                });

                if (joinPort.HasValue)
                {
                    await mC.AddMemberAsync(new Member() { Port = joinPort.Value });
                }

                await tc.ListenAsync(tokenSource.Token);
            }, tokenSource.Token);

            // start error component
            var t2 = Task.Run(async () =>
            {
                while (true)
                {
                    if (tokenSource.IsCancellationRequested) { return; }
                    Console.WriteLine($"A: {port}: {await mC.GetMemberCountAsync()}/{await gl.StackCount()}");// + JsonSerializer.Serialize(l));
                    Console.WriteLine(await metricComponent.Dump());

                    await fdc.DoProtocolPeriod();
                }
            }, tokenSource.Token);

            return tokenSource;
        }
    }
}
