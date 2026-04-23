namespace Services;

public class Auth0Options
{
    public const string SectionName = "Auth0";

    public string Domain { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public string ManagementAudience { get; set; } = string.Empty;
}
