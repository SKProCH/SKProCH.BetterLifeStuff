using WTelegram;

namespace SKProCH.TelegramManager.Configuration; 

public static class ConfigurationExtensions {
    public static IServiceCollection AddAndConfigureTelegramClient(this IServiceCollection serviceCollection) {
        serviceCollection.AddSingleton(provider => {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var telegramConfigurationSection = configuration.GetSection("Telegram").Get<TelegramConfigurationSection>();
            if (telegramConfigurationSection == null) {
                throw new InvalidOperationException("No Telegram section in config");
            }

            return new Client(telegramConfigurationSection.GetConfigValue);
        });

        return serviceCollection;
    }

    public static IServiceProvider SetupTelegramLogging(this IServiceProvider provider) {
        var telegramLogger = provider.GetRequiredService<ILogger<Client>>();
        WTelegram.Helpers.Log = (i, s) => telegramLogger.Log((LogLevel)i, "Telegram: {TelegramMessage}", s);

        return provider;
    }
}