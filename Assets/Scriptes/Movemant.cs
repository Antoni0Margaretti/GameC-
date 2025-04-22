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
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- Параметры рывка (Dash)
    public float dashDistance = 5f;
    public float dashDuration = 0.2f;  // длительность рывка (не мгновенная телепортация)
    public float dashCooldown = 1f;
    private bool canDash = true;
    private bool isInvulnerable = false;  // используется в конце дэша

    // --- Параметры подката (Slide)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // --- Приседание – для dash не допускается
    private bool isCrouching = false;

    // --- Параметры цепления за стену (Wall Hang/Slide)
    public float wallHangTime = 0.5f;    // время, которое персонаж висит неподвижно сразу после цепления за стену
    public float wallSlideAcceleration = 10f;  // ускорение скольжения по стене (единиц/сек)
    public float wallSlideMaxSpeed = 5f;       // максимальная скорость скольжения (абсолютное значение)
    public float wallJumpForce = 10f;    // вертикальная компонента wall jump
    public float wallJumpHorizForce = 5f; // горизонтальная составляющая wall jump (настраиваемая)
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;  // false – режим "висения", true – режим ускоренного скольжения
    public float wallDetachCooldown = 0.3f;   // время, в течение которого нельзя повторно зацепиться за ту же стену
    private float timeSinceDetached = 0f;
    // Храним сторону стены (1 если стена справа, -1 если слева)
    private int wallContactSide = 0;

    // --- Флаг блокировки горизонтального управления после wall jump
    // (чтобы импульс не затирался обновлением ввода)
    private bool isWallJumping = false;
    public float wallJumpLockDuration = 0.2f; // время блокировки горизонтального обновления

    // --- Параметры гравитации при цеплении
    public float wallHangGravityScale = 0f;   // значение gravityScale, когда персонаж цепляется за стену
    private float originalGravityScale;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    // --- Переменные для размеров хитбокса (для восстановления после слайда/приседания)
    private Vector2 normalSize;
    private Vector2 normalOffset;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        // Сохраняем исходное значение гравитации и размеров хитбокса
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

        // Если персонаж цепляется за стену, но стена пропала – прекращаем цепление.
        if (isSlidingOnWall && !touchingWall)
        {
            StopWallSlide();
        }

        // Обработка горизонтального движения
        // Если не цепляемся, не слайдим, не приседаем и не на блокировке после wall jump:
        if (!isSlidingOnWall && !isSliding && !isCrouching && !isWallJumping)
        {
            if (grounded)
            {
                // На земле перезаписываем горизонтальную скорость полностью.
                rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);
                if (moveInput > 0 && !facingRight)
                    Flip();
                else if (moveInput < 0 && facingRight)
                    Flip();
            }
            else
            {
                // В воздухе: если есть ввод, постепенно меняем горизонтальную скорость,
                // иначе оставляем сохранённую (импульс от wall jump сохраняется).
                if (Mathf.Abs(moveInput) > 0.01f)
                {
                    float newX = Mathf.Lerp(rb.linearVelocity.x, moveInput * speed, 5f * Time.deltaTime);
                    rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
                    if (moveInput > 0 && !facingRight)
                        Flip();
                    else if (moveInput < 0 && facingRight)
                        Flip();
                }
            }
        }
        // Если персонаж цепляется за стену, разрешаем ввод только для смены направления (без записи скорости):
        if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Прыжок (Jump)
        // Если нажата кнопка Jump и персонаж либо на земле, либо имеет ещё прыжки, либо цепляется за стену.
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                /* 
                   Если персонаж цепляется за стену:
                   • Если он повёрнут лицом к стене (например, стена справа и он смотрит вправо),
                     выполняется обычный вертикальный прыжок.
                   • Если же он повернут от стены, производится wall jump с горизонтальным импульсом.
                */
                if ((wallContactSide == 1 && facingRight) || (wallContactSide == -1 && !facingRight))
                {
                    // Лицом к стене – обычный прыжок
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                }
                else
                {
                    // От стены – с горизонтальным импульсом
                    rb.linearVelocity = new Vector2(-wallContactSide * wallJumpHorizForce, wallJumpForce);
                    StartCoroutine(WallJumpLockCoroutine());
                }
                StopWallSlide();
                timeSinceDetached = 0f;
                jumpCount = 0;
            }
            else
            {
                // Обычный прыжок с земли или из воздуха
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpCount++;
            }
        }

        if (grounded)
        {
            // При касании земли сбрасываем счётчик прыжков и прекращаем цепление.
            jumpCount = 0;
            StopWallSlide();
        }

        // --- Инициирование цепления за стену (Wall Hang)
        // Если персонаж касается стены, не на земле, ввод по горизонтали есть,
        // и достаточно времени прошло с предыдущего цепления – начинаем цепление.
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

        // Если включён режим ускоренного скольжения по стене, плавно изменить вертикальную скорость.
        if (isSlidingOnWall && wallSlideActive && touchingWall)
        {
            float newY = Mathf.MoveTowards(rb.linearVelocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.deltaTime);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, newY);
        }

        // --- Остальные механики (Dash, Slide, Crouch)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isSliding && !isCrouching)
        {
            StartCoroutine(Dash());
        }
        if (Input.GetKeyDown(KeyCode.LeftControl) && grounded && Mathf.Abs(moveInput) > 0.01f && !isSliding)
        {
            StartCoroutine(Slide(moveInput));
        }
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && grounded && Mathf.Abs(moveInput) < 0.01f)
        {
            isCrouching = true;
            rb.linearVelocity = Vector2.zero;
        }
        else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.S))
        {
            isCrouching = false;
        }

        if (!isSliding && !isCrouching)
        {
            boxCollider.size = normalSize;
            boxCollider.offset = normalOffset;
        }

        // Сброс настроек для проверки столкновений со стеной
        collisionController.ignoreFlipForWallChecks = false;
    }

    // --- Методы цепления за стену (Wall Hang / Slide)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false;  // Сначала персонаж висит неподвижно.
            rb.linearVelocity = Vector2.zero;
            // Отключаем гравитацию, устанавливая её в wallHangGravityScale.
            rb.gravityScale = wallHangGravityScale;
            jumpCount = 0;
            // Запоминаем сторону стены (если персонаж цепляется, его текущее направление определяет сторону).
            wallContactSide = facingRight ? 1 : -1;
            StartCoroutine(WallHangCoroutine());
        }
    }

    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
        {
            wallSlideActive = true;  // После wallHangTime переходим в режим ускоренного скольжения.
        }
    }

    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        wallSlideActive = false;
        timeSinceDetached = 0f;
        // Восстанавливаем исходное значение гравитации.
        rb.gravityScale = originalGravityScale;
    }

    // --- Рывок (Dash)
    private IEnumerator Dash()
    {
        canDash = false;
        float currentVertical = rb.linearVelocity.y;
        if (collisionController.IsGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
            currentVertical = 0;
        }
        float dashDirection = (facingRight ? 1 : -1);
        float dashSpeed = dashDistance / dashDuration;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, currentVertical);
        yield return new WaitForSeconds(dashDuration);
        rb.gravityScale = originalGravity;
        if (collisionController.IsGrounded)
            rb.linearVelocity = Vector2.zero;
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
            // Если персонаж теряет контакт с землей, прерываем подкат.
            if (!collisionController.IsGrounded)
            {
                break;
            }
            float currentX = Mathf.Lerp(initialVel, 0, elapsed / slideDuration);
            rb.linearVelocity = new Vector2(currentX, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }
        isSliding = false;
        // Если персонаж на земле, принудительно обнуляем горизонтальную скорость.
        if (collisionController.IsGrounded)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            // Если клавиши Ctrl или S всё ещё зажаты, переходим в режим приседа.
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S))
            {
                isCrouching = true;
            }
        }
        // Если подкат прервался, горизонтальный импульс остаётся.
    }

    // --- Блокировка горизонтального управления после wall jump
    private IEnumerator WallJumpLockCoroutine()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpLockDuration);
        isWallJumping = false;
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
    // чтобы сохранялся импульс wall jump (импульс не затирался обновлением ввода)
    private IEnumerator WallJumpLockCoroutine()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpLockDuration);
        isWallJumping = false;
    }
}