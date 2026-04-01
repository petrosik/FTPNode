using FTPNode.Components;
using FTPNode.Hubs;
using FTPNode.Services;
using Microsoft.AspNetCore.DataProtection;
using Shared;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace FTPNode
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            if (!builder.Environment.IsDevelopment())
            {
                Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "certs"));
                var certPath = builder.Configuration["Kestrel:Certificates:Default:Path"];
                var certPassword = builder.Configuration["Kestrel:Certificates:Default:Password"];
  
                var cert = CreateOrLoadSelfSignedCertificate("localhost", certPath, certPassword);


                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(8080);
                    options.ListenAnyIP(8081, listenOptions => listenOptions.UseHttps(cert));
                });
                var keyPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys");
                Directory.CreateDirectory(keyPath);
                builder.Services.AddDataProtection()
                    .ProtectKeysWithCertificate(cert)
                    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")))
                    .SetApplicationName("FTPNode");
            }

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

        private static X509Certificate2 CreateOrLoadSelfSignedCertificate(string hostname, string certPath, string password)
        {
            // If a certificate file already exists, load it
            if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
            {
                return X509CertificateLoader.LoadPkcs12FromFile(certPath, password);
            }

            // Otherwise, create a new self‑signed cert
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                $"CN={hostname}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            using var cert = request.CreateSelfSigned(
                DateTimeOffset.Now.AddDays(-1),
                DateTimeOffset.Now.AddYears(1)
            );

            // Export to PFX with password
            var pfxBytes = cert.Export(X509ContentType.Pfx, password);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(certPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(certPath, pfxBytes);

            // Load via X509CertificateLoader
            return X509CertificateLoader.LoadPkcs12FromFile(certPath, password);
        }
    }
}
