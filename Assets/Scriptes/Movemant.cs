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

        // Если персонаж цепляется за стену, но стена не обнаруживается – прекращаем цепление.
        if (isSlidingOnWall && !touchingWall)
        {
            StopWallSlide();
        }

        // Если персонаж цепляется за стену, ввод A/D используется только для смены направления.
        if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }
        // Если не цепляемся, не скользим и не приседаем – обычное движение.
        else if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Прыжок (Jump)
        // Если нажата кнопка Jump и персонаж на земле, может прыгать или цепляется за стену.
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                /* 
                   Доработанная механика wall jump:
                   Если персонаж цепляется за стену, то мы хотим, чтобы он отталкивался от неё с горизонтальным импульсом.
                   Если персонаж всё ещё смотрит в сторону стены (то есть его направление совпадает с изначальной стороной цепления),
                   то мы автоматически переворачиваем его с помощью Flip(), чтобы он смотрел от стены, 
                   а затем применяем горизонтальный импульс.
                */
                if ((wallContactSide == 1 && facingRight) || (wallContactSide == -1 && !facingRight))
                {
                    Flip();
                }
                rb.linearVelocity = new Vector2(-wallContactSide * wallJumpHorizForce, wallJumpForce);
                StopWallSlide();
                timeSinceDetached = 0f;
                jumpCount = 0;
            }
            else
            {
                // Прыжок с пола – обычный вертикальный.
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpCount++;
            }
        }

        if (grounded)
        {
            // При касании земли сбрасываем счётчик прыжков и прекращаем цепление за стеной.
            jumpCount = 0;
            StopWallSlide();
        }

        // --- Инициирование цепления за стену (Wall Hang)
        // Чтобы предотвратить повторное цепление к той же стене слишком быстро, проверяем detach cooldown.
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

        // Если включён режим ускоренного скольжения, плавно изменяем вертикальную скорость до -wallSlideMaxSpeed.
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

        // Сброс настроек для проверки стены (для корректного TransformPoint)
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
            // Отключаем воздействие гравитации, устанавливая его в значение wallHangGravityScale.
            rb.gravityScale = wallHangGravityScale;
            jumpCount = 0;
            // Сохраняем сторону стены, к которой цепляемся.
            // Если персонаж цепляется, его текущее направление определяет сторону стены.
            wallContactSide = facingRight ? 1 : -1;
            StartCoroutine(WallHangCoroutine());
        }
    }

    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
        {
            wallSlideActive = true;  // После wallHangTime переключаемся в режим ускоренного скольжения по стене.
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
        rb.linearVelocity = new Vector2(slideDirection * slideSpeed, rb.linearVelocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.linearVelocity = Vector2.zero;
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
}