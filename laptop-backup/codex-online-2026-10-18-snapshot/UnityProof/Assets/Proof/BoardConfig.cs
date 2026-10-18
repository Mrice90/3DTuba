using System;
using System.Collections.Generic;
using UnityEngine;
using EngineBoard = InfiniteConquest.RulesCore.Core.BoardPosition;
namespace InfiniteConquest.Proof {
    public enum BoardShape { Rectangle, Hexagon }
    // One authoritative playable mask, shared with the native engine.
    public static class BoardConfig {
        public const int RulesWidth=EngineBoard.Width, RulesHeight=EngineBoard.Height, RulesPlotHeight=EngineBoard.PlotHeight;
        public const int PlannedWidth=RulesWidth, PlannedHeight=RulesHeight, PlannedPlotHeight=RulesPlotHeight;
        public const int HexRadius=EngineBoard.Radius;
        public static readonly Vector2Int HexCenter=new Vector2Int(EngineBoard.CenterX,EngineBoard.CenterY);
        public static BoardShape Shape => BoardShape.Hexagon;
        public static int Width => RulesWidth;
        public static int Height => RulesHeight;
        public static int PlotHeight => RulesPlotHeight;
        public static Vector2Int Offset => Vector2Int.zero;
        static readonly List<Vector2Int> drawn=new List<Vector2Int>();
        public static IReadOnlyList<Vector2Int> DrawnCells => drawn;
        public static Vector2 RawCentroid {get; private set;}
        public static bool IsPreview => false;
        public static int CellCount => drawn.Count;
        public static string Label => "hex37";
        static BoardConfig() { UseRules(); }
        public static void UseRules() {
            drawn.Clear(); var sum=Vector2.zero;
            for(int y=0;y<RulesHeight;y++) for(int x=0;x<RulesWidth;x++) if(InRules(x,y)) {
                drawn.Add(new Vector2Int(x,y)); sum+=new Vector2(x+.5f*(y&1),y);
            }
            RawCentroid=sum/drawn.Count;
        }
        public static void Use(int width,int height,int plotHeight) => UseRules();
        public static void UsePlanned() => UseRules();
        public static void UseHexagon() => UseRules();
        public static void ApplyCommandLine(string[] args) => UseRules();
        public static bool InRules(int x,int y) => EngineBoard.Contains(x,y);
        public static int HomeSide(int x,int y) => EngineBoard.HomeSide(x,y);
    }
}
