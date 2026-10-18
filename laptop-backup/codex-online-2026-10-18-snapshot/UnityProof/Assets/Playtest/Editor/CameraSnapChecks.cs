using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using InfiniteConquest.Playtest;
using InfiniteConquest.Proof;

// AI-080-CAMERA-SNAP-VIEWS: editor-only pose checks for the snap views (no player build).
public static class CameraSnapChecks {
    static readonly List<string> checks = new List<string>();
    static void Check(bool ok, string name) { if (!ok) throw new Exception("CAMERA_SNAP FAIL " + name); checks.Add(name); Debug.Log("CAMERA_SNAP PASS " + name); }
    static Vector3 Flat(Vector3 v) { v.y = 0; return v.normalized; }

    public static void Run() {
        checks.Clear();
        var go = new GameObject("Camera snap check"); var rig = go.AddComponent<CameraRig>(); rig.InputEnabled = false;
        try {
            var zeusCap = BoardLayout.CellCenter(2, 0); var poseidonCap = BoardLayout.CellCenter(3, 6);
            var center = Vector3.zero;
            foreach (int seat in new[] { 0, 1 }) {
                var own = seat == 0 ? zeusCap : poseidonCap; var enemy = seat == 0 ? poseidonCap : zeusCap;
                string s = seat == 0 ? "Zeus" : "Poseidon";
                rig.FaceFrom(own, center, true); rig.FaceOpponentFrom(enemy, center);
                foreach (CameraRig.View v in Enum.GetValues(typeof(CameraRig.View))) {
                    rig.SnapTo(v); rig.Snap();
                    var t = go.transform;
                    Check(rig.CurrentView == v, s + " " + v + " current view label");
                    // WASD pans along the yaw basis; it must match the camera's own screen right / screen up.
                    var panRight = Quaternion.Euler(0, rig.Yaw, 0) * Vector3.right; var panFwd = Quaternion.Euler(0, rig.Yaw, 0) * Vector3.forward;
                    Check(Vector3.Dot(panRight, Flat(t.right)) > .999f, s + " " + v + " D pans screen-right");
                    Check(Vector3.Dot(panFwd, Flat(t.up + t.forward)) > .99f, s + " " + v + " W pans screen-up");
                    Check(Vector3.Distance(rig.Target, center) < .01f, s + " " + v + " centred on board");
                    var camSide = t.position - center; camSide.y = 0;
                    if (v == CameraRig.View.Own) Check(Vector3.Dot(camSide, own - center) > 0, s + " Own view sits behind own capital");
                    if (v == CameraRig.View.Opponent) Check(Vector3.Dot(camSide, enemy - center) > 0, s + " Opponent view sits behind opponent capital");
                    if (v == CameraRig.View.TopDown) Check(t.forward.y < -.99f, s + " TopDown looks straight down");
                    if (v == CameraRig.View.TopDown || v == CameraRig.View.Overview)
                        Check(Vector3.Dot(Flat(t.up + t.forward), Flat(center - own)) > .7f, s + " " + v + " keeps own side at the bottom");
                    if (v == CameraRig.View.Overview) Check(rig.Distance > rig.HomeDistance, s + " Overview is further out than home");
                }
                rig.SnapTo(CameraRig.View.Overview); rig.NextView();
                Check(rig.CurrentView == CameraRig.View.Own, s + " V cycles from Overview back to Own");
                rig.SnapTo(CameraRig.View.TopDown); rig.ResetView();
                Check(rig.CurrentView == CameraRig.View.Own && Mathf.Approximately(rig.Yaw, rig.HomeYaw), s + " Home resets to Own");
            }
            // Report where the (unused) fixed-faction-side option places each seat, so Mathew can decide.
            foreach (bool far in new[] { false, true }) {
                rig.FaceFixedSide(far, center, true);
                var side = go.transform.position.z; float zeusRow = zeusCap.z, poseidonRow = poseidonCap.z;
                bool behindIntended = far ? Mathf.Sign(side) == Mathf.Sign(poseidonRow) : Mathf.Sign(side) == Mathf.Sign(zeusRow);
                Debug.Log("CAMERA_SNAP INFO FixedFactionSides farSide=" + far + " camera z=" + side.ToString("0.0") + " zeusRow z=" + zeusRow.ToString("0.0") + " poseidonRow z=" + poseidonRow.ToString("0.0") + " behindOwnRows=" + behindIntended);
                Check(behindIntended, "FixedFactionSides farSide=" + far + " camera sits behind that faction's rows");
            }
        } finally { UnityEngine.Object.DestroyImmediate(go); }
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/camera-snap-validation.json", "{\"passed\":true,\"checks\":" + checks.Count + "}");
    }
}
