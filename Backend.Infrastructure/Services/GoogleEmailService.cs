using System;
using Backend.Application.Interfaces;
using Backend.Infrastructure.DependencyInjection.Options;
using Backend.Share.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Backend.Infrastructure.Services;

public class GoogleEmailService : IEmailService<GoogleMailRequest>
{
    private readonly ILogger<GoogleEmailService> _logger;
    private readonly SmtpSettings _smtpSettings;
    public GoogleEmailService(ILoggerFactory loggerFactory, IOptions<SmtpSettings> smtpSettings)
    {
        _logger = loggerFactory.CreateLogger<GoogleEmailService>();
        _smtpSettings = smtpSettings.Value;
    }
    public async Task SendMailAsync(GoogleMailRequest request)
    {
        try
        {
            var emailMessage = new MimeMessage
            {
                Sender = new MailboxAddress(_smtpSettings.SenderName, _smtpSettings.UserName),
                Subject = request.Subject,
                Body = new BodyBuilder
                {
                    HtmlBody = request.Body
                }.ToMessageBody()
            };

            if (request.ToEmails.Any())
            {
                foreach (var email in request.ToEmails)
                {
                    emailMessage.To.Add(MailboxAddress.Parse(email));
                }
            }

            if (request.CcEmails.Any())
            {
                foreach (var email in request.CcEmails)
                {
                    emailMessage.Cc.Add(MailboxAddress.Parse(email));
                }
            }

            if (request.BccEmails.Any())
            {
                foreach (var email in request.BccEmails)
                {
                    emailMessage.Bcc.Add(MailboxAddress.Parse(email));
                }
            }

            using (var client = new MailKit.Net.Smtp.SmtpClient())
            {
                await client.ConnectAsync(_smtpSettings.Server, _smtpSettings.Port, MailKit.Security.SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_smtpSettings.UserName, _smtpSettings.Password);
                await client.SendAsync(emailMessage);
                await client.DisconnectAsync(true);
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }
    }
}
