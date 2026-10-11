using InfiniteConquest.Playtest;

// AI-080-COMBINED-2026-10-11: reaction cue/turn sound checks, then the native-rules chain
// (native checks, R3, camera, then CoinPrototypeBuild: coin, restoration, gameplay, audio and the player build).
// Unity -batchmode -quit -projectPath <combined>/UnityProof -executeMethod CombinedBuild.Build -buildPath <exe>
public static class CombinedBuild {
    public static void Build() { InterruptCueBuild.Validate(); NativeIntegrationBuild.Build(); }
}
