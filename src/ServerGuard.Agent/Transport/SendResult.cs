namespace ServerGuard.Agent.Transport;

public enum SendResult
{
    /// <summary>Backend kabul etti.</summary>
    Sent,

    /// <summary>Backend'e ulaşılamadı veya geçici hata; kayıt kuyrukta kalmalı.</summary>
    Unavailable,

    /// <summary>Backend kaydı kalıcı olarak reddetti (ör. 400); tekrar denemek anlamsız.</summary>
    Rejected
}
