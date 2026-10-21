using System;
using System.Collections.Generic;
using System.Linq;
using InfiniteConquest.Playtest;
using UnityEngine;

// AI-209: laptop performance build. Checks that a summon using the cached skinned-bounds fit places, sizes and culls
// every rigged character exactly like the full fit, then runs the card-sfx build chain unchanged.
public static class PerfBuild {
    public static void Build() { Check(); CardSfxBuild.Build(); }

    public static void Check() {
        int checks = 0; var fails = new List<string>();
        Action<bool, string> check = (ok, m) => { checks++; if (!ok) fails.Add(m); };
        PlaytestCatalog.Load();
        var factory = new TokenFactory(Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("Universal Render Pipeline/Unlit"), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        var rigged = PlaytestCatalog.All.Where(c => c.type == "CHARACTER" && c.HasModel).ToList();
        int riggedChecked = 0;
        foreach (var card in rigged) {
            TokenFactory.ClearBoundsCache();
            TokenFactory.NoBoundsCache = true;
            var fresh = factory.Create(card, 0, out bool real);
            TokenFactory.NoBoundsCache = false;
            var cached = factory.Create(card, 0, out _);
            try {
                var a = fresh.GetComponentsInChildren<SkinnedMeshRenderer>(); var b = cached.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (!real || a.Length == 0) continue;
                riggedChecked++;
                check(a.Length == b.Length, card.id + " renderer count");
                for (int i = 0; i < Mathf.Min(a.Length, b.Length); i++)
                    check(a[i].localBounds == b[i].localBounds, card.id + " bounds " + a[i].localBounds + " vs " + b[i].localBounds);
                var ma = fresh.transform.Find("Model"); var mb = cached.transform.Find("Model");
                if (ma == null) ma = fresh.GetComponentInChildren<Animator>().transform;
                if (mb == null) mb = cached.GetComponentInChildren<Animator>().transform;
                check((ma.position - mb.position).sqrMagnitude < 1e-10f, card.id + " placement " + ma.position + " vs " + mb.position);
                check((ma.lossyScale - mb.lossyScale).sqrMagnitude < 1e-10f, card.id + " scale " + ma.lossyScale + " vs " + mb.lossyScale);
                check(Mathf.Abs(fresh.GetComponent<Piece>().Height - cached.GetComponent<Piece>().Height) < 1e-5f, card.id + " height");
            } finally { UnityEngine.Object.DestroyImmediate(fresh); UnityEngine.Object.DestroyImmediate(cached); }
        }
        TokenFactory.ClearBoundsCache();
        check(riggedChecked == 48, "48 rigged characters compared, found " + riggedChecked);
        if (fails.Count > 0) throw new Exception("PERF_CHECK FAIL " + fails.Count + "/" + checks + ": " + string.Join("; ", fails.Take(10)));
        Debug.Log("PERF_CHECK PASS " + checks + " checks, " + riggedChecked + " rigged characters: cached bounds fit matches the full fit");
    }
}
