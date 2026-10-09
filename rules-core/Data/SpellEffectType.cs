// Port of game-core/src/main/java/com/infiniteconquest/core/SpellEffectType.java @ Desolate-Tuba e0e565b
namespace InfiniteConquest.RulesCore.Data
{
    /// <summary>Mirrors Java SpellEffectType. UPPER_SNAKE member names kept for wire parity.</summary>
    public enum SpellEffectType
    {
        STRIKE_CHARACTER,
        DAMAGE_PERMANENT,
        HEAL_PERMANENT,
        TELEPORT_CHARACTER,
        RETURN_CHARACTER,
        BUFF_ATTACK,
        BUFF_DEFENSE
    }
}
