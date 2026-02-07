using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarketplaceSyncer.Service.BusinessRu.Http;

public class BusinessRuDateTimeConverter : JsonConverter<DateTimeOffset>
{
    // The user saw: "28.02.2025 14:10:21.552417"
    // Assuming these are in Moscow Time (UTC+3)
    private static readonly TimeSpan MoscowOffset = TimeSpan.FromHours(3);

    private readonly string[] _formats = new[]
    {
        "dd.MM.yyyy HH:mm:ss.ffffff",
        "dd.MM.yyyy HH:mm:ss.fffff",
        "dd.MM.yyyy HH:mm:ss.ffff",
        "dd.MM.yyyy HH:mm:ss.fff",
        "dd.MM.yyyy HH:mm:ss.ff",
        "dd.MM.yyyy HH:mm:ss.f",
        "dd.MM.yyyy HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "dd.MM.yyyy"
    };

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        if (string.IsNullOrEmpty(str)) 
            return default;

        // Clean up common suffixes
        var cleanStr = str.Replace(" MSK", "").Trim();

        foreach (var format in _formats)
        {
            if (DateTime.TryParseExact(cleanStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                // Treat the parsed date as unspecified (implicitly Moscow local time)
                // and apply the offset.
                return new DateTimeOffset(date, MoscowOffset);
            }
        }

        // Final fallback
        if (DateTime.TryParse(cleanStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
             return new DateTimeOffset(dt, MoscowOffset);
        }
            
        throw new JsonException($"Unable to convert \"{str}\" (clean: \"{cleanStr}\") to DateTimeOffset. Supported formats include: {string.Join(", ", _formats)}");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        // Convert back to Moscow time for writing, preserving the format
        var mskTime = value.ToOffset(MoscowOffset);
        writer.WriteStringValue(mskTime.ToString("dd.MM.yyyy HH:mm:ss.ffffff", CultureInfo.InvariantCulture));
    }
}
