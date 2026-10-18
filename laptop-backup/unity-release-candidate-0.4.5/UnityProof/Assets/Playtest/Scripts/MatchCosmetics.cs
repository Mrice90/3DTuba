using System;
namespace InfiniteConquest.Playtest {
    // Future room handshake supplies each authenticated seat's tile ID. Background and coin
    // remain viewer-local. Presentation snapshots never belong in authoritative rules hashes.
    public static class MatchCosmetics {
        static string[] seats;
        public static bool Active=>seats!=null;
        public static string Resolve(int seat,string fallback)=>seats!=null&&seat>=0&&seat<2?seats[seat]:fallback;
        public static string Validate(string id,int seat)=>Array.IndexOf(BoardSkin.Presets,id)>=0?id:BoardSkin.DefaultFor(seat);
        public static void SetSeats(string seat0,string seat1){seats=new[]{Validate(seat0,0),Validate(seat1,1)};}
        public static void SetForViewer(int viewerSeat,string own,string opponent){if(viewerSeat<0||viewerSeat>1)throw new ArgumentOutOfRangeException(nameof(viewerSeat));SetSeats(viewerSeat==0?own:opponent,viewerSeat==1?own:opponent);}
        public static void Clear(){seats=null;}
    }
}
