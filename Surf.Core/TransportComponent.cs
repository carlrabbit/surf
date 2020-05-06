using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Surf.Core
{
    /// <summary>
    /// The transport component is responsible for performing the actual network based communication and routing of incomming messages
    /// to other protocol components
    /// </summary>
    public class TransportComponent
    {
        private readonly StateAndConfigurationComponent _state;
        private readonly UdpClient _client;

        private FailureDetectorComponent _fdc = null; //TEMP CODE

        public TransportComponent(StateAndConfigurationComponent state,/*TEMP*/ int port)
        {
            _state = state;
            _client = new UdpClient(new IPEndPoint(IPAddress.IPv6Loopback, port));
        }

        public void SetFDC(FailureDetectorComponent fdc)
        {
            Interlocked.Exchange(ref _fdc, fdc);
        }

        public async Task SendMessageAsync(Proto.MessageEnvelope msg, Member toMember)
        {
            var data = msg.ToByteArray();
            await _client.SendAsync(data, data.Length, new IPEndPoint(IPAddress.IPv6Loopback, toMember.Address));
        }

        public async Task ListenAsync()
        {
            while (true)
            {
                var r = await _client.ReceiveAsync();

                try
                {
                    var udpEnvelope = Surf.Proto.MessageEnvelope.Parser.ParseFrom(r.Buffer);
                    var requester = new Surf.Core.Member() { Address = r.RemoteEndPoint.Port };

                    switch (udpEnvelope.TypeCase)
                    {
                        case Proto.MessageEnvelope.TypeOneofCase.Ping:
                            await _fdc.OnPing(udpEnvelope.Ping, requester);
                            break;
                        case Proto.MessageEnvelope.TypeOneofCase.Ack:
                            await _fdc.OnAck(udpEnvelope.Ack, requester);
                            break;
                        case Proto.MessageEnvelope.TypeOneofCase.PingReq:
                            await _fdc.OnPingReq(udpEnvelope.PingReq, requester);
                            break;
                        case Proto.MessageEnvelope.TypeOneofCase.AckReq:
                            await _fdc.OnAckReq(udpEnvelope.AckReq, requester);
                            break;
                        default:
                            break;
                    }
                }
                catch (Google.Protobuf.InvalidProtocolBufferException)
                {

                }
            }
        }
    }
}
