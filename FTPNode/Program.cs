using FTPNode.Client.Pages;
using FTPNode.Components;
using FTPNode.Hubs;
using FTPNode.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
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
                if (certPath == null)
                {
                    certPath = "certs/cert.pfx";
                    builder.Configuration["Kestrel:Certificates:Default:Path"] = certPath;
                }
                var certPassword = builder.Configuration["Kestrel:Certificates:Default:Password"];
                if (certPassword == null)
                {
                    certPassword = "defaultStrongPassword123!";
                    builder.Configuration["Kestrel:Certificates:Default:Password"] = certPassword;
                }
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

            var sharedService = app.Services.GetRequiredService<ISharedStorage>();
            if (ftpsettings != null)
            {
                if (!builder.Environment.IsDevelopment())
                {
                    Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "custom"));
                }
                foreach (var item in ftpsettings.GetChildren())
                {
                    if (item.Key.ToLower() == "availablethemes" && !builder.Environment.IsDevelopment())
                    {
                        var themes = item.Get<List<string>>();
                        if (themes != null)
                        {
                            themes.Remove("light");
                            themes.Remove("dark");
                            sharedService.SetArg("availablethemestrimmed", themes);
                            foreach (var item1 in themes)
                            {
                                if (File.Exists(Path.Combine(builder.Environment.ContentRootPath, "config", $"{item1}.css")))
                                    File.Copy(Path.Combine(builder.Environment.ContentRootPath, "config", $"{item1}.css"), Path.Combine(builder.Environment.ContentRootPath, "custom", $"{item1}.css"), true);
                                else
                                    Console.WriteLine($"Warning: Theme CSS file for '{item1}' not found at 'config/{item1}.css'. Skipping copy.");
                            }
                        }
                    }
                    if (item.Key.ToLower() == "pageicon" && !builder.Environment.IsDevelopment())
                    {
                        if (File.Exists(Path.Combine(builder.Environment.ContentRootPath, "config", item.Value)))
                            File.Copy(Path.Combine(builder.Environment.ContentRootPath, "config", item.Value), Path.Combine(builder.Environment.ContentRootPath, "custom", $"favicon1{Path.GetExtension(item.Value)}"), true);
                        else
                            Console.WriteLine($"Warning: icon file '{item.Value}' not found. Skipping copy.");
                    }

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
                if (!sharedService.ContainsArg("title"))
                {
                    sharedService.SetArg("title","FTP Node");
                }
                if (!sharedService.ContainsArg("availablethemes"))
                {
                    sharedService.SetArg("availablethemes", "[\"light\", \"dark\"]");
                }
            }
            if (app.Environment.IsDevelopment())
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new CompositeFileProvider(new PhysicalFileProvider(app.Environment.WebRootPath))
                });
            }
            else
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new CompositeFileProvider(
                        new PhysicalFileProvider(app.Environment.WebRootPath),
                        new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "custom")))
                });
            }


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
