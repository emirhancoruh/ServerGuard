using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServerGuard.Shared;

/// <summary>
/// Agent, Api ve web istemcisinin ortak JSON sözleşmesi. Enum'lar sayı yerine adlarıyla
/// taşınır; böylece yeni bir enum değeri eklendiğinde mevcut sıralamaya bağımlılık oluşmaz
/// ve gövde insan tarafından okunabilir kalır.
/// </summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
