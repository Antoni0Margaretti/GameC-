using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // === Компоненты (назначьте collisionController через Inspector, либо убедитесь, что он находится в том же объекте)
    [Header("Components")]
    public CollisionController collisionController;
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

    // === Цепление / скольжение за стеной
    [Header("Wall Clinging")]
    public float wallHangTime = 2f;          // Время, которое персонаж висит неподвижно (2 секунды)
    public float wallSlideAcceleration = 10f;  // Ускорение скольжения по стене (ед/сек)
    public float wallSlideMaxSpeed = 5f;       // Максимальная скорость скольжения (абсолютное значение)
    public float wallJumpForce = 10f;          // Сила отталкивания при wall jump
    public float wallDetachCooldown = 0.3f;    // время перед новым цеплением
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;      // false – режим "висения", true – режим ускоренного скольжения
    private float timeSinceDetached = 0f;

    // === Направление
    [Header("Direction")]
    public bool facingRight = true;

    // === Входные данные (они берутся напрямую из Input, чтобы изменения в Inspector влияли на поведение)
    private float horizontalInput = 0f;
    private bool jumpPressed = false;
    private bool dashPressed = false;
    private bool slidePressed = false;
    private bool stopWallPressed = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        if (collisionController == null)
            collisionController = GetComponent<CollisionController>();
        // Ниже мы не меняем значения публичных полей – они задаются в Inspector!
    }

    void Update()
    {
        // Собираем входные данные
        horizontalInput = Input.GetAxis("Horizontal");
        jumpPressed = Input.GetButtonDown("Jump");
        dashPressed = Input.GetKeyDown(KeyCode.LeftShift);
        slidePressed = Input.GetKeyDown(KeyCode.LeftControl);
        stopWallPressed = Input.GetKeyDown(KeyCode.S);

        // Для отладки: можно видеть значения параметров, чтобы убедиться, что Inspector их берет
        Debug.Log($"[PlayerController] wallHangTime: {wallHangTime}, dashDuration: {dashDuration}, slideDuration: {slideDuration}");

        // Если цепляемся за стену – ввод A/D служит только для смены направления
        if (isSlidingOnWall)
        {
            if (horizontalInput > 0 && !facingRight)
                Flip();
            else if (horizontalInput < 0 && facingRight)
                Flip();
        }

        // Обрабатываем прыжок (мгновенно)
        if (jumpPressed && (collisionController.IsGrounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            Jump();
        }

        // Если dash нажата и можем выполнять рывок
        if (dashPressed && canDash && !isSliding && !isCrouching)
        {
            StartCoroutine(Dash());
        }

        // Если нажата кнопка подката и игрок на земле с ненулевым вводом
        if (slidePressed && collisionController.IsGrounded && Mathf.Abs(horizontalInput) > 0.01f && !isSliding)
        {
            StartCoroutine(Slide(horizontalInput));
        }

        // Если нажата кнопка Stop Wall (S), а персонаж цепляется, прекращаем цепление
        if (isSlidingOnWall && stopWallPressed)
        {
            StopWallSlide();
        }

        // Запускаем цепление за стену, если игрок касается стены, не на земле и прошло нужное время перед новым цеплением.
        if (collisionController.IsTouchingWall && !collisionController.IsGrounded && timeSinceDetached >= wallDetachCooldown)
        {
            // Если еще не в цеплении – начинаем
            if (!isSlidingOnWall)
                StartWallHang();
        }

        // Обрабатываем приседание: если зажата кнопка и горизонтальный ввод почти нулевой.
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && collisionController.IsGrounded && Mathf.Abs(horizontalInput) < 0.01f)
        {
            isCrouching = true;
            rb.velocity = new Vector2(rb.velocity.x, 0); // остановка по вертикали при приседании
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

        // Если игрок не цепляется и не в приседании/подкате – стандартное движение.
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.velocity = new Vector2(horizontalInput * speed, rb.velocity.y);
        }

        // Если в режиме ускоренного скольжения (после Wall Hang) – плавное изменение вертикальной скорости
        if (isSlidingOnWall && wallSlideActive && touchingWall)
        {
            float newY = Mathf.MoveTowards(rb.velocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.fixedDeltaTime);
            rb.velocity = new Vector2(rb.velocity.x, newY);
        }
    }

    // ПРИЖОПКИ
    void Jump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        if (isSlidingOnWall)
        {
            // При прыжке с цепления осуществляется wall jump — горизонтальный импульс в противоположном направлении.
            rb.velocity = new Vector2((facingRight ? -1 : 1) * speed, wallJumpForce);
            StopWallSlide();
            timeSinceDetached = 0f;
        }
        jumpCount++;
    }

    // ЦЕПЛЕНИЕ ЗА СТЕНОЙ (Wall Hang / Slide)
    void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false; // Сначала режим "висения" (без скольжения)
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
            wallSlideActive = true; // После 2 секунды переключаемся в режим Скользить
        }
    }

    void StopWallSlide()
    {
        isSlidingOnWall = false;
        wallSlideActive = false;
        timeSinceDetached = 0f;
    }

    // РЫВОК (Dash)
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

    // ПОДКАТ (Slide)
    IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDirection = Mathf.Sign(moveInput);
        rb.velocity = new Vector2(slideDirection * slideSpeed, rb.velocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.velocity = Vector2.zero;
        isSliding = false;
    }

    // ПОВОРОТ (Flip) – меняет направление движения.
    void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}