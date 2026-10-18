using System.Collections;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    // Particle bursts, energy bolts and floating numbers. Spell cards are pure VFX (AI-063: no board presence).
    public sealed class Vfx {
        readonly Material particleMat;
        readonly TokenFactory factory;
        readonly Font font;
        public Vfx(Material particleMat, TokenFactory factory, Font font) { this.particleMat = particleMat; this.factory = factory; this.font = font; }

        public ParticleSystem Burst(Vector3 at, Color color, int count = 60, float speed = 2.2f, float size = .09f, float life = .8f, bool upward = false) {
            var go = new GameObject("Burst");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .2f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * .5f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, .6f));
            main.gravityModifier = upward ? -.3f : .4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var shape = ps.shape; shape.shapeType = upward ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Sphere; shape.radius = .12f; shape.angle = 25;
            if (upward) go.transform.rotation = Quaternion.Euler(-90, 0, 0);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(color, .3f), new GradientColorKey(color, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .5f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, .1f));
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = particleMat; r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        // SPELL stand-in: a faction-coloured column + ring burst at the target hex, plus a light flash.
        public IEnumerator SpellBurst(MonoBehaviour host, Vector3 at, string faction, float speed) {
            var c = PlaytestCatalog.FactionColor(faction); var a = PlaytestCatalog.FactionAccent(faction);
            Burst(at + Vector3.up * .1f, a, 90, 3.2f, .12f, 1.1f, true);
            Burst(at + Vector3.up * .3f, c, 60, 1.6f, .1f, .9f);
            var lightGo = new GameObject("Spell flash"); lightGo.transform.position = at + Vector3.up * .8f;
            var l = lightGo.AddComponent<Light>(); l.type = LightType.Point; l.color = a; l.range = 3.5f; l.intensity = 0;
            var ring = PlaytestMeshes.Make("Spell ring", PlaytestMeshes.HexRing(.4f, .32f), factory.Glow(a), null, at + Vector3.up * .12f);
            float t = 0, dur = .7f / speed;
            while (t < dur) {
                t += Time.deltaTime; float k = t / dur;
                l.intensity = Mathf.Sin(k * Mathf.PI) * 6f;
                ring.transform.localScale = Vector3.one * (1 + k * 3.2f);
                yield return null;
            }
            Object.Destroy(lightGo); Object.Destroy(ring);
        }

        public IEnumerator Bolt(Vector3 from, Vector3 to, Color color, float speed) {
            var go = PlaytestMeshes.Make("Energy bolt", PlaytestMeshes.Prism(4, .035f, .035f, 1f, 45), factory.Glow(Color.Lerp(color, Color.white, .4f)), null, from);
            float t = 0, dur = .28f / speed;
            var dir = to - from;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            while (t < dur) {
                t += Time.deltaTime; float k = Mathf.Clamp01(t / dur);
                float head = k, tail = Mathf.Max(0, k - .35f);
                go.transform.position = from + dir * tail;
                go.transform.localScale = new Vector3(1, Mathf.Max(.01f, dir.magnitude * (head - tail)), 1);
                yield return null;
            }
            Object.Destroy(go);
            Burst(to, color, 30, 1.6f, .07f, .5f);
        }

        public void FloatText(Vector3 at, string text, Color color, float speed) {
            if (font == null) return;
            var go = new GameObject("Float " + text, typeof(MeshRenderer), typeof(TextMesh));
            go.transform.position = at;
            var tm = go.GetComponent<TextMesh>(); tm.font = font; tm.text = text; tm.fontSize = 64; tm.characterSize = .03f;
            tm.anchor = TextAnchor.MiddleCenter; tm.color = color; tm.fontStyle = FontStyle.Bold;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            go.AddComponent<Billboard>();
            go.AddComponent<Floater>().Speed = speed;
        }
    }

    sealed class Floater : MonoBehaviour {
        public float Speed = 1; float t;
        void Update() {
            t += Time.deltaTime * Speed;
            transform.position += Vector3.up * Time.deltaTime * Speed * .6f;
            var tm = GetComponent<TextMesh>(); var c = tm.color; c.a = Mathf.Clamp01(1.4f - t); tm.color = c;
            if (t > 1.4f) Destroy(gameObject);
        }
    }

    public static class Tween {
        public static IEnumerator Move(Transform tr, Vector3 to, float dur, float arc = 0) {
            Vector3 from = tr.position; float t = 0;
            if (dur <= 0) { tr.position = to; yield break; }
            while (t < dur) {
                t += Time.deltaTime; float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / dur));
                tr.position = Vector3.Lerp(from, to, k) + Vector3.up * arc * Mathf.Sin(k * Mathf.PI);
                yield return null;
            }
            tr.position = to;
        }
        public static IEnumerator Scale(Transform tr, Vector3 from, Vector3 to, float dur, bool overshoot = false) {
            float t = 0;
            if (dur <= 0) { tr.localScale = to; yield break; }
            while (t < dur) {
                t += Time.deltaTime; float k = Mathf.Clamp01(t / dur);
                float e = overshoot ? 1 + 2.2f * Mathf.Pow(k - 1, 3) + 1.2f * Mathf.Pow(k - 1, 2) : Mathf.SmoothStep(0, 1, k);
                tr.localScale = Vector3.LerpUnclamped(from, to, e);
                yield return null;
            }
            tr.localScale = to;
        }
        public static IEnumerator Shake(Transform tr, float dur, float amount) {
            Vector3 home = tr.position; float t = 0;
            while (t < dur) {
                t += Time.deltaTime; float k = 1 - t / dur;
                tr.position = home + new Vector3(Mathf.Sin(t * 70) * amount * k, 0, Mathf.Cos(t * 53) * amount * k);
                yield return null;
            }
            tr.position = home;
        }
        public static IEnumerator Lunge(Transform tr, Vector3 toward, float dur) {
            Vector3 home = tr.position; var d = toward - home; d.y = 0;
            Vector3 peak = home + d.normalized * Mathf.Min(.35f, d.magnitude * .4f);
            yield return Move(tr, peak, dur * .4f);
            yield return Move(tr, home, dur * .6f);
        }
    }
}
