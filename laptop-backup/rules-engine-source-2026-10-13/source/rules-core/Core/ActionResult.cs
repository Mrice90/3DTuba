// Port of game-core/src/main/java/com/infiniteconquest/core/ActionResult.java @ Desolate-Tuba e0e565b
namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Engine action outcome. Positional names stay lowercase to match the Java
    /// record exactly and to avoid colliding with the Accepted/Rejected factories.
    /// </summary>
    public record ActionResult(bool accepted, string message)
    {
        public static ActionResult Accepted(string message) => new ActionResult(true, message);
        public static ActionResult Rejected(string message) => new ActionResult(false, message);
    }
}
