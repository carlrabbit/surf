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
                    case ConsoleKey.I: //Info

                        break;
                    default: break;
                }
            }
            Console.WriteLine();
            Console.WriteLine("exit");
        }

        public static CancellationTokenSource StartMember(int port, int? joinPort)
        {
            var tokenSource = new CancellationTokenSource();

            var cfg = new SurfConfiguration()
            {
                BindAddress = IPAddress.IPv6Loopback.ToString(),
                Port = port,
                PingTimeoutInMilliseconds = 40,
                ProtocolPeriodDurationInMilliseconds = 150
            };

            var tp = new TimeProvider();
            var metricComponent = new PrometheusMetricComponent();
            var scC = new ProtocolStateComponent(cfg, metricComponent);

            var tc = new LocalMachineTransportDummyComponent(scC, tp);
            // var tc = new UdpTransportComponent(scC);

            var mC = new MembershipComponent(scC);
            var gl = new DisseminationComponent(scC);
            var fdc = new FailureDetectorComponent(scC, tc, mC, gl, tp);

            // listen for events
            var t1 = Task.Run(async () =>
            {
                await gl.AddAsync(new Proto.GossipEnvelope()
                {
                    MemberJoined = new Proto.MemberJoinedMe()
                    {
                        Member = Member.ToProto(scC.GetSelf())
                    }
                });

                if (joinPort.HasValue)
                {
                    await mC.AddMemberAsync(new Member(IPAddress.IPv6Loopback, joinPort.Value));
                }

                await tc.ListenAsync(tokenSource.Token);
            }, tokenSource.Token);

            // start error component
            var t2 = Task.Run(async () =>
            {
                while (true)
                {
                    if (tokenSource.IsCancellationRequested) { return; }
                    Console.WriteLine($"A: {port}: {await mC.GetMemberCountAsync()}/{await gl.StackCount()}");
                    // Console.WriteLine(await metricComponent.Dump());

                    await fdc.DoProtocolPeriod();
                }
            }, tokenSource.Token);

            return tokenSource;
        }
    }
}
