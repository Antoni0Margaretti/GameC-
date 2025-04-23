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
    // Для управления в воздухе:
    public float airMaxSpeed = 2f;          // Целевая скорость в воздухе при наличии ввода (обычно гораздо ниже).
    public float airAcceleration = 5f;      // Базовое изменение скорости в воздухе (ед/с²).
    public float airDrag = 0.1f;            // Коэффициент сопротивления воздуха при отсутствии ввода.
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
    private bool isDashing = false;         // Блокирует управление во время рывка.
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
    public float wallHangTime = 0.5f;         // Время, которое персонаж висит неподвижно после цепления.
    public float wallSlideAcceleration = 10f; // Ускорение скольжения по стене (ед./сек).
    public float wallSlideMaxSpeed = 5f;      // Максимальная скорость скольжения.
    public float wallJumpForce = 10f;         // Вертикальная компонента wall jump.
    public float wallJumpHorizForce = 5f;     // Горизонтальная составляющая wall jump (фиксированная).
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;     // false – режим «висения», true – режим ускоренного скольжения.
    public float wallDetachCooldown = 0.3f;     // Минимальное время между цеплениями.
    private float timeSinceDetached = 0f;
    private int wallContactSide = 0;          // 1: если стена справа; -1: если слева.

    // --- Параметры для определения касания стены
    // Добавляем слой, в котором находятся стены
    public LayerMask wallLayer;

    // --- Блокировка управления после wall jump.
    private bool isWallJumping = false;
    public float wallJumpLockDuration = 0.2f;

    // --- Параметры гравитации при цеплении за стену.
    public float wallHangGravityScale = 0f;  // gravityScale при цеплении.
    private float originalGravityScale;

    // --- Флаг направления (куда смотрит персонаж).
    private bool facingRight = true;

    // --- Переменные хитбокса (для восстановления после приседа/подката).
    private Vector2 normalSize;
    private Vector2 normalOffset;

    // --- Переменная для хранения горизонтального ввода.
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

        // Поворот персонажа согласно направлению ввода.
        if (hInput > 0 && !facingRight)
            Flip();
        else if (hInput < 0 && facingRight)
            Flip();

        // Если персонаж цепляется за стену, но стена пропадает – прекращаем цепление.
        if (isSlidingOnWall && !touchingWall)
            StopWallSlide();

        // --- Прыжок
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                if ((wallContactSide == 1 && facingRight) || (wallContactSide == -1 && !facingRight))
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
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
        // Теперь для активации цепления проверяем, что персонаж полностью касается стены (через IsFullyTouchingWall).
        if (collisionController.IsTouchingWall && !grounded &&
            Mathf.Abs(hInput) > 0.01f &&
            ((facingRight && hInput > 0) || (!facingRight && hInput < 0)) &&
            timeSinceDetached >= wallDetachCooldown &&
            IsFullyTouchingWall()) // нововведение: проверка полного касания
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

        // --- Остальные механики (Dash, Slide, Crouch)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isSliding && !isCrouching)
        {
            if (isSlidingOnWall)
            {
                if ((wallContactSide == 1 && !facingRight) || (wallContactSide == -1 && facingRight))
                    StartCoroutine(Dash());
            }
            else
            {
                StartCoroutine(Dash());
            }
        }

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
                        // Если уже быстрее, сохраняем импульс.
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

    // --- Метод проверки полного касания стены.
    // Вычисляем две точки (верхнюю и нижнюю) на передней и задней сторонах коллайдера.
    // Если хотя бы на одной стороне обе точки совпадают со стеной (через OverlapPoint и заданный слой wallLayer), возвращаем true.
    private bool IsFullyTouchingWall()
    {
        Vector2 pos = (Vector2)transform.position;
        Vector2 frontTop, frontBottom, backTop, backBottom;
        // Если персонаж смотрит вправо:
        if (facingRight)
        {
            frontTop = pos + boxCollider.offset + new Vector2(boxCollider.size.x / 2f, boxCollider.size.y / 2f);
            frontBottom = pos + boxCollider.offset + new Vector2(boxCollider.size.x / 2f, -boxCollider.size.y / 2f);
            backTop = pos + boxCollider.offset + new Vector2(-boxCollider.size.x / 2f, boxCollider.size.y / 2f);
            backBottom = pos + boxCollider.offset + new Vector2(-boxCollider.size.x / 2f, -boxCollider.size.y / 2f);
        }
        else
        {
            frontTop = pos + boxCollider.offset + new Vector2(-boxCollider.size.x / 2f, boxCollider.size.y / 2f);
            frontBottom = pos + boxCollider.offset + new Vector2(-boxCollider.size.x / 2f, -boxCollider.size.y / 2f);
            backTop = pos + boxCollider.offset + new Vector2(boxCollider.size.x / 2f, boxCollider.size.y / 2f);
            backBottom = pos + boxCollider.offset + new Vector2(boxCollider.size.x / 2f, -boxCollider.size.y / 2f);
        }
        bool frontFull = Physics2D.OverlapPoint(frontTop, wallLayer) && Physics2D.OverlapPoint(frontBottom, wallLayer);
        bool backFull = Physics2D.OverlapPoint(backTop, wallLayer) && Physics2D.OverlapPoint(backBottom, wallLayer);
        return frontFull || backFull;
    }

    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            // Только если персонаж полностью касается стены, разрешаем цепление.
            if (!IsFullyTouchingWall())
                return;
            isSlidingOnWall = true;
            wallSlideActive = false; // Сначала персонаж просто висит.
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
        rb.gravityScale = originalGravityScale;
    }

    private IEnumerator Dash()
    {
        isDashing = true;
        canDash = false;
        // Сброс вертикальной составляющей для чистого горизонтального рывка.
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
        float dashDirection = (facingRight ? 1f : -1f);
        float duration = dashDistance / dashSpeed;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0);
        yield return new WaitForSeconds(duration);
        rb.gravityScale = originalGravity;
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

    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }

    private IEnumerator WallJumpLockCoroutine()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpLockDuration);
        isWallJumping = false;
    }
}