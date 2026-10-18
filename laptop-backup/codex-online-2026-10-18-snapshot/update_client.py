from pathlib import Path
p=Path(__file__).resolve().parent/'UnityProof/Assets/Playtest/Scripts'
f=p/'PrototypeDeckEditor.cs';s=f.read_text(encoding='utf-8-sig')
s=s.replace('c.faction==(seat==0?"ZEUS":"POSEIDON") && c.type!="CAPITAL"','(c.faction=="ZEUS"||c.faction=="POSEIDON"||c.faction=="NEUTRAL") && c.type!="CAPITAL"')
s=s.replace('c.faction==(setupSeat==0?"ZEUS":"POSEIDON") && c.type!="CAPITAL"','(c.faction=="ZEUS"||c.faction=="POSEIDON"||c.faction=="NEUTRAL") && c.type!="CAPITAL"')
s=s.replace('        string PrototypeStrategy', '''        string SelectedCapital(int primary) {
            string faction=primary==0?"ZEUS":"POSEIDON";
            string fallback=primary==0?"zeus_capital_olympus_citadel":"poseidon_capital_atlantis_nexus";
            string saved=PlayerPrefs.GetString("ic.purchase.capital."+primary,fallback);
            return CardFaces.All.Any(c=>c.id==saved&&c.type=="CAPITAL"&&c.faction==faction)?saved:fallback;
        }
        string PrototypeStrategy''')
s=s.replace('GUI.Label(new Rect(panel.x+16,panel.y+46,panel.width-32,25),PrototypeStrategy(setupSeat),smallStyle);','''var capitals=CardFaces.All.Where(c=>c.type=="CAPITAL"&&c.faction==(setupSeat==0?"ZEUS":"POSEIDON")).ToArray();
            int selectedCapital=Array.FindIndex(capitals,c=>c.id==SelectedCapital(setupSeat));
            GUI.Label(new Rect(panel.x+16,panel.y+43,panel.width-32,23),"Primary: "+(setupSeat==0?"Zeus":"Poseidon")+" | Choose a capital from this faction",smallStyle);
            int choice=GUI.SelectionGrid(new Rect(panel.x+16,panel.y+70,panel.width-32,32),selectedCapital,capitals.Select(c=>c.name).ToArray(),capitals.Length);
            if(choice>=0&&choice!=selectedCapital)PlayerPrefs.SetString("ic.purchase.capital."+setupSeat,capitals[choice].id);''')
s=s.replace('panel.y+73','panel.y+107').replace('panel.y+106,panel.width-28,panel.height-174','panel.y+140,panel.width-28,panel.height-208')
s=s.replace('Maximum 4 copies per card | At least 10 different cards | Only selected cards can be bought','Up to 2 factions | Maximum 4 copies | At least 10 different cards | Capital stays outside deck')
s=s.replace('card.name+" | "+card.type','card.name+" | "+card.faction+" | "+card.type')
f.write_text(s,encoding='utf-8')
f=p/'BridgeClient.cs';s=f.read_text(encoding='utf-8-sig');s=s.replace('        public static string[] SelectedDeck;','        public static string[] SelectedDeck;\n        public static string HumanSetupJson, OnlineSetupJson;')
s=s.replace('static string DeckRequest(bool multiplayer) => multiplayer || SelectedDeck == null ?', 'static string DeckRequest(bool multiplayer) => multiplayer ? (OnlineSetupJson==null?"":",\\"online_setup\\":"+OnlineSetupJson) : HumanSetupJson!=null ? ",\\"human_setup\\":"+HumanSetupJson : SelectedDeck == null ?')
f.write_text(s,encoding='utf-8')
f=p/'PlaytestMenus.cs';s=f.read_text(encoding='utf-8-sig').replace('BoardAppearance, Collection }','BoardAppearance, Collection, Online }')
s=s.replace('            CloseOnline();','            CancelLobby();\n            CloseOnline();')
s=s.replace('BridgeClient.SelectedDeck=SelectedPrototypeDeck(setupSeat);','BridgeClient.SelectedDeck=SelectedPrototypeDeck(setupSeat);\n            BridgeClient.HumanSetupJson=IC.Net.JsonLite.Stringify(LocalDeckSetup());')
f.write_text(s,encoding='utf-8')
f=p/'ConquestFrontEnd.cs';s=f.read_text(encoding='utf-8-sig');s=s.replace('                } else if(menuScreen==Screen2.HowTo)', '                } else if(menuScreen==Screen2.Online) { DrawOnlineLobby();\n                } else if(menuScreen==Screen2.HowTo)')
s=s.replace('if(FrontButton(new Rect(76,704,164,34),"Quit"))','if(FrontButton(new Rect(250,704,170,34),"Play online"))OpenOnlineLobby();\n                    if(FrontButton(new Rect(76,704,164,34),"Quit"))')
f.write_text(s,encoding='utf-8')
f=p/'PlaytestGame.cs';s=f.read_text(encoding='utf-8-sig').replace('            PumpOnline();','            PollLobby();\n            PumpOnline();');f.write_text(s,encoding='utf-8')
f=Path(__file__).resolve().parent/'net-lab/casual.js';s=f.read_text().replace("decks:'faction-starters', capitals:'default'","decks:'custom-decks-1', capitals:'primary-faction'");f.write_text(s,encoding='utf-8')
