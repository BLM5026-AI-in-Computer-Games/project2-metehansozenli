using System;
using UnityEngine;

namespace AITest.Core
{
    /// <summary>
    /// Clue Event Bus - Singleton for clue events
    /// 
    /// PROMPT 15: Clue event system
    /// - Similar to NoiseBus
    /// - Emits clues (device activated, door unlocked, etc.)
    /// - Enemy perception listens to these events
    /// </summary>
    public class ClueEventBus : MonoBehaviour
    {
        public static ClueEventBus Instance { get; private set; }
        
        /// <summary>
        /// Clue event delegate (position, strength, clueType)
        /// </summary>
        public event Action<Vector2, float, string> OnClue;
        
        [Header("Debug")]
        public bool debugMode = false;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        /// <summary>
        /// Emit clue event
        /// </summary>
        /// <param name="position">Clue position</param>
        /// <param name="strength">Clue strength (0-1)</param>
        /// <param name="clueType">Clue type/description</param>
        public void EmitClue(Vector2 position, float strength, string clueType)
        {
            if (debugMode)
            {
                Debug.Log($"<color=magenta>[ClueEventBus] Clue: {clueType} @ {position} (strength={strength:F2})</color>");
            }
            
            OnClue?.Invoke(position, strength, clueType);
        }
    }
}
