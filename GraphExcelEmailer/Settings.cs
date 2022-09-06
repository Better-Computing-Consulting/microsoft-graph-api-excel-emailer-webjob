using Microsoft.Extensions.Configuration;
using Azure.Identity;

public class Settings
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? TenantId { get; set; }
    public string? DocumentPath { get; set; }
    public string? ADUser { get; set; }
    public static Settings LoadSettings()
    {
        string? keyVaultName = Environment.GetEnvironmentVariable("KEY_VAULT_NAME");
        var kvUri = "https://" + keyVaultName + ".vault.azure.net";
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddAzureKeyVault(new Uri(kvUri), new DefaultAzureCredential())
            .Build();
        return config.Get<Settings>();
    }
}