# Steam (SteamPipe) build scripts

Ready for the day Infinite Conquest has a Steamworks app. Nothing here creates an account, pays the
Steam Direct fee or uploads anything on its own. The plan behind it is the "Steamworks plan" tab of the
launch kit doc.

## What Matt needs first

1. A Steamworks partner account with the $100 Steam Direct fee paid, and the App ID and Windows depot ID
   from the partner site (App Admin → SteamPipe → Depots).
2. A Steam account with build-upload rights on the app (a dedicated build account is safest), and
   [steamcmd](https://partner.steamgames.com/doc/sdk/uploading) unpacked on the build PC.

## Use

From a finished Unity Windows build (the same folder `UnityProof/Tools/installer/build_installer.ps1`
packages: the exe, its `_Data` folder and `Bridge\` with the bundled `jre\`):

```
python releases/steam/steampipe.py stage  --build-dir <build folder> --out <work folder>
python releases/steam/steampipe.py render --work <work folder> --app-id <AppID> --depot-id <DepotID> --desc "0.4.5 RC"
python releases/steam/steampipe.py upload --work <work folder> --steam-user <build account> --confirm-upload
```

- `render` makes a **preview** build by default: steamcmd checks and logs the file list without uploading.
  Pass `--no-preview` for a real upload.
- `--set-live <branch>` only takes a beta branch (for example `playtest`). Setting the default branch live
  stays a manual click in Steamworks, on purpose.
- `upload` refuses without `--confirm-upload`. steamcmd asks for the password and Steam Guard code
  itself; nothing is stored in the repo.
- `steam_appid.txt`, `.pdb` files, screenshots, logs and Unity's debug-symbol backup folder never ship.

## Before the first real upload

- Rename the player from `InfiniteConquestPlaytest` (Unity product name) to the final name, and update
  `EXE`/`REQUIRED` in `steampipe.py` to match.
- Set the launch option in Steamworks: executable `InfiniteConquestPlaytest.exe` (or the renamed exe),
  OS Windows, 64-bit.
- The bundled Java runtime in `Bridge\jre` must stay: Steam players won't have Java installed.

Tests: `cd releases/steam && python -m unittest test_steampipe` (also run in `verify.yml`).
