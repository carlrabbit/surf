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
        private readonly ProtocolStateComponent _state;
        private readonly UdpClient _client;

        private FailureDetectorComponent? _fdc = null; //TEMP CODE

        public TransportComponent(ProtocolStateComponent state)
        {
            _state = state;
            _client = new UdpClient(new IPEndPoint(IPAddress.IPv6Loopback, _state.GetSelf().Port));
        }

        //TODO: find better way for callback mechanism
        public void SetFDC(FailureDetectorComponent fdc)
        {
            Interlocked.Exchange(ref _fdc, fdc);
        }

        public async Task SendMessageAsync(Proto.MessageEnvelope msg, Member toMember)
        {
            var data = msg.ToByteArray();
            await _client.SendAsync(data, data.Length, new IPEndPoint(IPAddress.IPv6Loopback, toMember.Port));
        }

        public async Task ListenAsync(CancellationToken token = default)
        {
            while (true)
            {
                if (token.IsCancellationRequested) return;
                var r = await _client.ReceiveAsync();

                try
                {
                    var envelope = Surf.Proto.MessageEnvelope.Parser.ParseFrom(r.Buffer);
                    var requester = new Surf.Core.Member() { Port = r.RemoteEndPoint.Port };

                    // TODO: find better way for callback mechanism
                    await _fdc!.HandleMessage(envelope, requester);
                }
                catch (Google.Protobuf.InvalidProtocolBufferException)
                {

                }
            }
        }
    }
}
