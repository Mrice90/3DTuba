using System;
using System.Linq;
namespace InfiniteConquest.Playtest {
    public static class DeckMarketQuery {
        public static bool KeepSavedDeck(string[] saved,string[] previous,string[] allowed) {
            return saved!=null && saved.Length<=40 && saved.Distinct().Count()>=10 && saved.GroupBy(id=>id).All(g=>g.Count()<=4) && saved.All(id=>allowed.Contains(id)) && !saved.OrderBy(id=>id,StringComparer.Ordinal).SequenceEqual(previous.OrderBy(id=>id,StringComparer.Ordinal));
        }
        public static readonly string[] Categories={"ALL","LAND","STRUCTURE","CHARACTER","SPELL"};
        public static BridgeMarketCard[] Filter(BridgeMarketCard[] source,int tab,string search,int min,int max,bool affordable,int gold,int sort) {
            var q=(source??new BridgeMarketCard[0]).Where(c=>c.remaining>0 && (tab==0 || c.category==Categories[tab]) && c.price>=min && c.price<=max && (!affordable || c.price<=gold) && (string.IsNullOrWhiteSpace(search) || (c.name??"").IndexOf(search.Trim(),StringComparison.OrdinalIgnoreCase)>=0));
            IOrderedEnumerable<BridgeMarketCard> ordered;
            switch(sort) {
                case 1:ordered=q.OrderByDescending(c=>c.price).ThenBy(c=>c.name,StringComparer.OrdinalIgnoreCase);break;
                case 2:ordered=q.OrderBy(c=>c.name,StringComparer.OrdinalIgnoreCase);break;
                case 3:ordered=q.OrderByDescending(c=>c.remaining).ThenBy(c=>c.name,StringComparer.OrdinalIgnoreCase);break;
                default:ordered=q.OrderBy(c=>c.price).ThenBy(c=>c.name,StringComparer.OrdinalIgnoreCase);break;
            }
            return ordered.ThenBy(c=>c.card_id,StringComparer.Ordinal).ToArray();
        }
    }
}
