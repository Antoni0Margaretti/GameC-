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
    // Дополнительный импульс пропорционален текущей горизонтальной скорости.
    public float jumpImpulseFactor = 0.2f;

    // --- Параметры цепления за стену (Wall Hang / Slide)
    public float wallHangTime = 0.5f;         // время, которое персонаж висит неподвижно после цепления
    public float wallSlideAcceleration = 10f; // ускорение скольжения по стене (ед./сек)
    public float wallSlideMaxSpeed = 5f;      // максимальная скорость скольжения
    public float wallJumpForce = 10f;         // вертикальная компонента wall jump
    public float wallJumpHorizForce = 5f;     // горизонтальная составляющая wall jump (фиксированная)
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;     // false – режим "висения", true – режим ускоренного скольжения
    public float wallDetachCooldown = 0.3f;     // минимум времени между цеплениями
    private float timeSinceDetached = 0f;
    // Сторона стены, к которой цепляемся: 1 (если стена справа) или -1 (если слева).
    private int wallContactSide = 0;

    // --- Блокировка горизонтального управления после wall jump
    private bool isWallJumping = false;
    public float wallJumpLockDuration = 0.2f;

    // --- Параметры гравитации при цеплении за стену
    public float wallHangGravityScale = 0f;  // gravityScale во время цепления
    private float originalGravityScale;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    // --- Переменные для размеров хитбокса (для восстановления после подката/приседа)
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

        // Если персонаж цепляется за стену и клавиши отцепления нажаты, отцепляем.
        if ((Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.S)) && isSlidingOnWall)
        {
            StopWallSlide();
        }
        // Если персонаж цепляется за стену, но стена исчезает, прекращаем цепление.
        if (isSlidingOnWall && !touchingWall)
        {
            StopWallSlide();
        }

        // Если в режиме цепления – игнорируем изменение направления для коллизии.
        if (isSlidingOnWall)
        {
            collisionController.ignoreFlipForWallChecks = true;
        }
        else
        {
            collisionController.ignoreFlipForWallChecks = false;
        }

        // --- Обработка горизонтального движения (если не подкат, не присед и не блокирован wall jump)
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
        // Если на земле и зажаты Ctrl или S – приседаем.
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && grounded)
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
            isCrouching = true;
        }

        // (Удалён автоматический Flip внутри блока wall hang, чтобы поворот не отцеплял цепление.)

        // --- Прыжок
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                // При цеплении за стену: если персонаж смотрит лицом к стене – вертикальный прыжок;
                // если смотрит от стены – wall jump с фиксированным горизонтальным импульсом.
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
                // Каждый прыжок (если не wall jump) получает дополнительный горизонтальный импульс,
                // пропорциональный текущей горизонтальной скорости.
                float extraX = rb.velocity.x * jumpImpulseFactor;
                rb.velocity = new Vector2(rb.velocity.x + extraX, jumpForce);
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

        // Подкат (Slide) и присед (Crouch):
        if (grounded)
        {
            // Если есть горизонтальный ввод и нажата клавиша Ctrl – запускаем подкат.
            if (Input.GetKeyDown(KeyCode.LeftControl) && Mathf.Abs(moveInput) > 0.01f && !isSliding)
            {
                StartCoroutine(Slide(moveInput));
            }
            // Если ввода нет, но зажаты Ctrl или S – режим приседа.
            else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && Mathf.Abs(moveInput) < 0.01f)
            {
                isCrouching = true;
                rb.velocity = new Vector2(0, rb.velocity.y);
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

        // Если в воздухе, а горизонтальная скорость ненулевая и зажат Ctrl,
        // то при приземлении автоматически запускается подкат.
        if (grounded && Input.GetKey(KeyCode.LeftControl) && Mathf.Abs(rb.velocity.x) > 0.1f && !isSliding && !isCrouching)
        {
            StartCoroutine(Slide(rb.velocity.x));
        }

        if (!isSliding && !isCrouching)
        {
            boxCollider.size = normalSize;
            boxCollider.offset = normalOffset;
        }

        // Если не в режиме wall hang, не устанавливаем флаг для игнорирования поворота.
        if (!isSlidingOnWall)
            collisionController.ignoreFlipForWallChecks = false;
    }

    // --- Методы цепления за стену (Wall Hang / Slide)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false;   // сначала персонаж висит неподвижно
            rb.velocity = Vector2.zero;
            rb.gravityScale = wallHangGravityScale;
            jumpCount = 0;
            // Фиксируем сторону цепления по текущему направлению.
            wallContactSide = facingRight ? 1 : -1;
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
            rb.velocity = new Vector2(0, rb.velocity.y);
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
        isInvulnerable = false;
    }

    // --- Подкат (Slide)
    /*  
         Доработанный подкат:
         • Если персонаж имеет горизонтальную скорость на земле, подкат начинается с резкого увеличения скорости 
           (с коэффициентом slideBoost), затем скорость плавно уменьшается до нуля.
         • Если в процессе подката персонаж отрывается от земли – подкат прерывается, 
           и накопленный импульс сохраняется.
         • Если подкат завершается на земле и Ctrl/S всё ещё зажаты – переключаемся в режим приседа.
    */
    private IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDirection = Mathf.Sign(moveInput);
        float initialVel = slideDirection * slideSpeed * slideBoost;
        rb.velocity = new Vector2(initialVel, rb.velocity.y);
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            if (!collisionController.IsGrounded)
            {
                break;
            }
            float currentX = Mathf.Lerp(initialVel, 0, elapsed / slideDuration);
            rb.velocity = new Vector2(currentX, rb.velocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }
        isSliding = false;
        if (collisionController.IsGrounded)
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S))
            {
                isCrouching = true;
            }
        }
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