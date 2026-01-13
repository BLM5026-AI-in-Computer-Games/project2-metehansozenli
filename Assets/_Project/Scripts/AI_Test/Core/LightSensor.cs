using UnityEngine;

namespace AITest.Core
{
    /// <summary>
    /// Light level sensor for enemy threat assessment
    /// Detects if a position is in light or darkness
    /// </summary>
    public class LightSensor : MonoBehaviour
    {
        [Header("Light Detection")]
        [Tooltip("LightZone layer mask")]
        public LayerMask lightZoneMask;
        
        [Tooltip("Detection radius")]
        [Range(0.1f, 3f)]
        public float detectionRadius = 1f;
        
        [Header("Debug")]
        public bool drawGizmos = false;
        
        /// <summary>
        /// Get light level at a specific position
        /// </summary>
        /// <param name="position">World position to check</param>
        /// <returns>Light level (0 = dark, 1 = light)</returns>
        public float GetLightLevel(Vector2 position)
        {
            // Simple overlap check
            Collider2D lightZone = Physics2D.OverlapCircle(position, detectionRadius, lightZoneMask);
            return lightZone != null ? 1f : 0f;
        }
        
        /// <summary>
        /// Check if position is in light
        /// </summary>
        public bool IsInLight(Vector2 position)
        {
            return GetLightLevel(position) > 0.5f;
        }
        
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            
            // Draw detection radius
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
