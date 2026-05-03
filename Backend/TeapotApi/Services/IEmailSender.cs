using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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
            throw new InvalidOperationException("Email configuration is incomplete. Check EMailOptions SMTP variables.");
        }

        using var smtpClient = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_options.SmtpUsername, _options.SmtpPassword),
            EnableSsl = true,
            Timeout = 15000
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
            throw;
        }
    }
}

public class ResendEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> emailOptions,
    IOptions<ResendOptions> resendOptions,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _emailOptions = emailOptions.Value;
    private readonly ResendOptions _resendOptions = resendOptions.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_resendOptions.ApiKey))
        {
            throw new InvalidOperationException("Resend API key is missing. Set Resend__ApiKey.");
        }

        if (string.IsNullOrWhiteSpace(_emailOptions.FromEmail))
        {
            throw new InvalidOperationException("Email sender is missing. Set EMailOptions__FromEmail.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _resendOptions.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                from = _emailOptions.FromEmail,
                to = new[] { to },
                subject,
                text = body
            }),
            Encoding.UTF8,
            MediaTypeNames.Application.Json);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Resend email failed ({(int)response.StatusCode}): {responseBody}");
        }

        logger.LogInformation("Email sent to {To} via Resend.", to);
    }
}

public class ConfiguredEmailSender(
    IServiceProvider serviceProvider,
    IOptions<ResendOptions> resendOptions) : IEmailSender
{
    private readonly ResendOptions _resendOptions = resendOptions.Value;

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var senderType = string.IsNullOrWhiteSpace(_resendOptions.ApiKey)
            ? typeof(SmtpEmailSender)
            : typeof(ResendEmailSender);
        var sender = (IEmailSender)serviceProvider.GetRequiredService(senderType);

        return sender.SendAsync(to, subject, body, cancellationToken);
    }
}
