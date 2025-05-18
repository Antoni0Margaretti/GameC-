using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // --- Компоненты
    private CollisionController collisionController;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private CombatController combatController;

    // --- Параметры движения
    [Header("Movement")]
    public float speed = 10f;
    public float airMaxSpeed = 2f;
    public float airAcceleration = 5f;
    public float airDrag = 0.1f;
    public float airControlInfluence = 0.2f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- Рывок (Dash)
    [Header("Dash")]
    public float dashDistance = 5f;
    public float dashSpeed = 20f;
    public float dashCooldown = 1f;
    private bool canDash = true;
    public bool isInvulnerable = false;
    private bool isDashing = false;
    public float dashAfterLockDuration = 0.2f;
    private bool isDashLocked = false;
    public float dashImpulseRetention = 0.2f;

    // --- Подкат и присед
    [Header("Slide & Crouch")]
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    public float slideBoost = 1.5f;
    private bool isSliding = false;
    private bool isCrouching = false;

    // --- Прыжок
    public float jumpImpulseFactor = 0.2f;

    // --- Цепление за стену
    [Header("Wall Hang / Slide")]
    public float wallHangTime = 0.5f;
    public float wallSlideAcceleration = 10f;
    public float wallSlideMaxSpeed = 5f;
    public float wallJumpForce = 10f;
    public float wallJumpHorizForce = 5f;
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;
    public float wallDetachCooldown = 0.3f;
    private float timeSinceDetached = 0f;
    [Header("Wall Jump (Auto Climb Boost)")]
    public float wallJumpForceAutoClimb = 14f;      // Усиленный вертикальный импульс
    public float wallJumpHorizForceAutoClimb = 8f;  // Усиленный горизонтальный импульс
    [Header("Wall Slide Deceleration")]
    public float wallGrabDecel = 30f;
    private int wallContactSide = 0;

    // --- Авто-подъём по стене
    [Header("Wall Auto Climb")]
    public float wallAutoClimbDistance = 0.5f;
    public float wallAutoClimbSpeed = 2f;
    private float initialGrabVerticalSpeed = 0f;
    private Coroutine autoClimbCoroutine = null;
    private bool autoClimbing = false;
    private float wallClimbStartY = 0f;

    [Header("Step Over Settings")]
    public float stepCheckDistance = 0.2f; // Дистанция проверки перед персонажем
    public float stepHeight = 0.3f;        // Максимальная высота "ступеньки"
    public float stepUpSpeed = 10f;        // Скорость подъёма на ступеньку

    // --- Гравитация
    public float defaultGravityScale;
    private float originalGravityScale;

    // --- Wall Jump Lock
    private bool isWallJumping = false;
    public float wallJumpLockDuration = 0.2f;

    // --- Wall Hang Gravity
    public float wallHangGravityScale = 0f;

    // --- Направление
    private bool facingRight = true;

    // --- Хитбокс
    private Vector2 normalSize;
    private Vector2 normalOffset;

    // --- Ввод
    private float hInput = 0f;

    // --- Ledge Climb
    [Header("Ledge Climb Settings")]
    public Transform ledgeRayOrigin;
    public float ledgeRayLength = 0.1f;
    public float ledgeClimbVerticalDistanceLower = 0.6f;
    public float ledgeClimbVerticalDistanceUpper = 1.0f;
    public float ledgeClimbHorizontalOffset = 0.3f;
    public float ledgeClimbDuration = 0.4f;
    public float ledgeProbeVerticalOffsetLower = 0f;
    public float ledgeProbeVerticalOffsetUpper = 0.2f;
    public float ledgeRayLengthLower = 0.1f;
    public float ledgeRayLengthUpper = 0.15f;
    private bool isLedgeClimbing = false;
    private Vector2 ledgeClimbStartPos;
    private Vector2 ledgeClimbTargetPos;
    public float ledgeProbeCenterVerticalOffset = 0f;
    public Vector2 ledgeProbeOffset;
    public Transform ledgeProbeCenter;
    public float ledgeProbeHorizontalDistance = 0.3f;

    public bool isAlive = true;

    void Start()
    {
        combatController = GetComponent<CombatController>();
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        originalGravityScale = rb.gravityScale;
        normalSize = boxCollider.size;
        normalOffset = boxCollider.offset;
    }

    void Update()
    {
        // Блокируем wall hang/ledge climb при атаке или парировании
        if (combatController != null && (combatController.IsParrying || combatController.IsAttacking))
        {
            if (isSlidingOnWall) StopWallSlide();
            if (isLedgeClimbing) StopLedgeClimb();
        }

        if (Time.timeScale == 0 && !Input.GetKeyDown(KeyCode.Escape))
            return;

        UpdateInput();
        UpdateHitboxState();
        UpdateFacing();
        UpdateWallDetach();
        TryStartLedgeClimb();
        DrawLedgeDebug();

        HandleJump();
        HandleWallHangAndSlide();
        HandleDash();
        HandleSlideAndCrouch();
        RestoreColliderSize();
        if (!isSlidingOnWall)
            collisionController.ignoreFlipForWallChecks = false;
        HandleStepOver();

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
                        return;
                    float targetX = hInput * airMaxSpeed;
                    float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetX, airAcceleration * Time.fixedDeltaTime * airControlInfluence);
                    rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
                }
                else
                {
                    float dragFactor = 1f - airDrag * Time.fixedDeltaTime;
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x * dragFactor, rb.linearVelocity.y);
                }
            }
        }
    }

    // --- Вспомогательные методы Update ---
    private void UpdateInput()
    {
        float rawH = Input.GetAxisRaw("Horizontal");
        hInput = rawH;
    }

    private void UpdateHitboxState()
    {
        if (isSliding)
            collisionController.currentHitboxState = CollisionController.HitboxState.Sliding;
        else if (isCrouching)
            collisionController.currentHitboxState = CollisionController.HitboxState.Crouching;
        else
            collisionController.currentHitboxState = CollisionController.HitboxState.Normal;
    }

    private void UpdateFacing()
    {
        int inputDir = hInput > 0.2f ? 1 : hInput < -0.2f ? -1 : 0;
        if (inputDir > 0 && !facingRight) Flip();
        else if (inputDir < 0 && facingRight) Flip();
    }

    private void UpdateWallDetach()
    {
        timeSinceDetached += Time.deltaTime;
        if (isSlidingOnWall && !collisionController.IsTouchingWall)
            StopWallSlide();
    }

    private void DrawLedgeDebug()
    {
        Debug.DrawRay(GetLedgeProbePointForLower(), Vector2.down * ledgeRayLengthLower, Color.green);
        Debug.DrawRay(GetLedgeProbePointForUpper(), Vector2.down * ledgeRayLengthUpper, Color.blue);
    }

    // --- Прыжок ---
    private void HandleJump()
    {
        bool grounded = collisionController.IsGrounded;
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
                    (Mathf.Sign(rb.linearVelocity.x) != Mathf.Sign(hInput)))
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
    }

    // --- Цепление за стену и скольжение ---
    private void HandleWallHangAndSlide()
    {
        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;
        int inputDir = hInput > 0.2f ? 1 : hInput < -0.2f ? -1 : 0;

        if (touchingWall && !grounded && timeSinceDetached >= wallDetachCooldown)
        {
            if (!isSlidingOnWall && inputDir == collisionController.GetLastWallContactSide())
                StartWallHang();
        }

        if (Input.GetKeyDown(KeyBindings.Crouch) && isSlidingOnWall)
            StopWallSlide();

        if (isSlidingOnWall && wallSlideActive && touchingWall)
        {
            float newY = Mathf.MoveTowards(rb.linearVelocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.deltaTime);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, newY);
        }
    }

    // --- Рывок (Dash) ---
    private void HandleDash()
    {
        if (Input.GetKeyDown(KeyBindings.Dash) && canDash && !isSliding && !isCrouching)
        {
            if (isSlidingOnWall)
            {
                if ((collisionController.GetLastWallContactSide() == 1 && !facingRight) ||
                    (collisionController.GetLastWallContactSide() == -1 && facingRight))
                    StartCoroutine(Dash());
            }
            else
            {
                StartCoroutine(Dash());
            }
        }
    }

    // --- Подкат и присед ---
    private void HandleSlideAndCrouch()
    {
        bool grounded = collisionController.IsGrounded;
        float threshold = 0.2f;
        if (grounded)
        {
            if (Input.GetKeyDown(KeyBindings.Slide) && Mathf.Abs(hInput) >= threshold && !isSliding)
                StartCoroutine(Slide(hInput));
            else if ((Input.GetKey(KeyBindings.Slide) || Input.GetKey(KeyBindings.Crouch)) && Mathf.Abs(hInput) < threshold)
            {
                isCrouching = true;
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            else if (!Input.GetKey(KeyBindings.Slide) && !Input.GetKey(KeyBindings.Crouch))
                isCrouching = false;
        }
        else
        {
            isCrouching = false;
        }

        if (grounded && Input.GetKey(KeyBindings.Slide) && Mathf.Abs(rb.linearVelocity.x) > 0.1f && !isSliding && !isCrouching)
            StartCoroutine(Slide(rb.linearVelocity.x));
    }

    // --- Восстановление размера коллайдера ---
    private void RestoreColliderSize()
    {
        if (!isSliding && !isCrouching)
        {
            boxCollider.size = normalSize;
            boxCollider.offset = normalOffset;
        }
    }

    // --- Wall Hang / Slide ---
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false;
            initialGrabVerticalSpeed = rb.linearVelocity.y;
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
            else if (initialGrabVerticalSpeed < 0)
            {
                StartCoroutine(WallGrabDecelerateCoroutine(initialGrabVerticalSpeed));
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
                StartCoroutine(WallHangCoroutine());
            }
        }
    }

    private IEnumerator WallGrabDecelerateCoroutine(float startSpeed)
    {
        float v = startSpeed;
        while (v < 0)
        {
            float decel = wallGrabDecel * Time.deltaTime;
            v = Mathf.Min(v + decel, 0f);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, v);
            if (!collisionController.IsTouchingWall)
                yield break;
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;
        StartCoroutine(WallHangCoroutine());
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
            yield return null;
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

        // Определяем, используем ли усиленный wall jump (во время авто-взбирания)
        bool boosted = autoClimbing;

        float vert = boosted ? wallJumpForceAutoClimb : wallJumpForce;
        float horiz = boosted ? wallJumpHorizForceAutoClimb : wallJumpHorizForce;

        if (Mathf.Abs(hInput) > 0.01f && Mathf.Sign(hInput) == -wallContactSide)
        {
            rb.linearVelocity = new Vector2(-wallContactSide * horiz, vert);
            StartCoroutine(WallJumpLockCoroutine());
        }
        else
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, vert);
        }

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

    // --- Dash ---
    private IEnumerator Dash()
    {
        isInvulnerable = true;
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

    // --- Slide ---
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

    // --- Ledge Climb ---
    private Vector2 GetBaseCenter()
    {
        Vector2 baseCenter = (ledgeProbeCenter != null)
            ? ledgeProbeCenter.position
            : boxCollider.bounds.center;
        baseCenter.y += ledgeProbeCenterVerticalOffset;
        return baseCenter;
    }

    private Vector2 GetLedgeProbePointForLower()
    {
        Vector2 baseCenter = GetBaseCenter();
        int side = collisionController.GetLastWallContactSide();
        if (side == 0)
            side = (facingRight ? 1 : -1);
        return baseCenter + new Vector2(side * ledgeProbeHorizontalDistance, ledgeProbeVerticalOffsetLower);
    }

    private Vector2 GetLedgeProbePointForUpper()
    {
        Vector2 baseCenter = GetBaseCenter();
        int side = collisionController.GetLastWallContactSide();
        if (side == 0)
            side = (facingRight ? 1 : -1);
        return baseCenter + new Vector2(side * ledgeProbeHorizontalDistance, ledgeProbeVerticalOffsetUpper);
    }

    private enum LedgeType { None, Lower, Upper }

    private LedgeType GetLedgeType()
    {
        Vector2 lowerProbe = GetLedgeProbePointForLower();
        Vector2 upperProbe = GetLedgeProbePointForUpper();
        RaycastHit2D hitLower = Physics2D.Raycast(lowerProbe, Vector2.down, ledgeRayLengthLower, collisionController.groundLayer);
        RaycastHit2D hitUpper = Physics2D.Raycast(upperProbe, Vector2.down, ledgeRayLengthUpper, collisionController.groundLayer);
        bool lowerDetected = (hitLower.collider != null);
        bool upperDetected = (hitUpper.collider != null);
        if (lowerDetected && !upperDetected)
            return LedgeType.Lower;
        else if (!lowerDetected && upperDetected)
            return LedgeType.Upper;
        else if (lowerDetected && upperDetected)
            return LedgeType.Lower;
        else
            return LedgeType.None;
    }

    private void TryStartLedgeClimb()
    {
        if (!isLedgeClimbing)
        {
            LedgeType ledgeType = GetLedgeType();
            if (ledgeType != LedgeType.None)
            {
                isLedgeClimbing = true;
                ledgeClimbStartPos = transform.position;
                int side = collisionController.GetLastWallContactSide();
                if (side == 0)
                    side = (facingRight ? 1 : -1);
                float usedVerticalDistance = (ledgeType == LedgeType.Upper) ? ledgeClimbVerticalDistanceUpper : ledgeClimbVerticalDistanceLower;
                ledgeClimbTargetPos = ledgeClimbStartPos + new Vector2(side * ledgeClimbHorizontalOffset, usedVerticalDistance);
                rb.linearVelocity = Vector2.zero;
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
            float t = Mathf.SmoothStep(0f, 1f, timer / ledgeClimbDuration);
            transform.position = Vector2.Lerp(ledgeClimbStartPos, ledgeClimbTargetPos, t);
            timer += Time.deltaTime;
            yield return null;
        }
        transform.position = ledgeClimbTargetPos;
        isLedgeClimbing = false;
        rb.gravityScale = defaultGravityScale;
    }

    public void ForceDetachFromWall()
    {
        if (isSlidingOnWall)
            StopWallSlide();
        if (isLedgeClimbing)
            StopLedgeClimb();
    }

    private void StopLedgeClimb()
    {
        isLedgeClimbing = false;
        rb.gravityScale = defaultGravityScale;
    }

    private void HandleStepOver()
    {
        if (!collisionController.IsGrounded || isSliding || isCrouching || isDashing)
            return;

        // Определяем направление движения
        int moveDir = hInput > 0.1f ? 1 : hInput < -0.1f ? -1 : 0;
        if (moveDir == 0) return;

        // Точка для нижнего луча (на уровне ног)
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.05f;
        Vector2 dir = Vector2.right * moveDir;

        // Проверяем, есть ли препятствие на уровне ног
        RaycastHit2D hitLow = Physics2D.Raycast(origin, dir, stepCheckDistance, collisionController.groundLayer);
        if (hitLow.collider == null) return;

        // Точка для верхнего луча (на высоте ступеньки)
        Vector2 stepOrigin = origin + Vector2.up * stepHeight;
        RaycastHit2D hitHigh = Physics2D.Raycast(stepOrigin, dir, stepCheckDistance, collisionController.groundLayer);

        // Если сверху свободно — поднимаем персонажа
        if (hitHigh.collider == null)
        {
            // Плавно поднимаем персонажа на высоту ступеньки
            transform.position += Vector3.up * stepUpSpeed * Time.deltaTime;
        }
    }


    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
        collisionController.ResetWallContactBuffer();
    }

    private IEnumerator WallJumpLockCoroutine()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpLockDuration);
        isWallJumping = false;
    }

    public void Die()
    {
        // Корректная обработка смерти через PlayerDeath
        var death = GetComponent<PlayerDeath>();
        if (death != null)
            death.Die();
        else
            Debug.Log("Игрок погиб!");
    }
}