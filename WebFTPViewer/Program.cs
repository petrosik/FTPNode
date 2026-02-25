using Shared;
using WebFTPViewer.Components;
using WebFTPViewer.Hubs;
using WebFTPViewer.Services;

namespace WebFTPViewer
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
                    sharedService.SetArg(item.Key.ToLower(), item.Value.ToString());
                }
            }
            app.Run();
        }
    }
}
