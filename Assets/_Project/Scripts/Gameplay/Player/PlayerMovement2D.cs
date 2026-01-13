using UnityEngine;

namespace Project.Gameplay
{
    /// <summary>
    /// Basit 2D top-down player hareketi (WASD + Sprint)
    /// Rigidbody2D tabanlý - PlayerNoiseEmitter ile uyumlu
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement2D : MonoBehaviour
    {
        #region Configuration
        [Header("Movement Settings")]
        [Tooltip("Normal yürüme hýzý")]
        public float walkSpeed = 3f;

        [Tooltip("Sprint hýzý")]
        public float sprintSpeed = 6f;

        [Tooltip("Hýzlanma/yavaþlama katsayýsý")]
        [Range(0.1f, 1f)] public float acceleration = 0.8f;

        [Header("Input Settings")]
        [Tooltip("Sprint tuþu (PlayerNoiseEmitter ile senkron olmalý)")]
        public KeyCode sprintKey = KeyCode.LeftShift;

        [Header("Debug")]
        [Tooltip("Mevcut hýz (readonly)")]
        public float currentSpeed;

        [Tooltip("Sprint aktif mi?")]
        public bool isSprinting;
        #endregion

        #region Private Fields
        private Rigidbody2D rb;
        private Vector2 moveInput;
        private AITest.Player.PlayerHideController hideController; // ? Hiding check
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            hideController = GetComponent<AITest.Player.PlayerHideController>(); // ? Get hide controller
            
            // 2D top-down için gravity kapalý
            rb.gravityScale = 0f;
            
            // Collision detection için
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            
            // Rotasyon kilitli (top-down için)
            rb.freezeRotation = true;
        }

        private void Update()
        {
            // ? CRITICAL: Block input if hiding!
            if (hideController != null && hideController.IsHiding)
            {
                moveInput = Vector2.zero;
                isSprinting = false;
                return; // ? Skip input reading!
            }
            
            // Input okuma
            float horizontal = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right Arrow
            float vertical = Input.GetAxisRaw("Vertical");     // W/S or Up/Down Arrow
            
            moveInput = new Vector2(horizontal, vertical).normalized;

            // Sprint kontrolü
            isSprinting = Input.GetKey(sprintKey);
            
            // ? DEBUG: Input kontrolü
            if (moveInput.sqrMagnitude > 0.01f)
            {
                Debug.Log($"<color=cyan>[PlayerMovement2D] Input: {moveInput}, Speed: {currentSpeed}</color>");
            }
        }

        private void FixedUpdate()
        {
            // ? CRITICAL: Force freeze if hiding!
            if (hideController != null && hideController.IsHiding)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                currentSpeed = 0f;
                return; // ? Skip movement!
            }
            
            // Hedef hýz
            float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
            Vector2 targetVelocity = moveInput * targetSpeed;

            // Smooth acceleration
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVelocity, acceleration);

            // Debug
            currentSpeed = rb.linearVelocity.magnitude;

            // Opsiyonel: Hareket yönüne bakma
            if (moveInput.sqrMagnitude > 0.01f)
            {
                float angle = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// Player þu anda hareket ediyor mu?
        /// </summary>
        public bool IsMoving()
        {
            return rb.linearVelocity.sqrMagnitude > 0.1f;
        }

        /// <summary>
        /// Player þu anda sprint yapýyor mu?
        /// </summary>
        public bool IsSprinting()
        {
            return isSprinting && IsMoving();
        }

        /// <summary>
        /// Mevcut hareket hýzýný al
        /// </summary>
        public float GetCurrentSpeed()
        {
            return currentSpeed;
        }
        #endregion
    }
}
