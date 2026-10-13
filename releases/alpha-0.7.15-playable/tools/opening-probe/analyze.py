import csv,glob,sys,collections,statistics as st,json
rows=[]
for f in sorted(glob.glob(sys.argv[1]+'/*.csv')): rows+=list(csv.DictReader(open(f)))
res={}
T=collections.defaultdict(list); M={}
for r in rows:
    k=(r['slots'],r['difficulty'],r['seed'],r['faction'],r['seat'])
    if r['kind']=='turn': T[k].append(r)
    else: M[k]=r
def I(x): return int(x)
for slots in ['off','on']:
  for diff in ['HERO','MORTAL']:
    keys=[k for k in M if k[0]==slots and k[1]==diff]
    out={}
    # fields nothing: no summon this turn and no characters on board at end
    for pt in range(1,7):
        tr=[t for k in keys for t in T[k] if I(t['pturn'])==pt]
        n=len(tr)
        out[pt]=dict(n=n,
          fields_nothing=sum(t['chars_end']=='0' for t in tr)/n,
          end_only=sum(t['actions']=='0' for t in tr)/n,
          locked=sum(t['summon_offered']=='1' and t['summons']=='0' and t['chars_end']=='0' and I(t['blocked_offers'])>0 for t in tr)/n,
          no_struct_hand=sum(t['structs_board']=='0' and t['hand_structs']=='0' for t in tr)/n)
    # empty-board streak from turn 1
    streaks=[]
    for k in keys:
        s=0
        for t in sorted(T[k],key=lambda t:I(t['pturn'])):
            if t['chars_end']=='0': s+=1
            else: break
        streaks.append((s,k))
    lockstreak=[]
    for k in keys:
        best=cur=0
        for t in sorted(T[k],key=lambda t:I(t['pturn'])):
            if t['summon_offered']=='1' and t['summons']=='0' and I(t['blocked_offers'])>0: cur+=1; best=max(best,cur)
            else: cur=0
        lockstreak.append((best,k))
    fs=[I(M[k]['first_summon']) for k in keys]
    never=[k for k in keys if M[k]['never_summoned']=='1']
    timeouts=sum(1 for k in keys if M[k]['winner']=='none')
    # first structure reason at personal turn 3 for players without any structure on board
    reasons=collections.Counter()
    for k in keys:
        t3=[t for t in T[k] if I(t['pturn'])<=3]
        if not t3: continue
        last=t3[-1]
        if last['structs_board']!='0' or len(t3)<3: continue
        hs=any(I(t['hand_structs'])>0 for t in t3); us=any(t['usable_struct']=='1' for t in t3); off=any(t['struct_offered']=='1' for t in t3)
        reasons['no structure drawn' if not hs else 'structure too costly for turn' if not us else 'no land/GP to place it' if not off else 'offered but bot declined']+=1
    print(f"\n== slots {slots} / {diff}: {len(keys)} player-matches ==")
    for pt,v in out.items(): print(f"  pturn {pt}: fields nothing {v['fields_nothing']*100:5.1f}%  end-only {v['end_only']*100:5.1f}%  summon blocked & empty {v['locked']*100:5.1f}%  no structure anywhere {v['no_struct_hand']*100:5.1f}%")
    sc=collections.Counter(min(s,8) for s,_ in streaks)
    print("  opening empty-board streak (turns):",sorted(sc.items()))
    lc=collections.Counter(min(s,8) for s,_ in lockstreak)
    print("  longest blocked-summon streak:",sorted(lc.items()))
    print("  first summon turn: mean %.2f median %s p90 %s max %s; never summoned %d; timeouts %d"%(st.mean([x for x in fs if x>0]),st.median([x for x in fs if x>0]),sorted([x for x in fs if x>0])[int(.9*len([x for x in fs if x>0]))],max(fs),len(never),timeouts))
    print("  no structure on board after 3 turns, why:",dict(reasons))
    if slots=='on':
        print("  worst streaks:",[(s,k[2],k[3],k[4],M[k]['starter'],M[k]['turns'],M[k]['winner']) for s,k in sorted(streaks,reverse=True)[:6]])
        print("  worst lock streaks:",[(s,k[2],k[3],k[4],M[k]['starter']) for s,k in sorted(lockstreak,reverse=True)[:6]])
        nev_reason=collections.Counter()
        for k in never:
            won=M[k]['winner']==k[4]
            nev_reason[('never placed a structure' if M[k]['first_struct']=='-1' else 'had a structure, no character to place' if all(t['summon_offered']=='0' or t['chars_hand']=='0' for t in T[k] if I(t['structs_board'])>0) else 'had structure + characters, bot declined')+(' (won)' if won else ' (lost)')]+=1
        print("  never summoned:",dict(nev_reason), "median total turns", st.median([I(M[k]['turns']) for k in never]) if never else "-")
