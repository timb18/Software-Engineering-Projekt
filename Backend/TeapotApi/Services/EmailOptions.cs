namespace Services;

public class EmailOptions
{
    public const string SectionName = "EMailOptions";
    
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
}
