using System.Net;
using Google.Protobuf;

namespace Surf.Core
{
    public class Member
    {

        public int Address { get; set; }

        public static Member FromProto(Proto.MemberAddress m)
        {
            return new Member()
            {
                Address = m.Port
            };
        }

        public static Proto.MemberAddress ToProto(Member m)
        {
            return new Proto.MemberAddress()
            {
                V6 = ByteString.CopyFrom(IPAddress.IPv6Loopback.GetAddressBytes()),
                Port = m.Address
            };
        }

    }
}
