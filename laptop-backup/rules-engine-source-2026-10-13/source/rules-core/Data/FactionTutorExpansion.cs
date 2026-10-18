// Port of game-cli/FactionTutorExpansion.java at authoritative TubaExperiment 992bc95.
using System.Collections.Generic;
using InfiniteConquest.RulesCore.Core;

namespace InfiniteConquest.RulesCore.Data
{
    public static class FactionTutorExpansion
    {
        public static IReadOnlyList<CardDefinition> Cards()
        {
            var result = new List<CardDefinition>();
            // Alphabetical faction order matches the Java source's sorted stream.
            Add(result,"POSEIDON",new[]{"Mole-Tide Channel","Vanguard Reef","Leviathan Mooring Shelf","Sunken Muster Basin","Worldsea Anchorage"},
                new[]{"Undertow Burrow Gate","Tideguard Barracks","Nereid Calling Conch","Leviathan Muster Dock","Thalassic Hero Hall"});
            Add(result,"ZEUS",new[]{"Stormwright's Approach","Blinkway Plateau","Far-Sight Cloudbank","Keraunic Assembly Field","Olympian Muster Sky"},
                new[]{"Sparkstep Beacon","Cloudline Dispatch","Sharp-Shot Observatory","Seraphic Relay","Skyfather's Summons"});
            return result.AsReadOnly();
        }

        private static void Add(List<CardDefinition> result,string faction,string[] lands,string[] structures)
        {
            for(int index=0;index<5;index++)
            {
                result.Add(Tutor(faction,lands[index],CardType.LAND,index));
                result.Add(Tutor(faction,structures[index],CardType.STRUCTURE,index));
            }
        }

        private static CardDefinition Tutor(string faction,string name,CardType type,int index)
        {
            bool land = type == CardType.LAND;
            int hp = land ? System.Math.Min(22,8+index*4) : 10+index*4;
            Keyword specialty = land ? (faction=="ZEUS" ? Keyword.HIGH_GROUND : Keyword.SANCTUARY)
                : (faction=="ZEUS" ? Keyword.WATCHTOWER : Keyword.MEDIC_TENT);
            var keywords = new List<Keyword>();
            var values = new Dictionary<Keyword,KeywordValue>();
            if(index>=3)
            {
                keywords.Add(specialty);
                values.Add(specialty,new KeywordValue(specialty==Keyword.MEDIC_TENT ? 1 : 0,1));
            }
            return new CardDefinition(faction.ToLowerInvariant()+"_tutor_"+type.ToString().ToLowerInvariant()+"_"+(index+1),
                name,type,faction,2+index*2,0,0,0,0,hp,keywords,new List<SpellEffect>(),1+index/2,DevelopmentPassive.NONE,
                new[]{new CardAbility(AbilityTrigger.ACTIVATED,land ? AbilityEffectType.DRAW_STRUCTURE : AbilityEffectType.DRAW_CHARACTER,1,2)},
                values,new[]{land ? "MUSTER_GROUND" : "RECRUITMENT"},index<2 ? 0 : index==2 ? 1 : 2);
        }
    }
}
