using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // === Компоненты
    [Header("Components")]
    public CollisionController collisionController; // Убедитесь, что этот компонент назначен через Inspector
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // === Движение
    [Header("Movement")]
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount = 0;

    // === Рывок (Dash)
    [Header("Dash")]
    public float dashDistance = 5f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    private bool canDash = true;

    // === Подкат (Slide)
    [Header("Slide")]
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // === Приседание (Crouch)
    [Header("Crouch")]
    public bool isCrouching = false;

    // === Цепление / скольжение за стеной (Wall Hang / Slide)
    [Header("Wall Clinging")]
    public float wallHangTime = 2.0f;          // Персонаж висит 2 секунды до начала скольжения
    public float wallSlideAcceleration = 10f;  // Ускорение скольжения (ед/сек)
    public float wallSlideMaxSpeed = 5f;       // Максимальная скорость скольжения (абсолютное значение)
    public float wallJumpForce = 10f;          // Сила wall jump
    public float wallDetachCooldown = 0.3f;    // Cooldown перед повторным цеплением
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;      // false – режим "висения", true – режим ускоренного скольжения
    private float timeSinceDetached = 0f;

    // === Направление
    [Header("Direction")]
    public bool facingRight = true;

    // === Входные данные (сохраняются для применения их в FixedUpdate)
    private float horizontalInput;
    private bool jumpPressed, dashPressed, slidePressed, stopWallPressed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        if (collisionController == null)
            collisionController = GetComponent<CollisionController>();
        // Обратите внимание: мы не изменяем публичные поля здесь – они задаются в Inspector.
    }

    void Update()
    {
        // Сбор входных данных
        horizontalInput = Input.GetAxis("Horizontal");
        jumpPressed = Input.GetButtonDown("Jump");
        dashPressed = Input.GetKeyDown(KeyCode.LeftShift);
        slidePressed = Input.GetKeyDown(KeyCode.LeftControl);
        stopWallPressed = Input.GetKeyDown(KeyCode.S);

        timeSinceDetached += Time.deltaTime;

        // Если персонаж цепляется за стену, горизонтальный ввод используется только для смены направления.
        if (isSlidingOnWall)
        {
            if (horizontalInput > 0 && !facingRight)
                Flip();
            else if (horizontalInput < 0 && facingRight)
                Flip();
        }

        // Вызываем прыжок сразу, если нажата кнопка и условия выполнены.
        if (jumpPressed && (collisionController.IsGrounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            Jump();
        }

        // Если нажата кнопка рывка (Dash) и можно выполнять рывок, запускаем корутину.
        if (dashPressed && canDash && !isSliding && !isCrouching)
        {
            StartCoroutine(Dash());
        }

        // Подкат (Slide)
        if (slidePressed && collisionController.IsGrounded && Mathf.Abs(horizontalInput) > 0.01f && !isSliding)
        {
            StartCoroutine(Slide(horizontalInput));
        }

        // Если нажата кнопка остановки цепления (S), прекращаем цепление.
        if (isSlidingOnWall && stopWallPressed)
        {
            StopWallSlide();
        }

        // Цепление за стену: если персонаж касается стены, не на земле и cooldown выполнен, запускаем цепление.
        if (collisionController.IsTouchingWall && !collisionController.IsGrounded && timeSinceDetached >= wallDetachCooldown)
        {
            if (!isSlidingOnWall)
                StartWallHang();
        }

        // Приседание: если зажаты кнопки и горизонтальный ввод почти нулевой.
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && collisionController.IsGrounded && Mathf.Abs(horizontalInput) < 0.01f)
        {
            isCrouching = true;
            // Заставляем остановить вертикальное движение при приседании.
            rb.velocity = new Vector2(rb.velocity.x, 0);
        }
        else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.S))
        {
            isCrouching = false;
        }
    }

    void FixedUpdate()
    {
        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        // Если персонаж не цепляется, не в подкате и не в приседании, применяем стандартное горизонтальное движение.
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.velocity = new Vector2(horizontalInput * speed, rb.velocity.y);
        }

        // Если в режиме ускоренного скольжения (после Wall Hang), плавно изменяем вертикальную скорость до -wallSlideMaxSpeed.
        if (isSlidingOnWall && wallSlideActive && touchingWall)
        {
            float newY = Mathf.MoveTowards(rb.velocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.fixedDeltaTime);
            rb.velocity = new Vector2(rb.velocity.x, newY);
        }

        // Если цепление активно, но стена более не обнаруживается, прекращаем цепление.
        if (isSlidingOnWall && !touchingWall)
        {
            StopWallSlide();
        }
    }

    // === Функция прыжка
    void Jump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        if (isSlidingOnWall)
        {
            // При wall jump задаём горизонтальный импульс в противоположном направлении.
            rb.velocity = new Vector2((facingRight ? -1 : 1) * speed, wallJumpForce);
            StopWallSlide();
            timeSinceDetached = 0f;
        }
        jumpCount++;
    }

    // === Цепление за стену (Wall Hang / Slide)
    void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false; // Сначала режим "висения" без скольжения.
            rb.velocity = Vector2.zero;
            jumpCount = 0;
            StartCoroutine(WallHangCoroutine());
        }
    }

    IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
        {
            wallSlideActive = true; // После 2 секунд включаем режим ускоренного скольжения.
        }
    }

    void StopWallSlide()
    {
        isSlidingOnWall = false;
        wallSlideActive = false;
        timeSinceDetached = 0f;
    }

    // === Рывок (Dash)
    IEnumerator Dash()
    {
        canDash = false;
        isInvulnerable = true;

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

    // === Подкат (Slide)
    IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDirection = Mathf.Sign(moveInput);
        rb.velocity = new Vector2(slideDirection * slideSpeed, rb.velocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.velocity = Vector2.zero;
        isSliding = false;
    }

    // === Поворот (Flip)
    void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }
}