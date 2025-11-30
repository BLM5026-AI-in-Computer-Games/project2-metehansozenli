using System;
using UnityEngine;

namespace AITest.Core
{
    /// <summary>
    /// Ses event'lerini yayýnlayan singleton bus
    /// </summary>
    public class NoiseBus : MonoBehaviour
    {
        public static NoiseBus Instance { get; private set; }
        
        /// <summary>
        /// Ses event delegate (position, radius, sectorId, isGlobal)
        /// </summary>
        public event Action<Vector2, float, string, bool> OnNoise;
        
        [Header("Debug")]
        public bool debugMode = true;
        
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
        /// Ses event'i yayýnla
        /// </summary>
        /// <param name="position">Ses pozisyonu</param>
        /// <param name="radius">Duyulma yarýçapý</param>
        /// <param name="sectorId">Hangi sektörde oluþtu</param>
        /// <param name="isGlobal">Range kontrolü atlansýn mý? (K tuþu, görev sesleri için TRUE)</param>
        public void Emit(Vector2 position, float radius, string sectorId, bool isGlobal = false)
        {
            if (debugMode)
            {
                string typeLabel = isGlobal ? "GLOBAL" : "LOCAL";
                Debug.Log($"<color=orange>[NoiseBus] {typeLabel} Noise @ {position} (radius={radius:F1}, sector={sectorId})</color>");
            }
            
            OnNoise?.Invoke(position, radius, sectorId, isGlobal);
        }
        
        private void OnDrawGizmos()
        {
            // Gizmo çizimi noise emit olduðunda olacak
        }
    }
}
