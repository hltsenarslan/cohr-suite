using MailKit.Net.Smtp;
using MimeKit;
using Notification.Api.Models;

namespace Notification.Api.Services;

public interface IMailer
{
    Task SendAsync(MailSetting cfg, MailQueueItem item, IEnumerable<(Stream stream,string fileName)>? attachments, CancellationToken ct);
}

public class Mailer : IMailer
{
    public async Task SendAsync(MailSetting cfg, MailQueueItem item, IEnumerable<(Stream,string)>? attachments, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(cfg.FromName, cfg.FromEmail));

        foreach (var to in item.ToList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            message.To.Add(MailboxAddress.Parse(to));

        message.Subject = item.Subject;

        var builder = new BodyBuilder { HtmlBody = item.HtmlBody };

        if (attachments != null)
        {
            foreach (var (stream, filename) in attachments)
            {
                // stream’in başını garanti et
                if (stream.CanSeek) stream.Position = 0;
                builder.Attachments.Add(filename, stream);
            }
        }

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(cfg.Host, cfg.Port, cfg.UseSsl, ct);
        await client.AuthenticateAsync(cfg.Username, cfg.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}