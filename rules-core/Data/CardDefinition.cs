// Port of game-core/src/main/java/com/infiniteconquest/core/CardDefinition.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using InfiniteConquest.RulesCore.Core; // TerrainRules, DevelopmentRules (ported in the Core module)
using DevPassive = InfiniteConquest.RulesCore.Data.DevelopmentPassive; // alias: the DevelopmentPassive property would otherwise shadow the enum in expressions

namespace InfiniteConquest.RulesCore.Data
{
    /// <summary>
    /// Mirrors Java record CardDefinition, including the compact-constructor
    /// validation (same checks, same order) and the convenience constructors.
    /// Java Set.copyOf/List.copyOf/Map.copyOf become defensive copies exposed
    /// as IReadOnlySet/IReadOnlyList/IReadOnlyDictionary. The keywordValues map
    /// is a SortedDictionary (CONVENTIONS.md: no Dictionary iteration in logic;
    /// enum keys sort by declaration order, deterministically).
    /// </summary>
    public record CardDefinition
    {
        private static readonly Regex ArchetypePattern = new Regex("^[A-Z][A-Z_]*$", RegexOptions.Compiled);

        public string Id { get; }
        public string Name { get; }
        public CardType Type { get; }
        public string? Faction { get; }
        public int Cost { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int Movement { get; }
        public int Range { get; }
        public int HitPoints { get; }
        public IReadOnlySet<Keyword> Keywords { get; }
        public IReadOnlyList<SpellEffect> Effects { get; }
        public int GpGeneration { get; }
        public DevelopmentPassive DevelopmentPassive { get; }
        public IReadOnlyList<CardAbility> Abilities { get; }
        public IReadOnlyDictionary<Keyword, KeywordValue> KeywordValues { get; }
        public IReadOnlySet<string> Archetypes { get; }
        public int DevelopmentGoldCost { get; }

        public CardDefinition(
            string id, string name, CardType type, string? faction, int cost,
            int attack, int defense, int movement, int range, int hitPoints,
            IEnumerable<Keyword>? keywords, IEnumerable<SpellEffect>? effects,
            int gpGeneration, DevelopmentPassive developmentPassive, IEnumerable<CardAbility>? abilities,
            IReadOnlyDictionary<Keyword, KeywordValue>? keywordValues, IEnumerable<string>? archetypes, int developmentGoldCost)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Stable card ID is required", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name", nameof(name));
            if (cost < 0 || attack < 0 || defense < 0 || movement < 0 || range < 0 || hitPoints < 0 || gpGeneration < 0)
                throw new ArgumentException("Card numbers cannot be negative");
            if (IsPermanent(type) && hitPoints == 0)
                throw new ArgumentException("Lands, Structures and Capitals require positive HP");

            var keywordSet = keywords == null ? new HashSet<Keyword>() : new HashSet<Keyword>(keywords);
            var effectList = effects == null ? new List<SpellEffect>() : new List<SpellEffect>(effects);
            var developmentPassiveValue = developmentPassive;
            var abilityList = abilities == null ? new List<CardAbility>() : new List<CardAbility>(abilities);
            var keywordValueMap = new SortedDictionary<Keyword, KeywordValue>();
            if (keywordValues != null)
                foreach (var kv in keywordValues) keywordValueMap.Add(kv.Key, kv.Value);
            var archetypeSet = archetypes == null ? new HashSet<string>() : new HashSet<string>(archetypes);

            if (archetypeSet.Any(a => !ArchetypePattern.IsMatch(a)))
                throw new ArgumentException("Archetypes use uppercase identifiers");
            if (keywordValueMap.Keys.Any(k => !keywordSet.Contains(k)))
                throw new ArgumentException("Keyword values require matching keywords");
            bool development = type == CardType.LAND || type == CardType.STRUCTURE;
            if (developmentGoldCost < 0 || (!development && developmentGoldCost != 0))
                throw new ArgumentException("Only developments may have a nonnegative development gold cost");
            foreach (var entry in keywordValueMap)
            {
                if (!TerrainRules.LandKeywords().Contains(entry.Key) && !TerrainRules.StructureKeywords().Contains(entry.Key))
                    throw new ArgumentException("Only development keywords accept numeric values");
                if (!new HashSet<Keyword> { Keyword.TURRET, Keyword.MEDIC_TENT, Keyword.WORKSHOP, Keyword.BEACON }.Contains(entry.Key)
                    && entry.Value.Range != 0)
                    throw new ArgumentException("This keyword affects only its own hex");
            }
            foreach (var keyword in keywordSet)
            {
                if (TerrainRules.LandKeywords().Contains(keyword) && type != CardType.LAND)
                    throw new ArgumentException("Land keyword on non-Land");
                if (TerrainRules.StructureKeywords().Contains(keyword) && type != CardType.STRUCTURE)
                    throw new ArgumentException("Structure keyword on non-Structure");
            }
            if (type != CardType.LAND && type != CardType.STRUCTURE
                    && (gpGeneration != 0 || developmentPassiveValue != DevPassive.NONE))
                throw new ArgumentException("Only Lands and Structures may generate GP or use development passives");
            if (type == CardType.SPELL && effectList.Count == 0)
                throw new ArgumentException("Spells require at least one typed effect");
            if (type != CardType.SPELL && effectList.Count != 0)
                throw new ArgumentException("Only Spells may define spell effects");

            Id = id;
            Name = name;
            Type = type;
            Faction = faction;
            Cost = cost;
            Attack = attack;
            Defense = defense;
            Movement = movement;
            Range = range;
            HitPoints = hitPoints;
            Keywords = keywordSet;
            Effects = effectList;
            GpGeneration = gpGeneration;
            DevelopmentPassive = developmentPassiveValue;
            Abilities = abilityList;
            KeywordValues = keywordValueMap;
            Archetypes = archetypeSet;
            DevelopmentGoldCost = developmentGoldCost;
        }

        public CardDefinition(string id, string name, CardType type, string? faction, int cost,
                              int attack, int defense, int movement, int range, int hitPoints,
                              IEnumerable<Keyword>? keywords, IEnumerable<SpellEffect>? effects,
                              int gpGeneration, DevelopmentPassive developmentPassive, IEnumerable<CardAbility>? abilities)
            : this(id, name, type, faction, cost, attack, defense, movement, range, hitPoints, keywords, effects,
                   gpGeneration, developmentPassive, abilities,
                   new Dictionary<Keyword, KeywordValue>(), new HashSet<string>(), 0)
        {
        }

        public CardDefinition(string id, string name, CardType type, string? faction, int cost,
                              int attack, int defense, int movement, int range, int hitPoints,
                              IEnumerable<Keyword>? keywords, IEnumerable<SpellEffect>? effects,
                              int gpGeneration, DevelopmentPassive developmentPassive)
            : this(id, name, type, faction, cost, attack, defense, movement, range, hitPoints,
                   keywords, effects, gpGeneration, developmentPassive, new List<CardAbility>())
        {
        }

        public CardDefinition(string id, string name, CardType type, string? faction, int cost,
                              int attack, int defense, int movement, int range, int hitPoints,
                              IEnumerable<Keyword>? keywords, IEnumerable<SpellEffect>? effects)
            : this(id, name, type, faction, cost, attack, defense, movement, range, hitPoints,
                   keywords, effects, DevelopmentRules.StandardGp(type, cost), DevPassive.NONE, new List<CardAbility>())
        {
        }

        public CardDefinition(string id, string name, CardType type, string? faction, int cost,
                              int attack, int defense, int movement, int range, int hitPoints,
                              IEnumerable<Keyword>? keywords)
            : this(id, name, type, faction, cost, attack, defense, movement, range, hitPoints,
                   keywords, new List<SpellEffect>())
        {
        }

        public CardDefinition(string id, string name, CardType type, string? faction, int cost,
                              int attack, int defense, int movement, int range, int hitPoints)
            : this(id, name, type, faction, cost, attack, defense, movement, range, hitPoints,
                   new HashSet<Keyword>(), new List<SpellEffect>())
        {
        }

        public CardDefinition(string id, string name, CardType type, string? faction, int cost,
                              int attack, int defense, int movement, int range)
            : this(id, name, type, faction, cost, attack, defense, movement, range,
                   IsPermanent(type) ? 1 : 0, new HashSet<Keyword>(), new List<SpellEffect>())
        {
        }

        /// <summary>Mirrors Java keywordValue(Keyword): map lookup with TerrainRules default fallback.</summary>
        public KeywordValue KeywordValue(Keyword keyword)
        {
            return KeywordValues.TryGetValue(keyword, out var value) ? value : TerrainRules.DefaultValue(keyword);
        }

        /// <summary>Mirrors Java goldCost(): developments use the development gold cost.</summary>
        public int GoldCost() => Type == CardType.LAND || Type == CardType.STRUCTURE ? DevelopmentGoldCost : Cost;

        /// <summary>Mirrors Java income(): GP generation plus FERTILE bonus.</summary>
        public int Income() => GpGeneration + (HasKeyword(Keyword.FERTILE) ? KeywordValue(Keyword.FERTILE).Amount : 0);

        public bool HasKeyword(Keyword keyword) => Keywords.Contains(keyword);
        public bool IsPermanent() => IsPermanent(Type);

        private static bool IsPermanent(CardType type)
        {
            return type == CardType.LAND || type == CardType.STRUCTURE || type == CardType.CAPITAL;
        }
    }
}
