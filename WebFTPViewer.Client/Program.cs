using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace WebFTPViewer.Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            //builder.Configuration
            //    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            //    .AddJsonFile($"appsettings.{builder.HostEnvironment.EnvironmentName}.json", optional: true)
            //    .AddEnvironmentVariables();
            var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
            ?? throw new InvalidOperationException("ApiBaseUrl not configured");
            builder.Services.AddScoped(sp =>
            new HttpClient
            {
                BaseAddress = new Uri(apiBaseUrl)
            });
            await builder.Build().RunAsync();
        }
    }
}
