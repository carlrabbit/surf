using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Surf.Kestrel
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    // This shows how a custom framework could plug in an experience without using Kestrel APIs directly
                    // services.AddFramework(new IPEndPoint(IPAddress.Loopback, 2222));
                    services.AddSingleton<ISurf>(new SurfS());
                })
                .UseKestrel(options =>
                {
                    // TCP 8007
                    options.ListenLocalhost(2222, builder =>
                    {
                        builder.UseConnectionHandler<SurfTcpConnectionHandler>();
                    });
                })
                .UseStartup<Startup>();
    }
}
