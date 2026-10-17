using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using FolderSizeAurora.Models;

namespace FolderSizeAurora.Services;

public sealed record NotificationResult(string Channel, bool Success, string Detail)
{
    public string Status => Success ? "OK" : "Fallo";
}

/// <summary>
/// Despacha alertas por varios canales independientes. Un canal que falla nunca impide
/// que los demas se entreguen, y el resultado de cada intento se reporta a la interfaz.
/// </summary>
public sealed class NotificationService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly AppSettings _settings;
    private readonly Action<string, bool> _trace;

    public NotificationService(AppSettings settings, Action<string, bool>? trace = null)
    {
        _settings = settings;
        _trace = trace ?? ((_, _) => { });
    }

    public async Task<IReadOnlyList<NotificationResult>> SendAsync(AlertItem alert)
    {
        var results = new List<NotificationResult>();

        if (_settings.NotifyInApp)
        {
            results.Add(Ok("En la aplicacion"));
        }

        if (_settings.NotifySound)
        {
            results.Add(Sound());
        }

        if (_settings.NotifyEmail)
        {
            results.Add(await SendEmailAsync(alert));
        }

        if (_settings.NotifyWebhook)
        {
            results.Add(await SendWebhookAsync(alert));
        }

        results.Add(AppendToLogFile(alert));

        return results;
    }

    private NotificationResult Ok(string channel) => new(channel, true, "Entregado");

    private NotificationResult Sound()
    {
        try
        {
            System.Media.SystemSounds.Hand.Play();
            return Ok("Sonido");
        }
        catch (Exception ex)
        {
            return new NotificationResult("Sonido", false, ex.Message);
        }
    }

    private async Task<NotificationResult> SendEmailAsync(AlertItem alert)
    {
        if (string.IsNullOrWhiteSpace(_settings.SmtpHost) || string.IsNullOrWhiteSpace(_settings.EmailTo))
            return new NotificationResult("Correo", false, "Falta servidor SMTP o destinatario");

        try
        {
            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                EnableSsl = _settings.SmtpUseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(_settings.SmtpUser))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new System.Net.NetworkCredential(_settings.SmtpUser, _settings.SmtpPassword);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(string.IsNullOrWhiteSpace(_settings.EmailFrom) ? _settings.SmtpUser : _settings.EmailFrom, "Folder Size Aurora"),
                Subject = $"Limite de espacio superado: {Path.GetFileName(alert.Path.TrimEnd('\\'))}",
                Body = BuildBody(alert),
                IsBodyHtml = false
            };
            message.To.Add(_settings.EmailTo);

            await client.SendMailAsync(message);
            return Ok("Correo");
        }
        catch (Exception ex)
        {
            _trace($"Correo: {ex.Message}", true);
            return new NotificationResult("Correo", false, ex.Message);
        }
    }

    private async Task<NotificationResult> SendWebhookAsync(AlertItem alert)
    {
        if (string.IsNullOrWhiteSpace(_settings.WebhookUrl))
            return new NotificationResult("Webhook", false, "URL vacia");

        try
        {
            var payload = new
            {
                origen = "FolderSizeAurora",
                severidad = "warning",
                fecha = alert.Timestamp.ToString("O"),
                ruta = alert.Path,
                mensaje = alert.Message,
                bytes_actuales = alert.CurrentBytes,
                bytes_limite = alert.LimitBytes
            };

            using var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await Http.PostAsync(_settings.WebhookUrl, content);
            return response.IsSuccessStatusCode
                ? Ok("Webhook")
                : new NotificationResult("Webhook", false, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            _trace($"Webhook: {ex.Message}", true);
            return new NotificationResult("Webhook", false, ex.Message);
        }
    }

    private NotificationResult AppendToLogFile(AlertItem alert)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FolderSizeAurora");

            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "alertas.log");
            File.AppendAllText(path,
                $"[{alert.Date}] {alert.Path} | {alert.Message}{Environment.NewLine}",
                Encoding.UTF8);
            return Ok("Registro local");
        }
        catch (Exception ex)
        {
            return new NotificationResult("Registro local", false, ex.Message);
        }
    }

    private static string BuildBody(AlertItem alert) =>
        $"Se supero el limite de espacio configurado.{Environment.NewLine}{Environment.NewLine}" +
        $"Ruta      : {alert.Path}{Environment.NewLine}" +
        $"Uso       : {Formatter.Bytes(alert.CurrentBytes)}{Environment.NewLine}" +
        $"Limite    : {Formatter.Bytes(alert.LimitBytes)}{Environment.NewLine}" +
        $"Hora      : {alert.Date}";
}