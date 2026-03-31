using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Shared;

namespace FTPNode.Client
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
            var apiBaseUrl = new Uri(builder.HostEnvironment.BaseAddress);
            apiBaseUrl = new Uri(apiBaseUrl, "api/");
            builder.Configuration["ApiBaseUrl"] = apiBaseUrl.ToString();
            builder.Services.AddScoped<AppState>();
            builder.Services.AddScoped(sp =>
            new HttpClient
            {
                BaseAddress = apiBaseUrl
            });
            await builder.Build().RunAsync();
        }
    }
}
