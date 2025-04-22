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
    private bool isInvulnerable = false;

    // --- Параметры подката (Slide) и приседа (Crouch)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    public float slideBoost = 1.5f;         // коэффициент резкого увеличения скорости при подкате
    private bool isSliding = false;
    private bool isCrouching = false;

    // --- Новый параметр для горизонтального импульса прыжка (если не wall jump)
    // Дополнительный горизонтальный импульс = текущая горизонтальная скорость * jumpImpulseFactor.
    public float jumpImpulseFactor = 0.2f;

    // --- Параметры цепления за стену (Wall Hang / Slide)
    public float wallHangTime = 0.5f;         // время, которое персонаж висит неподвижно после цепления
    public float wallSlideAcceleration = 10f; // ускорение скольжения по стене (ед./сек)
    public float wallSlideMaxSpeed = 5f;      // максимальная скорость скольжения
    public float wallJumpForce = 10f;         // вертикальная компонента wall jump
    public float wallJumpHorizForce = 5f;     // горизонтальная составляющая wall jump (фиксированная)
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;     // false – режим "висения", true – режим ускоренного скольжения
    public float wallDetachCooldown = 0.3f;     // минимальное время между цеплениями
    private float timeSinceDetached = 0f;
    // Сторона стены, к которой цепляемся: 1 – если стена справа; -1 – если слева.
    private int wallContactSide = 0;

    // --- Блокировка горизонтального управления после wall jump,
    // позволяющая сохранить горизонтальный импульс wall jump, не затираемый обычным вводом.
    private bool isWallJumping = false;
    public float wallJumpLockDuration = 0.2f;

    // --- Параметры гравитации при цеплении за стену:
    public float wallHangGravityScale = 0f;  // gravityScale в режиме цепления
    private float originalGravityScale;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    // --- Переменные для размеров хитбокса (для восстановления после приседа/подката)
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
                rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);
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
                    float newX = Mathf.Lerp(rb.linearVelocity.x, moveInput * speed, 5f * Time.deltaTime);
                    rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
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
                if ((wallContactSide == 1 && facingRight) || (wallContactSide == -1 && !facingRight))
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                }
                else
                {
                    rb.linearVelocity = new Vector2(-wallContactSide * wallJumpHorizForce, wallJumpForce);
                    StartCoroutine(WallJumpLockCoroutine());
                }
                StopWallSlide();
                timeSinceDetached = 0f;
                jumpCount = 0;
            }
            else
            {
                // **Добавляем горизонтальный импульс прыжка, пропорциональный текущей горизонтальной скорости.**
                float extraX = rb.linearVelocity.x * jumpImpulseFactor;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x + extraX, jumpForce);
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
            float newY = Mathf.MoveTowards(rb.linearVelocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.deltaTime);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, newY);
        }

        // --- Остальные механики (Dash, Slide, Crouch)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isSliding && !isCrouching)
        {
            // Если персонаж цепляется за стену, допускаем рывок только если он смотрит НЕ в сторону стены.
            // Условие: если isSlidingOnWall == true, то рывок разрешён только если (wallContactSide == 1 и !facingRight)
            // или (wallContactSide == -1 и facingRight). Иначе - рывок не выполняется.
            if (isSlidingOnWall)
            {
                if ((wallContactSide == 1 && !facingRight) || (wallContactSide == -1 && facingRight))
                {
                    StartCoroutine(Dash());
                }
            }
            else
            {
                StartCoroutine(Dash());
            }
        }

        // Подкат (Slide) и присед (Crouch) – логика только на земле.
        if (grounded)
        {
            // Если имеется горизонтальный ввод и нажата клавиша Ctrl – запускаем подкат.
            if (Input.GetKeyDown(KeyCode.LeftControl) && Mathf.Abs(moveInput) > 0.01f && !isSliding)
            {
                StartCoroutine(Slide(moveInput));
            }
            // Если горизонтального ввода нет, а зажаты Ctrl или S – включаем режим приседа.
            else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && Mathf.Abs(moveInput) < 0.01f)
            {
                isCrouching = true;
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.S))
            {
                isCrouching = false;
            }
        }
        else
        {
            isCrouching = false;
        }

        // Если персонаж находится в воздухе, имеет ненулевую горизонтальную скорость
        // и зажат Ctrl – при приземлении автоматически запускаем подкат.
        if (grounded && Input.GetKey(KeyCode.LeftControl) && Mathf.Abs(rb.linearVelocity.x) > 0.1f && !isSliding && !isCrouching)
        {
            StartCoroutine(Slide(rb.linearVelocity.x));
        }

        if (!isSliding && !isCrouching)
        {
            boxCollider.size = normalSize;
            boxCollider.offset = normalOffset;
        }

        if (!isSlidingOnWall)
            collisionController.ignoreFlipForWallChecks = false;
    }

    // --- Методы цепления за стену (Wall Hang / Slide)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false;   // Сначала персонаж висит неподвижно.
            rb.linearVelocity = Vector2.zero;
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
        // Удаляем принудительное обнуление горизонтальной скорости, чтобы импульс сохранялся.
        // if (collisionController.IsGrounded)
        //     rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
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
            if (!collisionController.IsGrounded)
                break;
            float currentX = Mathf.Lerp(initialVel, 0, elapsed / slideDuration);
            rb.linearVelocity = new Vector2(currentX, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }
        isSliding = false;
        if (collisionController.IsGrounded)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S))
                isCrouching = true;
        }
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