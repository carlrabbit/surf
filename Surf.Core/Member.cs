using System;
using System.Net;
using Google.Protobuf;

namespace Surf.Core
{
    public class Member
    {
        public Member(IPAddress address, int port)
        {
            Port = port;
            Address = address;
        }
        /// <summary>
        /// An optional friendly name of a member.
        /// </summary>
        public string? FriendlyName { get; set; }

        /// <summary>
        /// The IP address of a member.
        /// </summary>
        public IPAddress Address { get; set; }

        /// <summary>
        /// The port of the surf protocol. Port sharing is not supported.
        /// </summary>
        public int Port { get; set; }

        public static Member FromProto(Proto.MemberAddress m)
        {
            var ipAddress = m.IpAddrCase == Proto.MemberAddress.IpAddrOneofCase.V6
                ? new IPAddress(m.V6.ToByteArray())
                : new IPAddress(m.V4);

            return new Member(ipAddress, m.Port)
            {
                FriendlyName = ""
            };
        }

        public static Proto.MemberAddress ToProto(Member m)
        {
            return m.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? new Proto.MemberAddress()
                {
                    V6 = ByteString.CopyFrom(m.Address.GetAddressBytes()),
                    Port = m.Port
                }
                : new Proto.MemberAddress()
                {
                    V4 = Convert.ToUInt32(m.Address.Address),
                    Port = m.Port
                };
        }

    }
}
