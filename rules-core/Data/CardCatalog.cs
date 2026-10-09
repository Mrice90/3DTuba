// Port of game-core/src/main/java/com/infiniteconquest/data/CardCatalog.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfiniteConquest.RulesCore.Data
{
    /// <summary>
    /// Mirrors Java CardCatalog. Jackson ObjectMapper is replaced with
    /// System.Text.Json (no Jackson in the C# port). Same load semantics:
    /// schema-version gate, null/duplicate-ID rejection, and a toDefinition()
    /// validation pass over every card.
    ///
    /// Deviations from the Java API (flagged for Astra):
    /// - loadResource(String) (classpath resource) becomes LoadFromFile(string)
    ///   (filesystem path); embedded-resource loading can be added if desired.
    /// - The Java static ObjectMapper is a static JsonSerializerOptions here.
    /// </summary>
    public sealed class CardCatalog
    {
        public const int SupportedSchemaVersion = 1;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly List<CardData> _cards;

        private CardCatalog(List<CardData> cards)
        {
            _cards = cards;
        }

        public static CardCatalog Load(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            CatalogDocument? document;
            try
            {
                document = JsonSerializer.Deserialize<CatalogDocument>(input, JsonOptions);
            }
            catch (Exception exception) when (exception is JsonException || exception is IOException || exception is ArgumentException)
            {
                throw new ArgumentException("Invalid card catalog JSON", exception);
            }

            if (document == null) throw new ArgumentException("Invalid card catalog JSON");
            if (document.SchemaVersion != SupportedSchemaVersion)
                throw new ArgumentException("Unsupported card schema version: " + document.SchemaVersion);
            if (document.Cards == null)
                throw new ArgumentException("Catalog cards are required");

            var ids = new HashSet<string>();
            foreach (var card in document.Cards)
            {
                if (card == null) throw new ArgumentException("Catalog cannot contain null cards");
                if (!ids.Add(card.Id)) throw new ArgumentException("Duplicate card ID: " + card.Id);
                card.ToDefinition();
            }
            return new CardCatalog(new List<CardData>(document.Cards));
        }

        /// <summary>
        /// Filesystem counterpart to Java's loadResource (classpath) lookup.
        /// </summary>
        public static CardCatalog LoadFromFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            using (var input = File.OpenRead(path))
            {
                return Load(input);
            }
        }

        public IReadOnlyList<CardData> Cards => _cards;

        public IReadOnlyList<CardDefinition> Definitions() => _cards.Select(c => c.ToDefinition()).ToList();

        public CardData Require(string id)
        {
            return _cards.FirstOrDefault(card => card.Id == id)
                ?? throw new ArgumentException("Unknown card ID: " + id);
        }

        private sealed record CatalogDocument(
            [JsonPropertyName("schemaVersion")] int SchemaVersion,
            [JsonPropertyName("cards")] List<CardData>? Cards);
    }
}
