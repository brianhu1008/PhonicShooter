// ============================================================================
//  Phonics Island - quiz flow, hints, attacks and the boss turning friendly
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PhonicsIsland
{
    public partial class PhonicsIslandGame
    {
        List<Question> questions;
        int qIndex, hp, maxHp, wrongCount, praiseIdx, combo;
        string hint = "";
        bool answeredCorrect, spellRed, spellGreen, bossIdle = true;
        int[] optOrder;
        bool[] optWrong;
        string[] tileLabel;
        bool[] tileUsed;
        int[] slot;
        bool[] slotLocked;

        readonly string[] praise = { "Great!", "Awesome!", "Super!", "Yay!", "Wow!", "Perfect!" };

        Question CurQ
        {
            get { return state == State.Quiz && questions != null && qIndex < questions.Count ? questions[qIndex] : null; }
        }

        // ------------------------------------------------------------------
        //  Dialogue
        // ------------------------------------------------------------------
        void BeginDialogue()
        {
            state = State.Dialogue;
            dialogueIndex = 0;
            nearNpc = false;
            StartCoroutine(MoveTo(hero, battleSpot, 0.9f, 0f));
            Speak(cur.intro[0].say);
        }

        void AdvanceDialogue()
        {
            dialogueIndex++;
            if (dialogueIndex >= cur.intro.Length) StartQuiz();
            else Speak(cur.intro[dialogueIndex].say);
        }

        // ------------------------------------------------------------------
        //  Questions
        // ------------------------------------------------------------------
        static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        List<Question> MakeQuestionSet(IslandDef d)
        {
            // New random words every time (see PhonicsWordBank.cs).
            var list = d.make(!easyMode);
            int cap = easyMode ? d.easyCount : d.hardCount;
            // Rarely a generator runs out of unused words; top up from another random set.
            for (int tries = 0; list.Count < cap && tries < 5; tries++)
            {
                foreach (Question q in d.make(!easyMode))
                {
                    if (list.Count >= cap) break;
                    if (!list.Exists(x => x.answer == q.answer)) list.Add(q);
                }
            }
            if (list.Count > cap) list = list.GetRange(0, cap);
            return list;
        }

        void StartQuiz()
        {
            questions = MakeQuestionSet(cur);
            maxHp = questions.Count;
            hp = maxHp;
            qIndex = 0;
            combo = 0;
            state = State.Quiz;
            StartQuestion();
        }

        void StartQuestion()
        {
            Question q = questions[qIndex];
            wrongCount = 0;
            hint = "";
            spellRed = false;
            spellGreen = false;
            answeredCorrect = false;

            if (q.type == QType.Spell)
            {
                var labels = new List<string>(q.chunks);
                labels.AddRange(q.extras);
                string target = string.Join("|", q.chunks);
                int guard = 0;
                do
                {
                    Shuffle(labels);
                    guard++;
                }
                while (string.Join("|", labels.GetRange(0, q.chunks.Length)) == target && guard < 10);

                tileLabel = labels.ToArray();
                tileUsed = new bool[tileLabel.Length];
                slot = new int[q.chunks.Length];
                slotLocked = new bool[q.chunks.Length];
                for (int i = 0; i < slot.Length; i++) slot[i] = -1;
            }
            else if (q.options != null)
            {
                optOrder = new int[q.options.Length];
                for (int i = 0; i < optOrder.Length; i++) optOrder[i] = i;
                if (q.type != QType.Sort) Shuffle(optOrder);
                optWrong = new bool[q.options.Length];
            }

            if (q.type != QType.Find && (hero.position - battleSpot).sqrMagnitude > 0.05f)
                StartCoroutine(MoveTo(hero, battleSpot, 0.5f, 0.2f));
            SpeakQuestion(q);
        }

        void SpeakQuestion(Question q)
        {
            Speak(q.say);
        }

        void OnPick(int optIndex)
        {
            if (busy || state != State.Quiz) return;
            Question q = questions[qIndex];
            if (q.options[optIndex] == q.answer)
            {
                answeredCorrect = true;
                StartCoroutine(CorrectRoutine(q));
            }
            else
            {
                optWrong[optIndex] = true;
                StartCoroutine(WrongRoutine(q));
            }
        }

        void OnFindPick(int spotIndex)
        {
            if (busy || state != State.Quiz) return;
            Question q = questions[qIndex];
            FindSpot s = findSpots[spotIndex];
            if (s.key == q.answer)
            {
                answeredCorrect = true;
                s.collected = true;
                StartCoroutine(Burst(s.pos + Vector3.up * 0.6f, 12, 4f));
                s.root.gameObject.SetActive(false);
                StartCoroutine(CorrectRoutine(q));
            }
            else
            {
                s.wrongFlash = time;
                StartCoroutine(WrongRoutine(q));
            }
        }

        void OnTile(int tileIndex)
        {
            if (busy || state != State.Quiz || tileUsed[tileIndex]) return;
            Question q = questions[qIndex];
            int s = -1;
            for (int k = 0; k < slot.Length; k++)
            {
                if (slot[k] < 0) { s = k; break; }
            }
            if (s < 0) return;
            slot[s] = tileIndex;
            tileUsed[tileIndex] = true;
            PlaySfx(sfxTick);

            bool full = true;
            string built = "";
            for (int k = 0; k < slot.Length; k++)
            {
                if (slot[k] < 0) full = false;
                else built += tileLabel[slot[k]];
            }
            if (!full) return;

            if (built == q.answer)
            {
                spellGreen = true;
                answeredCorrect = true;
                StartCoroutine(CorrectRoutine(q));
            }
            else
            {
                spellRed = true;
                StartCoroutine(WrongRoutine(q));
            }
        }

        void OnSlot(int k)
        {
            if (busy || state != State.Quiz || slotLocked[k] || slot[k] < 0) return;
            tileUsed[slot[k]] = false;
            slot[k] = -1;
        }

        int FindUnusedTile(string label)
        {
            for (int i = 0; i < tileLabel.Length; i++)
            {
                if (!tileUsed[i] && tileLabel[i] == label) return i;
            }
            return -1;
        }

        int HintTile(Question q)
        {
            for (int k = 0; k < slot.Length; k++)
            {
                if (slot[k] < 0) return FindUnusedTile(q.chunks[k]);
            }
            return -1;
        }

        IEnumerator CorrectRoutine(Question q)
        {
            busy = true;
            hint = "";
            combo++;
            string p = praise[praiseIdx % praise.Length];
            praiseIdx++;
            PlaySfx(sfxDing);
            Speak(q.sayCorrect);
            AddFloat(p, hero.position + Vector3.up * 2.4f, new Color(1f, 0.85f, 0.2f), 64, 1.4f);
            if (combo >= 2) AddFloat("Combo x" + combo + "!", hero.position + Vector3.up * 3.3f, new Color(0.6f, 0.95f, 1f), 40, 1.4f);
            yield return new WaitForSeconds(0.6f);
            yield return AttackRoutine(q.type == QType.Find);
            if (hp <= 0)
            {
                yield return WinRoutine();
                yield break;
            }
            yield return new WaitForSeconds(0.25f);
            qIndex++;
            StartQuestion();
            busy = false;
        }

        IEnumerator WrongRoutine(Question q)
        {
            busy = true;
            wrongCount++;
            combo = 0;
            PlaySfx(sfxOops);
            string snd = string.IsNullOrEmpty(q.hintSound) ? "" : " Listen for \"" + q.hintSound + "\".";

            if (q.type == QType.Spell)
            {
                if (wrongCount == 1) hint = "Almost! Listen again and try one more time.";
                else if (wrongCount == 2) hint = "No worries! I put the first part in for you.";
                else hint = "You can do it! Follow the glowing tile.";
            }
            else if (q.type == QType.Find)
            {
                if (wrongCount == 1) hint = "Not this one! Listen again: \"" + q.answer + "\".";
                else hint = "Look for the glowing picture!";
            }
            else
            {
                if (wrongCount == 1) hint = "Oops! Try again." + snd;
                else hint = "Good try! Look at the glowing one.";
            }

            yield return BonkRoutine();

            if (q.type == QType.Spell)
            {
                for (int k = 0; k < slot.Length; k++)
                {
                    if (slotLocked[k]) continue;
                    if (slot[k] >= 0) tileUsed[slot[k]] = false;
                    slot[k] = -1;
                }
                if (wrongCount >= 2 && !slotLocked[0])
                {
                    int ti = FindUnusedTile(q.chunks[0]);
                    if (ti >= 0)
                    {
                        slot[0] = ti;
                        tileUsed[ti] = true;
                        slotLocked[0] = true;
                    }
                }
            }
            spellRed = false;
            SpeakQuestion(q);
            busy = false;
        }

        // ------------------------------------------------------------------
        //  Animations
        // ------------------------------------------------------------------
        IEnumerator MoveTo(Transform tr, Vector3 target, float dur, float arc)
        {
            Vector3 from = tr.position;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float lin = Mathf.Clamp01(t / dur);
                float u = Mathf.SmoothStep(0f, 1f, lin);
                Vector3 p = Vector3.Lerp(from, target, u);
                p.y += Mathf.Sin(lin * Mathf.PI) * arc;
                tr.position = p;
                yield return null;
            }
            tr.position = target;
        }

        IEnumerator AttackRoutine(bool fromHere)
        {
            Vector3 dir = boss.position - hero.position;
            dir.y = 0f;
            dir.Normalize();
            Vector3 home = hero.position;
            if (!fromHere)
            {
                Vector3 hitSpot = boss.position - dir * (2.4f + cur.bossScale * 0.6f);
                yield return MoveTo(hero, hitSpot, 0.25f, 0.3f);
            }
            else
            {
                hero.rotation = Quaternion.LookRotation(dir);
            }

            // Throw a yuzu!
            Transform yuzu = new GameObject("ThrownYuzu").transform;
            Prim(PrimitiveType.Sphere, "Fruit", yuzu, Vector3.zero, Vector3.one * 0.42f, YuzuColor);
            Prim(PrimitiveType.Sphere, "Leaf", yuzu, new Vector3(0.08f, 0.22f, 0f), new Vector3(0.2f, 0.05f, 0.1f), new Color(0.3f, 0.7f, 0.3f));
            PlaySfx(sfxThrow);
            Vector3 a = hero.position + Vector3.up * 1.4f + dir * 0.6f;
            Vector3 b = boss.position + Vector3.up * (1.0f * cur.bossScale);
            float dur = Mathf.Clamp((b - a).magnitude * 0.08f, 0.35f, 0.8f);
            float arc = Mathf.Clamp((b - a).magnitude * 0.15f, 0.6f, 2f);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                yuzu.position = Vector3.Lerp(a, b, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * arc;
                yuzu.Rotate(400f * Time.deltaTime, 0f, 600f * Time.deltaTime);
                yield return null;
            }
            Destroy(yuzu.gameObject);

            hp = Mathf.Max(0, hp - 1);
            PlaySfx(sfxPop);
            AddFloat("Pow!", boss.position + Vector3.up * (2.4f * cur.bossScale), new Color(1f, 0.5f, 0.3f), 60, 1.0f);
            StartCoroutine(Burst(b, 14, 5f));
            yield return HitReaction();
            if (!fromHere) yield return MoveTo(hero, home, 0.35f, 0f);
        }

        IEnumerator HitReaction()
        {
            bossIdle = false;
            float t = 0f;
            const float dur = 0.5f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                SetMatColor(bossCapy.fur, Color.Lerp(new Color(1f, 0.4f, 0.4f), bossCapy.badFur, u));
                float sq = 1f + Mathf.Sin(u * Mathf.PI * 3f) * 0.15f * (1f - u);
                bossCapy.visual.localScale = new Vector3(1f / sq, sq, 1f / sq);
                bossCapy.visual.localPosition = new Vector3(Mathf.Sin(u * 40f) * 0.12f * (1f - u), 0f, 0f);
                yield return null;
            }
            bossCapy.visual.localScale = Vector3.one;
            bossCapy.visual.localPosition = Vector3.zero;
            SetMatColor(bossCapy.fur, bossCapy.badFur);
            bossIdle = true;
        }

        // Wrong answer: the bad capybara does a silly hop and boings the hero. No damage, no fail.
        IEnumerator BonkRoutine()
        {
            bossIdle = false;
            Vector3 from = boss.position;
            Vector3 dir = hero.position - from;
            dir.y = 0f;
            dir.Normalize();
            Vector3 to = hero.position - dir * (1.4f + cur.bossScale * 0.6f);
            yield return MoveTo(boss, to, 0.35f, 0.9f);
            PlaySfx(sfxBoing);
            AddFloat("Boing!", hero.position + Vector3.up * 2.4f, new Color(1f, 0.55f, 0.75f), 56, 0.9f);

            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / 0.35f);
                heroCapy.visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(u * Mathf.PI * 4f) * 14f * (1f - u));
                yield return null;
            }
            heroCapy.visual.localRotation = Quaternion.identity;
            yield return MoveTo(boss, from, 0.4f, 0.3f);
            bossIdle = true;
        }

        // The bad capybara loses its mask, turns friendly and hops for joy.
        IEnumerator WinRoutine()
        {
            bossIdle = false;
            PlaySfx(sfxWin);
            StartCoroutine(Burst(boss.position + Vector3.up * 1.2f, 30, 6f));
            float t = 0f;
            const float spin = 0.8f;
            while (t < spin)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / spin);
                bossCapy.visual.localRotation = Quaternion.Euler(0f, u * 720f, 0f);
                bossCapy.visual.localPosition = Vector3.up * Mathf.Sin(u * Mathf.PI) * 1.2f;
                SetMatColor(bossCapy.fur, Color.Lerp(bossCapy.badFur, bossCapy.goodFur, u));
                if (u > 0.5f && bossCapy.badBits.activeSelf) SetBad(bossCapy, false);
                yield return null;
            }
            SetBad(bossCapy, false);
            bossCapy.visual.localRotation = Quaternion.identity;
            bossCapy.visual.localPosition = Vector3.zero;
            AddFloat("Thank you!", boss.position + Vector3.up * (2.6f * cur.bossScale), new Color(1f, 0.85f, 0.95f), 52, 2f);
            Speak("Thank you! Let's be friends!");
            MarkCleared(curIndex);
            for (int hop = 0; hop < 3; hop++)
            {
                t = 0f;
                while (t < 0.4f)
                {
                    t += Time.deltaTime;
                    bossCapy.visual.localPosition = Vector3.up * Mathf.Sin(Mathf.Clamp01(t / 0.4f) * Mathf.PI) * 0.6f;
                    yield return null;
                }
            }
            bossCapy.visual.localPosition = Vector3.zero;
            if (cur.isReview) StartCoroutine(Fireworks());
            state = State.Win;
            busy = false;
        }

        IEnumerator Fireworks()
        {
            for (int i = 0; i < 8; i++)
            {
                StartCoroutine(Burst(new Vector3(Random.Range(-5f, 5f), Random.Range(3f, 6f), Random.Range(0f, 5f)), 24, 6f));
                PlaySfx(sfxPop);
                yield return new WaitForSeconds(0.35f);
            }
        }

        IEnumerator Burst(Vector3 pos, int count, float power)
        {
            var parts = new List<Transform>();
            var vel = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                // Parented to the island (at the origin) so leaving mid-burst cleans the sparks up.
                GameObject g = Prim(PrimitiveType.Cube, "Spark", islandRoot, pos, Vector3.one * 0.18f, sparkMats[i % sparkMats.Count]);
                parts.Add(g.transform);
                Vector3 v = new Vector3(Random.Range(-1f, 1f), Random.Range(0.6f, 1.6f), Random.Range(-1f, 1f)).normalized;
                vel.Add(v * power * Random.Range(0.6f, 1.2f));
            }
            float t = 0f;
            const float dur = 0.9f;
            while (t < dur)
            {
                float dt = Time.deltaTime;
                t += dt;
                for (int i = 0; i < parts.Count; i++)
                {
                    if (parts[i] == null) continue;
                    vel[i] += Vector3.down * 9f * dt;
                    parts[i].position += vel[i] * dt;
                    parts[i].localScale = Vector3.one * 0.18f * Mathf.Clamp01(1f - t / dur);
                    parts[i].Rotate(300f * dt, 200f * dt, 0f);
                }
                yield return null;
            }
            foreach (Transform tr in parts)
            {
                if (tr != null) Destroy(tr.gameObject);
            }
        }
    }
}
