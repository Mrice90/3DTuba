// Port of game-core/src/main/java/com/infiniteconquest/data/KeywordEffect.java @ Desolate-Tuba e0e565b
namespace InfiniteConquest.RulesCore.Data
{
    /// <summary>
    /// Mirrors Java's @FunctionalInterface KeywordEffect&lt;C&gt; { void apply(C context); }.
    /// The idiomatic C# equivalent is a delegate type.
    /// </summary>
    /// <typeparam name="C">The context type the keyword effect applies to.</typeparam>
    public delegate void KeywordEffect<C>(C context);
}
