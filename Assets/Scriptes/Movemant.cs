using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // --- Компоненты
    private CollisionController collisionController;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // --- Параметры движения
    public float speed = 10f;             // Максимальная скорость на земле.
    public float airMaxSpeed = 2f;          // Целевая скорость в воздухе.
    public float airAcceleration = 5f;
    public float airDrag = 0.1f;
    public float airControlInfluence = 0.2f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- Параметры рывка (Dash)
    public float dashDistance = 5f;
    public float dashSpeed = 20f;
    public float dashCooldown = 1f;
    private bool canDash = true;
    private bool isInvulnerable = false;
    private bool isDashing = false;
    public float dashAfterLockDuration = 0.2f;
    private bool isDashLocked = false;
    public float dashImpulseRetention = 0.2f;

    // --- Параметры подката (Slide) и приседа (Crouch)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    public float slideBoost = 1.5f;
    private bool isSliding = false;
    private bool isCrouching = false;

    // --- Дополнительный горизонтальный импульс прыжка (если не wall jump)
    public float jumpImpulseFactor = 0.2f;

    // --- Параметры цепления за стену (Wall Hang / Slide)
    public float wallHangTime = 0.5f;
    public float wallSlideAcceleration = 10f;
    public float wallSlideMaxSpeed = 5f;
    public float wallJumpForce = 10f;
    public float wallJumpHorizForce = 5f;
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;
    public float wallDetachCooldown = 0.3f;
    private float timeSinceDetached = 0f;
    // Сторона стены, к которой цепляемся: 1 – справа, -1 – слева.
    private int wallContactSide = 0;

    // --- Автоматический подъём (Auto Climb)
    public float wallAutoClimbDistance = 0.5f;
    public float wallAutoClimbSpeed = 2f;
    private float initialGrabVerticalSpeed = 0f;
    private Coroutine autoClimbCoroutine = null;
    private bool autoClimbing = false;
    private float wallClimbStartY = 0f;

    // --- Стандартная гравитация
    public float defaultGravityScale;
    // --- Блокировка управления после wall jump.
    private bool isWallJumping = false;
    public float wallJumpLockDuration = 0.2f;
    // --- Гравитация при цеплении.
    public float wallHangGravityScale = 0f;
    private float originalGravityScale;
    // --- Флаг направления.
    private bool facingRight = true;
    // --- Хитбокс (для восстановления после приседа/подката)
    private Vector2 normalSize;
    private Vector2 normalOffset;
    // --- Горизонтальный ввод
    private float hInput = 0f;

    // --- Параметры залезания на край (Ledge Climb)
    public Transform ledgeRayOrigin;         // Точка, из которой идёт raycast вниз для определения края.
    public float ledgeRayLength = 0.1f;        // Длина луча проверки.
    public float ledgeClimbVerticalDistance = 0.6f;
    public float ledgeClimbHorizontalOffset = 0.3f;
    public float ledgeClimbDuration = 0.4f;
    private bool isLedgeClimbing = false;
    private Vector2 ledgeClimbStartPos;
    private Vector2 ledgeClimbTargetPos;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        originalGravityScale = rb.gravityScale;
        defaultGravityScale = originalGravityScale;
        normalSize = boxCollider.size;
        normalOffset = boxCollider.offset;
    }

    void Update()
    {
        float rawH = Input.GetAxisRaw("Horizontal");
        float threshold = 0.2f;
        int inputDir = 0;
        if (rawH >= threshold)
            inputDir = 1;
        else if (rawH <= -threshold)
            inputDir = -1;

        hInput = rawH;
        timeSinceDetached += Time.deltaTime;
        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        // Обновление хитбокса
        if (isSliding)
            collisionController.currentHitboxState = CollisionController.HitboxState.Sliding;
        else if (isCrouching)
            collisionController.currentHitboxState = CollisionController.HitboxState.Crouching;
        else
            collisionController.currentHitboxState = CollisionController.HitboxState.Normal;

        // Поворот персонажа
        if (inputDir > 0 && !facingRight)
            Flip();
        else if (inputDir < 0 && facingRight)
            Flip();

        if (isSlidingOnWall && !touchingWall)
            StopWallSlide();

        // --- Прыжок
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                if (autoClimbing)
                {
                    autoClimbing = false;
                    if (autoClimbCoroutine != null)
                    {
                        StopCoroutine(autoClimbCoroutine);
                        autoClimbCoroutine = null;
                    }
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                    StartCoroutine(PerformWallJump());
                }
                else
                {
                    if (Mathf.Abs(hInput) > 0.01f && Mathf.Sign(hInput) == -wallContactSide)
                    {
                        rb.linearVelocity = new Vector2(-wallContactSide * wallJumpHorizForce, wallJumpForce);
                        StartCoroutine(WallJumpLockCoroutine());
                    }
                    else
                    {
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                    }
                    StopWallSlide();
                    timeSinceDetached = 0f;
                    jumpCount = 0;
                }
            }
            else
            {
                if (!grounded && Mathf.Abs(hInput) > 0.01f && rb.linearVelocity.x != 0 &&
                    Mathf.Sign(rb.linearVelocity.x) != Mathf.Sign(hInput))
                {
                    rb.linearVelocity = new Vector2(hInput * airMaxSpeed, jumpForce);
                }
                else
                {
                    float extraX = rb.linearVelocity.x * jumpImpulseFactor;
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x + extraX, jumpForce);
                }
                jumpCount++;
            }
        }
        if (grounded)
        {
            jumpCount = 0;
            StopWallSlide();
        }

        // --- Цепление за стеной (Wall Hang) и авто-подъём (Auto Climb)
        if (collisionController.IsTouchingWall && !grounded && timeSinceDetached >= wallDetachCooldown)
        {
            if (!isSlidingOnWall)
            {
                if (inputDir == collisionController.GetLastWallContactSide())
                    StartWallHang();
            }
            else
            {
                if (!autoClimbing)
                {
                    // Дополнительная логика, если требуется.
                }
            }
        }
        if (Input.GetKeyDown(KeyCode.S) && isSlidingOnWall)
            StopWallSlide();
        if (isSlidingOnWall && wallSlideActive && touchingWall)
        {
            float newY = Mathf.MoveTowards(rb.linearVelocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.deltaTime);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, newY);
        }

        // --- Остальные механики (Dash, Slide, Crouch)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isSliding && !isCrouching)
        {
            if (isSlidingOnWall)
            {
                if ((collisionController.GetLastWallContactSide() == 1 && !facingRight) ||
                    (collisionController.GetLastWallContactSide() == -1 && facingRight))
                    StartCoroutine(Dash());
            }
            else
                StartCoroutine(Dash());
        }

        if (grounded)
        {
            if (Input.GetKeyDown(KeyCode.LeftControl) && Mathf.Abs(rawH) >= threshold && !isSliding)
                StartCoroutine(Slide(rawH));
            else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && Mathf.Abs(rawH) < threshold)
            {
                isCrouching = true;
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.S))
                isCrouching = false;
        }
        else
            isCrouching = false;

        if (grounded && Input.GetKey(KeyCode.LeftControl) && Mathf.Abs(rb.linearVelocity.x) > 0.1f && !isSliding && !isCrouching)
            StartCoroutine(Slide(rb.linearVelocity.x));

        if (!isSliding && !isCrouching)
        {
            boxCollider.size = normalSize;
            boxCollider.offset = normalOffset;
        }
        if (!isSlidingOnWall)
            collisionController.ignoreFlipForWallChecks = false;

        // --- Механика залезания на край (Ledge Climb)
        if (!isLedgeClimbing && !isWallJumping && !autoClimbing && !isSlidingOnWall && collisionController.IsTouchingWall)
            TryStartLedgeClimb();
    }

    void FixedUpdate()
    {
        bool grounded = collisionController.IsGrounded;
        if (!isDashing && !isDashLocked && !isSlidingOnWall && !isSliding && !isCrouching && !isWallJumping)
        {
            if (grounded)
            {
                rb.linearVelocity = new Vector2(hInput * speed, rb.linearVelocity.y);
            }
            else
            {
                if (Mathf.Abs(hInput) > 0.01f)
                {
                    if (Mathf.Sign(rb.linearVelocity.x) == Mathf.Sign(hInput) && Mathf.Abs(rb.linearVelocity.x) > Mathf.Abs(hInput * airMaxSpeed))
                    {
                        // Сохраняем импульс.
                    }
                    else
                    {
                        float targetX = hInput * airMaxSpeed;
                        float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetX, airAcceleration * Time.fixedDeltaTime * airControlInfluence);
                        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
                    }
                }
                else
                {
                    float dragFactor = 1f - airDrag * Time.fixedDeltaTime;
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x * dragFactor, rb.linearVelocity.y);
                }
            }
        }
    }

    // --- Методы цепления за стену (Wall Hang / Slide)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false;
            initialGrabVerticalSpeed = rb.linearVelocity.y;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0;
            jumpCount = 0;
            wallContactSide = collisionController.GetLastWallContactSide();
            if (initialGrabVerticalSpeed > 0)
            {
                wallClimbStartY = transform.position.y;
                autoClimbing = true;
                StartCoroutine(AutoClimbCoroutine());
                StartCoroutine(WaitForAutoClimbThenWallHang());
            }
            else
                StartCoroutine(WallHangCoroutine());
        }
    }

    private IEnumerator AutoClimbCoroutine()
    {
        while (autoClimbing && transform.position.y < wallClimbStartY + wallAutoClimbDistance)
        {
            if (!collisionController.IsTouchingWall)
            {
                autoClimbing = false;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                StartCoroutine(WallHangCoroutine());
                yield break;
            }
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, wallAutoClimbSpeed);
            yield return null;
        }
        autoClimbing = false;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        StartCoroutine(WallHangCoroutine());
    }

    private IEnumerator WaitForAutoClimbThenWallHang()
    {
        while (autoClimbing)
        {
            yield return null;
        }
        StartCoroutine(WallHangCoroutine());
    }

    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
            wallSlideActive = true;
    }

    private IEnumerator PerformWallJump()
    {
        yield return null;
        if (Mathf.Abs(hInput) > 0.01f && Mathf.Sign(hInput) == -wallContactSide)
        {
            rb.linearVelocity = new Vector2(-wallContactSide * wallJumpHorizForce, wallJumpForce);
            StartCoroutine(WallJumpLockCoroutine());
        }
        else
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        StopWallSlide();
        timeSinceDetached = 0f;
        jumpCount = 0;
    }

    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        wallSlideActive = false;
        autoClimbing = false;
        timeSinceDetached = 0f;
        rb.gravityScale = defaultGravityScale;
    }

    // --- Рывок (Dash)
    private IEnumerator Dash()
    {
        isDashing = true;
        canDash = false;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
        float dashDirection = (facingRight ? 1f : -1f);
        float duration = dashDistance / dashSpeed;
        float originalGravity = defaultGravityScale;
        rb.gravityScale = 0;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0);
        yield return new WaitForSeconds(duration);
        rb.gravityScale = originalGravity;
        yield return new WaitForSeconds(0.1f);
        if (!collisionController.IsGrounded)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * dashImpulseRetention, rb.linearVelocity.y);
        isDashing = false;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
        isInvulnerable = false;
    }

    // --- Подкат (Slide)
    private IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDirection = Mathf.Sign(moveInput);
        float initialVel = slideDirection * slideSpeed * slideBoost;
        rb.linearVelocity = new Vector2(initialVel, rb.linearVelocity.y);
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            if (!collisionController.IsGrounded)
                break;
            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.S))
                break;
            float currentX = Mathf.Lerp(initialVel, 0, elapsed / slideDuration);
            rb.linearVelocity = new Vector2(currentX, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }
        isSliding = false;
        if (collisionController.IsGrounded)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S))
                isCrouching = true;
        }
    }

    // --- Изменение направления (Flip)
    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
        collisionController.ResetWallContactBuffer();
    }

    // --- Блокировка управления после wall jump.
    private IEnumerator WallJumpLockCoroutine()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpLockDuration);
        isWallJumping = false;
    }

    // --- Механика залезания на край (Ledge Climb)
    private void TryStartLedgeClimb()
    {
        if (ledgeRayOrigin == null) return;
        RaycastHit2D hit = Physics2D.Raycast(ledgeRayOrigin.position, Vector2.down, ledgeRayLength, collisionController.groundLayer);
        if (hit.collider == null && collisionController.IsTouchingWall())
        {
            if (!isLedgeClimbing)
            {
                isLedgeClimbing = true;
                ledgeClimbStartPos = transform.position;
                wallContactSide = collisionController.GetLastWallContactSide();
                ledgeClimbTargetPos = ledgeClimbStartPos + new Vector2(wallContactSide * ledgeClimbHorizontalOffset, ledgeClimbVerticalDistance);
                rb.velocity = Vector2.zero;
                rb.gravityScale = 0;
                StartCoroutine(LedgeClimbRoutine());
            }
        }
    }

    private IEnumerator LedgeClimbRoutine()
    {
        float timer = 0f;
        while (timer < ledgeClimbDuration)
        {
            float t = timer / ledgeClimbDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            transform.position = Vector2.Lerp(ledgeClimbStartPos, ledgeClimbTargetPos, smoothT);
            timer += Time.deltaTime;
            yield return null;
        }
        transform.position = ledgeClimbTargetPos;
        isLedgeClimbing = false;
        rb.gravityScale = defaultGravityScale;
    }
}