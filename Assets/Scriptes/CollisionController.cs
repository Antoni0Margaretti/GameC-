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
    public float dashDuration = 0.2f;  // длительность рывка
    public float dashCooldown = 1f;
    private bool canDash = true;

    // --- Параметры подката (Slide)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // --- Приседание (для dash не допускается)
    private bool isCrouching = false;

    // --- Параметры цепления за стену (Wall Hang/Slide)
    public float wallHangTime = 0.5f;    // время, которое персонаж висит неподвижно после цепления
    public float wallSlideAcceleration = 10f;  // ускорение скольжения по стене
    public float wallSlideMaxSpeed = 5f;       // максимальная скорость скольжения
    public float wallJumpForce = 10f;    // вертикальная компонента wall jump
    public float wallJumpHorizForce = 5f; // горизонтальная составляющая wall jump
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;
    public float wallDetachCooldown = 0.3f;   // время, в течение которого нельзя повторно зацепиться за ту же стену
    private float timeSinceDetached = 0f;

    // --- Параметры гравитации при цеплении
    public float wallHangGravityScale = 0f;   // значение gravityScale во время цепления
    private float originalGravityScale;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    // --- Сторона стены при цеплении: 1, если стена справа; -1, если слева.
    private int wallSide = 0;

    // --- Новое: Параметры для временного игнорирования цепления после wall jump
    public float wallJumpDisableDuration = 0.5f; // сколько секунд игнорировать цепление после wall jump
    private bool skipWallCollision = false;      // если true, не пытаемся цепляться за стену

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        // Сохраняем исходное значение гравитации
        originalGravityScale = rb.gravityScale;
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

        // При цеплении, ввод используется только для смены направления.
        if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }
        else if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            // Обычное движение
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);
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
                // Определяем сторону цепления:
                // Если ввода почти нет, считаем, что персонаж уже перевёрнут от стены;
                // иначе определяем по вводу.
                if (Mathf.Abs(moveInput) < 0.01f)
                    wallSide = (facingRight) ? -1 : 1;
                else
                    wallSide = (moveInput > 0) ? 1 : -1;

                // Если персонаж уже повернут от стены (то есть его взгляд противоположен wallSide),
                // выполняем wall jump с горизонтальным импульсом (без дополнительного ввода)
                if ((facingRight && wallSide < 0) || (!facingRight && wallSide > 0))
                {
                    WallJump();
                }
                else
                {
                    // Иначе обычный вертикальный прыжок.
                    NormalJump();
                    jumpCount++;
                }
            }
            else
            {
                NormalJump();
                jumpCount++;
            }
        }
        if (grounded)
        {
            jumpCount = 0;
            StopWallSlide();
        }

        // --- Инициирование цепления за стену (Wall Hang)
        // Не начинаем цепление, если skipWallCollision установлено.
        if (!skipWallCollision && collisionController.IsTouchingWall && !grounded &&
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

        // Обновляем wall slide только если не в состоянии wall jump.
        if (isSlidingOnWall && wallSlideActive && touchingWall && !skipWallCollision)
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
    }

    // --- Нормальный прыжок (вертикальный)
    private void NormalJump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
    }

    // --- Wall Jump: отпрыгивание от стены с заданными импульсами.
    private void WallJump()
    {
        rb.velocity = new Vector2(-wallSide * wallJumpHorizForce, wallJumpForce);
        jumpCount = 0;
        StopWallSlide();
        timeSinceDetached = 0f;

        // Устанавливаем флаг блокировки цепления на время wallJumpDisableDuration.
        skipWallCollision = true;
        StartCoroutine(ResetWallCollision());
    }

    private IEnumerator ResetWallCollision()
    {
        yield return new WaitForSeconds(wallJumpDisableDuration);
        skipWallCollision = false;
    }

    // --- Запуск цепления за стену (Wall Hang)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false;  // сначала неподвижное висение
            rb.velocity = Vector2.zero;
            rb.gravityScale = wallHangGravityScale;
            jumpCount = 0;
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

    // --- Прекращение цепления за стену (Wall Slide)
    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        wallSlideActive = false;
        timeSinceDetached = 0f;
        rb.gravityScale = originalGravityScale;
    }

    // --- Dash
    private IEnumerator Dash()
    {
        canDash = false;
        float currentVertical = rb.velocity.y;
        if (collisionController.IsGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0);
            currentVertical = 0;
        }
        float dashDir = (facingRight ? 1 : -1);
        float dashSpeed = dashDistance / dashDuration;
        float origGravity = rb.gravityScale;
        rb.gravityScale = 0;
        rb.velocity = new Vector2(dashDir * dashSpeed, currentVertical);
        yield return new WaitForSeconds(dashDuration);
        rb.gravityScale = origGravity;
        if (collisionController.IsGrounded)
            rb.velocity = Vector2.zero;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // --- Slide
    private IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDir = Mathf.Sign(moveInput);
        rb.velocity = new Vector2(slideDir * slideSpeed, rb.velocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.velocity = Vector2.zero;
        isSliding = false;
    }

    // --- Flip: меняем направление взгляда
    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }
}