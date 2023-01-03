using System.Text;
using Azure;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Storage.Blobs;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SNIPERBot.Utils;

namespace SNIPERBot;

public class Program
{
    public static void Main(string[] args)
    {
        // Forcing UTF8
        Console.OutputEncoding = Encoding.UTF8;

        // Use application builder to configure the console app
        var builder = Host.CreateApplicationBuilder(args);

        // Configure:
        // - Key Vault when using production (proper secret management)
        // - Configure Blob Storage for sharing the projects
        if (builder.Environment.IsProduction())
        {
            var keyVaultEndpoint = $"https://{builder.Configuration["KeyVaultName"]}.vault.azure.net/";

            builder.Configuration.AddAzureKeyVault(new Uri(keyVaultEndpoint), new DefaultAzureCredential(),
                new AzureKeyVaultConfigurationOptions
                {
                    ReloadInterval = TimeSpan.FromMinutes(60),
                    Manager = new PrefixKeyVaultSecretManager("Sniper")
                });
            builder.Services.AddSingleton(new BlobClient(new Uri(builder.Configuration["Storage:ProjectLocation"]!), new DefaultAzureCredential()));
        }
        else
        {
            // Development 
            builder.Services.AddSingleton(new BlobClient(new Uri(builder.Configuration["Storage:ProjectLocation"]!), new AzureSasCredential(builder.Configuration["Authentication:BlobStorageSas"]!)));
        }

        // Additional configuration
        var config = new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildPresences
        };

        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton<DiscordSocketClient>();
        builder.Services.AddSingleton<LoggingService>();

        // Run the main logic inside a hosted service
        // This will make sure it will be properly started and stopped
        builder.Services.AddHostedService<SniperHostedService>();
        builder.Services.AddHostedService<PodHealthyListener>();

        var app = builder.Build();
        app.Run();
    }
}