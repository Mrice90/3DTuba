using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    // AI-215: the 123 card idle sounds and the 24 signature sounds that were imported but silent (PR #24 set).
    // Idle: every 5-9 s of a running match one piece in play that has its own idle sound murmurs it quietly at its
    // position (never the same piece twice in a row; off while paused, over, fast-forwarded or muted).
    // Signature: top-rarity spells, lands and structures play their signature instead of cast/place/deploy (the 8
    // top-rarity characters already did), and each capital plays its signature once when both capitals are in play.
    public sealed partial class PlaytestGame {
        public const float IdleGain = .35f, CapitalSignatureGain = .8f;
        public const float IdleMinGap = 5f, IdleMaxGap = 9f;
        float nextIdleAt; string lastIdleInstance; bool capitalSignaturesPlayed;
        public int IdleCuesPlayed { get; private set; }
        public int SignatureCuesPlayed { get; private set; }
        readonly List<string> ambientCueLog = new List<string>();

        public static bool HasOwnCue(SfxBank bank, CardEntry card, string cue) => card.HasCue(cue) || bank.ResolveLibrary(card.id + "_" + cue) != null;
        // The cue a card plays when it arrives: its signature when it has one, otherwise the usual cue for its type.
        public static string ArrivalCue(SfxBank bank, CardEntry card) =>
            HasOwnCue(bank, card, "signature") ? "signature" : card.type == "LAND" ? "place" : card.type == "SPELL" ? "cast" : "deploy";

        void ResetIdleSignatureAudio() {
            capitalSignaturesPlayed = false; lastIdleInstance = null;
            nextIdleAt = Time.time + IdleMinGap;
        }
        void PlayArrival(CardEntry card, Vector3? at, float volume = 1f) {
            string cue = ArrivalCue(sfx, card);
            sfx.Play(card.id, cue, at, volume);
            if (cue == "signature") CountAmbient(card.id, cue);
        }
        void CountAmbient(string cardId, string cue) {
            if (sfx.Muted || sfx.LastCue != cardId + "_" + cue) return;
            if (cue == "idle") IdleCuesPlayed++; else SignatureCuesPlayed++;
            ambientCueLog.Add(Time.time.ToString("0.0") + " " + sfx.LastCue);
        }

        void UpdateIdleAudio() {
            if (sfx == null || sfx.Muted || paused || MatchOver || speed >= 50 || (mode != Mode.Playback && mode != Mode.Live)) return;
            if (Time.time < nextIdleAt) return;
            nextIdleAt = Time.time + UnityEngine.Random.Range(IdleMinGap, IdleMaxGap);
            var pool = pieces.Values.Where(p => p != null && p.InstanceId != lastIdleInstance && sfx.ResolveLibrary(p.Card.id + "_idle") != null).ToList();
            if (pool.Count == 0) return;
            var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            lastIdleInstance = pick.InstanceId;
            sfx.Play(pick.Card.id, "idle", Chest(pick), IdleGain);
            CountAmbient(pick.Card.id, "idle");
        }

        void PlayCapitalSignatures() {
            // Editor audio checks call the capital reveal outside play mode, where no coroutine host exists.
            if (!Application.isPlaying || anim == null) return;
            if (capitalSignaturesPlayed || sfx == null || CapitalOf(0) == null || CapitalOf(1) == null) return;
            capitalSignaturesPlayed = true;
            anim.StartCoroutine(CapitalSignatureSequence());
        }
        // The viewer's own capital first, then the opponent's.
        IEnumerator CapitalSignatureSequence() {
            if (speed < 50) yield return new WaitForSeconds(.8f);
            int first = mode == Mode.Live ? humanSeat : 0;
            foreach (int seat in new[] { first, 1 - first }) {
                var cap = CapitalOf(seat);
                if (cap == null || !HasOwnCue(sfx, cap.Card, "signature")) continue;
                sfx.Play(cap.Card.id, "signature", Chest(cap), CapitalSignatureGain);
                CountAmbient(cap.Card.id, "signature");
                if (speed < 50) yield return new WaitForSeconds(1.2f);
            }
        }

        // -idleSigProbe <result.json> [-idleSigSeconds 90]: plays the bundled match at normal speed with the listener
        // silenced and records which idle and signature sounds actually played in the packaged player.
        void StartIdleSigProbe(string path) {
            sfx.Muted = false; AudioListener.volume = 0;
            float seconds = float.TryParse(Arg("-idleSigSeconds"), out var s) ? s : 90f;
            RestartPlayback();
            StartCoroutine(IdleSigProbe(path, seconds));
        }
        IEnumerator IdleSigProbe(string path, float seconds) {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end && !MatchOver) yield return null;
            var idleCards = ambientCueLog.Where(l => l.EndsWith("_idle")).Select(l => l.Split(' ')[1]).Distinct().Count();
            string json = "{\"seconds\":" + seconds.ToString("0") + ",\"eventsApplied\":" + cursor + ",\"events\":" + events.Count +
                ",\"idlePlayed\":" + IdleCuesPlayed + ",\"distinctIdleCards\":" + idleCards + ",\"signaturePlayed\":" + SignatureCuesPlayed +
                ",\"cardCuesPlayed\":" + sfx.CardCuesPlayed + ",\"genericCuesPlayed\":" + sfx.GenericCuesPlayed +
                ",\"log\":[" + string.Join(",", ambientCueLog.Select(l => "\"" + l + "\"")) + "]}";
            File.WriteAllText(path, json);
            Debug.Log("IDLE_SIG_PROBE " + json);
            Application.Quit(IdleCuesPlayed > 0 && SignatureCuesPlayed > 0 ? 0 : 1);
        }
    }
}
