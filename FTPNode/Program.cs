using Shared;
using System.Text.Json;
using FTPNode.Components;
using FTPNode.Hubs;
using FTPNode.Services;

namespace FTPNode
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveWebAssemblyComponents();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddControllers();
            builder.Services.AddHttpClient();

            builder.Services.AddScoped<AppState>();
            builder.Services.AddSignalR(options =>
            {
                options.MaximumReceiveMessageSize = 1024 * 128; // 128KB
            });
            builder.Services.AddSingleton<ISharedStorage, SharedStorage>();
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()   // important for SignalR
                        .SetIsOriginAllowed(_ => true); // allow all origins (for dev)
                });
            });
            var env = builder.Environment.EnvironmentName;
            builder.Configuration
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false)
                .AddJsonFile("/config/appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();

            var ftpsettings = builder.Configuration.GetSection("FTP");
            var app = builder.Build();
            app.UseStaticFiles();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseWebAssemblyDebugging();
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseAntiforgery();
            app.UseCors();
            app.MapHub<FTPHub>("/api/ftphub");

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);
            app.MapControllers();

            var sharedService = app.Services.GetRequiredService<ISharedStorage>();
            if (ftpsettings != null)
            {
                foreach (var item in ftpsettings.GetChildren())
                {
                    if (item.GetChildren().Any())
                    {
                        var dict = ToDictionary(item);
                        var json = JsonSerializer.Serialize(dict);

                        sharedService.SetArg(item.Key.ToLower(), json);
                    }
                    else
                    {
                        sharedService.SetArg(item.Key.ToLower(), item.Value?.ToString());
                    }
                }
            }
            app.Run();
        }
        private static object ToDictionary(IConfigurationSection section)
        {
            var children = section.GetChildren().ToList();

            if (!children.Any())
                return section.Value;

            // Detect array (numeric keys)
            if (children.All(c => int.TryParse(c.Key, out _)))
            {
                return children
                    .OrderBy(c => int.Parse(c.Key))
                    .Select(ToDictionary)
                    .ToList();
            }

            // Otherwise object
            return children.ToDictionary(
                c => c.Key,
                c => ToDictionary(c)
            );
        }
    }
}
