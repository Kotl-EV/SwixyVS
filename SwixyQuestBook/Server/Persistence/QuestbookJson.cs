using System.Text.Json;

namespace SwixyQuestBook.Server.Persistence
{
    /// <summary>Shared JSON options for quest / progress files.</summary>
    public static class QuestbookJson
    {
        public static JsonSerializerOptions CreateOptions(bool writeIndented = false) => new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = writeIndented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
