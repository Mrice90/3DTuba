using System;
using System.Linq;
using UnityEngine;
namespace InfiniteConquest.Playtest {
    public static class CollectionCards {
        public static CardFace[] Query(string faction,string type,string search,bool byPrice){
            string term=(search??"").Trim();
            var cards=PlaytestCatalog.All.Select(c=>CardFaces.Get(c.id)).Where(c=>
                (string.IsNullOrEmpty(faction)||c.faction==faction)&&(string.IsNullOrEmpty(type)||c.type==type)&&
                (term.Length==0||((c.name??"")+" "+(c.rulesText??"")+" "+(c.description??"")).IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0));
            return byPrice?cards.OrderBy(CardFaces.PurchasePrice).ThenBy(c=>c.name,StringComparer.OrdinalIgnoreCase).ThenBy(c=>c.id).ToArray():cards.OrderBy(c=>c.name,StringComparer.OrdinalIgnoreCase).ThenBy(c=>c.id).ToArray();
        }
    }
    public sealed partial class PlaytestGame {
        string collectionFaction="",collectionType="",collectionSearch="",collectionSelected;
        bool collectionByPrice;
        int collectionPage;
        CardFace[] collectionCards;
        GUIStyle collectionSearchStyle,collectionCaption;
        static readonly string[] CollectionTypes={"","CAPITAL","LAND","STRUCTURE","CHARACTER","SPELL"};
        static readonly string[] CollectionLabels={"All cards","Capitals","Lands","Structures","Characters","Spells"};
        void OpenCollection(){menuScreen=Screen2.Collection;RefreshCollection();}
        void RefreshCollection(){
            collectionCards=CollectionCards.Query(collectionFaction,collectionType,collectionSearch,collectionByPrice);collectionPage=0;
            if(!collectionCards.Any(c=>c.id==collectionSelected))collectionSelected=collectionCards.FirstOrDefault()?.id;
        }
        void DrawCollection(){
            FrontBackdrop();FrontFill(new Rect(0,0,Screen.width,Screen.height),new Color(.015f,.03f,.05f,.94f));
            if(collectionSearchStyle==null){collectionSearchStyle=new GUIStyle(GUI.skin.textField){fontSize=18,padding=new RectOffset(12,12,8,6)};collectionCaption=new GUIStyle(frontCopy){fontSize=14,alignment=TextAnchor.MiddleLeft};}
            if(collectionCards==null)RefreshCollection();
            var old=GUI.matrix;float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
            bool purchase=CardFaces.PurchaseMode;CardFaces.PurchaseMode=true;int buttonFont=frontButton.fontSize;frontButton.fontSize=14;
            try{
                GUI.Label(new Rect(52,35,650,24),"THE ARCHIVE  /  ZEUS & POSEIDON",frontKicker);
                int font=frontTitle.fontSize;frontTitle.fontSize=38;GUI.Label(new Rect(48,65,720,58),"Card collection",frontTitle);frontTitle.fontSize=font;
                if(FrontButton(new Rect(1060,54,164,44),"Back")){menuScreen=Screen2.Main;GUI.FocusControl(null);return;}
                string[] factions={"","ZEUS","POSEIDON"};string[] labels={"Both gods","Zeus","Poseidon"};
                for(int i=0;i<3;i++)if(FrontButton(new Rect(52+i*122,128,114,38),labels[i],false,collectionFaction==factions[i])){collectionFaction=factions[i];RefreshCollection();}
                GUI.Label(new Rect(444,102,500,24),"Search names or rules",collectionCaption);
                GUI.SetNextControlName("collection-search");string search=GUI.TextField(new Rect(440,128,364,38),collectionSearch,120,collectionSearchStyle);
                if(search!=collectionSearch){collectionSearch=search;RefreshCollection();}
                if(FrontButton(new Rect(822,128,230,38),collectionByPrice?"Sort: purchase cost":"Sort: name")){collectionByPrice=!collectionByPrice;RefreshCollection();}
                if(FrontButton(new Rect(1064,128,160,38),"Clear filters")){collectionFaction=collectionType=collectionSearch="";collectionByPrice=false;RefreshCollection();GUI.FocusControl(null);}
                for(int i=0;i<CollectionTypes.Length;i++)if(FrontButton(new Rect(52+i*130,178,122,34),CollectionLabels[i],false,collectionType==CollectionTypes[i])){collectionType=CollectionTypes[i];RefreshCollection();}
                FrontFill(new Rect(822,224,402,496),new Color(.045f,.065f,.09f));
                int begin=collectionPage*8;
                for(int i=0;i<8&&begin+i<collectionCards.Length;i++){
                    var card=collectionCards[begin+i];var r=new Rect(52+(i%4)*192,224+(i/4)*252,160,240);
                    if(card.id==collectionSelected)FrontFill(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),FrontGold);
                    CardFaces.Draw(r,card.id,true);
                    if(GUI.Button(r,GUIContent.none,GUIStyle.none)){collectionSelected=card.id;sfx.PlayUi("click");GUI.FocusControl(null);}
                }
                if(collectionCards.Length==0){GUI.Label(new Rect(70,300,690,55),"No cards match these filters.",frontCopy);GUI.Label(new Rect(70,350,690,55),"Try another name, or clear the filters to browse the archive.",collectionCaption);}
                if(collectionSelected!=null)CardFaces.Draw(new Rect(858,224,330,495),collectionSelected,false);
                else GUI.Label(new Rect(850,390,348,70),"Select a card to inspect its art and rules.",frontCopy);
                int pages=Mathf.Max(1,(collectionCards.Length+7)/8);
                GUI.Label(new Rect(52,744,400,32),collectionCards.Length+" cards  ·  Page "+(collectionPage+1)+" of "+pages,collectionCaption);
                bool enabled=GUI.enabled;GUI.enabled=enabled&&collectionPage>0;
                if(FrontButton(new Rect(444,740,160,38),"Previous"))collectionPage--;
                GUI.enabled=enabled&&collectionPage+1<pages;if(FrontButton(new Rect(620,740,160,38),"Next"))collectionPage++;
                GUI.enabled=enabled;GUI.Label(new Rect(842,744,382,32),"Browse the full playable card catalogue",collectionCaption);
            }finally{GUI.matrix=old;CardFaces.PurchaseMode=purchase;frontButton.fontSize=buttonFont;}
        }
    }
}
