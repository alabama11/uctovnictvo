using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Uctovnictvo.Core.Services;

public record EmailSprava(string Komu, string MenoPrijemcu, string Predmet, string Text, IReadOnlyList<(string Nazov, byte[] Obsah)> Prilohy);

/// <summary>Odosielanie e-mailov cez SMTP server (napr. Gmail, Outlook, poskytovateľ domény).</summary>
public static class Email
{
    public static MimeMessage Vytvor(EmailNastavenia n, EmailSprava s)
    {
        var m = new MimeMessage();
        m.From.Add(new MailboxAddress(string.IsNullOrWhiteSpace(n.MenoOdosielatela) ? n.Odosielatel : n.MenoOdosielatela, n.Odosielatel));
        foreach (var adresa in s.Komu.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            m.To.Add(new MailboxAddress(s.MenoPrijemcu, adresa));
        m.Subject = s.Predmet;
        var b = new BodyBuilder { TextBody = s.Text };
        foreach (var (nazov, obsah) in s.Prilohy)
            b.Attachments.Add(nazov, obsah, new ContentType("application", "pdf"));
        m.Body = b.ToMessageBody();
        return m;
    }

    /// <summary>Odošle správy jedným spojením; vráti zoznam chýb (prázdny = všetko odišlo).</summary>
    public static async Task<List<string>> Odosli(EmailNastavenia n, IReadOnlyList<EmailSprava> spravy, IProgress<int>? priebeh = null, CancellationToken ct = default)
    {
        var chyby = new List<string>();
        using var smtp = new SmtpClient();
        smtp.Timeout = 30000;
        await smtp.ConnectAsync(n.Server, n.Port, n.Ssl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, ct);
        if (!string.IsNullOrEmpty(n.Pouzivatel))
            await smtp.AuthenticateAsync(n.Pouzivatel, n.Heslo, ct);
        for (int i = 0; i < spravy.Count; i++)
        {
            try
            {
                await smtp.SendAsync(Vytvor(n, spravy[i]), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                chyby.Add($"{spravy[i].Komu}: {ex.Message}");
            }
            priebeh?.Report(i + 1);
        }
        await smtp.DisconnectAsync(true, ct);
        return chyby;
    }

    public static bool JePlatnaAdresa(string? adresa) =>
        !string.IsNullOrWhiteSpace(adresa) && adresa.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(a => MailboxAddress.TryParse(a, out _) && a.Contains('@'));
}
