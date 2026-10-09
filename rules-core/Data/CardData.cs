// Port of game-core/src/main/java/com/infiniteconquest/data/CardData.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using InfiniteConquest.RulesCore.Core; // DevelopmentRules (ported in the Core module)
using DevPassive = InfiniteConquest.RulesCore.Data.DevelopmentPassive; // alias: the DevelopmentPassive property would otherwise shadow the enum in expressions

namespace InfiniteConquest.RulesCore.Data
{
    /// <summary>
    /// Mirrors Java record CardData, including the compact-constructor validation.
    /// JSON property names are bound explicitly via JsonPropertyName to the
    /// catalog's camelCase shape (Jackson field names in Java). Enum members stay
    /// UPPER_SNAKE; deserialization uses JsonStringEnumConverter (see CardCatalog).
    /// Note the Java field order (range before movement) is preserved in the
    /// constructor signature.
    /// </summary>
    public record CardData
    {
        private static readonly Regex IdPattern = new Regex("^[a-z0-9]+(?:_[a-z0-9]+)*$", RegexOptions.Compiled);

        [JsonPropertyName("id")] public string Id { get; }
        [JsonPropertyName("name")] public string Name { get; }
        [JsonPropertyName("type")] public CardType Type { get; }
        [JsonPropertyName("faction")] public string Faction { get; }
        [JsonPropertyName("cost")] public int Cost { get; }
        [JsonPropertyName("attack")] public int Attack { get; }
        [JsonPropertyName("defense")] public int Defense { get; }
        [JsonPropertyName("range")] public int Range { get; }
        [JsonPropertyName("movement")] public int Movement { get; }
        [JsonPropertyName("hitPoints")] public int HitPoints { get; }
        [JsonPropertyName("keywords")] public IReadOnlyList<Keyword> Keywords { get; }
        [JsonPropertyName("effects")] public IReadOnlyList<SpellEffect> Effects { get; }
        [JsonPropertyName("rulesText")] public string RulesText { get; }
        [JsonPropertyName("description")] public string Description { get; }
        [JsonPropertyName("rarity")] public int Rarity { get; }
        [JsonPropertyName("contentStatus")] public ContentStatus ContentStatus { get; }
        [JsonPropertyName("gpGeneration")] public int? GpGeneration { get; }
        [JsonPropertyName("developmentPassive")] public DevelopmentPassive? DevelopmentPassive { get; }
        [JsonPropertyName("abilities")] public IReadOnlyList<CardAbility> Abilities { get; }
        [JsonPropertyName("keywordValues")] public IReadOnlyDictionary<Keyword, KeywordValue> KeywordValues { get; }
        [JsonPropertyName("archetypes")] public IReadOnlySet<string> Archetypes { get; }
        [JsonPropertyName("developmentGoldCost")] public int DevelopmentGoldCost { get; }

        [JsonConstructor]
        public CardData(
            string id, string name, CardType type, string faction, int cost,
            int attack, int defense, int range, int movement, int hitPoints,
            List<Keyword>? keywords, List<SpellEffect>? effects,
            string? rulesText, string? description, int rarity, ContentStatus contentStatus,
            int? gpGeneration, DevelopmentPassive? developmentPassive, List<CardAbility>? abilities,
            Dictionary<Keyword, KeywordValue>? keywordValues, HashSet<string>? archetypes, int developmentGoldCost)
        {
            if (id == null || !IdPattern.IsMatch(id))
                throw new ArgumentException("Card ID must be a stable lowercase snake_case identifier", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Card name is required", nameof(name));
            if (string.IsNullOrWhiteSpace(faction)) throw new ArgumentException("Faction is required", nameof(faction));
            if (cost < 0 || attack < 0 || defense < 0 || range < 0 || movement < 0 || hitPoints < 0 || rarity < 0
                    || (gpGeneration != null && gpGeneration < 0))
                throw new ArgumentException("Card numbers cannot be negative");

            var keywordList = keywords == null ? new List<Keyword>() : new List<Keyword>(keywords);
            var effectList = effects == null ? new List<SpellEffect>() : new List<SpellEffect>(effects);
            var abilityList = abilities == null ? new List<CardAbility>() : new List<CardAbility>(abilities);
            // Java also null-checks keyword elements; Keyword is a non-nullable value
            // type in C#, so that check is vacuous here.
            if (effectList.Any(e => e == null) || abilityList.Any(a => a == null))
                throw new ArgumentException("Keywords, effects and abilities cannot contain null");

            Id = id;
            Name = name;
            Type = type;
            Faction = faction;
            Cost = cost;
            Attack = attack;
            Defense = defense;
            Range = range;
            Movement = movement;
            HitPoints = hitPoints;
            Keywords = keywordList;
            Effects = effectList;
            RulesText = rulesText ?? "";
            Description = description ?? "";
            Rarity = rarity;
            ContentStatus = contentStatus;
            GpGeneration = gpGeneration;
            DevelopmentPassive = developmentPassive;
            Abilities = abilityList;
            KeywordValues = keywordValues == null
                ? new SortedDictionary<Keyword, KeywordValue>()
                : new SortedDictionary<Keyword, KeywordValue>(keywordValues);
            Archetypes = archetypes == null ? new HashSet<string>() : new HashSet<string>(archetypes);
            DevelopmentGoldCost = developmentGoldCost;
        }

        /// <summary>Mirrors Java toDefinition().</summary>
        public CardDefinition ToDefinition()
        {
            return new CardDefinition(Id, Name, Type, Faction, Cost, Attack, Defense, Movement, Range,
                    HitPoints, new HashSet<Keyword>(Keywords), Effects,
                    GpGeneration ?? DevelopmentRules.StandardGp(Type, Cost),
                    DevelopmentPassive ?? DevPassive.NONE,
                    Abilities, KeywordValues, Archetypes, DevelopmentGoldCost);
        }
    }
}
