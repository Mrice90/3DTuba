using InfiniteConquest.RulesCore.Core;
int checks=0;
void Check(bool ok,string name) {if(!ok)throw new Exception(name);checks++;}
var cells=new BoardState().Positions();
Check(BoardPosition.Width==6 && BoardPosition.Height==8 && BoardPosition.PlotHeight==4,"dimensions");
Check(cells.Count==48 && cells.Distinct().Count()==48,"all cells constructed");
foreach(var a in cells) {
 Check(a.IsOnPlayerSide(a.Y<4?0:1),"home side");
 foreach(var b in cells) {
  Check(BoardGeometry.HEX.Distance(a,b)==BoardGeometry.HEX.Distance(b,a),"distance symmetry");
  Check(BoardGeometry.HEX.Neighbors(a).Contains(b)==(BoardGeometry.HEX.Distance(a,b)==1),"neighbor consistency");
  foreach(double n in new[]{0.000001,-0.000001}) {
   var trace=BoardGeometry.HEX.HexTrace(a,b,n);
   Check(trace.SequenceEqual(BoardGeometry.HEX.HexTrace(b,a,n)),"LOS direction symmetry");
   Check(trace.All(x=>cells.Contains(x) && x!=a && x!=b),"LOS internal cells in bounds");
  }
 }
 if(a.X>0 && a.X<5 && a.Y>0 && a.Y<7)Check(BoardGeometry.HEX.Neighbors(a).Count==6,"interior six neighbors");
}
foreach(var xy in new[]{(-1,0),(6,0),(0,-1),(0,8),(5,8)}) {
 bool threw=false;try {new BoardPosition(xy.Item1,xy.Item2);}catch(ArgumentException){threw=true;}
 Check(threw,"reject bounds");
}
Console.WriteLine($"PASS {checks} geometry assertions; 48 cells, all 2304 LOS pairs");
