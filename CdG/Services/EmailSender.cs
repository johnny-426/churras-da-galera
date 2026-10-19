using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
namespace CdG.Services;

public class EmailSender(IConfiguration config, IWebHostEnvironment ambiente) : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var host = config["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            if (ambiente.IsDevelopment()) return; // Testes locais, sem confirmação obrigatória.
            throw new InvalidOperationException("Configure Smtp:Host para enviar mensagens da conta.");
        }
        using var client = new SmtpClient(host, config.GetValue("Smtp:Port", 587))
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(config["Smtp:Username"], config["Smtp:Password"])
        };
        using var message = new MailMessage(config["Smtp:From"] ?? throw new InvalidOperationException("Configure Smtp:From."), email, subject, htmlMessage) { IsBodyHtml = true };
        await client.SendMailAsync(message);
    }
}
