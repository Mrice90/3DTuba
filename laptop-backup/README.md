# Laptop backup (AI-200), 2026-10-18

Copies of game work that existed only on Matt's laptop, taken read-only on 2026-10-18. Nothing here is a new build or an edit; each folder is a snapshot of the laptop folder named below. Unity's generated folders (Library, Temp, Logs, obj, bin, Build, UserSettings) and the Unity-generated root .csproj/.sln files are left out. No secrets or .env files were found or copied.

| Folder | Laptop source (under `OneDrive/Documents/AI Dev/Infinite Conquest/` unless noted) | What it is |
|---|---|---|
| `unity-release-candidate-0.4.5/UnityProof` | `3DTuba-release-candidate/UnityProof` | Codex's 0.4.5 release-line Unity project (build: `playtest/unity-build-release-candidate-0.4.5`). Ships Native `7f72db84…` and RulesCore `cb527524…` in `Assets/Plugins/NativeRules`. |
| `unity-poseidon30k-2026-10-15/UnityProof` | `3DTuba-poseidon30k-2026-10-15/UnityProof` | Newest Unity project: structures build plus the 12 Poseidon 30K tokens (runtime assembly identical to 0.4.5). |
| `rules-engine-source-2026-10-13` | `reviews/release-push-2026-10-13/{source,runner,geometry}` + manifests | C# rules engine (rules-core), Native adapter, protocol runner. All 60 files match `native-source-manifest.json`, and this source's own build output is byte-identical to the shipped Native and RulesCore DLLs above. |
| `net-lab/launch-readiness-2026-10-15` | `worktrees/launch-readiness-2026-10-15` | Codex's native ranked referee candidate (relay + PR22 ranked foundation, native runner/DLLs). |
| `net-lab/relay-resume-2026-10-15` | `worktrees/relay-resume-2026-10-15` | Reconnect-hardened relay. |
| `net-lab/socket-identity-2026-10-18` | `worktrees/socket-identity-2026-10-18` | Latest relay with signed seat identity (121 tests pass per its delivery note). |
| `codex-online-2026-10-18-snapshot` | `C:/Users/mattj/.codex/.chatgpt-projects/.../work/3DTuba-online-2026-10-18` | Mid-work snapshot of Codex's in-progress AI-097 Unity online entry. Not a finished state. |

To restore a Unity project, open its `UnityProof` folder in Unity 6000.6.3f1; Unity regenerates Library on first open.
