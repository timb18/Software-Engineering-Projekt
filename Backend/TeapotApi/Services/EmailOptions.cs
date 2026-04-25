namespace Services;

public class EmailOptions
{
    public const string SectionName = "EMailOptions";

    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply.teapot@gmail.com";
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string ApiBaseUrl { get; set; } = "http://localhost:5186";
    public string FrontendBaseUrl { get; set; } = "http://127.0.0.1:5173/";
}
