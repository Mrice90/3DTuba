using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IC.Net;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        string lobbyEndpoint="http://127.0.0.1:8787",lobbyCode="",lobbyName="Player",lobbyStatus="Choose a primary faction, edit your deck, then host or join.";
        OnlineService lobbyService;
        RankedIdentity lobbyIdentity;
        Dictionary<string,object> lobbyRoom;
        bool lobbyBusy,lobbyRanked;
        float lobbyNextPoll;
        int lobbyGeneration;
        CancellationTokenSource lobbyCancel;
        static string Field(Dictionary<string,object> d,string name)=>d!=null&&d.TryGetValue(name,out var v)?v?.ToString():null;
        static bool Yes(Dictionary<string,object> d,string name)=>d!=null&&d.TryGetValue(name,out var v)&&v is bool b&&b;
        Dictionary<string,object> LocalDeckSetup() {
            var cards=SelectedPrototypeDeck(setupSeat);var primary=setupSeat==0?"ZEUS":"POSEIDON";
            var allies=cards.Select(id=>CardFaces.All.First(c=>c.id==id).faction).Where(f=>f!=primary&&f!="NEUTRAL").Distinct().ToArray();
            if(allies.Length>1)throw new InvalidOperationException("Choose cards from at most two factions.");
            return JsonLite.Obj(("primaryFaction",primary),("allyFaction",allies.FirstOrDefault()),("capital",SelectedCapital(setupSeat)),("cards",cards.Cast<object>().ToList()));
        }
        void OpenOnlineLobby(){CancelLobby();menuScreen=Screen2.Online;lobbyStatus="Choose your primary faction and deck. Share the room code with your opponent.";}
        void CancelLobby(){lobbyGeneration++;lobbyCancel?.Cancel();lobbyCancel?.Dispose();lobbyCancel=null;lobbyService?.Dispose();lobbyService=null;lobbyRoom=null;lobbyBusy=false;}
        async void LobbyBegin(bool join,bool ranked=false) {
            if(lobbyBusy)return;
            CancelLobby();var generation=lobbyGeneration;lobbyBusy=true;lobbyRanked=ranked;
            try {
                lobbyCancel=new CancellationTokenSource();lobbyService=new OnlineService(lobbyEndpoint);
                var deck=LocalDeckSetup();var primary=Field(deck,"primaryFaction").ToLowerInvariant();
                lobbyStatus=ranked?"Signing in and looking for an opponent…":join?"Joining room…":"Creating room…";
                Dictionary<string,object> result;
                if(ranked){
                    var keyName=RelayClient.CommitFor(lobbyService.Endpoint.AbsoluteUri,"identity").Substring(0,24);
                    lobbyIdentity=WindowsIdentityStore.LoadOrCreate(Path.Combine(Application.persistentDataPath,"Ranked",keyName+".key"));
                    await lobbyService.Request("POST","/v3/players",JsonLite.Obj(("displayName",lobbyName),("publicKey",lobbyIdentity.PublicKey)),lobbyIdentity,lobbyCancel.Token);
                    if(generation!=lobbyGeneration)return;
                    result=await lobbyService.Request("POST","/v3/ranked/queue",JsonLite.Obj(("faction",primary),("deck",deck)),lobbyIdentity,lobbyCancel.Token);
                } else result=await lobbyService.Request("POST",join?"/v2/casual/"+lobbyCode.Trim().ToUpperInvariant()+"/join":"/v2/casual",JsonLite.Obj(("faction",primary),("dataVersion","lab-2"),("deck",deck)),null,lobbyCancel.Token);
                if(generation==lobbyGeneration)AcceptLobby(result);
            }catch(Exception e){if(generation==lobbyGeneration)lobbyStatus=e.Message;}
            finally{if(generation==lobbyGeneration)lobbyBusy=false;}
        }
        void AcceptLobby(Dictionary<string,object> room){
            lobbyRoom=room;lobbyNextPoll=Time.realtimeSinceStartup+1;
            var status=Field(room,"status");
            if(status=="matched") {
                var seats=new[]{Field(room,"seat0"),Field(room,"seat1")};int seat=Array.IndexOf(seats,Field(room,"seat"));
                if(seat<0||seats.Any(string.IsNullOrEmpty)||seats[0]==seats[1])throw new InvalidOperationException("The server supplied an invalid seat mapping.");
                if(!room.TryGetValue("setup",out var setup)||!(setup is Dictionary<string,object> setupObject))throw new InvalidOperationException("The server did not confirm both edited decks.");
                var players=setupObject["players"] as List<object>;
                if(players==null||players.Count!=2||JsonLite.Stringify(players[seat])!=JsonLite.Stringify(LocalDeckSetup()))throw new InvalidOperationException("The room changed your selected deck or capital. Leave and create a new room.");
                var uri=lobbyService.Socket(Field(room,"roomId"),seats[seat]);
                var identity=lobbyRanked?lobbyIdentity:null;
                BeginOnlineSession(uri,Field(room,"roomId"),seats,seat,JsonLite.Stringify(setup),identity);return;
            }
            lobbyStatus=status=="waiting"?"Room "+Field(room,"code")+" — waiting for your opponent.":status=="queued"?"Finding an opponent for your edited deck…":status=="idle"?"Queue expired. Choose Find ranked match to try again.":Yes(room,"selfReady")?"Ready. Waiting for your opponent to confirm.":"Opponent joined. Confirm your deck and capital to begin.";
        }
        async void PollLobby(){
            if(lobbyBusy||lobbyRoom==null||lobbyService==null||Time.realtimeSinceStartup<lobbyNextPoll)return;
            var status=Field(lobbyRoom,"status");if(status=="idle")return;
            int generation=lobbyGeneration;lobbyBusy=true;
            try {var result=await lobbyService.Request("GET",lobbyRanked?"/v3/ranked/queue":"/v2/casual/"+Field(lobbyRoom,"code")+"?seat="+Uri.EscapeDataString(Field(lobbyRoom,"seat")),null,lobbyRanked?lobbyIdentity:null,lobbyCancel.Token);if(generation==lobbyGeneration)AcceptLobby(result);}
            catch(Exception e){if(generation==lobbyGeneration){lobbyStatus=e.Message;lobbyNextPoll=Time.realtimeSinceStartup+3;}}
            finally{if(generation==lobbyGeneration)lobbyBusy=false;}
        }
        async void ReadyLobby(){
            if(lobbyBusy||lobbyRoom==null)return;int generation=lobbyGeneration;lobbyBusy=true;
            try{var result=await lobbyService.Request("POST","/v2/casual/"+Field(lobbyRoom,"code")+"/ready",JsonLite.Obj(("seat",Field(lobbyRoom,"seat")),("rulesVersion","purchase-v3-hex37"),("decks","custom-decks-1"),("capitals","primary-faction")),null,lobbyCancel.Token);if(generation==lobbyGeneration)AcceptLobby(result);}
            catch(Exception e){if(generation==lobbyGeneration)lobbyStatus=e.Message;}finally{if(generation==lobbyGeneration)lobbyBusy=false;}
        }
        async void LeaveLobby(){
            if(lobbyBusy)return;int generation=lobbyGeneration;lobbyBusy=true;
            try{if(lobbyRoom!=null&&lobbyService!=null)await lobbyService.Request("DELETE",lobbyRanked?"/v3/ranked/queue":"/v2/casual/"+Field(lobbyRoom,"code"),lobbyRanked?null:JsonLite.Obj(("seat",Field(lobbyRoom,"seat"))),lobbyRanked?lobbyIdentity:null,lobbyCancel.Token);}
            catch(Exception e){if(generation==lobbyGeneration)lobbyStatus=e.Message;}
            finally{if(generation==lobbyGeneration){CancelLobby();menuScreen=Screen2.Main;}}
        }
        void DrawOnlineLobby(){
            FrontFill(new Rect(48,42,1184,718),new Color(.025f,.045f,.075f,.96f));
            GUI.Label(new Rect(84,72,950,40),"PLAY ONLINE",frontHeadingStyle());
            GUI.Label(new Rect(84,132,180,32),"Server address",frontCopy);
            var prior=GUI.enabled;GUI.enabled=!lobbyBusy&&lobbyRoom==null;
            lobbyEndpoint=GUI.TextField(new Rect(280,128,870,36),lobbyEndpoint,240);
            GUI.Label(new Rect(84,190,500,26),"PRIMARY FACTION — determines your capital",frontKicker);
            if(FrontButton(new Rect(84,228,240,44),"Zeus",false,setupSeat==0))setupSeat=0;
            if(FrontButton(new Rect(340,228,240,44),"Poseidon",false,setupSeat==1))setupSeat=1;
            if(FrontButton(new Rect(608,228,300,44),"Edit deck and capital"))deckEditorOpen=true;
            GUI.Label(new Rect(84,291,1050,48),SelectedPrototypeDeck(setupSeat).Length+" cards · up to two factions · "+CardFaces.All.First(c=>c.id==SelectedCapital(setupSeat)).name,frontCopy);
            GUI.Label(new Rect(84,355,180,32),"Room code",frontCopy);lobbyCode=GUI.TextField(new Rect(280,349,300,40),lobbyCode,10);
            if(FrontButton(new Rect(608,349,240,44),"Host casual"))LobbyBegin(false);
            if(FrontButton(new Rect(872,349,278,44),"Join casual"))LobbyBegin(true);
            GUI.Label(new Rect(84,433,180,32),"Ranked name",frontCopy);lobbyName=GUI.TextField(new Rect(280,427,300,40),lobbyName,24);
            if(FrontButton(new Rect(608,427,542,44),"Find ranked match (test server)"))LobbyBegin(false,true);
            GUI.enabled=prior;
            GUI.Label(new Rect(84,497,1060,65),lobbyStatus,frontCopy);
            GUI.enabled=!lobbyBusy&&lobbyRoom!=null&&!lobbyRanked&&Field(lobbyRoom,"status")=="preparing"&&!Yes(lobbyRoom,"selfReady");
            if(FrontButton(new Rect(608,582,542,54),"Confirm deck · Ready",true))ReadyLobby();
            GUI.enabled=!lobbyBusy;if(FrontButton(new Rect(84,686,220,44),"Leave / Back"))LeaveLobby();GUI.enabled=prior;
            GUI.Label(new Rect(340,680,810,48),"Both players use the same server. Local testing is available now; hosted play has not been deployed.",frontSmall);
        }
        GUIStyle frontHeadingStyle()=>new GUIStyle(frontCopy){fontSize=30,fontStyle=FontStyle.Bold};
    }
}
