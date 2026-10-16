#!/bin/bash
# fetch-native-rules.sh — put the native C# rules core (DLLs + card data) in ./native
# for IC.RefereeHost, read-only from pinned commits in this repository.
#
# Pinned source (the newest native core committed to the repo, 2026-10-11):
#   DLLs + starters: claude/project-thread-fz00fa @ 49f6907 (deck-shop-board overlay)
#   card JSON:       claude/combined-2026-10-11  @ 0f5e8b0 (combined overlay)
# The 0.4.5 release candidate on the PC may be newer: to referee 0.4.5 matches,
# copy its InfiniteConquest.Native.dll / InfiniteConquest.RulesCore.dll into
# native/lib and its StreamingAssets/NativeRules folder into native/data instead.
set -euo pipefail
cd "$(dirname "$0")"
DLL_COMMIT=49f6907ba16d1dbb0636bab9d1b3b4a21fdca5ee
CARDS_COMMIT=0f5e8b0cc76c510d6eb85e7fc921a071e909703b
DLL_DIR=prototypes/deck-shop-board-2026-10-11/overlay/Assets
CARDS_DIR=prototypes/combined-2026-10-11/overlay/Assets/StreamingAssets/NativeRules

for c in $DLL_COMMIT $CARDS_COMMIT; do
    git cat-file -e "$c^{commit}" 2>/dev/null || git fetch -q origin "$c"
done
rm -rf native && mkdir -p native/lib native/data
for f in InfiniteConquest.Native.dll InfiniteConquest.RulesCore.dll; do
    git show "$DLL_COMMIT:$DLL_DIR/Plugins/NativeRules/$f" > "native/lib/$f"
done
git ls-tree -r --name-only --full-tree "$CARDS_COMMIT" -- "$CARDS_DIR" | grep -v '\.meta$' | while read -r f; do
    rel="${f#$CARDS_DIR/}"; mkdir -p "native/data/$(dirname "$rel")"
    git show "$CARDS_COMMIT:$f" > "native/data/$rel"
done
for f in faction-starters.json previous-starters.json; do
    git show "$DLL_COMMIT:$DLL_DIR/StreamingAssets/NativeRules/$f" > "native/data/$f"
done
sha256sum -c --quiet <<SUMS
59945d8ee76fb84c12c75167fa824a65cbfd9ed17013ad17e4439ce74a84daf9  native/lib/InfiniteConquest.Native.dll
741af66574faec22e1a19d50db1141adc0922a0fad6b6a55a127055e2110306b  native/lib/InfiniteConquest.RulesCore.dll
SUMS
echo "OK: native rules core in $(pwd)/native (build: dotnet build -c Release -o out)"
