using Serilog;
using SKProCH.TelegramManager;
using SKProCH.TelegramManager.Configuration;
using WTelegram;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => {
        services.AddAndConfigureTelegramClient();
        services.AddSingleton(provider => provider.GetRequiredService<IConfiguration>().GetSection("ChatsArchiver").Get<ChatsArchiverConfigurationSection>());
        services.AddHostedService<ChatsArchiverWorker>();
    })
    .UseSerilog((context, configuration) => {
        if (!context.Configuration.GetSection("Serilog").Exists()) {
            throw new InvalidOperationException("No Serilog section in config found");
        }
        configuration.ReadFrom.Configuration(context.Configuration);
    })
    .Build();

host.Services.SetupTelegramLogging();
await host.Services.GetRequiredService<Client>().LoginUserIfNeeded();

await host.RunAsync();