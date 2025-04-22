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
    public float wallDetachCooldown = 0.3f;     // минимальное время между цеплениями за одну и ту же стену
    private float timeSinceDetached = 0f;
    // Храним сторону стены, к которой цепляемся: 1, если стена справа; -1, если слева.
    private int wallContactSide = 0;

    // --- Блокировка горизонтального управления после wall jump
    // Это позволяет сохранить горизонтальный импульс, пока не начнётся обычное управление.
    private bool isWallJumping = false;
    public float wallJumpLockDuration = 0.2f;

    // --- Параметры гравитации при цеплении
    public float wallHangGravityScale = 0f; // значение gravityScale, когда персонаж цепляется за стену
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
        // Если персонаж не цепляется, не слайдит, не приседает и не находится на блокировке после wall jump:
        if (!isSlidingOnWall && !isSliding && !isCrouching && !isWallJumping)
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
                // В воздухе: если есть ввод – плавно интерполируем горизонтальную скорость.
                // Если ввода нет – сохраняем текущую горизонтальную скорость (то есть импульс от wall jump остаётся).
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
        // Если персонаж цепляется за стену, ввод используется только для смены направления.
        if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Прыжок
        // Если нажата кнопка Jump и персонаж либо на земле, либо имеет ещё прыжки, либо цепляется за стену.
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                /*  
                    Если персонаж цепляется за стену:
                      • Если он смотрит лицом к стене (например, стена справа и он смотрит вправо),
                        выполняется обычный вертикальный прыжок.
                      • Если же он уже смотрит от стены (то есть направлен противоположно),
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
        // Если персонаж касается стены, не находится на земле, присутствует горизонтальный ввод,
        // и прошло минимальное время после предыдущего цепления – запускаем режим цепления.
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
            wallSlideActive = false;   // Сначала персонаж висит неподвижно.
            rb.velocity = Vector2.zero;
            rb.gravityScale = wallHangGravityScale;
            jumpCount = 0;
            // Определяем сторону цепления по направлению персонажа:
            wallContactSide = facingRight ? 1 : -1;
            StartCoroutine(WallHangCoroutine());
        }
    }

    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
        {
            wallSlideActive = true; // После wallHangTime переходим в режим ускоренного скольжения.
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
    private IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDirection = Mathf.Sign(moveInput);
        rb.velocity = new Vector2(slideDirection * slideSpeed, rb.velocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.velocity = Vector2.zero;
        isSliding = false;
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
    // чтобы горизонтальный импульс сохранялся и не затирался обычным обновлением ввода.
    private IEnumerator WallJumpLockCoroutine()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpLockDuration);
        isWallJumping = false;
    }
}