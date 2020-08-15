using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Surf.Proto;

namespace Surf.Core
{
    /// <summary>
    /// The transport component is responsible for performing the actual network based communication and routing of incomming messages
    /// to other protocol components
    /// </summary>
    public class UdpTransportComponent : IDatagramTransportComponent
    {
        private readonly IProtocolStateComponent _state;
        private readonly UdpClient _client;

        private IFailureDetectorComponent? _fdc = null; //TEMP CODE

        public UdpTransportComponent(IProtocolStateComponent state)
        {
            _state = state;
            _client = new UdpClient(new IPEndPoint(IPAddress.IPv6Loopback, _state.GetSelf().Port));
        }

        public async Task SendMessageAsync(Proto.MessageEnvelope msg, Member toMember)
        {
            var data = msg.ToByteArray();
            await _client.SendAsync(data, data.Length, new IPEndPoint(IPAddress.IPv6Loopback, toMember.Port));
        }

        public async Task ListenAsync(Func<MessageEnvelope, Member, CancellationToken, Task> handler, CancellationToken token = default)
        {
            while (true)
            {
                if (token.IsCancellationRequested) return;
                var r = await _client.ReceiveAsync();

                try
                {
                    var envelope = Surf.Proto.MessageEnvelope.Parser.ParseFrom(r.Buffer);
                    var requester = new Member(r.RemoteEndPoint.Address, r.RemoteEndPoint.Port);

                    await handler(envelope, requester, token);
                }
                catch (Google.Protobuf.InvalidProtocolBufferException)
                {

                }
            }
        }
    }
}
