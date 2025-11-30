using UnityEngine;
using AITest.Core;
using AITest.Sector;

namespace AITest.Player
{
    /// <summary>
    /// Player'dan ses yayýnlar (koþma/manuel tuþ)
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class NoiseEmitter : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Koþma ses yarýçapý")]
        public float runNoiseRadius = 10f;
        
        [Tooltip("Koþarken ses aralýðý (saniye)")]
        public float runNoiseInterval = 1.5f;
        
        [Tooltip("Manuel ses tuþu (test)")]
        public KeyCode manualNoiseKey = KeyCode.K;
        
        [Tooltip("Manuel ses yarýçapý")]
        public float manualNoiseRadius = 15f;
        
        private PlayerController playerController;
        private float lastNoiseTime;
        
        // Public counter (PlayerStatsMonitor için)
        public int NoiseCount { get; private set; }
        
        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
        }
        
        private void Update()
        {
            // Koþarken otomatik ses (LOCAL - range kontrolü var)
            if (playerController.IsRunning && Time.time - lastNoiseTime >= runNoiseInterval)
            {
                EmitNoise(runNoiseRadius, isGlobal: false); // ? LOCAL
            }
            
            // Manuel ses (GLOBAL - range kontrolü yok, K tuþu, görev sesleri)
            if (Input.GetKeyDown(manualNoiseKey))
            {
                EmitNoise(manualNoiseRadius, isGlobal: true); // ? GLOBAL!
                Debug.Log("<color=orange>[NoiseEmitter] ?? Manual GLOBAL noise (K key)</color>");
            }
        }
        
        private void EmitNoise(float radius, bool isGlobal)
        {
            Vector2 pos = transform.position;
            
            // Sektör ID al
            string sectorId = "None";
            if (Sectorizer.Instance != null)
            {
                sectorId = Sectorizer.Instance.GetIdByPosition(pos);
            }
            
            // NoiseBus'a yayýnla
            if (NoiseBus.Instance != null)
            {
                NoiseBus.Instance.Emit(pos, radius, sectorId, isGlobal);
            }
            
            lastNoiseTime = Time.time;
            NoiseCount++;
        }
        
        /// <summary>
        /// PlayerStatsMonitor için counter reset
        /// </summary>
        public void ResetNoiseCount()
        {
            NoiseCount = 0;
        }
    }
}
