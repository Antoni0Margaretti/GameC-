using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // --- Компоненты
    private CollisionController collisionController;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // --- (Для динамических хитбоксов – тема рассматривается отдельно)
    private Vector2 normalSize, normalOffset;

    // --- Флаг неуязвимости (для логики получения урона)
    private bool isInvulnerable = false;

    // --- Параметры движения
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- Параметры рывка (Dash)
    public float dashDistance = 5f;
    public float dashDuration = 0.2f;  // Длительность рывка (не телепортация)
    public float dashCooldown = 1f;
    private bool canDash = true;

    // --- Параметры подката (Slide)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // --- Приседание – для dash не допускается
    private bool isCrouching = false;

    // --- Параметры цепления за стену (Wall Hang / Slide)
    public float wallHangTime = 0.5f;    // Время, которое персонаж висит неподвижно сразу после цепления за стену
    public float wallSlideAcceleration = 10f;  // Ускорение скольжения по стене (единиц/сек)
    public float wallSlideMaxSpeed = 5f;       // Максимальная скорость скольжения (при достижении эта величина используется как абсолютное значение)
    public float wallJumpForce = 10f;    // Сила отталкивания при wall jump
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;  // Флаг, переведён ли режим "висения" в режим скольжения с ускорением
    public float wallDetachCooldown = 0.3f;   // Время, в течение которого нельзя повторно зацепиться за стену
    private float timeSinceDetached = 0f;

    // --- Период благодати при смене направления в цеплении за стену
    private float wallFlipTimer = 0f;
    public float wallFlipGracePeriod = 0.1f;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        normalSize = boxCollider.size;
        normalOffset = boxCollider.offset;
    }

    void Update()
    {
        timeSinceDetached += Time.deltaTime;
        if (isSlidingOnWall) // Если цепление активно, накапливаем таймер после поворота
        {
            wallFlipTimer += Time.deltaTime;
        }
        else
        {
            wallFlipTimer = 0f;
        }

        float moveInput = Input.GetAxis("Horizontal");

        bool grounded = collisionController.IsGrounded;
        // Проверка контакта стены используется дополнительно в IsAnyEdgeFullyOnWall()

        // Если цепление активно, проверяем, что хотя бы один узкий участок хитбокса полностью контактирует со стеной.
        // Но пропускаем проверку, если только что поменяли направление (в пределах grace period).
        if (isSlidingOnWall && wallFlipTimer >= wallFlipGracePeriod)
        {
            if (!IsAnyEdgeFullyOnWall())
            {
                StopWallSlide();
            }
        }
        else if (isSlidingOnWall) // Даже если в пределах grace, разрешаем поворот.
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Стандартное движение (если не цепляемся, не в подкате и не в приседе)
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Прыжок
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            if (isSlidingOnWall)
            {
                rb.linearVelocity = new Vector2((facingRight ? -1 : 1) * speed, wallJumpForce);
                StopWallSlide();
                timeSinceDetached = 0f;
            }
            jumpCount++;
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

        // Если ускоренный режим скольжения активен, плавно увеличиваем скорость по Y до –wallSlideMaxSpeed.
        if (isSlidingOnWall && wallSlideActive && collisionController.IsTouchingWall)
        {
            float newY = Mathf.MoveTowards(rb.linearVelocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.deltaTime);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, newY);
        }

        // --- Остальные механики (Dash, Slide, Crouch) остаются без изменений.
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

        collisionController.ignoreFlipForWallChecks = isSlidingOnWall;
    }

    // --- Метод проверки полного контакта узкого участка (с обеих сторон).
    private bool IsAnyEdgeFullyOnWall()
    {
        Bounds playerBounds = boxCollider.bounds;
        Vector2 edgeSize = new Vector2(0.05f, playerBounds.size.y * 0.9f);
        Vector2 leftEdgeCenter = new Vector2(playerBounds.min.x, playerBounds.center.y);
        Vector2 rightEdgeCenter = new Vector2(playerBounds.max.x, playerBounds.center.y);

        Collider2D leftWall = Physics2D.OverlapBox(leftEdgeCenter, edgeSize, 0f, collisionController.wallLayer);
        Collider2D rightWall = Physics2D.OverlapBox(rightEdgeCenter, edgeSize, 0f, collisionController.wallLayer);

        bool leftContact = false;
        bool rightContact = false;

        if (leftWall != null)
        {
            Bounds leftWallBounds = leftWall.bounds;
            Bounds leftEdgeBounds = new Bounds(leftEdgeCenter, edgeSize);
            leftContact = leftWallBounds.Contains(leftEdgeBounds.min) && leftWallBounds.Contains(leftEdgeBounds.max);
        }
        if (rightWall != null)
        {
            Bounds rightWallBounds = rightWall.bounds;
            Bounds rightEdgeBounds = new Bounds(rightEdgeCenter, edgeSize);
            rightContact = rightWallBounds.Contains(rightEdgeBounds.min) && rightWallBounds.Contains(rightEdgeBounds.max);
        }
        return leftContact || rightContact;
    }

    // --- Методы цепления за стену (Wall Hang/Slide)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false;  // Сначала персонаж висит неподвижно.
            rb.linearVelocity = Vector2.zero;
            jumpCount = 0;  // Сброс прыжкового счётчика.
            wallFlipTimer = 0f; // Сброс таймера поворота.
            StartCoroutine(WallHangCoroutine());
        }
    }

    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
        {
            wallSlideActive = true;  // Режим ускоренного скольжения активирован.
        }
    }

    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        wallSlideActive = false;
        timeSinceDetached = 0f;
        wallFlipTimer = 0f;
    }

    // --- Рывок (Dash)
    private IEnumerator Dash()
    {
        canDash = false;
        isInvulnerable = true;

        if (!collisionController.IsGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
        }

        float dashDirection = (facingRight ? 1 : -1);
        float dashSpeed = dashDistance / dashDuration;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0);

        yield return new WaitForSeconds(dashDuration);

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = originalGravity;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
        isInvulnerable = false;
    }

    // --- Подкат (Slide)
    private IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDirection = Mathf.Sign(moveInput);
        rb.linearVelocity = new Vector2(slideDirection * slideSpeed, rb.linearVelocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.linearVelocity = Vector2.zero;
        isSliding = false;
    }

    // --- Изменение направления (Flip)
    // При повороте в режиме цепления сбрасываем таймер grace period.
    private void Flip()
    {
        facingRight = !facingRight;
        if (isSlidingOnWall)
        {
            wallFlipTimer = 0f;
        }
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }
}