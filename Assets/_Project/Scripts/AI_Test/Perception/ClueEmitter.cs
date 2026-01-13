using UnityEngine;

namespace AITest.Perception
{
    /// <summary>
    /// Clue Emitter - Emit environmental clues when interacted with
    /// 
    /// USAGE:
    /// 1. Attach to interactable objects (doors, items, etc.)
    /// 2. Set clueType and clueStrength
    /// 3. Call EmitClue() when player interacts
    /// 4. Enemies with Perception will detect if in range + LOS
    /// 
    /// EXAMPLES:
    /// - Door: EmitClue on Open (ClueType.OpenDoor, strength=0.8)
    /// - Item: EmitClue on Pickup (ClueType.DroppedItem, strength=0.6)
    /// - Furniture: EmitClue on Move (ClueType.MovedObject, strength=0.7)
    /// </summary>
    public class ClueEmitter : MonoBehaviour
    {
        [Header("Clue Properties")]
        [Tooltip("Type of clue emitted")]
        public ClueType clueType = ClueType.Generic;
        
        [Tooltip("Clue strength (0-1) - higher = more noticeable")]
        [Range(0f, 1f)]
        public float clueStrength = 0.7f;
        
        [Tooltip("Cooldown between emits (seconds)")]
        [Range(0f, 10f)]
        public float emitCooldown = 2f;
        
        [Header("Auto Emit (Optional)")]
        [Tooltip("Emit clue automatically on Start")]
        public bool emitOnStart = false;
        
        [Tooltip("Emit clue when player enters trigger")]
        public bool emitOnPlayerTrigger = false;
        
        [Header("Debug")]
        [Tooltip("Show debug logs")]
        public bool showDebugLogs = true;
        
        [Tooltip("Show gizmo in Scene view")]
        public bool showGizmo = true;
        
        [Tooltip("Gizmo color")]
        public Color gizmoColor = Color.magenta;

        // Private state
        private float lastEmitTime = -999f;
        private bool hasEmittedOnce = false;

        private void Start()
        {
            if (emitOnStart)
            {
                EmitClue();
            }
        }

        /// <summary>
        /// Emit clue event (public API)
        /// </summary>
        public void EmitClue()
        {
            // Cooldown check
            if (Time.time - lastEmitTime < emitCooldown)
            {
                if (showDebugLogs)
                    Debug.LogWarning($"[ClueEmitter] {gameObject.name} - Emit on cooldown ({emitCooldown:F1}s)");
                return;
            }
            
            // Emit via ClueEventBus
            if (ClueEventBus.Instance)
            {
                Vector2 position = transform.position;
                ClueEventBus.Instance.EmitClue(position, clueStrength, clueType.ToString());
                
                lastEmitTime = Time.time;
                hasEmittedOnce = true;
                
                if (showDebugLogs)
                    Debug.Log($"<color=magenta>[ClueEmitter] {gameObject.name} emitted clue: {clueType} (strength={clueStrength})</color>");
            }
            else
            {
                Debug.LogError($"[ClueEmitter] {gameObject.name} - ClueEventBus not found!");
            }
        }

        /// <summary>
        /// Emit clue with custom strength
        /// </summary>
        public void EmitClue(float customStrength)
        {
            float originalStrength = clueStrength;
            clueStrength = Mathf.Clamp01(customStrength);
            EmitClue();
            clueStrength = originalStrength;
        }

        #region Trigger Callbacks (Optional)

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!emitOnPlayerTrigger) return;
            
            if (other.CompareTag("Player"))
            {
                EmitClue();
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!showGizmo) return;
            
            // Draw clue marker
            Gizmos.color = gizmoColor;
            
            // Icon based on clue type
            float size = 0.5f;
            Vector3 pos = transform.position;
            
            switch (clueType)
            {
                case ClueType.OpenDoor:
                    // Rectangle (door)
                    Gizmos.DrawWireCube(pos, new Vector3(size * 2f, size * 1.5f, 0f));
                    break;
                
                case ClueType.Footprint:
                    // Small circle (footprint)
                    Gizmos.DrawWireSphere(pos, size * 0.7f);
                    break;
                
                case ClueType.MovedObject:
                    // Box (furniture)
                    Gizmos.DrawWireCube(pos, Vector3.one * size);
                    break;
                
                case ClueType.DroppedItem:
                    // Small sphere (item)
                    Gizmos.DrawWireSphere(pos, size * 0.5f);
                    break;
                
                default:
                    // Generic (diamond)
                    Gizmos.DrawLine(pos + Vector3.up * size, pos + Vector3.right * size);
                    Gizmos.DrawLine(pos + Vector3.right * size, pos + Vector3.down * size);
                    Gizmos.DrawLine(pos + Vector3.down * size, pos + Vector3.left * size);
                    Gizmos.DrawLine(pos + Vector3.left * size, pos + Vector3.up * size);
                    break;
            }
            
            // Strength indicator (filled)
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, clueStrength);
            Gizmos.DrawSphere(pos, size * 0.3f);
            
            #if UNITY_EDITOR
            // Label
            GUIStyle style = new GUIStyle();
            style.normal.textColor = gizmoColor;
            style.fontSize = 11;
            style.alignment = TextAnchor.MiddleCenter;
            
            string label = $"?? {clueType}\nStr: {clueStrength:F1}";
            
            if (Application.isPlaying && hasEmittedOnce)
            {
                float timeSinceEmit = Time.time - lastEmitTime;
                label += $"\n({timeSinceEmit:F1}s ago)";
            }
            
            Vector3 labelPos = pos + Vector3.up * (size + 0.7f);
            UnityEditor.Handles.Label(labelPos, label, style);
            #endif
        }

        #endregion
    }
}
