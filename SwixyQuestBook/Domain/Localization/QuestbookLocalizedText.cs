using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwixyQuestBook.Domain.Localization
{
    /// <summary>
    /// Multi-language text stored in quest JSON.
    /// JSON may be a plain string (legacy) or a map: { "en": "...", "ru": "..." }.
    /// </summary>
    [JsonConverter(typeof(QuestbookLocalizedTextJsonConverter))]
    public sealed class QuestbookLocalizedText
    {
        public const string DefaultLang = "en";

        private readonly Dictionary<string, string> entries =
            new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, string> Entries => entries;

        public bool IsEmpty => entries.Count == 0
            || entries.Values.All(string.IsNullOrWhiteSpace);

        public QuestbookLocalizedText()
        {
        }

        public QuestbookLocalizedText(string? singleValue, string lang = DefaultLang)
        {
            if (!string.IsNullOrWhiteSpace(singleValue))
                entries[NormalizeLang(lang)] = singleValue.Trim();
        }

        public QuestbookLocalizedText(IDictionary<string, string>? map)
        {
            if (map == null)
                return;

            foreach ((string lang, string value) in map)
                Set(lang, value);
        }

        public static string NormalizeLang(string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
                return DefaultLang;

            string code = lang.Trim().Replace('_', '-');
            int dash = code.IndexOf('-');
            if (dash > 0)
                code = code[..dash];

            return code.ToLowerInvariant();
        }

        public void Set(string? lang, string? value)
        {
            string key = NormalizeLang(lang);
            if (string.IsNullOrWhiteSpace(value))
            {
                entries.Remove(key);
                return;
            }

            entries[key] = value.Trim();
        }

        public void MergeFrom(QuestbookLocalizedText? other)
        {
            if (other == null)
                return;

            foreach ((string lang, string value) in other.entries)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    entries[lang] = value;
            }
        }

        /// <summary>Prefer requested language, then en, then any non-empty entry.</summary>
        public string Resolve(string? lang, string fallbackLang = DefaultLang)
        {
            string requested = NormalizeLang(lang);
            if (entries.TryGetValue(requested, out string? direct) && !string.IsNullOrWhiteSpace(direct))
                return direct;

            string fallback = NormalizeLang(fallbackLang);
            if (entries.TryGetValue(fallback, out string? fb) && !string.IsNullOrWhiteSpace(fb))
                return fb;

            foreach (string value in entries.Values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        public QuestbookLocalizedText Clone() => new(entries);

        public override string ToString() => Resolve(DefaultLang);
    }

    public sealed class QuestbookLocalizedTextJsonConverter : JsonConverter<QuestbookLocalizedText>
    {
        public override QuestbookLocalizedText Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return new QuestbookLocalizedText();

            if (reader.TokenType == JsonTokenType.String)
                return new QuestbookLocalizedText(reader.GetString());

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;

                    if (reader.TokenType != JsonTokenType.PropertyName)
                        continue;

                    string lang = reader.GetString() ?? QuestbookLocalizedText.DefaultLang;
                    reader.Read();
                    string value = reader.TokenType == JsonTokenType.String
                        ? (reader.GetString() ?? string.Empty)
                        : string.Empty;
                    if (!string.IsNullOrWhiteSpace(value))
                        map[QuestbookLocalizedText.NormalizeLang(lang)] = value.Trim();
                }

                return new QuestbookLocalizedText(map);
            }

            throw new JsonException("Localized text must be a string or language map object.");
        }

        public override void Write(
            Utf8JsonWriter writer,
            QuestbookLocalizedText value,
            JsonSerializerOptions options)
        {
            if (value == null || value.IsEmpty)
            {
                writer.WriteStringValue(string.Empty);
                return;
            }

            writer.WriteStartObject();
            foreach ((string lang, string text) in value.Entries.OrderBy(static e => e.Key, StringComparer.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(text))
                    writer.WriteString(lang, text);
            }

            writer.WriteEndObject();
        }
    }
}
