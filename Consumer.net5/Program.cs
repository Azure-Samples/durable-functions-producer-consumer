using System;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Consumer.net5
{
    public class Program
    {
        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices(b =>
                {
                    var appInsightsInstrumentationKey = Environment.GetEnvironmentVariable(@"APPINSIGHTS_INSTRUMENTATIONKEY");
                    if (!string.IsNullOrWhiteSpace(appInsightsInstrumentationKey))
                    {
                        b.AddSingleton(new TelemetryConfiguration(appInsightsInstrumentationKey));
                    }
                })
                .Build();

            host.Run();
        }
    }
}