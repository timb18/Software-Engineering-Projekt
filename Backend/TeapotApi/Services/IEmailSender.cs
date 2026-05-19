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

/// <summary>
/// Sends an email message through the configured delivery provider.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends a plain-text email message to the given recipient.
    /// </summary>
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
        var apiKey = ResolveResendApiKey(_resendOptions);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Resend API key is missing. Set Resend__ApiKey or RESEND_API_KEY.");
        }

        var fromEmail = ResolveResendFromEmail(_resendOptions, _emailOptions);
        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException("Email sender is missing. Set Resend__FromEmail, RESEND_FROM_EMAIL, or EMailOptions__FromEmail.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                from = fromEmail,
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

    public static string ResolveResendApiKey(ResendOptions resendOptions) =>
        FirstConfigured(resendOptions.ApiKey, Environment.GetEnvironmentVariable("RESEND_API_KEY"));

    public static string ResolveResendFromEmail(ResendOptions resendOptions, EmailOptions emailOptions) =>
        FirstConfigured(
            resendOptions.FromEmail,
            Environment.GetEnvironmentVariable("RESEND_FROM_EMAIL"),
            emailOptions.FromEmail);

    private static string FirstConfigured(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}

public class ConfiguredEmailSender(
    IServiceProvider serviceProvider,
    IOptions<ResendOptions> resendOptions) : IEmailSender
{
    private readonly ResendOptions _resendOptions = resendOptions.Value;

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var senderType = string.IsNullOrWhiteSpace(ResendEmailSender.ResolveResendApiKey(_resendOptions))
            ? typeof(SmtpEmailSender)
            : typeof(ResendEmailSender);
        var sender = (IEmailSender)serviceProvider.GetRequiredService(senderType);

        return sender.SendAsync(to, subject, body, cancellationToken);
    }
}
