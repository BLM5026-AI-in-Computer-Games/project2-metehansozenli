using UnityEngine;
using AITest.Pathfinding;

namespace AITest.Enemy
{
    /// <summary>
    /// Düþman hareket kontrolü - Pathfinder wrapper
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Pathfinder))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Hareket hýzý")]
        public float speed = 3f;
        
        [Tooltip("Varýþ yarýçapý")]
        [Range(0.1f, 2f)] public float arriveRadius = 0.6f; // 1.5 ? 0.6 (ayný tolerance)
        
        [Header("Stuck Prevention")]
        [Tooltip("Stuck tespit süresi")]
        public float stuckTimeout = 3f;
        
        private Rigidbody2D rb;
        private Pathfinder pathfinder;
        
        private Vector2 lastPosition;
        private float stuckTimer = 0f;
        
        public bool IsMoving => rb.linearVelocity.magnitude > 0.1f;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            pathfinder = GetComponent<Pathfinder>();
            
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            
            lastPosition = transform.position;
        }
        
        /// <summary>
        /// Hedefe git (A* ile)
        /// </summary>
        public void GoTo(Vector2 target)
        {
            pathfinder.SetTarget(target);
            // Debug log kaldýr (spam önle)
            // Debug.Log($"<color=cyan>[EnemyController] GoTo: {target} (current: {transform.position})</color>");
        }
        
        /// <summary>
        /// Hedefe vardýk mý?
        /// </summary>
        public bool Arrived(float radius = -1f)
        {
            if (radius < 0) radius = arriveRadius;
            
            bool arrived = pathfinder.ReachedTarget;
            
            // Debug log'u yorum satýrýna al (spam önle)
            // if (Time.frameCount % 60 == 0 && pathfinder.CurrentTarget.HasValue)
            // {
            //     float dist = Vector2.Distance(transform.position, pathfinder.CurrentTarget.Value);
            //     bool hasPath = pathfinder.HasPath;
            //     Vector2? nextWaypoint = pathfinder.GetNextWaypoint();
            //     
            //     Debug.Log($"<color=yellow>[EnemyController] Target dist: {dist:F2} | HasPath: {hasPath} | NextWP: {(nextWaypoint.HasValue ? nextWaypoint.Value.ToString() : "NULL")} | Velocity: {rb.linearVelocity.magnitude:F2}</color>");
            // }
            
            return arrived;
        }
        
        /// <summary>
        /// Dur
        /// </summary>
        public void Stop()
        {
            rb.linearVelocity = Vector2.zero;
        }
        
        private void FixedUpdate()
        {
            if (!pathfinder.HasPath)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }
            
            // Bir sonraki waypoint'i al
            Vector2? waypoint = pathfinder.GetNextWaypoint();
            
            if (!waypoint.HasValue)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }
            
            // Waypoint'e doðru hareket et
            Vector2 direction = (waypoint.Value - (Vector2)transform.position).normalized;
            rb.linearVelocity = direction * speed;
            
            // Rotation (hareket yönüne bak)
            if (direction.magnitude > 0.1f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                rb.SetRotation(angle);
            }
            
            // Stuck prevention
            CheckStuck();
        }
        
        private void CheckStuck()
        {
            float dist = Vector2.Distance(transform.position, lastPosition);
            
            if (dist < 0.05f)
            {
                stuckTimer += Time.fixedDeltaTime;
                
                if (stuckTimer >= stuckTimeout)
                {
                    // Stuck! Küçük random düzeltme
                    Vector2 randomOffset = Random.insideUnitCircle * 0.5f;
                    transform.position = (Vector2)transform.position + randomOffset;
                    
                    stuckTimer = 0f;
                    Debug.LogWarning("[EnemyController] Stuck detected! Position corrected.");
                }
            }
            else
            {
                stuckTimer = 0f;
            }
            
            lastPosition = transform.position;
        }
    }
}
