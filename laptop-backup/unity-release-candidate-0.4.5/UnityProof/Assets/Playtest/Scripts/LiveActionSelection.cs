using System;
using System.Collections.Generic;
using System.Linq;

namespace InfiniteConquest.Playtest {
    // The bridge remains authoritative. This only groups its revision-scoped choices for the UI.
    public static class LiveActionSelection {
        public static WireHex Target(BridgeAction action) => action?.target ?? action?.to ?? action?.at ?? action?.destination;
        public static bool At(WireHex hex, int x, int y) => hex != null && hex.x == x && hex.y == y;
        public static bool Is(BridgeAction action, string type) => string.Equals(action?.type, type, StringComparison.OrdinalIgnoreCase);
        public static BridgeAction[] For(IEnumerable<BridgeAction> actions, string source) =>
            actions.Where(a => a != null && a.instance_id == source && !Is(a, "end_turn")).ToArray();
        public static BridgeAction[] AtTarget(IEnumerable<BridgeAction> actions, int x, int y) =>
            actions.Where(a => At(Target(a), x, y)).ToArray();
        public static BridgeAction[] AtDestination(IEnumerable<BridgeAction> actions, int x, int y) =>
            actions.Where(a => At(a.destination, x, y)).ToArray();
    }
}
