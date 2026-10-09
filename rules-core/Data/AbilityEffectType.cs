// Port of game-core/src/main/java/com/infiniteconquest/core/AbilityEffectType.java @ Desolate-Tuba e0e565b
namespace InfiniteConquest.RulesCore.Data
{
    /// <summary>Mirrors Java AbilityEffectType. UPPER_SNAKE member names kept for wire parity.</summary>
    public enum AbilityEffectType
    {
        DRAW_CARD,
        DRAW_CHARACTER,
        DRAW_STRUCTURE,
        GAIN_GP,
        HEAL_SELF,
        HEAL_CAPITAL,
        BUFF_SELF_ATTACK,
        BUFF_SELF_DEFENSE,
        DAMAGE_ENEMY_CAPITAL
    }
}
