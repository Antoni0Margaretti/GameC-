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
    public float airAcceleration = 5f;

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
    public bool isInvulnerable = false;
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
    private float initialGrabVerticalSpeed = 0f;
    private Coroutine autoClimbCoroutine = null;  // для хранения ссылки на корутину авто-подъёма

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

    [Header("Ledge Climb Settings")]
    // Если хотите вручную задавать ориентир для отрисовки отладочного представления — можно оставить.
    public Transform ledgeRayOrigin;         // Этот объект можно использовать для визуальной отладки (не обязателен, см. ниже)

    // Параметры, определяющие работу механики:
    public float ledgeRayLength = 0.1f;             // Длина вертикального луча, проверяющего наличие пола за краем
    // Два разных вертикальных расстояния подъёма
    public float ledgeClimbVerticalDistanceLower = 0.6f;
    public float ledgeClimbVerticalDistanceUpper = 1.0f; // На сколько единиц поднимается персонаж при залезании
    public float ledgeClimbHorizontalOffset = 0.3f;   // Горизонтальное смещение (выход на платформу)
    public float ledgeClimbDuration = 0.4f;         // Время, за которое происходит залезание

    // Отдельные вертикальные смещения для нижнего и верхнего луча
    public float ledgeProbeVerticalOffsetLower = 0f;
    public float ledgeProbeVerticalOffsetUpper = 0.2f;
    // Отдельные длины лучей для проверки нижнего и верхнего отрезков.
    public float ledgeRayLengthLower = 0.1f;
    public float ledgeRayLengthUpper = 0.15f;

    // Локальные переменные для состояния залезания
    private bool isLedgeClimbing = false;
    private Vector2 ledgeClimbStartPos;
    private Vector2 ledgeClimbTargetPos;

    // Настраиваемая дополнительная вертикальная поправка к базовой точке.
    public float ledgeProbeCenterVerticalOffset = 0f;

    // Новая переменная: дополнительное смещение для probe point. 
    // Значение по X позволяет регулировать горизонтальное положение луча.
    public Vector2 ledgeProbeOffset;  // Например, (0.3, 0) установит дополнительное смещение вправо.

    // Transform, задающий центр, относительно которого отмеряется позиция луча.
    public Transform ledgeProbeCenter;
    // Новые параметры для настройки положения probe point относительно центра.
    public float ledgeProbeHorizontalDistance = 0.3f; // Расстояние от центра до probe point по X.

    private CombatController combatController;
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
        // Получаем ссылку на CombatController (можно кэшировать в Start)
        if (combatController == null)
            combatController = GetComponent<CombatController>();

        // Если сейчас парирование или атака — не позволяем цепляться за стену
        if (combatController != null && (combatController.IsParrying || combatController.IsAttacking))
        {
            if (isSlidingOnWall)
                StopWallSlide();
            if (isLedgeClimbing)
                StopLedgeClimb();
        }
        //// Если игрок мёртв, вызываем метод Die
        //if (!isAlive)
        //{
        //    Die();
        //}

        // Если игра на паузе, разрешаем обрабатывать только клавишу Esc,
        // а весь остальной ввод игнорируем.
        if (Time.timeScale == 0 && !Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

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

        // Вызываем попытку начать залезание без дополнительных проверок.
        TryStartLedgeClimb();

        // Отладочная отрисовка: нижний луч – зелёный, верхний – синий.
        Debug.DrawRay(GetLedgeProbePointForLower(), Vector2.down * ledgeRayLengthLower, Color.green);
        Debug.DrawRay(GetLedgeProbePointForUpper(), Vector2.down * ledgeRayLengthUpper, Color.blue);

        // --- Прыжок
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                // Если персонаж находится в режиме цепления, перед прыжком проверяем авто-подъём.
                if (autoClimbing)
                {
                    // Останавливаем авто-подъём и сбрасываем вертикальную скорость.
                    autoClimbing = false;
                    if (autoClimbCoroutine != null)
                    {
                        StopCoroutine(autoClimbCoroutine);
                        autoClimbCoroutine = null;
                    }
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                    // Переход через корутину, чтобы позволить физике «освежиться».
                    StartCoroutine(PerformWallJump());
                }
                else
                {
                    // Если авто-подъём не активен, сразу выполняем wall jump:
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
                // Обработка обычного прыжка на земле или в воздухе.
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
        if (Input.GetKeyDown(KeyBindings.Crouch) && isSlidingOnWall)
            StopWallSlide();
        if (isSlidingOnWall && wallSlideActive && touchingWall)
        {
            float newY = Mathf.MoveTowards(rb.linearVelocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.deltaTime);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, newY);
        }

        // --- Остальные механики (Dash, Slide, Crouch)
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

        if (grounded)
        {
            if (Input.GetKeyDown(KeyBindings.Slide) && Mathf.Abs(rawH) >= threshold && !isSliding)
                StartCoroutine(Slide(rawH));
            else if ((Input.GetKey(KeyBindings.Slide) || Input.GetKey(KeyBindings.Crouch)) && Mathf.Abs(rawH) < threshold)
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

    // --- Методы цепления за стену (Wall Hang / Slide)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false;

            // Сначала запоминаем вертикальную скорость персонажа при зацеплении
            initialGrabVerticalSpeed = rb.linearVelocity.y;

            // Теперь обнуляем скорость и отключаем гравитацию для режима цепления.
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0;
            jumpCount = 0;

            // Сохраняем сторону контакта со стеной, полученную из CollisionController.
            wallContactSide = collisionController.GetLastWallContactSide();

            // Если персонаж летел вверх в момент зацепления (вертикальная скорость была положительной),
            // запускаем автоматический подъём.
            if (initialGrabVerticalSpeed > 0)
            {
                wallClimbStartY = transform.position.y;
                autoClimbing = true;
                StartCoroutine(AutoClimbCoroutine());
                StartCoroutine(WaitForAutoClimbThenWallHang());
            }
            else
            {
                // Иначе запускаем стандартный таймер висения/скольжения.
                StartCoroutine(WallHangCoroutine());
            }
        }
    }

    // Короутина автоматического подъёма (Auto Climb)
    private IEnumerator AutoClimbCoroutine()
    {
        // Цикл выполняется, пока авто-подъём активен и пока не достигнута заданная дистанция подъёма.
        while (autoClimbing && transform.position.y < wallClimbStartY + wallAutoClimbDistance)
        {
            // Если персонаж перестаёт полностью прилегать к стене...
            if (!collisionController.IsTouchingWall)
            {
                // Останавливаем авто-подъём и переводим персонажа в режим висения.
                autoClimbing = false;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                // Можно сразу вызвать корутину перехода в режим висения:
                StartCoroutine(WallHangCoroutine());
                yield break; // Выходим из корутины, так как контакт потерян.
            }

            // Если контакт сохранён — продолжаем подъем с заданной скоростью
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, wallAutoClimbSpeed);
            yield return null;
        }

        // По окончании подъёма (либо дистанция достигнута) выключаем авто-подъём
        autoClimbing = false;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        // После завершения авто-подъёма запускаем переход в режим висения (если ещё не вызван)
        StartCoroutine(WallHangCoroutine());
    }

    // Короутина, которая ждёт завершения авто-подъёма, а затем запускает стандартный таймер висения:
    private IEnumerator WaitForAutoClimbThenWallHang()
    {
        // Ждём, пока автоматический подъём не завершится.
        while (autoClimbing)
        {
            yield return null;
        }
        // После авто-подъёма запускаем общую корутину, которая через wallHangTime определяет начало скольжения.
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
        // Даем время для завершения авто-подъёма (один кадр – минимально) 
        yield return null;

        // Выполняем стандартный wall jump:
        // Если игрок зажимает клавишу движения, противоположную стороне стены,
        // выполняется wall jump с отталкивающей силой.
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

    // Метод StopWallSlide() сбрасывает все состояния цепления:
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
        // При начале рывка:
        isInvulnerable = true;
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
        yield return new WaitForSeconds(dashCooldown);
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

    // Вычисляем базовую точку для лучей.
    // Если задан ledgeProbeCenter, берём его позицию; иначе вычисляем центр хитбокса.
    // При этом к Y добавляем ledgeProbeCenterVerticalOffset.
    private Vector2 GetBaseCenter()
    {
        Vector2 baseCenter = (ledgeProbeCenter != null)
                             ? ledgeProbeCenter.position
                             : boxCollider.bounds.center;
        baseCenter.y += ledgeProbeCenterVerticalOffset;
        return baseCenter;
    }

    // Метод для вычисления probe point для нижнего луча.
    private Vector2 GetLedgeProbePointForLower()
    {
        Vector2 baseCenter = GetBaseCenter();
        int side = collisionController.GetLastWallContactSide();
        if (side == 0)
            side = (facingRight ? 1 : -1);
        return baseCenter + new Vector2(side * ledgeProbeHorizontalDistance, ledgeProbeVerticalOffsetLower);
    }

    // Метод для вычисления probe point для верхнего луча.
    private Vector2 GetLedgeProbePointForUpper()
    {
        Vector2 baseCenter = GetBaseCenter();
        int side = collisionController.GetLastWallContactSide();
        if (side == 0)
            side = (facingRight ? 1 : -1);
        return baseCenter + new Vector2(side * ledgeProbeHorizontalDistance, ledgeProbeVerticalOffsetUpper);
    }

    // Тип обнаружения выступа
    private enum LedgeType { None, Lower, Upper }

    // Выполняем два raycast’а с разными длинами и определяем, какая из частей обнаруживает пол.
    private LedgeType GetLedgeType()
    {
        Vector2 lowerProbe = GetLedgeProbePointForLower();
        Vector2 upperProbe = GetLedgeProbePointForUpper();

        RaycastHit2D hitLower = Physics2D.Raycast(lowerProbe, Vector2.down, ledgeRayLengthLower, collisionController.groundLayer);
        RaycastHit2D hitUpper = Physics2D.Raycast(upperProbe, Vector2.down, ledgeRayLengthUpper, collisionController.groundLayer);

        bool lowerDetected = (hitLower.collider != null);
        bool upperDetected = (hitUpper.collider != null);

        // Приоритет можно задать по-разному. Здесь, если оба обнаружены, даём нижнему приоритет.
        if (lowerDetected && !upperDetected)
            return LedgeType.Lower;
        else if (!lowerDetected && upperDetected)
            return LedgeType.Upper;
        else if (lowerDetected && upperDetected)
            return LedgeType.Lower;
        else
            return LedgeType.None;
    }

    // Запуск залезания, если один из лучей обнаруживает пол.
    // Единственное условие для активации – raycast (нижнего или верхнего сегмента) пересекает коллайдер пола.
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

                // Выбираем вертикальное расстояние подъёма в зависимости от того, какой луч сработал.
                float usedVerticalDistance = (ledgeType == LedgeType.Upper) ? ledgeClimbVerticalDistanceUpper : ledgeClimbVerticalDistanceLower;

                ledgeClimbTargetPos = ledgeClimbStartPos + new Vector2(side * ledgeClimbHorizontalOffset, usedVerticalDistance);
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0;
                StartCoroutine(LedgeClimbRoutine());
            }
        }
    }

    // Короутина для плавного перемещения персонажа из начальной позиции к целевой.
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
    public void Die()
    {
        // Здесь можно реализовать логику смерти: проиграть анимацию, остановить движение,
        // перезагрузить сцену или отобразить Game Over
        Debug.Log("Игрок погиб!");
        // Например, перезагрузим текущую сцену:
        // SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}