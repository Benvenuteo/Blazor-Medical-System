using System.Net;
using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Application.Notifications;

/// <summary>Small, dependency-free templates. Every dynamic value is HTML-encoded.</summary>
public static class EmailTemplates
{
    public static EmailMessage Create(string to, string subject, string heading, string intro, string? actionText, string? actionUrl,
        string footer, DateTime nowUtc)
    {
        var text = $"{heading}\n\n{intro}\n" + (actionUrl is null ? "" : $"\n{actionText}: {actionUrl}\n") + $"\n{footer}\n\n— Medical Booking System";
        var button = actionUrl is null ? "" : $$"""
            <p style="margin:24px 0"><a href="{{Encode(actionUrl)}}" style="background:#0b5ed7;color:#fff;padding:12px 20px;border-radius:6px;text-decoration:none;display:inline-block">{{Encode(actionText!)}}</a></p>
            <p style="color:#555;font-size:13px">Jeśli przycisk nie działa, skopiuj adres:<br><span style="word-break:break-all">{{Encode(actionUrl)}}</span></p>
            """;
        var html = $$"""
            <!doctype html><html lang="pl"><body style="margin:0;background:#f4f6f9;font-family:Segoe UI,Arial,sans-serif;color:#1d2733">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center" style="padding:24px">
            <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="max-width:560px;background:#fff;border-radius:10px;overflow:hidden">
            <tr><td style="background:#092447;color:#fff;padding:16px 24px;font-size:18px;font-weight:600">Medical Booking System</td></tr>
            <tr><td style="padding:24px"><h1 style="font-size:20px;margin:0 0 12px">{{Encode(heading)}}</h1>
            <p style="line-height:1.5">{{Encode(intro)}}</p>{{button}}
            <p style="color:#555;font-size:13px;line-height:1.5">{{Encode(footer)}}</p></td></tr>
            </table></td></tr></table></body></html>
            """;
        return new EmailMessage { To = to, Subject = subject, TextBody = text, HtmlBody = html, CreatedAtUtc = nowUtc, NextAttemptAtUtc = nowUtc };
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
