using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // --- Компоненты
    private CollisionController collisionController;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // --- Параметры движения
    public float speed = 10f;  // максимальная скорость на земле
    // airMaxSpeed – целевая скорость в воздухе (обычно значительно ниже, например, 2 ед/с)
    public float airMaxSpeed = 2f;
    // airAcceleration – максимальное изменение горизонтальной скорости в воздухе (ед/с^2)
    public float airAcceleration = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- Параметры рывка (Dash)
    public float dashDistance = 5f;         // расстояние, которое нужно пройти рывком
    public float dashSpeed = 20f;           // скорость рывка (задается независимо)
    public float dashCooldown = 1f;
    private bool canDash = true;
    private bool isInvulnerable = false;
    private bool isDashing = false;         // блокирует управление во время рывка

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
    // Сторона стены: 1, если стена справа; -1, если слева.
    private int wallContactSide = 0;

    // --- Блокировка управления после wall jump
    private bool isWallJumping = false;
    public float wallJumpLockDuration = 0.2f;

    // --- Параметры гравитации при цеплении за стену
    public float wallHangGravityScale = 0f;
    private float originalGravityScale;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    // --- Переменные для хитбокса (восстановление после подката/приседа)
    private Vector2 normalSize;
    private Vector2 normalOffset;

    // --- Переменная для хранения горизонтального ввода (считывается в Update)
    private float hInput = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        originalGravityScale = rb.gravityScale;
        normalSize = boxCollider.size;
        normalOffset = boxCollider.offset;
    }

    void Update()
    {
        hInput = Input.GetAxis("Horizontal");
        timeSinceDetached += Time.deltaTime;
        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        // Поворот персонажа согласно направлению ввода
        if (hInput > 0 && !facingRight)
            Flip();
        else if (hInput < 0 && facingRight)
            Flip();

        // Если персонаж цепляется за стену, но стена пропадает, прекращаем цепление.
        if (isSlidingOnWall && !touchingWall)
        {
            StopWallSlide();
        }

        // --- Прыжок
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                if ((wallContactSide == 1 && facingRight) || (wallContactSide == -1 && !facingRight))
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                }
                else
                {
                    rb.linearVelocity = new Vector2(-wallContactSide * wallJumpHorizForce, wallJumpForce);
                    StartCoroutine(WallJumpLockCoroutine());
                }
                StopWallSlide();
                timeSinceDetached = 0f;
                jumpCount = 0;
            }
            else
            {
                float extraX = rb.linearVelocity.x * jumpImpulseFactor;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x + extraX, jumpForce);
                jumpCount++;
            }
        }

        if (grounded)
        {
            jumpCount = 0;
            StopWallSlide();
        }

        // --- Цепление за стеной (Wall Hang)
        if (collisionController.IsTouchingWall && !grounded &&
            Mathf.Abs(hInput) > 0.01f &&
            ((facingRight && hInput > 0) || (!facingRight && hInput < 0)) &&
            timeSinceDetached >= wallDetachCooldown)
        {
            StartWallHang();
        }
        if (Input.GetKeyDown(KeyCode.S) && isSlidingOnWall)
        {
            StopWallSlide();
        }
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
                if ((wallContactSide == 1 && !facingRight) || (wallContactSide == -1 && facingRight))
                {
                    StartCoroutine(Dash());
                }
            }
            else
            {
                StartCoroutine(Dash());
            }
        }

        // Подкат (Slide) и присед (Crouch) работают только на земле.
        if (grounded)
        {
            if (Input.GetKeyDown(KeyCode.LeftControl) && Mathf.Abs(hInput) > 0.01f && !isSliding)
            {
                StartCoroutine(Slide(hInput));
            }
            else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && Mathf.Abs(hInput) < 0.01f)
            {
                isCrouching = true;
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.S))
            {
                isCrouching = false;
            }
        }
        else
        {
            isCrouching = false;
        }

        // Если в воздухе и удерживается Ctrl (для плавного подката при приземлении)
        if (grounded && Input.GetKey(KeyCode.LeftControl) && Mathf.Abs(rb.linearVelocity.x) > 0.1f && !isSliding && !isCrouching)
        {
            StartCoroutine(Slide(rb.linearVelocity.x));
        }

        if (!isSliding && !isCrouching)
        {
            boxCollider.size = normalSize;
            boxCollider.offset = normalOffset;
        }

        if (!isSlidingOnWall)
            collisionController.ignoreFlipForWallChecks = false;
    }

    // Обработка физики перемещения – теперь в FixedUpdate для управления силой в воздухе
    void FixedUpdate()
    {
        bool grounded = collisionController.IsGrounded;
        if (!isDashing && !isSlidingOnWall && !isSliding && !isCrouching && !isWallJumping)
        {
            if (grounded)
            {
                rb.linearVelocity = new Vector2(hInput * speed, rb.linearVelocity.y);
            }
            else
            {
                // В воздухе целевая скорость задается как hInput * airMaxSpeed,
                // а скорость изменяется постепенно с максимальным приростом airAcceleration.
                float targetX = hInput * airMaxSpeed;
                float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetX, airAcceleration * Time.fixedDeltaTime);
                rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
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
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = wallHangGravityScale;
            jumpCount = 0;
            wallContactSide = facingRight ? 1 : -1;
            StartCoroutine(WallHangCoroutine());
        }
    }

    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
        {
            wallSlideActive = true;
        }
    }

    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        wallSlideActive = false;
        timeSinceDetached = 0f;
        rb.gravityScale = originalGravityScale;
    }

    // --- Рывок (Dash)
    private IEnumerator Dash()
    {
        isDashing = true;
        canDash = false;
        float currentVertical = rb.linearVelocity.y;
        if (collisionController.IsGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
            currentVertical = 0;
        }
        float dashDirection = (facingRight ? 1 : -1);
        float duration = dashDistance / dashSpeed;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, currentVertical);
        yield return new WaitForSeconds(duration);
        rb.gravityScale = originalGravity;
        yield return new WaitForSeconds(0.1f);
        isDashing = false;
        canDash = true;
        isInvulnerable = false;
    }

    // --- Подкат (Slide)
    // Подкат выполняется только пока удерживается клавиша Ctrl (или S).
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
    }

    // --- Блокировка горизонтального управления после wall jump.
    private IEnumerator WallJumpLockCoroutine()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpLockDuration);
        isWallJumping = false;
    }
}