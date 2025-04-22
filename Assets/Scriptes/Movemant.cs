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
    public float dashDuration = 0.2f;      // длительность рывка (не мгновенная телепортация)
    public float dashCooldown = 1f;
    private bool canDash = true;
    private bool isInvulnerable = false;   // используется в логике дэша

    // --- Параметры подката (Slide)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // --- Приседание – для dash не допускается
    private bool isCrouching = false;

    // --- Параметры цепления за стену (Wall Hang / Slide)
    public float wallHangTime = 0.5f;         // время, которое персонаж висит неподвижно сразу после цепления за стену
    public float wallSlideAcceleration = 10f; // ускорение скольжения по стене (единиц/сек)
    public float wallSlideMaxSpeed = 5f;      // максимальная скорость скольжения (абсолютное значение)
    public float wallJumpForce = 10f;         // вертикальная компонента wall jump
    public float wallJumpHorizForce = 5f;     // горизонтальная составляющая wall jump (настраиваемая)
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;     // false – режим "висения", true – режим ускоренного скольжения
    public float wallDetachCooldown = 0.3f;     // время, в течение которого нельзя повторно зацепиться за ту же стену
    private float timeSinceDetached = 0f;
    // Храним сторону стены (1, если стена справа; -1, если слева).
    private int wallContactSide = 0;

    // --- Блокировка горизонтального управления после wall jump,
    // чтобы горизонтальный импульс не затирался обычным вводом.
    private bool isWallJumping = false;
    public float wallJumpLockDuration = 0.2f;

    // --- Параметры гравитации при цеплении за стену.
    public float wallHangGravityScale = 0f;  // значение gravityScale, когда персонаж цепляется за стену
    private float originalGravityScale;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    // --- Переменные для запоминания размеров хитбокса (для восстановления после слайда/приседания)
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

        // --- Обработка горизонтального движения
        // Если персонаж не находится в подкате, не приседает и не заблокирован после wall jump,
        // а также если ни Ctrl, ни S не зажаты – обновляем горизонтальную скорость по вводу.
        if (!isSliding && !isCrouching && !isWallJumping && !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)))
        {
            if (grounded)
            {
                rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);
                if (moveInput > 0 && !facingRight)
                    Flip();
                else if (moveInput < 0 && facingRight)
                    Flip();
            }
            else
            {
                if (Mathf.Abs(moveInput) > 0.01f)
                {
                    float newX = Mathf.Lerp(rb.velocity.x, moveInput * speed, 5f * Time.deltaTime);
                    rb.velocity = new Vector2(newX, rb.velocity.y);
                    if (moveInput > 0 && !facingRight)
                        Flip();
                    else if (moveInput < 0 && facingRight)
                        Flip();
                }
            }
        }
        // Если же зажаты Ctrl или S, то при нахождении на земле горизонтальная скорость обнуляется.
        else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S))
        {
            if (grounded)
            {
                rb.velocity = new Vector2(0, rb.velocity.y);
                isCrouching = true;
            }
        }

        // Если персонаж цепляется за стену, ввод используется только для смены направления.
        if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Прыжок
        // Если нажата кнопка прыжка и персонаж находится на земле, имеет ещё прыжки или цепляется за стену:
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                /*  
                    Если персонаж цепляется за стену:
                      • Если он смотрит лицом к стене (например, стена справа и он смотрит вправо),
                        выполняется обычный вертикальный прыжок.
                      • Если же он уже смотрит от стены,
                        выполняется wall jump с горизонтальным импульсом.
                */
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
                rb.velocity = new Vector2(rb.velocity.x, jumpForce);
                jumpCount++;
            }
        }

        if (grounded)
        {
            jumpCount = 0;
            StopWallSlide();
        }

        // --- Инициирование цепления за стену (Wall Hang)
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

        // Если включён режим ускоренного скольжения по стене,
        // плавно корректируется вертикальная скорость до -wallSlideMaxSpeed.
        if (isSlidingOnWall && wallSlideActive && touchingWall)
        {
            float newY = Mathf.MoveTowards(rb.velocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.deltaTime);
            rb.velocity = new Vector2(rb.velocity.x, newY);
        }

        // --- Остальные механики (Dash, Slide, Crouch)

        // Рывок (Dash)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isSliding && !isCrouching)
        {
            StartCoroutine(Dash());
        }

        // Подкат (Slide) – запускается, если нажата клавиша Ctrl, персонаж на земле и есть горизонтальный ввод.
        if (Input.GetKeyDown(KeyCode.LeftControl) && grounded && Mathf.Abs(moveInput) > 0.01f && !isSliding)
        {
            StartCoroutine(Slide(moveInput));
        }
        // Приседание – если зажаты Ctrl или S, но горизонтального ввода почти нет, и персонаж на земле.
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && grounded && Mathf.Abs(moveInput) < 0.01f)
        {
            isCrouching = true;
            rb.velocity = Vector2.zero;
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

        // Сброс настроек для корректной проверки столкновений со стеной.
        collisionController.ignoreFlipForWallChecks = false;
    }

    // --- Методы цепления за стену (Wall Hang / Slide)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false;  // сначала персонаж висит неподвижно
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
            wallSlideActive = true; // после wallHangTime переходим в режим ускоренного скольжения
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
        canDash = false;
        float currentVertical = rb.velocity.y;
        if (collisionController.IsGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0);
            currentVertical = 0;
        }
        float dashDirection = (facingRight ? 1 : -1);
        float dashSpeed = dashDistance / dashDuration;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;
        rb.velocity = new Vector2(dashDirection * dashSpeed, currentVertical);
        yield return new WaitForSeconds(dashDuration);
        rb.gravityScale = originalGravity;
        if (collisionController.IsGrounded)
            rb.velocity = Vector2.zero;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
        isInvulnerable = false;
    }

    // --- Подкат (Slide)
    // Доработанный подкат:
    // • Во время подката персонаж не реагирует на входящие сигналы A и D.
    // • Если подкат завершается в воздухе (персонаж оторвался от земли), подкат прерывается, а горизонтальный импульс сохраняется.
    private IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDirection = Mathf.Sign(moveInput);
        rb.velocity = new Vector2(slideDirection * slideSpeed, rb.velocity.y);
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            // Если персонаж теряет контакт с землей, прерываем подкат.
            if (!collisionController.IsGrounded)
            {
                break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        isSliding = false;
        // Если персонаж на земле, обнуляем горизонтальную скорость.
        if (collisionController.IsGrounded)
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
        }
        // Если же подкат прервался в воздухе, сохранённый импульс остается.
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
}