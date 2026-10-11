// Deck shop r2 + board size: board-size checks, then the deck shop checks, then the combined chain
// (cue, native, R3, camera, coin, restoration, gameplay, audio, player build).
// Unity -batchmode -quit -projectPath <checkout>/UnityProof -executeMethod DeckShopBoardBuild.Build -buildPath <exe>
public static class DeckShopBoardBuild {
    public static void Build() { BoardSizeChecks.Run(); DeckShopBuild.Build(); }
}
