using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace AITest.Quest
{
    /// <summary>
    /// Quest Manager - Minimal quest system for long gameplay
    ///
    /// Added:
    /// - Bot-friendly accessors (current interactable / objective)
    /// - ResetForTrainingEpisode() helper
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        [Header("Quest Configuration")]
        [Tooltip("List of quest objectives (in order)")]
        public List<QuestObjective> objectives = new List<QuestObjective>();

        [Header("Quest State")]
        [Range(0f, 1f)] public float Progress01 = 0f;
        public int CompletedCount = 0;
        public int TotalCount = 0;

        [Header("Events")]
        public UnityEvent OnQuestStarted;
        public UnityEvent<int, int> OnQuestProgress; // (completed, total)
        public UnityEvent OnQuestCompleted;

        [Header("Debug")]
        public bool showDebugLogs = false;

        // State
        private bool questStarted = false;
        private bool questCompleted = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            TotalCount = objectives.Count;
            RegisterInteractables();
        }

        private void Start()
        {
            StartQuest();
        }

        private void RegisterInteractables()
        {
            foreach (var objective in objectives)
            {
                if (objective.interactable != null)
                {
                    // Capture objective reference (safe in modern C# foreach)
                    var objRef = objective;
                    objRef.interactable.OnInteractionComplete += () => OnObjectiveCompleted(objRef);
                }
                else
                {
                    Debug.LogWarning($"[QuestManager] Objective '{objective.objectiveName}' has no interactable assigned!");
                }
            }
        }

        private void StartQuest()
        {
            if (questStarted)
                return;

            questStarted = true;
            questCompleted = false;
            CompletedCount = 0;
            Progress01 = 0f;

            OnQuestStarted?.Invoke();

            if (showDebugLogs)
                Debug.Log($"<color=cyan>[QuestManager] Quest started! ({TotalCount} objectives)</color>");
        }

        private void OnObjectiveCompleted(QuestObjective objective)
        {
            if (objective.completed)
                return;

            objective.completed = true;
            CompletedCount++;

            Progress01 = (TotalCount > 0) ? (float)CompletedCount / TotalCount : 1f;

            OnQuestProgress?.Invoke(CompletedCount, TotalCount);

            if (showDebugLogs)
            {
                Debug.Log($"<color=lime>[QuestManager] Objective completed: {objective.objectiveName} ({CompletedCount}/{TotalCount})</color>");
                Debug.Log($"<color=lime>[QuestManager] Progress: {Progress01:P0}</color>");
            }

            if (CompletedCount >= TotalCount)
            {
                OnQuestComplete();
            }
        }

        private void OnQuestComplete()
        {
            if (questCompleted)
                return;

            questCompleted = true;
            Progress01 = 1f;

            OnQuestCompleted?.Invoke();

            if (showDebugLogs)
                Debug.Log($"<color=lime>[QuestManager] QUEST COMPLETED!</color>");
        }

        /// <summary>
        /// Get current objective (next uncompleted)
        /// </summary>
        public QuestObjective GetCurrentObjective()
        {
            foreach (var objective in objectives)
            {
                if (!objective.completed)
                    return objective;
            }
            return null;
        }

        /// <summary>
        /// Bot-friendly: returns current interactable (next objective's interactable).
        /// </summary>
        public QuestInteractable GetCurrentInteractable()
        {
            var obj = GetCurrentObjective();
            return obj != null ? obj.interactable : null;
        }

        /// <summary>
        /// Bot-friendly: returns true if quest is fully completed.
        /// </summary>
        public bool IsQuestCompleted()
        {
            return questCompleted || (TotalCount > 0 && CompletedCount >= TotalCount);
        }

        /// <summary>
        /// Training helper: resets objectives + interactables for new episode.
        /// Call this from EpisodeManager / TrainingModeController before spawning agents.
        /// </summary>
        public void ResetForTrainingEpisode(bool restartQuest = true)
        {
            // Reset objective flags
            foreach (var obj in objectives)
            {
                obj.completed = false;
                if (obj.interactable != null)
                {
                    // Reset interactable runtime state
                    obj.interactable.ResetRuntimeState(resetCompleted: true);
                }
            }

            TotalCount = objectives.Count;
            CompletedCount = 0;
            Progress01 = 0f;
            questCompleted = false;

            if (restartQuest)
            {
                questStarted = false;
                StartQuest();
            }

            if (showDebugLogs)
                Debug.Log($"<color=cyan>[QuestManager] ResetForTrainingEpisode done. Objectives={TotalCount}</color>");
        }

        public string GetProgressSummary()
        {
            return $"{CompletedCount}/{TotalCount} ({Progress01:P0})";
        }

        /// <summary>
        /// Get progress heuristic (0.0 to 1.0) for strategic phase analysis
        /// </summary>
        public float GetProgress()
        {
            return Progress01;
        }
    }

    [System.Serializable]
    public class QuestObjective
    {
        public string objectiveName = "Activate Device";
        public string targetRoomId = "A";
        public QuestInteractable interactable;
        public bool completed = false;
    }
}
