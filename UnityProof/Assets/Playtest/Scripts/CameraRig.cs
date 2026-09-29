using UnityEngine;
using UnityEngine.InputSystem;

namespace InfiniteConquest.Playtest {
    // Orbit / zoom / pan camera: right-drag or Q/E to orbit, wheel to zoom, middle-drag or WASD to pan,
    // Home (or the HUD button) to reset. Smoothly eases toward the target pose.
    public sealed class CameraRig : MonoBehaviour {
        public Vector3 Target, HomeTarget;
        public float Yaw = -28, Pitch = 52, Distance = 11.5f;
        public float HomeYaw = -28, HomePitch = 52, HomeDistance = 11.5f;
        public float MinDistance = 3, MaxDistance = 40;
        public bool InputEnabled = true;
        Vector3 curTarget; float curYaw, curPitch, curDist;
        Vector2 lastMouse;

        public void SetHome(Vector3 target, float yaw, float pitch, float distance, bool snap) {
            HomeTarget = Target = target; HomeYaw = Yaw = yaw; HomePitch = Pitch = pitch; HomeDistance = Distance = distance;
            if (snap) Snap();
        }
        public void Look(Vector3 target, float yaw, float pitch, float distance, bool snap) {
            Target = target; Yaw = yaw; Pitch = pitch; Distance = distance; if (snap) Snap();
        }
        public void ResetView() { Target = HomeTarget; Yaw = HomeYaw; Pitch = HomePitch; Distance = HomeDistance; }
        public void Snap() { curTarget = Target; curYaw = Yaw; curPitch = Pitch; curDist = Distance; Apply(); }

        void LateUpdate() {
            if (InputEnabled) HandleInput();
            float k = 1 - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            curTarget = Vector3.Lerp(curTarget, Target, k);
            curYaw = Mathf.LerpAngle(curYaw, Yaw, k); curPitch = Mathf.Lerp(curPitch, Pitch, k); curDist = Mathf.Lerp(curDist, Distance, k);
            Apply();
        }
        void Apply() {
            var rot = Quaternion.Euler(curPitch, curYaw, 0);
            transform.position = curTarget - rot * Vector3.forward * curDist;
            transform.rotation = rot;
        }
        public static bool PointerOverHud;
        void HandleInput() {
            var mouse = Mouse.current; var kb = Keyboard.current;
            float dt = Time.unscaledDeltaTime;
            if (mouse != null) {
                var p = mouse.position.ReadValue();
                var d = p - lastMouse; lastMouse = p;
                if (mouse.rightButton.isPressed) { Yaw += d.x * .25f; Pitch = Mathf.Clamp(Pitch - d.y * .2f, 12, 88); }
                if (mouse.middleButton.isPressed) Pan(-d.x * Distance * .0015f, -d.y * Distance * .0015f);
                float wheel = mouse.scroll.ReadValue().y;
                if (!PointerOverHud && Mathf.Abs(wheel) > .01f) Distance = Mathf.Clamp(Distance * (wheel > 0 ? .9f : 1.1f), MinDistance, MaxDistance);
            }
            if (kb != null) {
                if (kb.qKey.isPressed) Yaw += 70 * dt;
                if (kb.eKey.isPressed) Yaw -= 70 * dt;
                float px = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0), pz = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
                if (px != 0 || pz != 0) Pan(px * Distance * .6f * dt, pz * Distance * .6f * dt);
                if (kb.rKey.isPressed) Distance = Mathf.Clamp(Distance * (1 - dt), MinDistance, MaxDistance);
                if (kb.fKey.isPressed) Distance = Mathf.Clamp(Distance * (1 + dt), MinDistance, MaxDistance);
                if (kb.homeKey.wasPressedThisFrame) ResetView();
            }
        }
        void Pan(float right, float forward) {
            var yawRot = Quaternion.Euler(0, Yaw, 0);
            Target += yawRot * new Vector3(right, 0, forward);
        }
    }
}
