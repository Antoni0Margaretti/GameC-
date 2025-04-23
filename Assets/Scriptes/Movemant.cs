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
    public float airMaxSpeed = 2f;          // Целевая скорость в воздухе при наличии ввода.
    public float airAcceleration = 5f;      // Изменение скорости в воздухе (ед/с²).
    public float airDrag = 0.1f;            // Сопротивление воздуха при отсутствии ввода.
    public float airControlInfluence = 0.2f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- Параметры рывка (Dash)
    public float dashDistance = 5f;         // Расстояние, которое должен пройти рывок.
    public float dashSpeed = 20f;           // Скорость рывка.
    public float dashCooldown = 1f;
    private bool canDash = true;
    private bool isInvulnerable = false;
    private bool isDashing = false;         // Флаг состояния рывка.
    public float dashAfterLockDuration = 0.2f;
    private bool isDashLocked = false;

    // --- Параметры подката (Slide) и приседа (Crouch)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    public float slideBoost = 1.5f;         // Коэффициент увеличения скорости при подкате.
    private bool isSliding = false;
    private bool isCrouching = false;

    // --- Дополнительный горизонтальный импульс прыжка (если не wall jump)
    public float jumpImpulseFactor = 0.2f;

    // --- Параметры цепления за стену (Wall Hang / Slide)
    public float wallHangTime = 0.5f;         // Время, которое персонаж висит после цепления.
    public float wallSlideAcceleration = 10f; // Ускорение скольжения по стене (ед./сек).
    public float wallSlideMaxSpeed = 5f;      // Максимальная скорость скольжения.
    public float wallJumpForce = 10f;         // Вертикальная компонента wall jump.
    public float wallJumpHorizForce = 5f;     // Горизонтальная составляющая wall jump (фиксированная).
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;     // false – режим «висения», true – режим ускоренного скольжения.
    public float wallDetachCooldown = 0.3f;     // Минимальное время между цеплениями.
    private float timeSinceDetached = 0f;
    private int wallContactSide = 0;          // 1, если стена справа; -1, если слева.

    // --- Блокировка управления после wall jump.
    private bool isWallJumping = false;
    public float wallJumpLockDuration = 0.2f;

    // --- Глобальная стандартная гравитация.
    public float defaultGravityScale;       // Если не задано в Inspector, инициализируется в Start.

    // --- Параметры для залезания на край стены (ledge climb)
    public float ledgeCheckDistance = 0.5f;     // Расстояние от верхней точки стены для проверки наличия пола.
    public Vector2 ledgeClimbOffset = new Vector2(0.5f, 1.0f); // Смещение, на которое переместится персонаж при подъёме.

    // --- Остальные параметры
    private bool facingRight = true;          // Направление взгляда персонажа.
    private Vector2 normalSize;               // Исходный размер BoxCollider2D.
    private Vector2 normalOffset;             // Исходное смещение BoxCollider2D.
    private float hInput = 0f;                // Горизонтальный ввод.

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        // Инициализируем стандартную гравитацию, если не задана
        if (defaultGravityScale == 0)
            defaultGravityScale = rb.gravityScale;

        normalSize = boxCollider.size;
        normalOffset = boxCollider.offset;
    }

    void Update()
    {
        hInput = Input.GetAxis("Horizontal");
        timeSinceDetached += Time.deltaTime;
        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        // Поворот персонажа
        if (hInput > 0 && !facingRight)
            Flip();
        else if (hInput < 0 && facingRight)
            Flip();

        if (isSlidingOnWall && !touchingWall)
            StopWallSlide();

        // Обработка прыжка
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                // Если персонаж цепляется за стену, проверяем, находится ли он на краю стены,
                // то есть, над ним нет пола (ledge climb).
                if (IsAtLedge())
                {
                    StartCoroutine(LedgeClimb());
                }
                else
                {
                    // Стандартное поведение wall jump
                    if ((wallContactSide == 1 && facingRight) || (wallContactSide == -1 && !facingRight))
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                    else
                    {
                        rb.linearVelocity = new Vector2(-wallContactSide * wallJumpHorizForce, wallJumpForce);
                        StartCoroutine(WallJumpLockCoroutine());
                    }
                }
                StopWallSlide();
                timeSinceDetached = 0f;
                jumpCount = 0;
            }
            else
            {
                // Обычный прыжок
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

        // Проверка цепления за стену
        if (collisionController.IsTouchingWall && !grounded &&
            Mathf.Abs(hInput) > 0.01f &&
            ((facingRight && hInput > 0) || (!facingRight && hInput < 0)) &&
            timeSinceDetached >= wallDetachCooldown)
        {
            StartWallHang();
        }
        if (Input.GetKeyDown(KeyCode.S) && isSlidingOnWall)
            StopWallSlide();
        if (isSlidingOnWall && wallSlideActive && touchingWall)
        {
            float newY = Mathf.MoveTowards(rb.linearVelocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.deltaTime);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, newY);
        }

        // Dash
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isSliding && !isCrouching)
        {
            if (isSlidingOnWall)
            {
                // Здесь можно добавить проверку на направление, если нужно
                StartCoroutine(Dash());
            }
            else
            {
                StartCoroutine(Dash());
            }
        }

        // Подкат и приседание
        if (grounded)
        {
            if (Input.GetKeyDown(KeyCode.LeftControl) && Mathf.Abs(hInput) > 0.01f && !isSliding)
                StartCoroutine(Slide(hInput));
            else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && Mathf.Abs(hInput) < 0.01f)
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
                    if (Mathf.Sign(rb.linearVelocity.x) == Mathf.Sign(hInput) &&
                        Mathf.Abs(rb.linearVelocity.x) > Mathf.Abs(hInput * airMaxSpeed))
                    {
                        // Сохраняем накопленный импульс.
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

    // --- Методы цепления за стену
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            // Прицепление происходит только если персонаж полностью касается стены (определяется в CollisionController).
            isSlidingOnWall = true;
            wallSlideActive = false; // Вначале персонаж висит неподвижно.
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
            wallSlideActive = true;
    }

    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        wallSlideActive = false;
        timeSinceDetached = 0f;
        rb.gravityScale = defaultGravityScale;
    }

    // --- Dash
    private IEnumerator Dash()
    {
        isDashing = true;
        canDash = false;
        // Сбрасываем вертикальную скорость для чистого горизонтального рывка.
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
        float dashDirection = (facingRight ? 1f : -1f);
        float duration = dashDistance / dashSpeed;
        rb.gravityScale = 0;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0);
        yield return new WaitForSeconds(duration);
        rb.gravityScale = defaultGravityScale;
        yield return new WaitForSeconds(0.1f);
        isDashing = false;
        canDash = true;
        isInvulnerable = false;
        StartCoroutine(DashAfterLockCoroutine());
    }

    private IEnumerator DashAfterLockCoroutine()
    {
        isDashLocked = true;
        yield return new WaitForSeconds(dashAfterLockDuration);
        isDashLocked = false;
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

    // --- Ledge Climb (залезание на край стены)
    private bool IsAtLedge()
    {
        // Определяем точку (верхнюю переднюю) для определения наличия пола над стеной.
        Vector2 pos = (Vector2)transform.position + boxCollider.offset;
        Vector2 halfSize = boxCollider.size * 0.5f;
        Vector2 rayOrigin;
        if (facingRight)
        {
            rayOrigin = pos + new Vector2(halfSize.x, halfSize.y);
        }
        else
        {
            rayOrigin = pos + new Vector2(-halfSize.x, halfSize.y);
        }
        // Отправляем луч вверх.
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up, ledgeCheckDistance, collisionController.groundLayer);
        return hit.collider == null;
    }

    private IEnumerator LedgeClimb()
    {
        // Отключаем внешние воздействия.
        rb.linearVelocity = Vector2.zero;
        // Можно запустить анимацию залезания, если требуется.
        yield return new WaitForSeconds(0.2f);
        // Вычисляем смещение: по оси X зависит от направления, по оси Y — подъём.
        float dir = facingRight ? 1f : -1f;
        Vector2 climbOffset = new Vector2(ledgeClimbOffset.x * dir, ledgeClimbOffset.y);
        Vector2 newPos = (Vector2)transform.position + climbOffset;
        transform.position = newPos;
        yield return new WaitForSeconds(0.1f);
        // Завершаем процесс залезания – сбрасываем состояние цепления.
        isSlidingOnWall = false;
        wallSlideActive = false;
        jumpCount = 0;
    }

    // --- Изменение направления (Flip)
    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }

    // --- Блокировка управления после wall jump.
    private IEnumerator WallJumpLockCoroutine()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpLockDuration);
        isWallJumping = false;
    }
}