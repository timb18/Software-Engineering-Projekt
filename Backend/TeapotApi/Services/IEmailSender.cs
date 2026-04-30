using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}

public class SmtpEmailSender(IOptions<EmailOptions> emailOptions, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = emailOptions.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost) ||
            string.IsNullOrWhiteSpace(_options.SmtpUsername) ||
            string.IsNullOrWhiteSpace(_options.SmtpPassword) ||
            string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            logger.LogWarning("Email configuration incomplete. Email to {To} not sent.", to);
            return;
        }

        using var smtpClient = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            Credentials = new NetworkCredential(_options.SmtpUsername, _options.SmtpPassword),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, "Teapot"),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        mailMessage.To.Add(to);

        try
        {
            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
            logger.LogInformation("Email sent to {To}.", to);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending email to {To}.", to);
        }
    }
}
