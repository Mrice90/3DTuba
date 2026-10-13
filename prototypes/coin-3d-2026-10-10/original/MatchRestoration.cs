using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using InfiniteConquest.Proof;

namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        int placementSeed, placementSeat;
        Vector2Int placement = new Vector2Int(-1, -1);
        string coinResult;
        float coinStarted;
        bool showingCoin;
        sealed class RetiredCueSource { public string cardId; public Vector3 at; }
        readonly Dictionary<string, RetiredCueSource> retiredCueSources = new Dictionary<string, RetiredCueSource>();
        Texture2D coinTexture;

        void BeginCapitalPlacement(int seed) {
            bridge?.Dispose(); bridge = null;
            anim.StopAllCoroutines(); StopAllCoroutines(); runner = null;
            ClearMatch(); mode = Mode.Setup; paused = false; FitViewport();
            placementSeed = seed; placementSeat = setupSeat; humanSeat = setupSeat;
            placement = new Vector2Int(-1, -1);
            rig.SetHome(new Vector3(0, 0, .2f), setupSeat == 1 ? 156 : -24, 50, 11.5f, true);
            board.SetLegal(from x in Enumerable.Range(0, 4) from y in Enumerable.Range(setupSeat == 0 ? 0 : 3, 3) select new Vector2Int(x, y));
        }
        void UpdateCapitalPlacement() {
            var mouse = Mouse.current; if (mouse == null) return;
            CameraRig.PointerOverHud = mouse.position.ReadValue().y < 90 || mouse.position.ReadValue().y > Screen.height - 95;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) { OpenMenu(); return; }
            if (CameraRig.PointerOverHud || !Physics.Raycast(cam.ScreenPointToRay(mouse.position.ReadValue()), out var hit, 200)) return;
            var cell = hit.collider.GetComponent<ProofCell>(); if (cell == null) return;
            var at = new Vector2Int(cell.X, cell.Y);
            board.Hover = at; board.Refresh();
            if (mouse.leftButton.wasPressedThisFrame && ValidCapitalCell(placementSeat, at)) {
                placement = at; board.Selected = at; board.Refresh(); sfx.PlayUi("click");
            }
        }
        public static bool ValidCapitalCell(int seat, Vector2Int at) =>
            (seat == 0 || seat == 1) && at.x >= 0 && at.x < 4 && at.y >= 0 && at.y < 6 && (seat == 0 ? at.y < 3 : at.y >= 3);
        void DrawCapitalPlacement() {
            Styles();
            GUI.Box(new Rect(0, 0, Screen.width, 94), "", panelStyle);
            GUI.Label(new Rect(20, 15, Screen.width - 40, 32), "Place your " + Faction(placementSeat) + " capital", titleStyle);
            GUI.Label(new Rect(20, 50, Screen.width - 40, 30), "Choose a glowing hex on your half. Both capitals reveal together when you confirm.", labelStyle);
            GUI.Box(new Rect(0, Screen.height - 90, Screen.width, 90), "", panelStyle);
            GUI.enabled = ValidCapitalCell(placementSeat, placement);
            if (GUI.Button(new Rect(Screen.width / 2 - 130, Screen.height - 70, 260, 44), "Confirm capital placement", buttonStyle)) {
                StartLive(resolvedBridgeCmd, null, Arg("-bridgeTranscript"), placementSeed, placementSeat, Difficulties[setupDifficulty], placement.x, placement.y);
            }
            GUI.enabled = true;
            if (GUI.Button(new Rect(20, Screen.height - 70, 150, 44), "Back", buttonStyle)) OpenMenu();
        }
        IEnumerator PresentCoinFlip(int winner) {
            showingCoin = true; coinStarted = Time.unscaledTime;
            coinResult = Faction(winner) + " goes first";
            sfx.PlayUi("card");
            yield return new WaitForSecondsRealtime(Application.isBatchMode ? .01f : 2.5f);
            sfx.PlayUi("turn");
            yield return new WaitForSecondsRealtime(Application.isBatchMode ? .01f : 1.2f);
            showingCoin = false;
        }
        void DrawCoinFlip() {
            if (!showingCoin) return;
            GUI.Box(new Rect(Screen.width / 2 - 220, Screen.height / 2 - 150, 440, 300), "", panelStyle);
            float elapsed = Time.unscaledTime - coinStarted;
            if (coinTexture == null) {
                coinTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++) {
                    float radius = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f));
                    coinTexture.SetPixel(x, y, radius > 63 ? Color.clear : radius > 56 ? new Color(.62f, .36f, .05f) : new Color(1f, .8f, .25f));
                }
                coinTexture.Apply();
            }
            float width = elapsed < 2.5f ? 32 + 110 * Mathf.Abs(Mathf.Cos(elapsed * 12)) : 142;
            GUI.DrawTexture(new Rect(Screen.width / 2 - width / 2, Screen.height / 2 - 100, width, 142), coinTexture);
            GUI.Label(new Rect(Screen.width / 2 - 180, Screen.height / 2 + 65, 360, 60), elapsed < 2.5f ? "Coin flip…" : coinResult, titleStyle);
        }
    }
}
