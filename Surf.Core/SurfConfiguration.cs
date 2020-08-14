namespace Surf.Core
{
    /// <summary>
    /// TODO: Define 'proper' defaults 
    /// </summary>
    public class SurfConfiguration
    {
        public string? MemberName { get; set; }
        public string BindAddress { get; set; } = System.Net.IPAddress.IPv6Loopback.ToString();
        public int? Port { get; set; }

        public int ProtocolPeriodDurationInMilliseconds { get; set; } = 1000;
        public int PingTimeoutInMilliseconds { get; set; } = 100;
        public double Lambda { get; set; } = 3.0;
    }
}
