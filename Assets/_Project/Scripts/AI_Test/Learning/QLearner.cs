using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AITest.Learning
{
    /// <summary>
    /// Tabular Q-Learning (epsilon-greedy, valid action masking, optimistic init)
    /// ? NO PERSISTENCE - Fresh start every game!
    /// </summary>
    public class QLearner : MonoBehaviour
    {
        [Header("Q-Learning")]
        [Range(0.01f, 0.99f)] public float alpha = 0.4f;
        [Range(0.50f, 0.99f)] public float gamma = 0.80f;

        [Header("Exploration")]
        [Range(0f, 1f)] public float epsilonStart = 0.35f; // ? 0.20 ? 0.35 (daha fazla exploration)
        [Range(0f, 0.5f)] public float epsilonEnd = 0.10f;  // ? 0.02 ? 0.10 (daha fazla randomness kalsýn)
        [Tooltip("Kaç KARAR adýmýnda epsilon, start'tan end'e iner")]
        public int epsilonDecayDecisions = 400; // ? 300 ? 400 (daha yavaþ decay)

        [Header("Debug")]
        public bool debugMode = false;

        // Q-table: key -> action[] (5)
        private readonly Dictionary<string, float[]> qTable = new();
        private const int ACTION_COUNT = 5;

        // exploration
        private float currentEpsilon;
        private int decisionCounter;
        private System.Random rng;

        // metrics
        public float CurrentEpsilon => currentEpsilon;
        public float LastDeltaQ { get; private set; }
        public float LastTDError { get; private set; }
        public RLAction LastAction { get; private set; } // ? YENÝ: Son seçilen action

        private void Awake()
        {
            rng = new System.Random();
            ResetLearning();
        }

        /// <summary>
        /// ? Reset learning state (called on Awake and manually)
        /// </summary>
        public void ResetLearning()
        {
            qTable.Clear();
            currentEpsilon = epsilonStart;
            decisionCounter = 0;
            LastDeltaQ = 0f;
            LastTDError = 0f;
            LastAction = RLAction.Patrol; // ? YENÝ
            
            Debug.Log($"<color=lime>[QLearner] ? Learning reset! ?={currentEpsilon:F2}, fresh Q-table</color>");
        }

        /// <summary>
        /// Karar sayýsýna baðlý epsilon güncelle
        /// </summary>
        private void StepEpsilon()
        {
            decisionCounter++;
            float t = Mathf.Clamp01(decisionCounter / Mathf.Max(1f, (float)epsilonDecayDecisions));
            currentEpsilon = Mathf.Lerp(epsilonStart, epsilonEnd, t);
        }

        /// <summary>
        /// Geçerli rastgele eylem (None ipuçlarýna göre maskeleme)
        /// </summary>
        private RLAction GetValidRandomAction(RLState s)
        {
            List<int> pool = new(ACTION_COUNT);
            for (int i = 0; i < ACTION_COUNT; i++)
            {
                if (IsActionValid(s, (RLAction)i)) pool.Add(i);
            }
            int pick = pool[rng.Next(pool.Count)];
            return (RLAction)pick;
        }

        /// <summary>
        /// Eylem geçerlilik kontrolü (duruma göre maskeleme)
        /// </summary>
        private bool IsActionValid(RLState s, RLAction a)
        {
            switch (a)
            {
                case RLAction.GoToLastSeen:
                    return s.lastSeenSectorId != "None";
                case RLAction.GoToLastHeard:
                    return s.lastHeardSectorId != "None";
                default:
                    return true; // Sweep/Ambush/Patrol her zaman mümkün
            }
        }

        /// <summary>
        /// Epsilon-greedy seçim (masking + tie-break random)
        /// </summary>
        public RLAction ChooseAction(RLState state)
        {
            StepEpsilon();

            string key = state.ToKey();
            if (!qTable.ContainsKey(key))
                InitializeState(key);

            // Exploration
            if ((float)rng.NextDouble() < currentEpsilon)
            {
                RLAction ra = GetValidRandomAction(state);
                LastAction = ra; // ? YENÝ: Track
                if (debugMode) Debug.Log($"[QLearner] explore: {ra} (?={currentEpsilon:F2})");
                return ra;
            }

            // Exploitation — sadece GEÇERLÝ aksiyonlar içinden argmax (+ tie-break)
            float[] q = qTable[key];
            float best = float.NegativeInfinity;
            List<int> bestIdx = new();

            for (int i = 0; i < ACTION_COUNT; i++)
            {
                if (!IsActionValid(state, (RLAction)i)) continue;
                float v = q[i];
                if (v > best)
                {
                    best = v;
                    bestIdx.Clear();
                    bestIdx.Add(i);
                }
                else if (Mathf.Approximately(v, best))
                {
                    bestIdx.Add(i);
                }
            }

            int pickIdx = bestIdx.Count == 1 ? bestIdx[0] : bestIdx[rng.Next(bestIdx.Count)];
            RLAction action = (RLAction)pickIdx;
            LastAction = action; // ? YENÝ: Track

            if (debugMode) Debug.Log($"[QLearner] exploit: {action} Q={best:F3} (?={currentEpsilon:F2})");
            return action;
        }

        /// <summary>
        /// Q(s,a) ? Q(s,a) + ? [r + ? max_a' Q(s',a') ? Q(s,a)]
        /// </summary>
        public void UpdateQ(RLState s, RLAction a, float reward, RLState s2)
        {
            string key = s.ToKey();
            string key2 = s2.ToKey();

            if (!qTable.ContainsKey(key)) InitializeState(key);
            if (!qTable.ContainsKey(key2)) InitializeState(key2);

            float oldQ = qTable[key][(int)a];
            float maxQ2 = GetMaxQ(key2);

            // Ödül ölçeðini daralt (stabilite)
            reward = Mathf.Clamp(reward, -1.0f, +3.0f);

            float target = reward + gamma * maxQ2;
            LastTDError = target - oldQ;

            float newQ = oldQ + alpha * LastTDError;
            qTable[key][(int)a] = newQ;

            LastDeltaQ = newQ - oldQ;

            if (debugMode)
                Debug.Log($"[QLearner] Q({key},{a}) {oldQ:F3}->{newQ:F3} dQ={LastDeltaQ:F3} ?={LastTDError:F3}");
        }

        private void InitializeState(string key)
        {
            // Optimistic initialization (keþfi hýzlandýrýr)
            qTable[key] = new float[ACTION_COUNT] { 0.08f, 0.08f, 0.08f, 0.08f, 0.08f };
        }

        private float GetMaxQ(string key)
        {
            if (!qTable.TryGetValue(key, out var q)) return 0f;
            float m = q[0];
            for (int i = 1; i < q.Length; i++) if (q[i] > m) m = q[i];
            return m;
        }

        /// <summary>
        /// Get current Q-value for state-action pair (for logging)
        /// </summary>
        public float GetQValue(RLState state, RLAction action)
        {
            string key = state.ToKey();
            if (!qTable.ContainsKey(key))
            {
                InitializeState(key);
            }
            return qTable[key][(int)action];
        }

        // Debug/istatistik
        public int GetQTableSize() => qTable.Count;
    }
}
