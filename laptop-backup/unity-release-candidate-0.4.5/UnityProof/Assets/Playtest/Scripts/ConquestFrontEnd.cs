using UnityEngine;

namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        Texture2D conquestBackdrop;
        GUIStyle frontTitle, frontKicker, frontCopy, frontButton, frontSmall;
        static readonly Color FrontGold = new Color(.83f,.71f,.47f);
        void FrontStyles() {
            if(frontTitle!=null) return;
            conquestBackdrop=Resources.Load<Texture2D>("Menu/Citadel");
            frontTitle=new GUIStyle(GUI.skin.label){fontSize=62,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperLeft,wordWrap=false};
            frontTitle.normal.textColor=new Color(.96f,.94f,.87f);
            frontKicker=new GUIStyle(GUI.skin.label){fontSize=12,fontStyle=FontStyle.Bold};frontKicker.normal.textColor=FrontGold;
            frontCopy=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true};frontCopy.normal.textColor=new Color(.73f,.79f,.83f);
            frontSmall=new GUIStyle(frontCopy){fontSize=12};
            frontButton=new GUIStyle(GUI.skin.label){fontSize=19,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(22,16,0,0)};
            frontButton.normal.textColor=new Color(.92f,.93f,.91f);
        }
        void FrontFill(Rect r,Color color) => GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,color,0,0);
        void FrontBackdrop() {
            FrontStyles();
            if(conquestBackdrop!=null) GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),conquestBackdrop,ScaleMode.ScaleAndCrop);
            else FrontFill(new Rect(0,0,Screen.width,Screen.height),new Color(.025f,.045f,.075f));
        }
        bool FrontButton(Rect r,string label,bool primary=false,bool selected=false) {
            bool hover=r.Contains(Event.current.mousePosition);
            FrontFill(r,primary ? new Color(.72f,.59f,.34f,hover?1f:.92f) : new Color(.04f,.08f,.12f,hover||selected?.94f:.74f));
            var line=primary?new Color(.97f,.85f,.58f):selected?FrontGold:new Color(.28f,.37f,.43f,.7f);
            FrontFill(new Rect(r.x,r.y,r.width,1),line); FrontFill(new Rect(r.x,r.yMax-1,r.width,1),line);
            if(selected) FrontFill(new Rect(r.x,r.y,3,r.height),FrontGold);
            frontButton.normal.textColor=primary?new Color(.06f,.08f,.10f):new Color(.93f,.94f,.92f);
            bool hit=GUI.Button(r,label,frontButton)&&Time.unscaledTime-menuOpenedAt>.35f;
            if(hit) sfx.PlayUi("click");
            return hit;
        }
        bool DrawConquestFrontEnd() {
            FrontBackdrop();
            if(deckEditorOpen) {DrawDeckEditor();return true;}
            var previous=GUI.matrix;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
            float ox=(Screen.width-1280*scale)*.5f,oy=(Screen.height-800*scale)*.5f;
            GUI.matrix=Matrix4x4.TRS(new Vector3(ox,oy,0),Quaternion.identity,new Vector3(scale,scale,1));
            try {
                if(menuScreen==Screen2.Main) {
                    GUI.Label(new Rect(74,72,550,24),"A  B A T T L E  O F  G O D S  A N D  E M P I R E S",frontKicker);
                    GUI.Label(new Rect(69,112,700,86),"INFINITE",frontTitle);
                    GUI.Label(new Rect(69,176,700,86),"CONQUEST",frontTitle);
                    FrontFill(new Rect(76,276,64,2),FrontGold);
                    GUI.Label(new Rect(76,302,365,58),"Raise your domain. Command the storm.\nClaim the battlefield, one hex at a time.",frontCopy);
                    if(FrontButton(new Rect(76,393,344,62),"Enter the battlefield",true)) menuScreen=Screen2.PlaySetup;
                    if(FrontButton(new Rect(76,469,344,48),"Card collection")) OpenCollection();
                    if(FrontButton(new Rect(76,527,344,48),"Learn to play")) menuScreen=Screen2.HowTo;
                    if(FrontButton(new Rect(76,585,344,48),"Settings")) menuScreen=Screen2.Settings;
                    if(FrontButton(new Rect(76,643,344,48),"Board appearance"))menuScreen=Screen2.BoardAppearance;
                    if(FrontButton(new Rect(76,704,164,34),"Quit")) Application.Quit();
                    GUI.Label(new Rect(76,745,580,22),"ZEUS  /  POSEIDON     ·     37-HEX BATTLEFIELD",frontKicker);
                    GUI.Label(new Rect(984,745,260,22),"DEVELOPMENT PREVIEW  ·  0.4.5",frontSmall);
                } else if(menuScreen==Screen2.HowTo) {
                    FrontFill(new Rect(48,42,1184,718),new Color(.025f,.045f,.075f,.96f));
                    GUI.Label(new Rect(84,72,650,24),"LEARN TO PLAY",frontKicker);
                    int fs=frontTitle.fontSize;frontTitle.fontSize=38;
                    GUI.Label(new Rect(80,109,1020,62),"Your first conquest",frontTitle);frontTitle.fontSize=fs;
                    FrontLesson(84,194,"01  CLAIM VICTORY","Destroy the enemy capital's 20 HP. Choose one of the 15 home hexes for your own capital; the middle seven hexes are neutral.");
                    FrontLesson(644,194,"02  GROW YOUR DOMAIN","Earn GP from your capital and lands. Buy remaining deck copies in the shop, once per category each turn. Playing cards from your hand is free.");
                    FrontLesson(84,342,"03  BUILD YOUR ARMY","Place a land, then a structure. Structures supply summon slots; characters reserve slots until they leave the board. Check each card's slot cost.");
                    FrontLesson(644,342,"04  COMMAND YOUR UNITS","Select a character, then a glowing hex to move or a red target to attack. Characters cannot attack on their summon turn. Passing enemies can trigger opportunity attacks.");
                    FrontLesson(84,490,"05  USE YOUR POWERS","Right-click a hex for land and structure abilities, including covered sources. Spells target exposed cards. Select a highlighted target, then a destination if asked.");
                    FrontLesson(644,490,"06  CONTROL THE VIEW","Right-drag to rotate and tilt. Scroll to zoom; WASD or middle-drag to pan. Keys 1–4 select views; Home resets. Hover cards to read them. Esc pauses.");
                    if(FrontButton(new Rect(84,686,160,44),"Back"))menuScreen=Screen2.Main;
                } else if(menuScreen==Screen2.Settings) {
                    FrontFill(new Rect(48,42,1184,718),new Color(.025f,.045f,.075f,.96f));
                    GUI.Label(new Rect(84,72,650,24),"MAKE IT YOURS",frontKicker);
                    int fs=frontTitle.fontSize;frontTitle.fontSize=38;
                    GUI.Label(new Rect(80,109,920,62),"Settings",frontTitle);frontTitle.fontSize=fs;
                    GUI.Label(new Rect(84,215,340,30),"Master volume",frontCopy);
                    masterVolume=GUI.HorizontalSlider(new Rect(480,225,660,30),masterVolume,0,1);
                    GUI.Label(new Rect(84,285,340,30),"Sound effects",frontCopy);
                    sfxVolume=GUI.HorizontalSlider(new Rect(480,295,660,30),sfxVolume,0,1);
                    AudioListener.volume=masterVolume;sfx.Volume=sfxVolume;
                    GUI.Label(new Rect(84,363,340,30),"Turn chime",frontCopy);
                    if(FrontButton(new Rect(480,350,316,48),"Calm",false,TurnChime))TurnChime=true;
                    if(FrontButton(new Rect(824,350,316,48),"Off",false,!TurnChime))TurnChime=false;
                    GUI.Label(new Rect(84,437,340,30),"Display",frontCopy);
                    bool full=Screen.fullScreen;
                    if(FrontButton(new Rect(480,424,316,48),"Fullscreen",false,full)&&!full)Screen.fullScreenMode=FullScreenMode.FullScreenWindow;
                    if(FrontButton(new Rect(824,424,316,48),"Windowed",false,!full)&&full)Screen.fullScreenMode=FullScreenMode.Windowed;
                    GUI.Label(new Rect(84,510,340,30),"Graphics quality",frontCopy);
                    int q=QualitySettings.GetQualityLevel();var names=QualitySettings.names;
                    if(FrontButton(new Rect(480,497,90,48),"−")&&q>0)QualitySettings.SetQualityLevel(q-1,true);
                    GUI.Label(new Rect(600,510,390,30),names.Length>0?names[q]:"Default",frontCopy);
                    if(FrontButton(new Rect(1050,497,90,48),"+")&&q<names.Length-1)QualitySettings.SetQualityLevel(q+1,true);
                    GUI.Label(new Rect(480,583,660,58),"Current resolution: "+Screen.width+" × "+Screen.height+". Resize the window to adjust your play area.",frontCopy);
                    if(FrontButton(new Rect(84,686,160,44),"Back")){SaveSettings();menuScreen=Screen2.Main;}
                } else {
                    FrontFill(new Rect(48,42,1184,718),new Color(.025f,.045f,.075f,.93f));
                    GUI.Label(new Rect(84,72,650,24),"PREPARE FOR CONQUEST",frontKicker);
                    int fs=frontTitle.fontSize;frontTitle.fontSize=38;
                    GUI.Label(new Rect(80,109,920,62),"Choose your allegiance",frontTitle);frontTitle.fontSize=fs;
                    DrawAllegiance(new Rect(84,190,536,238),0,"ZEUS","SOVEREIGN OF THE STORM","Storm-forged legions, swift strikes and precision fire.","zeus_capital_olympus_citadel");
                    DrawAllegiance(new Rect(644,190,536,238),1,"POSEIDON","LORD OF THE DEEP","Tide engines, resilient guardians and abyssal power.","poseidon_capital_atlantis_nexus");
                    GUI.Label(new Rect(84,455,500,24),"CHOOSE YOUR CHALLENGE",frontKicker);
                    string[] names={"Mortal  ·  Easy","Hero  ·  Normal","Demigod  ·  Hard"};
                    for(int i=0;i<3;i++)if(FrontButton(new Rect(84+i*372,493,352,48),names[i],false,setupDifficulty==i))setupDifficulty=i;
                    GUI.Label(new Rect(84,562,1030,45),"Build your deck, then choose a home hex for your capital. The opening coin decides who acts first.",frontCopy);
                    if(FrontButton(new Rect(84,658,160,48),"Back"))menuScreen=Screen2.Main;
                    if(FrontButton(new Rect(266,658,228,48),"Edit your deck"))deckEditorOpen=true;
                    if(FrontButton(new Rect(828,646,352,62),"Begin conquest",true))StartMatchFromMenu(Random.Range(1,1000000));
                }
            } finally { GUI.matrix=previous; }
            return true;
        }
        void FrontLesson(float x,float y,string heading,string copy) {
            GUI.Label(new Rect(x,y,510,25),heading,frontKicker);
            GUI.Label(new Rect(x,y+34,510,108),copy,frontCopy);
        }
        void DrawAllegiance(Rect r,int seat,string name,string subtitle,string description,string card) {
            FrontFill(r,new Color(.06f,.105f,.15f));
            var art=CardFaces.Art(card);
            if(art!=null) {var old=GUI.color;GUI.color=new Color(.8f,.8f,.8f,.75f);GUI.DrawTexture(new Rect(r.xMax-222,r.y,222,r.height),art,ScaleMode.ScaleAndCrop);GUI.color=old;}
            FrontFill(new Rect(r.x,r.y,r.width,r.height),new Color(.015f,.035f,.06f,.38f));
            if(FrontButton(new Rect(r.x,r.y,r.width,62),name,false,setupSeat==seat))setupSeat=seat;
            GUI.Label(new Rect(r.x+22,r.y+88,r.width-44,25),subtitle,frontKicker);
            GUI.Label(new Rect(r.x+22,r.y+126,290,65),description,frontCopy);
            GUI.Label(new Rect(r.x+22,r.y+206,280,22),setupSeat==seat?"SELECTED":"SELECT THIS FACTION",frontSmall);
            if(GUI.Button(r,GUIContent.none,GUIStyle.none)){setupSeat=seat;sfx.PlayUi("click");}
        }
    }
}
