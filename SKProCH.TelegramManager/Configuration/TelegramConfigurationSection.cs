namespace SKProCH.TelegramManager.Configuration; 

public class TelegramConfigurationSection {
    public string? GetConfigValue(string what) {
        return what switch {
            "api_id"       => ApiId,
            "api_hash"     => ApiHash,
            "phone_number" => PhoneNumber,
            _              => null
        };
    }
    public string ApiId { get; init; }
    public string ApiHash { get; init; }
    public string PhoneNumber { get; init; }
}