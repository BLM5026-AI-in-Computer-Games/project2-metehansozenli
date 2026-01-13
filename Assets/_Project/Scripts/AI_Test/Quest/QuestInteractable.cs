using UnityEngine;
using AITest.Core;

namespace AITest.Quest
{
    /// <summary>
    /// Quest Interactable - Interactive quest objective
    ///
    /// Player:
    /// - Hold E for duration (existing behavior)
    ///
    /// Bot:
    /// - Can start/continue interaction via API (no Input needed)
    /// - Emits periodic noise during interaction
    /// - Emits clue on completion
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class QuestInteractable : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [Tooltip("Interact key (player only)")]
        public KeyCode interactKey = KeyCode.E;

        [Tooltip("Interaction range (meters)")]
        [Range(1f, 3f)] public float interactionRange = 2f;

        [Tooltip("Required interaction time (seconds)")]
        [Range(0.5f, 10f)] public float interactionDuration = 3f;

        [Header("Noise Profile")]
        [Tooltip("Emit noise during interaction")]
        public bool emitNoiseDuringInteraction = true;

        [Tooltip("Noise emission interval (seconds)")]
        [Range(0.1f, 2f)] public float noiseInterval = 0.5f;

        [Tooltip("Noise radius (meters)")]
        [Range(5f, 20f)] public float noiseRadius = 15f;

        [Tooltip("Is noise global (heard everywhere)")]
        public bool isGlobalNoise = false;

        [Header("Clue Emission")]
        [Tooltip("Emit clue on completion")]
        public bool emitClueOnCompletion = true;

        [Tooltip("Clue type")]
        public string clueType = "device_activated";

        [Tooltip("Clue strength (0-1)")]
        [Range(0f, 1f)] public float clueStrength = 0.8f;

        [Header("Unlock (Optional)")]
        [Tooltip("Unlock door/area on completion")]
        public GameObject unlockTarget;

        [Header("State")]
        [Tooltip("Is this interactable completed?")]
        public bool IsCompleted = false;

        [Tooltip("Is someone currently interacting?")]
        public bool IsInteracting = false;

        [Tooltip("Current interactor (player or bot)")]
        public Transform CurrentInteractor { get; private set; }

        [Header("Debug")]
        public bool showDebugLogs = true;
        public bool showDebugGizmos = true;

        // Events
        public System.Action OnInteractionComplete;

        // Internal state
        private bool playerInRange = false;
        private float interactionProgress = 0f;
        private float nextNoiseTime = 0f;
        private Transform playerTransform;
        
        // ? FIX: Track if interaction was started by manual input
        private bool interactionIsManual = false;

        private void Awake()
        {
            // Ensure trigger
            var collider = GetComponent<Collider2D>();
            if (!collider.isTrigger)
            {
                collider.isTrigger = true;
                Debug.LogWarning($"[QuestInteractable] {gameObject.name} - Collider was not Trigger! Fixed.");
            }
        }

        private void Update()
        {
            if (IsCompleted)
                return;

            // Player-only input path
            if (playerInRange && Input.GetKey(interactKey))
            {
                // Ensure player is the interactor
                if (CurrentInteractor == null)
                {
                    CurrentInteractor = playerTransform;
                    interactionIsManual = true; // ? FIX: Mark as manual
                }

                // Only progress if manual interaction matches
                if (CurrentInteractor == playerTransform && interactionIsManual)
                    UpdateInteraction(Time.deltaTime);
            }
            else if (IsInteracting && CurrentInteractor == playerTransform)
            {
                // Player released key -> cancel
                // ? FIX: Only cancel via Input if it WAS started manually
                if (interactionIsManual)
                {
                    CancelInteraction();
                }
            }
        }

        // -------------------------
        // BOT / GENERIC API
        // -------------------------

        /// <summary>
        /// Can a given agent start/continue interaction?
        /// </summary>
        public bool CanInteract(Transform agent)
        {
            if (IsCompleted || agent == null) return false;
            return Vector2.Distance(agent.position, transform.position) <= interactionRange;
        }

        /// <summary>
        /// Bot (or any agent) starts interacting programmatically.
        /// Returns true if interaction started (or already running by same agent).
        /// </summary>
        public bool BeginInteraction(Transform agent)
        {
            if (IsCompleted || agent == null) return false;

            // Must be in range
            if (!CanInteract(agent)) return false;

            // If someone else is interacting, deny
            if (IsInteracting && CurrentInteractor != null && CurrentInteractor != agent)
                return false;

            if (!IsInteracting)
            {
                IsInteracting = true;
                interactionProgress = 0f;
                nextNoiseTime = Time.time + noiseInterval;
                CurrentInteractor = agent;
                interactionIsManual = false; // ? FIX: Started by API, not Input

                if (showDebugLogs)
                    Debug.Log($"<color=cyan>[QuestInteractable] Interaction started by {agent.name}: {gameObject.name}</color>");
            }

            return true;
        }

        /// <summary>
        /// Bot (or any agent) ticks interaction progress programmatically.
        /// Returns true if completed this tick.
        /// If agent leaves range, interaction is canceled automatically.
        /// </summary>
        public bool TickInteraction(Transform agent, float dt)
        {
            if (IsCompleted) return true;
            if (agent == null) return false;

            // must be same interactor
            if (!IsInteracting || CurrentInteractor != agent)
            {
                // allow auto-start if in range
                if (!BeginInteraction(agent)) return false;
            }

            // if agent moved out of range -> cancel
            if (!CanInteract(agent))
            {
                CancelInteraction();
                return false;
            }

            UpdateInteraction(dt);
            return IsCompleted;
        }

        /// <summary>
        /// Explicit cancel for bot/agent.
        /// </summary>
        public void CancelInteractionBy(Transform agent)
        {
            if (!IsInteracting) return;
            if (agent != null && CurrentInteractor != agent) return;
            CancelInteraction();
        }

        /// <summary>
        /// Reset runtime state for new training episode.
        /// (QuestManager can call this)
        /// </summary>
        public void ResetRuntimeState(bool resetCompleted = true)
        {
            IsInteracting = false;
            interactionProgress = 0f;
            nextNoiseTime = 0f;
            CurrentInteractor = null;
            interactionIsManual = false; // ? FIX

            if (resetCompleted)
                IsCompleted = false;
        }

        // -------------------------
        // INTERNAL MECHANICS
        // -------------------------

        private void UpdateInteraction(float dt)
        {
            if (!IsInteracting)
                return;

            // Update progress
            interactionProgress += dt;

            // Emit periodic noise
            if (emitNoiseDuringInteraction && Time.time >= nextNoiseTime)
            {
                EmitNoise();
                nextNoiseTime = Time.time + noiseInterval;
            }

            // Completion
            if (interactionProgress >= interactionDuration)
            {
                CompleteInteraction();
            }
        }

        private void CancelInteraction()
        {
            IsInteracting = false;
            interactionProgress = 0f;
            CurrentInteractor = null;

            if (showDebugLogs)
                Debug.Log($"<color=yellow>[QuestInteractable] Interaction canceled: {gameObject.name}</color>");
        }

        private void CompleteInteraction()
        {
            if (IsCompleted)
                return;

            IsCompleted = true;
            IsInteracting = false;

            if (showDebugLogs)
                Debug.Log($"<color=lime>[QuestInteractable] Interaction complete: {gameObject.name}</color>");

            if (emitClueOnCompletion)
            {
                EmitClue();
            }

            if (unlockTarget)
            {
                UnlockTarget();
            }

            OnInteractionComplete?.Invoke();

            // Clear interactor
            CurrentInteractor = null;
        }

        private void EmitNoise()
        {
            if (!NoiseBus.Instance)
                return;

            // Get room ID
            string roomId = "None";
            if (AITest.World.WorldRegistry.Instance)
            {
                if (AITest.World.WorldRegistry.Instance.TryGetRoomAtPosition(transform.position, out string room))
                {
                    roomId = room;
                }
            }

            NoiseBus.Instance.Emit(
                transform.position,
                noiseRadius,
                roomId,
                isGlobalNoise
            );

            if (showDebugLogs)
                Debug.Log($"<color=orange>[QuestInteractable] Noise emitted (radius={noiseRadius}m, global={isGlobalNoise})</color>");
        }

        private void EmitClue()
        {
            if (!ClueEventBus.Instance)
                return;

            ClueEventBus.Instance.EmitClue(
                transform.position,
                clueStrength,
                clueType
            );

            if (showDebugLogs)
                Debug.Log($"<color=magenta>[QuestInteractable] Clue emitted: {clueType} (strength={clueStrength:F2})</color>");
        }

        private void UnlockTarget()
        {
            var collider = unlockTarget.GetComponent<Collider2D>();
            if (collider)
            {
                collider.enabled = false;

                if (showDebugLogs)
                    Debug.Log($"<color=green>[QuestInteractable] Unlocked: {unlockTarget.name}</color>");
            }

            var spriteRenderer = unlockTarget.GetComponent<SpriteRenderer>();
            if (spriteRenderer)
            {
                spriteRenderer.color = Color.green;
            }
        }

        public float GetInteractionProgress()
        {
            return Mathf.Clamp01(interactionProgress / interactionDuration);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                playerInRange = true;
                playerTransform = other.transform;

                if (showDebugLogs && !IsCompleted)
                    Debug.Log($"<color=cyan>[QuestInteractable] Player in range: {gameObject.name}</color>");
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                playerInRange = false;
                playerTransform = null;

                // Cancel only if the player was the interactor
                if (IsInteracting && CurrentInteractor == other.transform)
                {
                    CancelInteraction();
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos)
                return;

            Gizmos.color = IsCompleted ? Color.green : (playerInRange ? Color.yellow : Color.gray);
            Gizmos.DrawWireSphere(transform.position, interactionRange);

            if (emitNoiseDuringInteraction && !IsCompleted)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, noiseRadius);
            }

            if (IsInteracting)
            {
                Gizmos.color = Color.cyan;
                float progress = GetInteractionProgress();
                Gizmos.DrawLine(transform.position, transform.position + Vector3.up * progress * 2f);
            }

#if UNITY_EDITOR
            GUIStyle style = new GUIStyle();
            style.normal.textColor = IsCompleted ? Color.green : Color.white;
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;

            string label = gameObject.name;
            if (IsCompleted)
                label += "\nCOMPLETE";
            else if (IsInteracting)
                label += $"\n{GetInteractionProgress():P0}";
            else if (playerInRange)
                label += "\n[E] Interact";

            if (CurrentInteractor != null)
                label += $"\nBy: {CurrentInteractor.name}";

            Vector3 labelPos = transform.position + Vector3.up * 2.5f;
            UnityEditor.Handles.Label(labelPos, label, style);
#endif
        }
    }
}
