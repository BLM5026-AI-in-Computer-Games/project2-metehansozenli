using UnityEngine;
using System;

namespace AITest.Perception
{
    /// <summary>
    /// Clue Event Bus - Global dispatcher for environmental clues
    /// 
    /// PROMPT 3: Optional clue system
    /// - Interactables emit clue events
    /// - Enemy perception detects clues if within range + LOS
    /// - Examples: footprints, open doors, moved objects
    /// </summary>
    public class ClueEventBus : MonoBehaviour
    {
        // Singleton instance
        private static ClueEventBus instance;
        public static ClueEventBus Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<ClueEventBus>();
                    
                    if (instance == null)
                    {
                        GameObject go = new GameObject("ClueEventBus");
                        instance = go.AddComponent<ClueEventBus>();
                    }
                }
                return instance;
            }
        }

        // Clue event delegate
        public delegate void ClueEventHandler(Vector2 position, float strength, string clueType);
        public event ClueEventHandler OnClue;

        private void Awake()
        {
            // Singleton enforcement
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
        }

        /// <summary>
        /// Emit a clue event (called by ClueEmitter components)
        /// </summary>
        public void EmitClue(Vector2 position, float strength, string clueType)
        {
            OnClue?.Invoke(position, strength, clueType);
            
            Debug.Log($"<color=magenta>[ClueEventBus] ?? Clue emitted: {clueType} @ {position} (strength={strength:F2})</color>");
        }

        /// <summary>
        /// Get number of subscribers
        /// </summary>
        public int SubscriberCount => OnClue != null ? OnClue.GetInvocationList().Length : 0;
    }

    /// <summary>
    /// Clue types (categorization)
    /// </summary>
    public enum ClueType
    {
        Generic = 0,        // Unknown clue
        Footprint = 1,      // Player footstep
        OpenDoor = 2,       // Door left open
        MovedObject = 3,    // Furniture moved
        DroppedItem = 4,    // Item on floor
        BloodStain = 5,     // (Horror theme)
        Sound = 6           // Residual sound (not NoiseEvent)
    }
}
