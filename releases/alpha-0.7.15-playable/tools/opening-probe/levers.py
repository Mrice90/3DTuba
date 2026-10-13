import csv,glob,sys,collections,statistics as st
rows=[]
for f in glob.glob(sys.argv[1]+'/*.csv')+glob.glob(sys.argv[2]+'/off-HERO-*.csv'):
    for r in csv.DictReader(open(f)):
        r['v']=(r.get("variant") or "base") if r['slots']=='on' else 'slots off'
        rows.append(r)
I=int
print("| Variant | Empty board T1 | T2 | T3 | First summon mean | p90 | worst | Never summoned | Blocked-summon streak >=4 | Win rate of player going first |")
print("|---|---|---|---|---|---|---|---|---|---|")
for v in ['slots off','base','mull','deck16','deck16mull']:
    t=[r for r in rows if r['v']==v and r['kind']=='turn']; m=[r for r in rows if r['v']==v and r['kind']=='match']
    e=lambda pt: sum(r['chars_end']=='0' for r in t if r['pturn']==str(pt))/sum(1 for r in t if r['pturn']==str(pt))
    fs=[I(r['first_summon']) for r in m if I(r['first_summon'])>0]
    never=sum(r['never_summoned']=='1' for r in m)
    streak=collections.defaultdict(list)
    for r in t: streak[(r['seed'],r['faction'],r['seat'],r['starter'])].append(r)
    s4=0
    for k,ts in streak.items():
        best=cur=0
        for r in sorted(ts,key=lambda r:I(r['pturn'])):
            if r['summon_offered']=='1' and r['summons']=='0' and I(r['blocked_offers'])>0: cur+=1;best=max(best,cur)
            else: cur=0
        s4+= best>=4
    firstwin=sum(1 for r in m if r['starter']=='1' and r['winner']==r['seat'])/sum(1 for r in m if r['starter']=='1')
    print(f"| {v} | {e(1)*100:.0f}% | {e(2)*100:.0f}% | {e(3)*100:.0f}% | {st.mean(fs):.2f} | {sorted(fs)[int(.9*len(fs))]} | {max(fs)} | {never} ({never/len(m)*100:.1f}%) | {s4} ({s4/len(m)*100:.1f}%) | {firstwin*100:.0f}% |")
