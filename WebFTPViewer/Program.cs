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
            foreach (var item in args)
            {
                var it = item;
                if (item.StartsWith('-'))
                {
                    it.TrimStart('-');
                }
                var splits = it.Split(':');
                sharedService.SetArg(splits[0].ToLower(), splits.Count() > 1 ? splits[1] : null);
            }
            app.Run();
        }
    }
}
