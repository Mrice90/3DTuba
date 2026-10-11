using System;
using System.Linq;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    // Front-end for the installable playtest: main menu (play vs bot, demo match, collection, how to
    // play, settings, quit), the in-match pause menu and the victory/defeat screen. IMGUI, like the HUD.
    public sealed partial class PlaytestGame {
        enum Screen2 { Main, PlaySetup, HowTo, Settings }
        Screen2 menuScreen = Screen2.Main;
        bool paused, settingsFromPause;
        int setupSeat; int setupDifficulty = 1;
        static readonly string[] Difficulties = { "MORTAL", "HERO", "DEMIGOD" };
        static readonly string[] DifficultyNames = { "Mortal (easy)", "Hero (normal)", "Demigod (hard)" };
        string resolvedBridgeCmd;
        GUIStyle menuTitle, menuButton, menuHeading, menuBody;
        int lastSeed, lastSeat, lastDifficulty; float menuOpenedAt;

        // ------------------------------------------------------------------ settings (PlayerPrefs)
        float masterVolume = 1f, sfxVolume = .8f;
        void LoadSettings() {
            masterVolume = PlayerPrefs.GetFloat("ic.master", 1f);
            sfxVolume = PlayerPrefs.GetFloat("ic.sfx", .8f);
            AudioListener.volume = masterVolume; sfx.Volume = sfxVolume;
            if (PlayerPrefs.HasKey("ic.quality")) QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt("ic.quality"), 0, QualitySettings.names.Length - 1), true);
            setupSeat = PlayerPrefs.GetInt("ic.seat", 0);
            setupDifficulty = PlayerPrefs.GetInt("ic.difficulty", 1);
        }
        void SaveSettings() {
            PlayerPrefs.SetFloat("ic.master", masterVolume); PlayerPrefs.SetFloat("ic.sfx", sfxVolume);
            PlayerPrefs.SetInt("ic.quality", QualitySettings.GetQualityLevel());
            PlayerPrefs.SetInt("ic.seat", setupSeat); PlayerPrefs.SetInt("ic.difficulty", setupDifficulty);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ flow
        void OpenMenu() {
            bridge?.Dispose(); bridge = null;
            anim.StopAllCoroutines(); StopAllCoroutines(); runner = null;
            if (galleryRoot != null) Destroy(galleryRoot);
            ClearMatch(); paused = false;
            mode = Mode.Menu; menuScreen = Screen2.Main; FitViewport(); menuOpenedAt = Time.unscaledTime;
            PlaceDemoCapitals();
            rig.ResetView(); rig.Snap();
            Time.timeScale = 1;
        }
        void StartMatchFromMenu(int seed) {
            lastSeed = seed; lastSeat = setupSeat; lastDifficulty = setupDifficulty; SaveSettings();
            if (string.IsNullOrEmpty(resolvedBridgeCmd)) { menuScreen = Screen2.Main; Banner(bridgeHint, 6); return; }
            StartLive(resolvedBridgeCmd, null, Arg("-bridgeTranscript"), seed, setupSeat, Difficulties[setupDifficulty]);
            rig.ResetView(); rig.Snap();
        }
        void Rematch() {
            setupSeat = lastSeat; setupDifficulty = lastDifficulty;
            bridge?.Dispose(); bridge = null; paused = false;
            StartMatchFromMenu(UnityEngine.Random.Range(1, 1000000));
        }
        void TogglePause() {
            if (mode != Mode.Live && mode != Mode.Playback) return;
            paused = !paused; settingsFromPause = false;
            if (mode == Mode.Playback) playing = !paused;
            sfx.PlayUi("click");
        }
        bool MatchOver => (mode == Mode.Live && liveState != null && liveState.GameOver) || (mode == Mode.Playback && model.Winner >= 0 && cursor >= events.Count);

        // ------------------------------------------------------------------ drawing
        void MenuStyles() {
            if (menuTitle != null) return;
            menuTitle = new GUIStyle(GUI.skin.label) { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            menuTitle.normal.textColor = new Color(.97f, .86f, .52f);
            menuHeading = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            menuHeading.normal.textColor = Color.white;
            menuBody = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, richText = true };
            menuBody.normal.textColor = new Color(.88f, .92f, .98f);
            menuButton = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            menuButton.normal.textColor = menuButton.hover.textColor = Color.white;
            // Selected toggles (god, difficulty) read as chosen: gold text on the pressed face.
            menuButton.onNormal.textColor = menuButton.onHover.textColor = menuButton.onActive.textColor = new Color(1f, .82f, .3f);
        }
        float UiScale => Mathf.Clamp(Screen.height / 900f, .75f, 1.6f);
        Rect Centered(float w, float h, float y) { float s = UiScale; return new Rect(Screen.width / 2f - w * s / 2, y * s, w * s, h * s); }
        bool MenuButton(Rect r, string text, bool enabled = true) {
            var prev = GUI.enabled; GUI.enabled = enabled;
            int fs = menuButton.fontSize; menuButton.fontSize = Mathf.RoundToInt(20 * UiScale);
            // Ignore clicks for a moment after a screen opens, so the click that opened it can't land on a button.
            bool hit = GUI.Button(r, text, menuButton) && Time.unscaledTime - menuOpenedAt > .5f;
            menuButton.fontSize = fs; GUI.enabled = prev;
            if (hit) sfx.PlayUi("click");
            return hit;
        }
        void Dim(float a) => GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(0, 0, 0, a), 0, 0);

        void DrawMenu() {
            MenuStyles();
            Dim(.45f);
            float s = UiScale;
            menuTitle.fontSize = Mathf.RoundToInt(54 * s); menuHeading.fontSize = Mathf.RoundToInt(26 * s); menuBody.fontSize = Mathf.RoundToInt(16 * s);
            GUI.Label(Centered(900, 80, 70), "INFINITE CONQUEST", menuTitle);
            GUI.Label(Centered(900, 30, 140), "Playtest build · Zeus vs Poseidon", menuHeading);
            switch (menuScreen) {
                case Screen2.Main: {
                    float y = 230;
                    if (MenuButton(Centered(360, 54, y), "Play vs Bot", true)) menuScreen = Screen2.PlaySetup; y += 66;
                    if (MenuButton(Centered(360, 54, y), "Watch a Demo Match")) { RestartPlayback(); } y += 66;
                    if (MenuButton(Centered(360, 54, y), "Card Collection")) { ShowGallery("all"); } y += 66;
                    if (MenuButton(Centered(360, 54, y), "How to Play")) menuScreen = Screen2.HowTo; y += 66;
                    if (MenuButton(Centered(360, 54, y), "Settings")) menuScreen = Screen2.Settings; y += 66;
                    if (MenuButton(Centered(360, 54, y), "Quit")) Application.Quit();
                    if (string.IsNullOrEmpty(resolvedBridgeCmd))
                        GUI.Label(Centered(700, 60, y + 70), "<color=#ffb070>" + bridgeHint + "</color>", new GUIStyle(menuBody) { alignment = TextAnchor.MiddleCenter });
                    break;
                }
                case Screen2.PlaySetup: {
                    GUI.Label(Centered(600, 40, 210), "Choose your god", menuHeading);
                    var r = Centered(560, 54, 260);
                    var half = new Rect(r.x, r.y, r.width / 2 - 6, r.height);
                    if (GUI.Toggle(half, setupSeat == 0, "Zeus", menuButton)) setupSeat = 0;
                    half.x += r.width / 2 + 6;
                    if (GUI.Toggle(half, setupSeat == 1, "Poseidon", menuButton)) setupSeat = 1;
                    GUI.Label(Centered(600, 40, 340), "Bot difficulty", menuHeading);
                    var d = Centered(660, 54, 390);
                    for (int i = 0; i < 3; i++) {
                        var b = new Rect(d.x + i * (d.width / 3), d.y, d.width / 3 - 8, d.height);
                        if (GUI.Toggle(b, setupDifficulty == i, DifficultyNames[i], menuButton)) setupDifficulty = i;
                    }
                    GUI.Label(Centered(660, 50, 455), setupSeat == 0 ? "Zeus: storm-forged legions of the high grid. You go first or second by coin flip." : "Poseidon: tide-engines and abyssal constructs of the deep. You go first or second by coin flip.", new GUIStyle(menuBody) { alignment = TextAnchor.MiddleCenter });
                    if (MenuButton(Centered(360, 58, 520), "Start Match")) StartMatchFromMenu(UnityEngine.Random.Range(1, 1000000));
                    if (MenuButton(Centered(360, 46, 590), "Back")) menuScreen = Screen2.Main;
                    break;
                }
                case Screen2.HowTo: {
                    var r = Centered(760, 420, 200);
                    GUI.Box(r, "", panelStyle);
                    GUI.Label(new Rect(r.x + 24, r.y + 18, r.width - 48, r.height - 36),
                        "<b>Goal.</b> Destroy the enemy Capital (20 HP).\n\n" +
                        "<b>Your turn.</b> You earn GP (gold power) each turn from your Capital and Lands. Spend it to play cards from your hand along the bottom of the screen: click a bright card, then click a glowing hex.\n\n" +
                        "<b>Units.</b> Click one of your Characters, then a glowing hex to move or a red marker to attack. Enemy Characters block movement; moving past one can trigger an opportunity attack.\n\n" +
                        "<b>Cards.</b> Lands raise your income, Structures defend and buff, Spells resolve at once. Hover any card or piece to read it.\n\n" +
                        "<b>Controls.</b> Right-drag to rotate and tilt, Q/E to rotate, Up/Down arrows to tilt, mouse wheel to zoom, middle-drag or WASD to pan, Home to reset the camera, Esc to pause, End turn at bottom-left.",
                        menuBody);
                    if (MenuButton(Centered(360, 46, 640), "Back")) menuScreen = Screen2.Main;
                    break;
                }
                case Screen2.Settings:
                    DrawSettings(210, () => menuScreen = Screen2.Main);
                    break;
            }
        }

        void DrawSettings(float top, Action back) {
            var r = Centered(620, 330, top);
            GUI.Box(r, "", panelStyle);
            float x = r.x + 24, w = r.width - 48, y = r.y + 20, line = 44 * UiScale;
            GUI.Label(new Rect(x, y, w * .4f, line), "Master volume", menuBody);
            masterVolume = GUI.HorizontalSlider(new Rect(x + w * .42f, y + 10, w * .58f, line), masterVolume, 0, 1); y += line;
            GUI.Label(new Rect(x, y, w * .4f, line), "Sound effects", menuBody);
            sfxVolume = GUI.HorizontalSlider(new Rect(x + w * .42f, y + 10, w * .58f, line), sfxVolume, 0, 1); y += line;
            AudioListener.volume = masterVolume; sfx.Volume = sfxVolume;
            GUI.Label(new Rect(x, y, w * .4f, line), "Display", menuBody);
            bool full = Screen.fullScreenMode == FullScreenMode.FullScreenWindow || Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen;
            if (GUI.Button(new Rect(x + w * .42f, y, w * .28f, line - 8), full ? "Fullscreen ✓" : "Fullscreen", buttonStyle) && !full) {
                var res = Screen.currentResolution; Screen.SetResolution(res.width, res.height, FullScreenMode.FullScreenWindow);
            }
            if (GUI.Button(new Rect(x + w * .72f, y, w * .28f, line - 8), full ? "Windowed" : "Windowed ✓", buttonStyle) && full)
                Screen.fullScreenMode = FullScreenMode.MaximizedWindow;
            y += line;
            GUI.Label(new Rect(x, y, w * .4f, line), "Resolution", menuBody);
            var resolutions = Screen.resolutions.Select(q => new Vector2Int(q.width, q.height)).Distinct().ToArray();
            int cur = Array.FindIndex(resolutions, q => q.x == Screen.width && q.y == Screen.height);
            string label = Screen.width + " x " + Screen.height;
            if (GUI.Button(new Rect(x + w * .42f, y, w * .12f, line - 8), "<", buttonStyle) && resolutions.Length > 0) { var q = resolutions[Mathf.Clamp((cur < 0 ? resolutions.Length : cur) - 1, 0, resolutions.Length - 1)]; Screen.SetResolution(q.x, q.y, Screen.fullScreenMode == FullScreenMode.MaximizedWindow ? FullScreenMode.Windowed : Screen.fullScreenMode); }
            GUI.Label(new Rect(x + w * .55f, y, w * .32f, line), label, new GUIStyle(menuBody) { alignment = TextAnchor.UpperCenter });
            if (GUI.Button(new Rect(x + w * .88f, y, w * .12f, line - 8), ">", buttonStyle) && resolutions.Length > 0) { var q = resolutions[Mathf.Clamp(cur + 1, 0, resolutions.Length - 1)]; Screen.SetResolution(q.x, q.y, Screen.fullScreenMode == FullScreenMode.MaximizedWindow ? FullScreenMode.Windowed : Screen.fullScreenMode); }
            y += line;
            GUI.Label(new Rect(x, y, w * .4f, line), "Graphics quality", menuBody);
            var names = QualitySettings.names; int ql = QualitySettings.GetQualityLevel();
            if (GUI.Button(new Rect(x + w * .42f, y, w * .12f, line - 8), "<", buttonStyle) && ql > 0) QualitySettings.SetQualityLevel(ql - 1, true);
            GUI.Label(new Rect(x + w * .55f, y, w * .32f, line), names.Length > 0 ? names[ql] : "-", new GUIStyle(menuBody) { alignment = TextAnchor.UpperCenter });
            if (GUI.Button(new Rect(x + w * .88f, y, w * .12f, line - 8), ">", buttonStyle) && ql < names.Length - 1) QualitySettings.SetQualityLevel(ql + 1, true);
            y += line;
            if (MenuButton(Centered(360, 46, top + 350), "Back")) { SaveSettings(); back(); }
        }

        void DrawPause() {
            MenuStyles(); Dim(.55f);
            menuHeading.fontSize = Mathf.RoundToInt(30 * UiScale);
            if (settingsFromPause) { GUI.Label(Centered(600, 50, 140), "Settings", menuHeading); DrawSettings(200, () => settingsFromPause = false); return; }
            GUI.Label(Centered(600, 50, 200), "Paused", menuHeading);
            if (MenuButton(Centered(340, 52, 270), "Resume")) TogglePause();
            if (MenuButton(Centered(340, 52, 334), "Settings")) settingsFromPause = true;
            if (MenuButton(Centered(340, 52, 398), mode == Mode.Live ? "Concede to Main Menu" : "Main Menu")) OpenMenu();
            if (MenuButton(Centered(340, 52, 462), "Quit Game")) Application.Quit();
        }

        void DrawGameOver() {
            MenuStyles(); Dim(.35f);
            int winner = mode == Mode.Live ? liveState.winner : model.Winner;
            bool won = mode == Mode.Live && winner == humanSeat;
            string title = mode == Mode.Live ? (won ? "VICTORY" : "DEFEAT") : $"{Faction(winner)} wins";
            menuTitle.fontSize = Mathf.RoundToInt(64 * UiScale);
            menuTitle.normal.textColor = won || mode != Mode.Live ? new Color(.97f, .86f, .52f) : new Color(.85f, .45f, .45f);
            GUI.Label(Centered(800, 90, 180), title, menuTitle);
            menuHeading.fontSize = Mathf.RoundToInt(22 * UiScale);
            GUI.Label(Centered(800, 34, 270), $"Turn {model.Turn} · Capitals {model.CapitalHp[0]} – {model.CapitalHp[1]} HP · Cards played {model.Played[0]} / {model.Played[1]}", menuHeading);
            if (mode == Mode.Live && MenuButton(Centered(340, 54, 330), "Play Again")) Rematch();
            if (mode == Mode.Playback && MenuButton(Centered(340, 54, 330), "Watch Again")) RestartPlayback();
            if (MenuButton(Centered(340, 54, 396), "Main Menu")) OpenMenu();
            menuTitle.normal.textColor = new Color(.97f, .86f, .52f);
        }
    }
}
