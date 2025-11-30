using UnityEngine;

namespace AITest.Player
{
    /// <summary>
    /// WASD + Shift koþu
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Normal yürüme hýzý")]
        public float walkSpeed = 3f;
        
        [Tooltip("Koþma hýzý (Shift)")]
        public float runSpeed = 6f;
        
        [Header("Input")]
        [Tooltip("Koþma tuþu")]
        public KeyCode runKey = KeyCode.LeftShift;
        
        private Rigidbody2D rb;
        private Vector2 moveInput;
        private bool isRunning;
        
        public bool IsRunning => isRunning;
        public Vector2 Velocity => rb ? rb.linearVelocity : Vector2.zero;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
        
        private void Update()
        {
            // Input
            moveInput.x = Input.GetAxisRaw("Horizontal");
            moveInput.y = Input.GetAxisRaw("Vertical");
            moveInput.Normalize();
            
            isRunning = Input.GetKey(runKey);
        }
        
        private void FixedUpdate()
        {
            float speed = isRunning ? runSpeed : walkSpeed;
            rb.linearVelocity = moveInput * speed;
        }
    }
}
