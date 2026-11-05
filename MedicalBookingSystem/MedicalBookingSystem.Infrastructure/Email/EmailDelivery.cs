using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using MedicalBookingSystem.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalBookingSystem.Infrastructure.Email;

public enum EmailDeliveryMode
{
    /// <summary>Messages stay in the outbox (tests, or a host that only produces mail).</summary>
    None,
    /// <summary>Each message is written as an .eml file — local development and the public demo.</summary>
    Pickup,
    /// <summary>Real delivery through an SMTP server.</summary>
    Smtp
}

public sealed class EmailDeliveryOptions
{
    public EmailDeliveryMode Mode { get; set; } = EmailDeliveryMode.None;
    public string From { get; set; } = "Medical Booking System <no-reply@example.test>";
    public string PickupDirectory { get; set; } = "data/mail";
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUserName { get; set; }
    public string? SmtpPassword { get; set; }
    public bool SmtpEnableSsl { get; set; } = true;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);
}

public interface IEmailTransport
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>SmtpClient covers both SMTP delivery and the pickup directory (.eml files) without extra packages.</summary>
public sealed class SmtpEmailTransport(IOptions<EmailDeliveryOptions> options, Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment) : IEmailTransport
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var mail = new MailMessage { From = new MailAddress(settings.From), Subject = message.Subject, SubjectEncoding = Encoding.UTF8 };
        mail.To.Add(new MailAddress(message.To));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.TextBody, Encoding.UTF8, MediaTypeNames.Text.Plain));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));
        if (message.AttachmentName is not null && message.AttachmentContent is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(message.AttachmentContent);
            mail.Attachments.Add(new Attachment(new MemoryStream(bytes), message.AttachmentName,
                message.AttachmentContentType ?? MediaTypeNames.Application.Octet));
        }
        using var client = new SmtpClient();
        if (settings.Mode == EmailDeliveryMode.Pickup)
        {
            var directory = Path.GetFullPath(settings.PickupDirectory, environment.ContentRootPath);
            Directory.CreateDirectory(directory);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = directory;
        }
        else
        {
            client.Host = settings.SmtpHost;
            client.Port = settings.SmtpPort;
            client.EnableSsl = settings.SmtpEnableSsl;
            if (!string.IsNullOrEmpty(settings.SmtpUserName))
                client.Credentials = new NetworkCredential(settings.SmtpUserName, settings.SmtpPassword);
        }
        await client.SendMailAsync(mail, cancellationToken);
    }

    public static void Register(IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<EmailDeliveryOptions>(configuration.GetSection("Email"));
        services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
        services.AddSingleton<EmailDeliveryService>();
        services.AddHostedService(provider => provider.GetRequiredService<EmailDeliveryService>());
    }
}

/// <summary>Delivers queued messages with exponential back-off; a failing mail server never blocks user actions.</summary>
public sealed class EmailDeliveryService(IServiceScopeFactory scopes, IEmailTransport transport,
    IOptions<EmailDeliveryOptions> options, TimeProvider time, ILogger<EmailDeliveryService> logger) : BackgroundService
{
    public const int MaxAttempts = 10;
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.Mode == EmailDeliveryMode.None) return;
        using var timer = new PeriodicTimer(options.Value.PollInterval, time);
        do
        {
            try { await DeliverPendingAsync(stoppingToken); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Email outbox processing failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> DeliverPendingAsync(CancellationToken cancellationToken)
    {
        // The context factory is scoped in this application, so each batch gets its own scope.
        await using var scope = scopes.CreateAsyncScope();
        await using var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow().UtcDateTime;
        var pending = await context.EmailOutbox
            .Where(m => m.SentAtUtc == null && m.Attempts < MaxAttempts && m.NextAttemptAtUtc <= now)
            .OrderBy(m => m.Id).Take(BatchSize).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var message in pending)
        {
            try
            {
                await transport.SendAsync(message, cancellationToken);
                message.SentAtUtc = time.GetUtcNow().UtcDateTime;
                message.LastError = null;
                sent++;
            }
            catch (Exception exception) when (exception is SmtpException or IOException or InvalidOperationException or FormatException)
            {
                message.Attempts++;
                message.NextAttemptAtUtc = now + TimeSpan.FromMinutes(Math.Min(Math.Pow(2, message.Attempts), 360));
                message.LastError = exception.Message.Length > 1000 ? exception.Message[..1000] : exception.Message;
                logger.LogWarning("Email {Id} delivery failed (attempt {Attempt}): {Error}", message.Id, message.Attempts, message.LastError);
            }
            await context.SaveChangesAsync(cancellationToken);
        }
        return sent;
    }

    public static void Register(IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<EmailDeliveryOptions>(configuration.GetSection("Email"));
        services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
        services.AddSingleton<EmailDeliveryService>();
        services.AddHostedService(provider => provider.GetRequiredService<EmailDeliveryService>());
    }
}
