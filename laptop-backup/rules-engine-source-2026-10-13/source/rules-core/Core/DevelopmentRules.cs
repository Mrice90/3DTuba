// Port of game-core/src/main/java/com/infiniteconquest/core/DevelopmentRules.java @ Desolate-Tuba e0e565b
using System;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    public static class DevelopmentRules
    {
        public static int StandardGp(CardType type, int developmentTurn)
        {
            if (type != CardType.LAND && type != CardType.STRUCTURE) return 0;
            int turn = Math.Max(1, developmentTurn);
            return Math.Min(5, 1 + (turn - 1) / 2);
        }

        public static string PassiveText(DevelopmentPassive passive) => passive switch
        {
            DevelopmentPassive.NONE => "",
            DevelopmentPassive.DRAW_ON_DEPLOY => "Deploy: draw 1 card.",
            DevelopmentPassive.HEAL_CAPITAL_ON_DEPLOY => "Deploy: heal your Capital for 3.",
            DevelopmentPassive.SELF_REPAIR => "Start of your turn: heal 2 damage from this card.",
            _ => throw new ArgumentOutOfRangeException(nameof(passive)),
        };
    }
}
