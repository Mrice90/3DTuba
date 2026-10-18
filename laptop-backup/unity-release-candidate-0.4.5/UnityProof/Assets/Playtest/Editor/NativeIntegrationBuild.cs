using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using InfiniteConquest.Playtest;
public static class NativeIntegrationBuild {
    static void Check(bool pass,string name) {if(!pass)throw new Exception("NATIVE FAIL "+name);Debug.Log("NATIVE PASS "+name);}
    static bool Receive(BridgeClient client,out BridgeResponse response) {
        var deadline=DateTime.UtcNow.AddSeconds(15);
        do {if(client.TryReceive(out response))return true;System.Threading.Thread.Sleep(10);}while(DateTime.UtcNow<deadline);
        response=null;return false;
    }
    public static void Validate() {
        for(int seat=0;seat<2;seat++) {
            using(var client=BridgeClient.Spawn("native",null)) {
                client.New(42,seat,"HERO",3,seat==0?0:6,false,true);
                Check(Receive(client,out var response)&&response.ok,"in-process new seat "+seat);
                Check(response.state.HasCapitals&&response.state.slots_enabled,"capital metadata/slot policy seat "+seat);
                Check(response.state.players[1-seat].hand.Length==0,"opponent hand redacted seat "+seat);
                Check(response.legal.Length>0,"legal native actions seat "+seat);
                int revision=response.revision;
                client.Act("fabricated");Check(Receive(client,out response)&&!response.ok&&response.revision==revision,"stale rejection without revision mutation");
                client.Hash();Check(Receive(client,out response)&&response.ok&&response.state_hash.Length==64,"native canonical hash");
            }
        }
        R3Build.Validate();CameraSnapChecks.Run();
        Directory.CreateDirectory("Build");File.WriteAllText("Build/native-integration-validation.json","{\"passed\":true,\"bothSeats\":true,\"native\":true}");
        Debug.Log("NATIVE_INTEGRATION PASS");
    }
    public static void Build() {Validate();CoinPrototypeBuild.Build();}
}
