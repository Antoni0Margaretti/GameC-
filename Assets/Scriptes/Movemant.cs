using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // === Компоненты
    [Header("Components")]
    public CollisionController collisionController; // Нужен для определения касания земли и стен (назначьте через Inspector)
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // === Настройки хитбокса (если требуются динамические эффекты)
    [Header("Hitbox Settings")]
    [SerializeField] private Vector2 normalSize;   // Сохраняется из BoxCollider2D в Start()
    [SerializeField] private Vector2 normalOffset; // Сохраняется из BoxCollider2D в Start()

    // === Настройки получения урона
    [Header("Damage & Invulnerability")]
    public bool isInvulnerable = false;

    // === Движение
    [Header("Movement")]
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount = 0;

    // === Рывок (Dash)
    [Header("Dash")]
    public float dashDistance = 5f;
    public float dashDuration = 0.2f;  // длительность рывка в секундах
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

    // === Цепление за стену (Wall Hang / Slide)
    [Header("Wall Clinging")]
    public float wallHangTime = 2.0f;          // Время, которое персонаж висит неподвижно (2 секунды)
    public float wallSlideAcceleration = 10f;  // Ускорение скольжения по стене (единиц/сек)
    public float wallSlideMaxSpeed = 5f;       // Максимальная скорость скольжения (абсолютное значение)
    public float wallJumpForce = 10f;          // Сила отталкивания при wall jump
    public float wallDetachCooldown = 0.3f;    // Время, в которое нельзя повторно зацепиться за стену
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;      // false - режим "висения", true - режим ускоренного скольжения
    private float timeSinceDetached = 0f;

    // === Направление
    [Header("Direction")]
    public bool facingRight = true;

    // === Входные данные (сохраняются для использования в FixedUpdate)
    private float horizontalInput = 0f;
    private bool jumpPressed = false;
    private bool dashPressed = false;
    private bool slidePressed = false;
    private bool stopWallPressed = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (collisionController == null)
            collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        // Сохраняем исходные размеры хитбокса для восстановления (если требуется)
        normalSize = boxCollider.size;
        normalOffset = boxCollider.offset;
    }

    void Update()
    {
        // Собираем входные данные
        horizontalInput = Input.GetAxis("Horizontal");
        jumpPressed = Input.GetButtonDown("Jump");
        dashPressed = Input.GetKeyDown(KeyCode.LeftShift);
        slidePressed = Input.GetKeyDown(KeyCode.LeftControl);
        stopWallPressed = Input.GetKeyDown(KeyCode.S);

        // Обновляем таймер для wallDetachCooldown
        timeSinceDetached += Time.deltaTime;

        // Если игрок находится в состоянии цепления за стену и нажата кнопка S — остановка цепления.
        if (isSlidingOnWall && stopWallPressed)
        {
            StopWallSlide();
        }

        // Обработка ввода прыжка (сразу вызываем Jump, так как требуется мгновенная реакция)
        if (jumpPressed && (collisionController.IsGrounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            Jump();
        }

        // Если dashPressed и условия выполнены, запускаем Dash
        if (dashPressed && canDash && !isSliding && !isCrouching)
        {
            StartCoroutine(Dash());
        }

        // Если slidePressed и игрок на земле с ненулевым горизонтальным вводом, запускаем Slide
        if (slidePressed && collisionController.IsGrounded && Mathf.Abs(horizontalInput) > 0.01f && !isSliding)
        {
            StartCoroutine(Slide(horizontalInput));
        }

        // Если удерживается кнопка приседания и горизонтальный ввод почти нулевой, включаем crouch
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) &&
             collisionController.IsGrounded && Mathf.Abs(horizontalInput) < 0.01f)
        {
            isCrouching = true;
            // Здесь мы можем остановить движение по Y при приседании
            rb.velocity = new Vector2(rb.velocity.x, 0);
        }
        else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.S))
        {
            isCrouching = false;
        }

        // Если в состоянии цепления за стену, горизонтальный ввод используется для смены направления только
        if (isSlidingOnWall)
        {
            if (horizontalInput > 0 && !facingRight)
                Flip();
            else if (horizontalInput < 0 && facingRight)
                Flip();
        }
    }

    void FixedUpdate()
    {
        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        // Если в цеплении, но стена отсутствует – прекращаем цепление.
        if (isSlidingOnWall && !touchingWall)
        {
            StopWallSlide();
        }

        // Если не цепление и не в подкате/приседании, выполняем стандартное движение.
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.velocity = new Vector2(horizontalInput * speed, rb.velocity.y);
        }

        // Если включён режим ускоренного скольжения (после Wall Hang) и игрок касается стены,
        // плавно изменяем вертикальную скорость до -wallSlideMaxSpeed.
        if (isSlidingOnWall && wallSlideActive && touchingWall)
        {
            float newY = Mathf.MoveTowards(rb.velocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.fixedDeltaTime);
            rb.velocity = new Vector2(rb.velocity.x, newY);
        }
    }

    // === Функция прыжка
    void Jump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        if (isSlidingOnWall)
        {
            // При wall jump отталкиваемся в противоположном направлении.
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
            wallSlideActive = false;  // Сначала режим "висения"
            rb.velocity = Vector2.zero;
            jumpCount = 0;  // Сброс счетчика прыжков
            StartCoroutine(WallHangCoroutine());
        }
    }

    IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
        {
            wallSlideActive = true;  // После 2 секунд включаем режим ускоренного скольжения
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

        // Сохраняем текущую вертикальную скорость.
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

    // === Поворот персонажа (Flip)
    void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }
}
