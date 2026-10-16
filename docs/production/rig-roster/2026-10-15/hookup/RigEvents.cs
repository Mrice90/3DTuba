using UnityEngine;

namespace InfiniteConquest.Playtest {
    // AI-060-RIG-COMBAT: receives the impact/down/land animation events baked into rigged token clips.
    // Unity logs an error for an animation event with no receiver, so every rigged token gets one.
    public sealed class RigEvents : MonoBehaviour {
        public event System.Action<string> Fired;
        public string Last { get; private set; }
        void OnRigEvent(string name) { Last = name; Fired?.Invoke(name); }
    }
}
