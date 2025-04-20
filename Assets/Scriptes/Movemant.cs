using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // --- Компоненты
    private CollisionController collisionController;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // --- Параметры для динамических хитбоксов
    private Vector2 normalSize, normalOffset;

    // --- Флаг неуязвимости (для логики получения урона)
    private bool isInvulnerable = false;

    // --- Параметры движения
    public float speed = 5f;
    public float jumpForce = 10f;            // сила прыжка с пола (вертикальная компонента обычного прыжка)
    public int maxJumps = 2;                 // для расчёта, хотя логика рассчитывается динамически
    private int jumpCount;

    // --- Параметры рывка (Dash)
    public float dashDistance = 5f;
    public float dashDuration = 0.2f;        // длительность рывка (не мгновенная телепортация)
    public float dashCooldown = 1f;
    private bool canDash = true;

    // --- Параметры подката (Slide)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // --- Приседание – для dash не допускается
    private bool isCrouching = false;

    // --- Параметры цепления за стену (Wall Hang/Slide)
    public float wallHangTime = 0.5f;         // время, которое персонаж висит неподвижно сразу после цепления за стену
    public float wallSlideAcceleration = 10f; // ускорение скольжения по стене (ед/сек)
    public float wallSlideMaxSpeed = 5f;      // максимальная скорость скольжения (абсолютное значение)
    public float wallJumpForce = 10f;         // вертикальная компонента wall jump
    public float wallJumpHorizForce = 5f;     // горизонтальный импульс для wall jump
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;     // false – режим "висения", true – режим ускоренного скольжения
    public float wallDetachCooldown = 0.3f;   // время, в течение которого нельзя повторно зацепиться за стену
    private float timeSinceDetached = 0f;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        // Сохраняем исходные параметры хитбокса
        normalSize = boxCollider.size;
        normalOffset = boxCollider.offset;
    }

    void Update()
    {
        timeSinceDetached += Time.deltaTime;
        float moveInput = Input.GetAxis("Horizontal");

        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        // Если цепление активно, но стена ушла – прекращаем цепление.
        if (isSlidingOnWall && !touchingWall)
        {
            StopWallSlide();
        }

        // Если цепляется за стену, ввод A/D используется только для смены направления.
        if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }
        // Стандартное движение, когда не цепляемся, не в подкате и не в приседе.
        else if (!isSlidingOnWall && !isSliding && !isCrouching)
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
            if (isSlidingOnWall)
            {
                // При цеплении: если ходовой ввод направлен в ту же сторону, что и стена,
                // выполняется обычный (вертикальный) прыжок.
                if (Mathf.Abs(moveInput) > 0.01f && Mathf.Sign(moveInput) == (facingRight ? 1 : -1))
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                    jumpCount++;
                }
                else
                {
                    // Иначе выполняется wall jump: диагональный импульс (отталкивание от стены).
                    rb.linearVelocity = new Vector2((facingRight ? 1 : -1) * wallJumpHorizForce, wallJumpForce);
                    jumpCount = 0;  // сброс счетчика, чтобы после wall jump оставался один прыжок (если он понадобится)
                }
                StopWallSlide();
                timeSinceDetached = 0f;
            }
            else
            {
                // Прыжок с пола: вертикальный.
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpCount++;
            }
        }
        if (grounded)
        {
            // При касании земли сбрасываем счетчик прыжков.
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

        // Если активен режим ускоренного скольжения, плавно изменяем вертикальную скорость до -wallSlideMaxSpeed.
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

        // Корректная обработка TransformPoint в CollisionController.
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

            // Отключаем гравитацию, чтобы скрипт управлял вертикальным движением.
            rb.gravityScale = 0;

            jumpCount = 0;  // Сброс счётчика прыжков для возможности прыжка с поверхности.
            StartCoroutine(WallHangCoroutine());
        }
    }

    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
        {
            wallSlideActive = true;  // После wallHangTime переключаемся в режим ускоренного скольжения.
        }
    }

    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        wallSlideActive = false;
        timeSinceDetached = 0f;

        // Восстанавливаем гравитацию (в данном случае предполагается значение 1)
        rb.gravityScale = 1f;
    }

    // --- Рывок (Dash)
    private IEnumerator Dash()
    {
        canDash = false;
        isInvulnerable = true;

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