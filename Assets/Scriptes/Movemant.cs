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

    // --- Параметры подката (Slide)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // --- Приседание (для dash не допускается)
    private bool isCrouching = false;

    // --- Параметры цепления за стену (Wall Hang/Slide)
    public float wallHangTime = 0.5f;    // время, которое персонаж висит неподвижно сразу после цепления
    public float wallSlideAcceleration = 10f;  // ускорение скольжения по стене
    public float wallSlideMaxSpeed = 5f;       // максимальная скорость скольжения
    public float wallJumpForce = 10f;    // вертикальная компонента wall jump
    public float wallJumpHorizForce = 5f; // горизонтальная составляющая wall jump (настраиваемая)
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;
    public float wallDetachCooldown = 0.3f;   // время, в течение которого нельзя повторно зацепиться за ту же стену
    private float timeSinceDetached = 0f;

    // --- Параметры гравитации при цеплении
    public float wallHangGravityScale = 0f;   // значение gravityScale, когда персонаж цепляется за стену
    private float originalGravityScale;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    // --- Сторона стены при цеплении: 1, если стена справа; -1, если слева.
    private int wallSide = 0;

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

        // Если персонаж цепляется за стену, но стена исчезает — прекращаем цепление.
        if (isSlidingOnWall && !touchingWall)
        {
            StopWallSlide();
        }

        // Если цепляемся, используем ввод только для смены направления.
        if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }
        // Общее движение, если мы не цепляемся, не подкатаем и не приседаем.
        else if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
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
                // При цеплении за стену: если ввода почти нет, считаем, что персонаж уже повернут от стены.
                if (Mathf.Abs(moveInput) < 0.01f)
                    wallSide = (facingRight) ? -1 : 1;
                else
                    wallSide = (moveInput > 0) ? 1 : -1;

                // Если персонаж уже повернут от стены (то есть его взгляд противоположен wallSide), выполняется wall jump.
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
        // Дополнительная проверка: запускаем цепление только если горизонтальная скорость почти нулевая.
        if (collisionController.IsTouchingWall && !grounded &&
            Mathf.Abs(moveInput) > 0.01f &&
            ((facingRight && moveInput > 0) || (!facingRight && moveInput < 0)) &&
            timeSinceDetached >= wallDetachCooldown &&
            Mathf.Abs(rb.velocity.x) < 0.1f)
        {
            StartWallHang();
        }
        if (Input.GetKeyDown(KeyCode.S) && isSlidingOnWall)
        {
            StopWallSlide();
        }

        // Обновляем wall slide: если активен режим ускоренного скольжения, плавно уменьшаем вертикальную скорость до -wallSlideMaxSpeed.
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
    }

    // --- Нормальный прыжок (вертикальный)
    private void NormalJump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
    }

    // --- Wall Jump: отталкивание от стены с заданными импульсами.
    private void WallJump()
    {
        rb.velocity = new Vector2(-wallSide * wallJumpHorizForce, wallJumpForce);
        jumpCount = 0;
        StopWallSlide();
        timeSinceDetached = 0f;
    }

    // --- Запуск цепления за стену (Wall Hang)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false; // Сначала персонаж висит неподвижно.
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