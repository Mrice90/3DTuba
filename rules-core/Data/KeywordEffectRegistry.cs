// Port of game-core/src/main/java/com/infiniteconquest/data/KeywordEffectRegistry.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;

namespace InfiniteConquest.RulesCore.Data
{
    /// <summary>
    /// Mirrors Java KeywordEffectRegistry&lt;C&gt;. The Java implementation uses an
    /// EnumMap; the C# port uses Dictionary&lt;Keyword, KeywordEffect&lt;C&gt;&gt;.
    /// No map iteration occurs in this class (lookup / ContainsKey only), so the
    /// CONVENTIONS.md Dictionary-iteration rule does not apply.
    /// </summary>
    /// <typeparam name="C">The context type keyword effects apply to.</typeparam>
    public sealed class KeywordEffectRegistry<C>
    {
        private readonly Dictionary<Keyword, KeywordEffect<C>> _handlers = new Dictionary<Keyword, KeywordEffect<C>>();

        public void Register(Keyword keyword, KeywordEffect<C> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            // putIfAbsent semantics: duplicate registration is an error.
            if (!_handlers.TryAdd(keyword, handler))
                throw new InvalidOperationException("Keyword already registered: " + keyword);
        }

        public KeywordEffect<C> Require(Keyword keyword)
        {
            if (_handlers.TryGetValue(keyword, out var handler)) return handler;
            throw new InvalidOperationException("No effect registered for keyword: " + keyword);
        }

        public bool Supports(Keyword keyword) => _handlers.ContainsKey(keyword);
    }
}
