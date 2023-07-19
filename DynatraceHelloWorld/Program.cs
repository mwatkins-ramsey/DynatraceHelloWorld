using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace DynatraceHelloWorld
{
    class Program
    {
        public static void Main(string[] args)
        {
            var port = GetPortFromEnvironment() ?? 5000;
            var host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.ListenAnyIP(port);
                })
                .ConfigureServices((context, services) =>
                {
                    // Add routing services
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    app.UseRouting();

                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/hello", async context =>
                        {
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync("{\"message\":\"Hello World\"}");
                        });
                    });
                })
                .Build();

            host.Run();
        }
        
        private static int? GetPortFromEnvironment()
        {
            var portString = Environment.GetEnvironmentVariable("API_PORT");
            if (int.TryParse(portString, out int port))
            {
                return port;
            }
            Console.WriteLine("API_PORT not set in env, resorting to default");
            return null;
        }
    }
}