using System.Text.Json.Serialization;

namespace ServerGuard.Api.Notifications;

/// <summary>Telegram Bot API <c>sendMessage</c> gövdesi.</summary>
public sealed record TelegramSendMessageRequest(
    [property: JsonPropertyName("chat_id")] string ChatId,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("parse_mode")] string ParseMode);
