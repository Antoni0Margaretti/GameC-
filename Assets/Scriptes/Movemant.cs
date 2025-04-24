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
                                            // Новый множитель влияния ввода – чем меньше airControlInfluence, тем меньше ввод изменяет скорость.
    public float airDrag = 0.1f;
    public float airControlInfluence = 0.2f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- Параметры рывка (Dash)
    public float dashDistance = 5f;         // Расстояние, которое должен пройти рывок.
    public float dashSpeed = 20f;           // Скорость рывка (задаётся независимо).
    public float dashCooldown = 1f;
    private bool canDash = true;
    private bool isInvulnerable = false;
    private bool isDashing = false;         // Блокирует управление во время рывка.
    public float dashAfterLockDuration = 0.2f;
    private bool isDashLocked = false;
    public float dashImpulseRetention = 0.2f; // Доля импульса, сохраняемая после завершения рывка в воздухе.

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
    // Сторона стены, к которой цепляемся: 1 – если справа; -1 – если слева.
    private int wallContactSide = 0;

    // Новые публичные переменные для настройки автоматического подъёма:
    public float wallAutoClimbDistance = 0.5f; // Расстояние подъёма (единиц)
    public float wallAutoClimbSpeed = 2f;      // Скорость подъёма (ед/с)

    // Новые приватные переменные для состояния автоматического подъёма:
    private bool autoClimbing = false;
    private float wallClimbStartY = 0f;

    // --- Глобальная переменная стандартной гравитации.
    public float defaultGravityScale;     // Задаётся через Inspector или инициализируется в Start.

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

    // --- Переменная для хранения горизонтального ввода (обновляется в Update).
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
        // Используем GetAxisRaw для мгновенного ввода по горизонтали.
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

        // Обновляем состояние динамического хитбокса через CollisionController.
        if (isSliding)
            collisionController.currentHitboxState = CollisionController.HitboxState.Sliding;
        else if (isCrouching)
            collisionController.currentHitboxState = CollisionController.HitboxState.Crouching;
        else
            collisionController.currentHitboxState = CollisionController.HitboxState.Normal;

        // Поворот персонажа согласно направлению ввода.
        if (inputDir > 0 && !facingRight)
            Flip();
        else if (inputDir < 0 && facingRight)
            Flip();

        // Если персонаж цепляется за стену, но уже не касается её – прекращаем цепление.
        if (isSlidingOnWall && !touchingWall)
            StopWallSlide();

        // --- Прыжок
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                // Для wall jump: если зажата клавиша движения от стены (в противоположную сторону от касания) –
                // выполняем wall jump; иначе выполняем обычный вертикальный прыжок.
                if (inputDir != 0 && inputDir == -collisionController.GetLastWallContactSide())
                {
                    rb.linearVelocity = new Vector2(-collisionController.GetLastWallContactSide() * wallJumpHorizForce, wallJumpForce);
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
            else
            {
                // Обычный прыжок
                if (!grounded && Mathf.Abs(rawH) >= threshold && rb.linearVelocity.x != 0 &&
                    (Mathf.Sign(rb.linearVelocity.x) != Mathf.Sign(rawH)))
                {
                    rb.linearVelocity = new Vector2(rawH * airMaxSpeed, jumpForce);
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

        // --- Цепление за стеной (Wall Hang) и автоматический подъём (Auto Climb)
        // Если персонаж касается стены, не на земле, прошло нужное время отсоединения
        // и игрок зажимает клавишу движения в ту же сторону, что и стена.
        if (collisionController.IsTouchingWall && !grounded && timeSinceDetached >= wallDetachCooldown)
        {
            if (!isSlidingOnWall)
            {
                // Если цепления ещё не было, и направление ввода совпадает с касанием – начинаем цепление.
                if (inputDir == collisionController.GetLastWallContactSide())
                {
                    StartWallHang();
                }
            }
            else
            {
                // Если персонаж уже в режиме цепления...
                // Если автоматический подъём запущен, то не переопределяем вертикальную скорость – корутина это делает.
                if (!autoClimbing)
                {
                    // Здесь можно сохранить возможность регулирования вертикальной скорости вручную,
                    // но по вашему условию требуется автоматический подъём.
                    // Поэтому, если player не нажимает кнопку вверх, остается висение/скольжение вниз.
                    // (В нашем варианте автоматический подъем уже запущен в StartWallHang.)
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
            {
                StartCoroutine(Dash());
            }
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
        {
            isCrouching = false;
        }

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



    // Обработка физики в FixedUpdate.
    // Если ввода отсутствует, горизонтальная скорость остаётся неизменной (сохраняется весь импульс).
    // Если же ввод есть — изменяем скорость только если он направлен противоположно или если её недостаточно.
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
                    // Если текущее направление совпадает с вводом и скорость уже больше целевой, не изменяем ее:
                    if (Mathf.Sign(rb.linearVelocity.x) == Mathf.Sign(hInput) && Mathf.Abs(rb.linearVelocity.x) > Mathf.Abs(hInput * airMaxSpeed))
                    {
                        // Не меняем, просто сохраняем импульс.
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
                    // Если ввод отсутствует, применяем небольшое замедление горизонтальной скорости.
                    float dragFactor = 1f - airDrag * Time.fixedDeltaTime;
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x * dragFactor, rb.linearVelocity.y);
                }
            }
        }
    }

    // --- Методы цепления за стену (Wall Hang / Slide
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false; // Сначала персонаж просто цепляется.
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0; // Гравитацию отключаем во время цепления.
            jumpCount = 0;
            // Фиксируем сторону контакта со стеной из CollisionController.
            wallContactSide = collisionController.GetLastWallContactSide();

            // Запоминаем начальную позицию по Y и запускаем автоматический подъем.
            wallClimbStartY = transform.position.y;
            autoClimbing = true;
            StartCoroutine(AutoClimbCoroutine());

            StartCoroutine(WallHangCoroutine());
        }
    }

    // Корутина автоматического подъёма:
    private IEnumerator AutoClimbCoroutine()
    {
        // Пока автоподъем включён и персонаж не поднялся на заданное расстояние:
        while (autoClimbing && transform.position.y < wallClimbStartY + wallAutoClimbDistance)
        {
            // Устанавливаем постоянную скорость подъёма.
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, wallAutoClimbSpeed);
            yield return null;
        }
        // Как только дистанция достигнута или автоподъем принудительно остановлен:
        autoClimbing = false;
        // Сбрасываем вертикальную скорость, чтобы персонаж начал висеть.
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
    }

    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
            wallSlideActive = true;
    }
    // Метод StopWallSlide сбрасывает и режим автоматического подъёма:
    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        wallSlideActive = false;
        autoClimbing = false;
        timeSinceDetached = 0f;
        rb.gravityScale = defaultGravityScale;
    }


    // --- Рывок (Dash) – одинаковое поведение на земле и в воздухе.

    private IEnumerator Dash()
    {
        isDashing = true;
        canDash = false;

        // Сбрасываем вертикальную составляющую, чтобы рывок был чисто горизонтальным.
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);

        float dashDirection = (facingRight ? 1f : -1f);
        float duration = dashDistance / dashSpeed;  // Длительность dash = расстояние / скорость.
        float originalGravity = defaultGravityScale;

        // Отключаем гравитацию на время dash.
        rb.gravityScale = 0;

        // Устанавливаем фиксированную горизонтальную скорость dash.
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0);

        yield return new WaitForSeconds(duration);

        // Восстанавливаем гравитацию.
        rb.gravityScale = originalGravity;
        yield return new WaitForSeconds(0.1f);

        // Если персонаж находится в воздухе, уменьшаем горизонтальную скорость до некоторой доли рывкового импульса.
        if (!collisionController.IsGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * dashImpulseRetention, rb.linearVelocity.y);
        }

        isDashing = false;
        canDash = true;
        isInvulnerable = false;
    }



    // --- Подкат (Slide) – выполняется, пока удерживается клавиша Ctrl (или S).
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
        // Сброс буфера контакта, чтобы старые данные не влияли.
        collisionController.ResetWallContactBuffer();
    }

    // --- Блокировка управления после wall jump.
    private IEnumerator WallJumpLockCoroutine()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpLockDuration);
        isWallJumping = false;
    }
}