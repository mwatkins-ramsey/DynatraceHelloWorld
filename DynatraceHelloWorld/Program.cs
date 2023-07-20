using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dynatrace.OneAgent.Sdk.Api;
using Dynatrace.OneAgent.Sdk.Api.Enums;
using Dynatrace.OneAgent.Sdk.Api.Infos;
using Microsoft.Extensions.Logging;

namespace DynatraceHelloWorld
{
    class Program
    {
        private static string
            DT_API_URL =
                Environment.GetEnvironmentVariable("DYINTRACE_API_URL") ?? "";

        private static string
            DT_API_TOKEN =
                Environment.GetEnvironmentVariable("DYINTRACE_API_TOKEN") ?? "";

        private const string
            activitySource = "MWatkins.DyinTrace.Lab";

        public static readonly ActivitySource MyActivitySource = new ActivitySource(activitySource);

        public static void Main(string[] args)
        {
            IOneAgentSdk oneAgentSdk = OneAgentSdkFactory.CreateInstance();
            var loggingCallback = new StdErrLoggingCallback();
            oneAgentSdk.SetLoggingCallback(loggingCallback);
            IOneAgentInfo agentInfo = oneAgentSdk.AgentInfo;
            initOpenTelemetry();
            IOneAgentInfo agentInfo2 = oneAgentSdk.AgentInfo;
            if (agentInfo2.AgentFound)
            {
                Console.WriteLine($"OneAgent Version: {agentInfo2.Version}");
                if (agentInfo2.AgentCompatible)
                {
                    Console.WriteLine("ITS ALIVE!!");
                }
            }
            SdkState state = oneAgentSdk.CurrentState;
            switch (state)
            {
                case SdkState.ACTIVE:
                    Console.WriteLine("sdk active");
                    break;
                case SdkState.TEMPORARILY_INACTIVE: // capturing disabled, tracing calls can be spared
                    Console.WriteLine("sdk inactive");
                    break;
                case SdkState.PERMANENTLY_INACTIVE: // SDK permanently inactive, tracing calls can be spared
                    Console.WriteLine("sdk permanently inactive");
                    break;
            }
            
            
            
            Meter meter = new Meter("hello-count", "1.0.0");
            Counter<long> counter = meter.CreateCounter<long>("request_counter");

            var port = GetPortFromEnvironment() ?? 5000;
            var host = new WebHostBuilder()
                .UseKestrel(options => { options.ListenAnyIP(port); })
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
                            using var activity = MyActivitySource.StartActivity("Call to /hello");
                            activity?.SetTag("http.method", "GET");
                            activity?.SetTag("net.protocol.version", "1.1");
                            counter.Add(1, new System.Collections.Generic.KeyValuePair<string,object?>("hello", "hello was called!"));
                            context.Response.ContentType = "application/json";
                            
                            ITraceContextInfo traceContextInfo = oneAgentSdk.TraceContextInfo;
                            string traceId = traceContextInfo.TraceId;
                            string spanId = traceContextInfo.SpanId;
                            Console.WriteLine("TID:" + traceId +", SID:" + spanId + " valid?:" + traceContextInfo.IsValid);
                            
                            await context.Response.WriteAsync("{\"message\":\"Hello World\"}");
                        });

                        endpoints.MapGet("/ready", async context =>
                        {
                            using var activity = MyActivitySource.StartActivity("Call to /ready");
                            activity?.SetTag("http.method", "GET");
                            activity?.SetTag("net.protocol.version", "1.1");
                            await context.Response.WriteAsync("");
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

        private static void initOpenTelemetry()
        {
            // ===== GENERAL SETUP =====

            List<KeyValuePair<string, object>> dt_metadata = new List<KeyValuePair<string, object>>();
            foreach (string name in new string[]
                     {
                         "dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties",
                         "/var/lib/dynatrace/enrichment/dt_metadata.properties"
                     })
            {
                try
                {
                    foreach (string line in System.IO.File.ReadAllLines(name.StartsWith("/var")
                                 ? name
                                 : System.IO.File.ReadAllText(name)))
                    {
                        var keyvalue = line.Split("=");
                        dt_metadata.Add(new KeyValuePair<string, object>(keyvalue[0], keyvalue[1]));
                    }
                }
                catch
                {
                }
            }

            Action<ResourceBuilder> configureResource = r => r
                .AddService(serviceName: "Mwatkins-Dyintrace-lab")
                .AddAttributes(dt_metadata);

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);


            // ===== TRACING SETUP =====

            var tracerBuilder = Sdk.CreateTracerProviderBuilder()
                .ConfigureResource(configureResource)
                .SetSampler(new AlwaysOnSampler())
                .AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri(DT_API_URL + "/v1/traces");
                    otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    otlpOptions.Headers = $"Authorization=Api-Token {DT_API_TOKEN}";
                    otlpOptions.ExportProcessorType = ExportProcessorType.Batch;
                })
                .AddSource(MyActivitySource.Name);

            tracerBuilder.Build();


            // ===== METRIC SETUP =====

            Sdk.CreateMeterProviderBuilder()
                .ConfigureResource(configureResource)
                .AddMeter("my_meter")
                .AddOtlpExporter((OtlpExporterOptions exporterOptions, MetricReaderOptions readerOptions) =>
                {
                    exporterOptions.Endpoint = new Uri(DT_API_URL + "/v1/metrics");
                    exporterOptions.Headers = $"Authorization=Api-Token {DT_API_TOKEN}";
                    exporterOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    readerOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
                })
                .Build();


            // ===== LOG SETUP =====

            var resourceBuilder = ResourceBuilder.CreateDefault();
            configureResource!(resourceBuilder);
        }
    }
    class StdErrLoggingCallback : ILoggingCallback
    {
        public void Error(string message) => Console.Error.WriteLine("[OneAgent SDK] Error:   " + message);
        public void Warn (string message) => Console.Error.WriteLine("[OneAgent SDK] Warning: " + message);
    }
}