using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        sealed class HandVisual { public string card; public Rect rect; public float firstSeen, removedAt=-1; }
        readonly Dictionary<string,HandVisual> handVisuals=new Dictionary<string,HandVisual>();
        int handMotionFrame=-1; string handHover;
        GUIStyle handSlotStyle;
        bool PointerOverPrintedHand(Vector2 point) => handVisuals.Values.Any(v=>v.removedAt<0 && v.rect.Contains(point));
        string PrintedHandAt(Vector2 point) {
            var hand=liveState?.players!=null && humanSeat<liveState.players.Length?liveState.players[humanSeat].hand:null;
            if(hand==null)return null;
            bool Hit(string id)=>handVisuals.TryGetValue(id,out var visual)&&visual.removedAt<0&&visual.rect.Contains(point);
            if(handHover!=null&&hand.Any(c=>c.instance_id==handHover)&&Hit(handHover))return handHover;
            for(int i=hand.Length-1;i>=0;i--)if(Hit(hand[i].instance_id))return hand[i].instance_id;
            return null;
        }
        bool TryClickPrintedHand(Vector2 point) {
            if(mode!=Mode.Live || paused || MatchOver || marketOpen || reactionRect.Contains(point) || viewRect.Contains(point)
                || commandRect.Contains(point) || logPanel.Contains(point) || topBar.Contains(point) || bottomBar.Contains(point)
                || leftPanel.Contains(point) || rightPanel.Contains(point))return false;
            string id=PrintedHandAt(point);if(id==null)return false;
            var c=liveState.players[humanSeat].hand.First(k=>k.instance_id==id);
            if(ActionsFor(id).Any()&&!waiting){if(liveSelected==id)ClearSelection();else SelectLive(id);}
            else{sfx.PlayUi("error");liveStatus=UnplayableReason(c);}
            return true;
        }
        void DrawPrintedHand(float W,float H) {
            handRect=Rect.zero;
            if(mode!=Mode.Live || liveState?.players==null || liveState.players.Length<=humanSeat) return;
            var hand=liveState.players[humanSeat].hand ?? new BridgeHandCard[0];
            float cw=Mathf.Clamp(H*.15f,96,132),ch=cw*1.39f,gap=8,lift=cw*.2f;
            float total=hand.Length*cw+Mathf.Max(0,hand.Length-1)*gap;
            float x0=Mathf.Max(270,W/2-total/2),y0=H-62-ch;
            float step=hand.Length<=1?cw+gap:Mathf.Min(cw+gap,Mathf.Max(20,(W-x0-cw-20)/(hand.Length-1)));
            bool tick=handMotionFrame!=Time.frameCount;
            if(tick) {
                handMotionFrame=Time.frameCount;
                var ids=new HashSet<string>(hand.Select(c=>c.instance_id));
                foreach(var pair in handVisuals) if(!ids.Contains(pair.Key) && pair.Value.removedAt<0) pair.Value.removedAt=Time.unscaledTime;
                foreach(var key in handVisuals.Where(p=>p.Value.removedAt>=0 && Time.unscaledTime-p.Value.removedAt>.32f).Select(p=>p.Key).ToArray()) handVisuals.Remove(key);
                if(handHover!=null&&!ids.Contains(handHover))handHover=null;
                for(int i=0;i<hand.Length;i++) {
                    var c=hand[i];
                    if(!handVisuals.TryGetValue(c.instance_id,out var v)) {
                        v=new HandVisual {card=c.card_id,firstSeen=Time.unscaledTime,rect=new Rect(W+cw,y0+ch*.4f,cw,ch)};
                        handVisuals[c.instance_id]=v;
                    }
                    bool focus=c.instance_id==handHover || c.instance_id==liveSelected;
                    float size=focus?1.16f:1;
                    var target=new Rect(x0+i*step-(cw*size-cw)/2,y0-(focus?lift:0)-(ch*size-ch),cw*size,ch*size);
                    float blend=1-Mathf.Exp(-18*Time.unscaledDeltaTime);
                    v.rect=new Rect(Mathf.Lerp(v.rect.x,target.x,blend),Mathf.Lerp(v.rect.y,target.y,blend),Mathf.Lerp(v.rect.width,target.width,blend),Mathf.Lerp(v.rect.height,target.height,blend));
                }
            }
            handRect=Rect.zero; // Input tests the current pointer against the actual visible card rectangles.
            foreach(var v in handVisuals.Values.Where(v=>v.removedAt>=0)) {
                float progress=Mathf.Clamp01((Time.unscaledTime-v.removedAt)/.32f);
                var old=GUI.color;GUI.color=new Color(1,1,1,1-progress);
                var r=v.rect;r.y-=progress*48;
                CardFaces.Draw(r,v.card,true);GUI.color=old;
            }
            foreach(var c in hand.Where(c=>c.instance_id!=handHover)) DrawOneHandCard(c);
            var hover=hand.FirstOrDefault(c=>c.instance_id==handHover);if(hover!=null) DrawOneHandCard(hover);
            // Clicks are dispatched once by Update, before either hand or board state changes.
        }
        void DrawOneHandCard(BridgeHandCard c) {
            if(!handVisuals.TryGetValue(c.instance_id,out var v)) return;
            bool playable=ActionsFor(c.instance_id).Any() && !waiting;
            bool selected=liveSelected==c.instance_id;
            if(c.instance_id==handHover) hoverCardId=c.card_id;
            CardFaces.Draw(v.rect,c.card_id,true,selected || (c.instance_id==handHover && playable),!playable);
            if(liveState.slots_enabled && (c.slot_cost>0 || c.slot_supply>0)) {
                var r=new Rect(v.rect.x+9,v.rect.y+v.rect.height*.56f,v.rect.width-18,14);
                if(handSlotStyle==null) { handSlotStyle=new GUIStyle(smallStyle){fontSize=9,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};handSlotStyle.normal.textColor=new Color(.98f,.91f,.7f); }
                CardFinish.Round(r,new Color(.06f,.07f,.08f,.92f));
                GUI.Label(r,c.slot_supply>0?"Slots +"+c.slot_supply:"Uses "+c.slot_cost+" slots",handSlotStyle);
            }
        }
    }
}
