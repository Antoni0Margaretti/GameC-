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

    // В начале класса (объявления публичных и приватных переменных добавьте новые поля):
    public float wallClimbDuration = 0.5f; // Время подъёма
    public float wallClimbSpeed = 2f;      // Скорость подъёма (ед/с)
    private bool isWallClimbing = false;   // Флаг фазы подъёма
    private float wallClimbStartTime = 0f;

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
        // Получаем ввод по горизонтали через GetAxisRaw для мгновенной реакции.
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

        // Обновляем состояние динамического хитбокса (оставляем без изменений)
        if (isSliding)
            collisionController.currentHitboxState = CollisionController.HitboxState.Sliding;
        else if (isCrouching)
            collisionController.currentHitboxState = CollisionController.HitboxState.Crouching;
        else
            collisionController.currentHitboxState = CollisionController.HitboxState.Normal;

        // Смена направления (Flip)
        if (inputDir > 0 && !facingRight)
            Flip();
        else if (inputDir < 0 && facingRight)
            Flip();

        // Если цеплялись, но теперь стены нет – сбрасываем цепление.
        if (isSlidingOnWall && !touchingWall)
            StopWallSlide();

        // --- Прыжок
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                // Wall jump: требуем, чтобы ввод был противоположным от стороны стены.
                if (inputDir != 0 && inputDir == -collisionController.GetLastWallContactSide())
                {
                    rb.linearVelocity = new Vector2(-collisionController.GetLastWallContactSide() * wallJumpHorizForce, wallJumpForce);
                    StartCoroutine(WallJumpLockCoroutine());
                }
                else
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);

                StopWallSlide();
                timeSinceDetached = 0f;
                jumpCount = 0;
            }
            else
            {
                // Обычный прыжок (оставляем примерно как было)
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

        // --- Цепление за стеной (Wall Hang / Climb)
        // Для активации цепления: персонаж касается стены, не на земле, прошло достаточно времени с отсоединения
        // и игрок зажимает кнопку движения в сторону стены (inputDir == saved wall side).
        if (collisionController.IsTouchingWall && !grounded &&
            timeSinceDetached >= wallDetachCooldown &&
            inputDir == collisionController.GetLastWallContactSide())
        {
            // Если еще не цепляемся, запускаем режим цепления.
            StartWallHang();
        }

        // Если персонаж цепляется за стену:
        if (isSlidingOnWall)
        {
            // Фаза подъёма: определяем, нажата ли кнопка вверх.
            float rawV = Input.GetAxisRaw("Vertical");
            // Если игрок нажимает вверх, инициируем climb, если ещё не начат или продолжаем.
            if (!isWallClimbing && rawV >= 0.2f)
            {
                // Запускаем climb:
                isWallClimbing = true;
                wallClimbStartTime = Time.time;
            }
            if (isWallClimbing)
            {
                // Если игрок продолжает нажимать вверх и время подъёма не превышено, устанавливаем постоянную скорость подъёма.
                if (rawV >= 0.2f && (Time.time - wallClimbStartTime) < wallClimbDuration)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, wallClimbSpeed);
                }
                else
                {
                    // Если время подъёма истек или игрок перестал нажимать вверх, прекращаем climb.
                    isWallClimbing = false;
                    // Переходим в режим "виса": вертикальная скорость сбрасывается в 0.
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
                }
            }
            else
            {
                // Если не в режиме climb, то выполняется стандартное скольжение/висение.
                if (wallSlideActive && touchingWall)
                {
                    float newY = Mathf.MoveTowards(rb.linearVelocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.deltaTime);
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, newY);
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.S) && isSlidingOnWall)
            StopWallSlide();

        // --- Остальные механики (Dash, Slide, Crouch) остаются без изменений.
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
            isCrouching = false;

        if (grounded && Input.GetKey(KeyCode.LeftControl) && Mathf.Abs(rb.linearVelocity.x) > 0.1f && !isSliding && !isCrouching)
            StartCoroutine(Slide(rb.linearVelocity.x));

        // Если без состоянию slide/crouch, применяем нормальный хитбокс
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
            rb.gravityScale = 0; // Отключаем гравитацию.
            jumpCount = 0;
            // Сохраняем сторону контакта.
            wallContactSide = collisionController.GetLastWallContactSide();
            // Если игрок уже нажимает вверх, можно сразу начать climb.
            if (Input.GetAxisRaw("Vertical") >= 0.2f)
            {
                isWallClimbing = true;
                wallClimbStartTime = Time.time;
            }
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
        isWallClimbing = false;
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