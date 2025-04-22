using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // --- Компоненты
    private CollisionController collisionController;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // --- Параметры движения
    public float speed = 5f;
    // Коэффициент дополнительного изменения скорости в воздухе;
    // значение 0 означает отсутствие ввода, 1 – такой же, как на земле.
    public float airControlFactor = 0.3f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- Параметры рывка (Dash)
    public float dashDistance = 5f;         // расстояние, которое должен пройти рывок
    public float dashSpeed = 20f;           // скорость рывка (независима от dashDistance)
    public float dashCooldown = 1f;
    private bool canDash = true;
    private bool isInvulnerable = false;
    // Флаг, блокирующий обычное обновление скорости во время рывка.
    private bool isDashing = false;

    // --- Параметры подката (Slide) и приседа (Crouch)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    public float slideBoost = 1.5f;         // коэффициент резкого увеличения скорости при подкате
    private bool isSliding = false;
    private bool isCrouching = false;

    // --- Параметр для горизонтального импульса прыжка (если не wall jump)
    // Дополнительный горизонтальный импульс = текущая горизонтальная скорость * jumpImpulseFactor.
    public float jumpImpulseFactor = 0.2f;

    // --- Параметры цепления за стену (Wall Hang / Slide)
    public float wallHangTime = 0.5f;         // время, которое персонаж висит неподвижно после цепления
    public float wallSlideAcceleration = 10f; // ускорение скольжения по стене (ед./сек)
    public float wallSlideMaxSpeed = 5f;      // максимальная скорость скольжения
    public float wallJumpForce = 10f;         // вертикальная компонента wall jump
    public float wallJumpHorizForce = 5f;     // горизонтальная составляющая wall jump (фиксированная)
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;     // false – режим "висения", true – режим ускоренного скольжения
    public float wallDetachCooldown = 0.3f;     // минимальное время между цеплениями
    private float timeSinceDetached = 0f;
    // Сторона стены, к которой цепляемся: 1 – если стена справа; -1 – если слева.
    private int wallContactSide = 0;

    // --- Блокировка горизонтального управления после wall jump,
    // позволяющая сохранить горизонтальный импульс, не затираемый обычным вводом.
    private bool isWallJumping = false;
    public float wallJumpLockDuration = 0.2f;

    // --- Параметры гравитации при цеплении за стену:
    public float wallHangGravityScale = 0f;  // gravityScale в режиме цепления
    private float originalGravityScale;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    // --- Переменные для хитбокса (для восстановления после приседа/подката)
    private Vector2 normalSize;
    private Vector2 normalOffset;

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
        timeSinceDetached += Time.deltaTime;
        float moveInput = Input.GetAxis("Horizontal");

        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        // Если персонаж цепляется за стену, но стена исчезает – прекращаем цепление.
        if (isSlidingOnWall && !touchingWall)
        {
            StopWallSlide();
        }

        // Обработка горизонтального движения:
        // Если не рывок, не цепляемся, не подкат, не приседаем, не wall jump.
        if (!isDashing && !isSlidingOnWall && !isSliding && !isCrouching && !isWallJumping)
        {
            if (grounded)
            {
                // На земле полное управление:
                rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);
                if (moveInput > 0 && !facingRight)
                    Flip();
                else if (moveInput < 0 && facingRight)
                    Flip();
            }
            else
            {
                // В воздухе не перезаписываем скорость, а добавляем небольшое изменение.
                if (Mathf.Abs(moveInput) > 0.01f)
                {
                    // Здесь вместо Lerp мы просто добавляем небольшое ускорение.
                    float airAccel = speed * airControlFactor;
                    float newX = rb.velocity.x + (moveInput * airAccel * Time.deltaTime);
                    // Можно, если нужно, ограничить максимальную скорость в воздухе:
                    // newX = Mathf.Clamp(newX, -speed, speed);
                    rb.velocity = new Vector2(newX, rb.velocity.y);

                    if (moveInput > 0 && !facingRight)
                        Flip();
                    else if (moveInput < 0 && facingRight)
                        Flip();
                }
            }
        }
        // Если персонаж цепляется за стену, ввод используется для смены направления.
        if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Прыжок
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                if ((wallContactSide == 1 && facingRight) || (wallContactSide == -1 && !facingRight))
                {
                    rb.velocity = new Vector2(rb.velocity.x, jumpForce);
                }
                else
                {
                    rb.velocity = new Vector2(-wallContactSide * wallJumpHorizForce, wallJumpForce);
                    StartCoroutine(WallJumpLockCoroutine());
                }
                StopWallSlide();
                timeSinceDetached = 0f;
                jumpCount = 0;
            }
            else
            {
                // При обычном прыжке добавляем горизонтальный импульс, пропорциональный текущей скорости.
                float extraX = rb.velocity.x * jumpImpulseFactor;
                rb.velocity = new Vector2(rb.velocity.x + extraX, jumpForce);
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
            Mathf.Abs(moveInput) > 0.01f &&
            ((facingRight && moveInput > 0) || (!facingRight && moveInput < 0)) &&
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
            float newY = Mathf.MoveTowards(rb.velocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.deltaTime);
            rb.velocity = new Vector2(rb.velocity.x, newY);
        }

        // --- Остальные механики (Dash, Slide, Crouch)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isSliding && !isCrouching)
        {
            // Если цепляемся за стену, рывок разрешён только если персонаж смотрит от стены.
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
        // Подкат (Slide) и присед (Crouch) – работают только на земле.
        if (grounded)
        {
            if (Input.GetKeyDown(KeyCode.LeftControl) && Mathf.Abs(moveInput) > 0.01f && !isSliding)
            {
                StartCoroutine(Slide(moveInput));
            }
            else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && Mathf.Abs(moveInput) < 0.01f)
            {
                isCrouching = true;
                rb.velocity = new Vector2(0, rb.velocity.y);
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

        // Если персонаж в воздухе, имеет ненулевую горизонтальную скорость 
        // и удерживается Ctrl – при приземлении автоматически запускаем подкат.
        if (grounded && Input.GetKey(KeyCode.LeftControl) && Mathf.Abs(rb.velocity.x) > 0.1f && !isSliding && !isCrouching)
        {
            StartCoroutine(Slide(rb.velocity.x));
        }

        if (!isSliding && !isCrouching)
        {
            boxCollider.size = normalSize;
            boxCollider.offset = normalOffset;
        }

        if (!isSlidingOnWall)
            collisionController.ignoreFlipForWallChecks = false;
    }

    // --- Методы цепления за стену (Wall Hang / Slide)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false;   // Сначала персонаж висит неподвижно.
            rb.velocity = Vector2.zero;
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
        float currentVertical = rb.velocity.y;
        if (collisionController.IsGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0);
            currentVertical = 0;
        }
        float dashDirection = (facingRight ? 1 : -1);
        float duration = dashDistance / dashSpeed;  // длительность рассчитывается как dashDistance / dashSpeed.
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;
        rb.velocity = new Vector2(dashDirection * dashSpeed, currentVertical);
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
        rb.velocity = new Vector2(initialVel, rb.velocity.y);
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            if (!collisionController.IsGrounded)
                break;
            // Если клавиши отпущены, прерываем подкат.
            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.S))
                break;
            float currentX = Mathf.Lerp(initialVel, 0, elapsed / slideDuration);
            rb.velocity = new Vector2(currentX, rb.velocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }
        isSliding = false;
        if (collisionController.IsGrounded)
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
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

    // --- Блокировка горизонтального управления после wall jump,
    // чтобы импульс сохранялся и не затирался обычным обновлением ввода.
    private IEnumerator WallJumpLockCoroutine()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpLockDuration);
        isWallJumping = false;
    }
}