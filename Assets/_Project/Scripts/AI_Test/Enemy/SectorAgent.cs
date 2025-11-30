using UnityEngine;
using System.Collections;
using AITest.Sector;

namespace AITest.Enemy
{
    /// <summary>
    /// FSM: Travel ? InSector ? Investigate/Sweep/Ambush ? Done
    /// </summary>
    public class SectorAgent : MonoBehaviour
    {
        [Header("References")]
        public EnemyController enemyController;
        public Sectorizer sectorizer;
        
        [Header("Investigate Settings")]
        [Tooltip("360° dönüþ süresi (saniye)")]
        public float investigateRotationTime = 2f;
        
        [Header("Debug")]
        public bool debugMode = true;
        
        public enum FSMState
        {
            Idle,
            Travel,
            InSector,
            Investigate,
            Sweep,
            Ambush,
            Done
        }
        
        private FSMState currentState = FSMState.Idle;
        private string targetSectorId;
        
        public bool IsBusy => currentState != FSMState.Idle && currentState != FSMState.Done;
        public FSMState CurrentState => currentState;
        
        private void Awake()
        {
            if (!enemyController) enemyController = GetComponent<EnemyController>();
            if (!sectorizer) sectorizer = Sectorizer.Instance;
        }
        
        /// <summary>
        /// Investigate mode baþlat
        /// </summary>
        public void DoInvestigate(string sectorId)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[SectorAgent] Already busy!");
                return;
            }
            
            targetSectorId = sectorId;
            StartCoroutine(InvestigateSequence());
        }
        
        /// <summary>
        /// Sweep mode baþlat
        /// </summary>
        public void DoSweep(string sectorId)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[SectorAgent] Already busy!");
                return;
            }
            
            targetSectorId = sectorId;
            StartCoroutine(SweepSequence());
        }
        
        /// <summary>
        /// Ambush mode baþlat
        /// </summary>
        public void DoAmbush(string sectorId, int portalIndex = 0)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[SectorAgent] Already busy!");
                return;
            }
            
            targetSectorId = sectorId;
            StartCoroutine(AmbushSequence(portalIndex));
        }
        
        private IEnumerator InvestigateSequence()
        {
            currentState = FSMState.Travel;
            
            var sector = sectorizer?.GetById(targetSectorId);
            if (sector == null)
            {
                currentState = FSMState.Done;
                yield break;
            }
            
            // 1. Sektöre git (portal ? anchor)
            Vector2 portal = sectorizer.GetNearestPortal(sector, transform.position);
            enemyController.GoTo(portal);
            yield return new WaitUntil(() => enemyController.Arrived());
            
            Vector2 anchor = sectorizer.GetNearestAnchor(sector, transform.position);
            enemyController.GoTo(anchor);
            yield return new WaitUntil(() => enemyController.Arrived());
            
            currentState = FSMState.InSector;
            
            // 2. Investigate (360° dön)
            currentState = FSMState.Investigate;
            
            if (debugMode)
                Debug.Log($"<color=cyan>[SectorAgent] Investigating sector {targetSectorId}...</color>");
            
            float startAngle = transform.eulerAngles.z;
            float elapsed = 0f;
            
            while (elapsed < investigateRotationTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / investigateRotationTime;
                float angle = startAngle + 360f * t;
                transform.rotation = Quaternion.Euler(0, 0, angle);
                
                yield return null;
            }
            
            currentState = FSMState.Done;
            
            if (debugMode)
                Debug.Log($"<color=lime>[SectorAgent] Investigate completed!</color>");
        }
        
        private IEnumerator SweepSequence()
        {
            currentState = FSMState.Travel;
            
            var sector = sectorizer?.GetById(targetSectorId);
            if (sector == null)
            {
                currentState = FSMState.Done;
                yield break;
            }
            
            // Portal'a git
            Vector2 portal = sectorizer.GetNearestPortal(sector, transform.position);
            enemyController.GoTo(portal);
            yield return new WaitUntil(() => enemyController.Arrived());
            
            currentState = FSMState.InSector;
            currentState = FSMState.Sweep;
            
            // Sweep route
            Vector2[] sweepRoute = sectorizer.GetNearestSweepRoute(sector, transform.position);
            
            if (debugMode)
                Debug.Log($"<color=cyan>[SectorAgent] Sweeping sector {targetSectorId} ({sweepRoute.Length} points)...</color>");
            
            foreach (var point in sweepRoute)
            {
                enemyController.GoTo(point);
                yield return new WaitUntil(() => enemyController.Arrived());
                yield return new WaitForSeconds(0.75f);
            }
            
            currentState = FSMState.Done;
            
            if (debugMode)
                Debug.Log($"<color=lime>[SectorAgent] Sweep completed!</color>");
        }
        
        private IEnumerator AmbushSequence(int portalIndex)
        {
            currentState = FSMState.Travel;
            
            var sector = sectorizer?.GetById(targetSectorId);
            if (sector == null)
            {
                currentState = FSMState.Done;
                yield break;
            }
            
            // Portal'a git
            Vector2 portal = sector.portals[Mathf.Clamp(portalIndex, 0, sector.portals.Length - 1)];
            enemyController.GoTo(portal);
            yield return new WaitUntil(() => enemyController.Arrived());
            
            currentState = FSMState.InSector;
            currentState = FSMState.Ambush;
            
            if (debugMode)
                Debug.Log($"<color=yellow>[SectorAgent] Ambush at sector {targetSectorId}, portal {portalIndex}...</color>");
            
            // Bekle (6 saniye)
            yield return new WaitForSeconds(6f);
            
            currentState = FSMState.Done;
            
            if (debugMode)
                Debug.Log($"<color=lime>[SectorAgent] Ambush completed!</color>");
        }
    }
}
