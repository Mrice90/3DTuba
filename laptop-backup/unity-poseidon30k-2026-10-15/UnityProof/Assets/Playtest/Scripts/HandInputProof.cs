using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using InfiniteConquest.Proof;
namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        IEnumerator GuardHandInputProof(string directory) {
            Directory.CreateDirectory(directory);var test=RunHandInputProof(directory);
            while(true){object step=null;bool more=false;Exception error=null;
                try{more=test.MoveNext();if(more)step=test.Current;}catch(Exception e){error=e;}
                if(error!=null){File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new HandProofResult{passed=false,error=error.ToString()}));Debug.LogException(error);Application.Quit(1);yield break;}
                if(!more)yield break;yield return step;
            }
        }
        [Serializable] sealed class HandProofResult {public bool passed;public int checks,fixtures;public string error;}
        IEnumerator RunHandInputProof(string directory) {
            sfx.Muted=true;int checks=0,fixtures=0;
            void Check(bool value,string description){if(!value)throw new Exception("HAND INPUT: "+description);checks++;}
            foreach(var device in InputSystem.devices.OfType<Mouse>().ToArray())InputSystem.DisableDevice(device);
            var pointer=InputSystem.AddDevice<Mouse>();
            void Point(Vector2 gui,bool down=false){InputSystem.QueueStateEvent(pointer,new MouseState{position=new Vector2(gui.x,Screen.height-gui.y)}.WithButton(MouseButton.Left,down));}
            foreach(int seat in new[]{0,1})foreach(int count in new[]{4,8}) {
                ClearMatch();mode=Mode.Live;humanSeat=seat;waiting=false;paused=false;marketOpen=false;playing=false;
                model.Turn=1;model.Active=seat;model.Phase="PLAY";model.Gp[seat]=10;model.Hand[seat]=count;
                var cards=Enumerable.Range(0,count).Select(i=>new BridgeHandCard{instance_id="proof-"+i,card_id=(seat==0?"zeus":"poseidon")+"_tutor_land_"+(i%5+1)}).ToArray();
                liveState=new BridgeState{phase="PLAY",active_player=seat,acting_player=seat,winner=-1,purchase_economy=true,
                    players=new[]{new BridgePlayer{hand=seat==0?cards:new BridgeHandCard[0]},new BridgePlayer{hand=seat==1?cards:new BridgeHandCard[0]}},
                    capitals=new[]{new BridgeCapital{seat=0,instance_id="proof-cap0",hp=20,max_hp=20,x=3,y=0},new BridgeCapital{seat=1,instance_id="proof-cap1",hp=20,max_hp=20,x=3,y=6}},abilities=new BridgeAbility[0]};
                liveActions=cards.Select(c=>new BridgeAction{id="action-"+c.instance_id,type="play",instance_id=c.instance_id,card_id=c.card_id,to=new WireHex{x=3,y=seat==0?1:5}}).ToArray();
                Spawn("proof-cap0",model.CapitalId0,0,3,0,false);Spawn("proof-cap1",model.CapitalId1,1,3,6,false);
                FitViewport();rig.FaceFrom(BoardLayout.CellCenter(3,seat==0?0:6),Vector3.zero,true);rig.FaceOpponentFrom(BoardLayout.CellCenter(3,seat==0?6:0),Vector3.zero);rig.SnapTo(CameraRig.View.Own);rig.Snap();
                liveStatus="Controlled hand-input fixture";Point(new Vector2(2,2));yield return new WaitForSecondsRealtime(.6f);
                Check(handVisuals.Count==count,"all fixture cards rendered");
                var first=handVisuals[cards[0].instance_id].rect;
                Point(first.center);yield return new WaitForSecondsRealtime(.18f);
                Check(handHover==cards[0].instance_id,"visible first card owns hover");
                Check(!viewRect.Overlaps(handVisuals[cards[0].instance_id].rect),"inspector does not cover the hand");
                yield return Shot(directory,"inspect-seat"+seat+"-count"+count+".png");
                Point(handVisuals[cards[0].instance_id].rect.center,true);yield return new WaitForSecondsRealtime(.12f);
                Point(handVisuals[cards[0].instance_id].rect.center);yield return new WaitForSecondsRealtime(.12f);
                Check(liveSelected==cards[0].instance_id,"one real mouse press selects exactly once");
                Point(handVisuals[cards[0].instance_id].rect.center,true);yield return new WaitForSecondsRealtime(.12f);
                Point(new Vector2(2,2));yield return new WaitForSecondsRealtime(.25f);
                Check(liveSelected==null,"second real mouse press toggles selection off once");
                var normal=handVisuals[cards[1].instance_id].rect;var above=new Vector2(normal.center.x,normal.yMin-6);
                Point(above);yield return new WaitForSecondsRealtime(.15f);
                Check(handHover==null,"blank strip above card does not acquire hover");
                Point(above,true);yield return new WaitForSecondsRealtime(.12f);Point(new Vector2(2,2));yield return new WaitForSecondsRealtime(.15f);
                Check(liveSelected==null,"blank strip never selects a hidden card");
                var last=handVisuals[cards[count-1].instance_id].rect;var prior=handVisuals[cards[count-2].instance_id].rect;
                var at=last.Overlaps(prior)?new Vector2((last.xMin+prior.xMax)/2,last.center.y):last.center;
                if(!last.Overlaps(prior)){
                    var gap=new Vector2((prior.xMax+last.xMin)/2,last.center.y);
                    Check(PrintedHandAt(gap)==null,"empty inter-card gap has no hit");
                    Point(gap,true);yield return new WaitForSecondsRealtime(.12f);Point(new Vector2(2,2));yield return new WaitForSecondsRealtime(.15f);
                    Check(liveSelected==null,"clicking gap does not select a card");
                }
                Check(PrintedHandAt(at)==cards[count-1].instance_id,"topmost overlapping card wins hit test");
                Point(at,true);yield return new WaitForSecondsRealtime(.12f);Point(at);yield return new WaitForSecondsRealtime(.12f);
                Check(liveSelected==cards[count-1].instance_id,"mouse selects topmost card");
                paused=true;Point(handVisuals[cards[0].instance_id].rect.center,true);yield return new WaitForSecondsRealtime(.12f);
                Point(new Vector2(2,2));yield return new WaitForSecondsRealtime(.12f);
                Check(liveSelected==cards[count-1].instance_id,"pause blocks underlying hand input");paused=false;
                Point(new Vector2(2,2));yield return new WaitForSecondsRealtime(.3f);
                Check(CardInspectRect(Screen.width,Screen.height).xMax<handVisuals[cards[0].instance_id].rect.xMin,"inspector stays outside crowded hand");
                fixtures++;yield return Shot(directory,"hand-seat"+seat+"-count"+count+".png");
            }
            InputSystem.RemoveDevice(pointer);
            File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new HandProofResult{passed=true,checks=checks,fixtures=fixtures,error=""}));Application.Quit();
        }
    }
}
