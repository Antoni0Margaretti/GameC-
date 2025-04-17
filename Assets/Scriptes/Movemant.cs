using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // --- Компоненты
    private CollisionController collisionController;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // --- Для динамических хитбоксов (обработка размеров при приседе/подкате – можно дорабатывать отдельно)
    private Vector2 normalSize, normalOffset;

    // --- Флаг неуязвимости (например, для проверки получения урона)
    private bool isInvulnerable = false;

    // --- Параметры движения
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- Параметры рывка (Dash)
    // (Оставляем эту механику – если потребуется, её можно доработать отдельно)
    public float dashDistance = 5f;
    public float dashDuration = 0.2f;  // Длительность рывка (не мгновенная телепортация)
    public float dashCooldown = 1f;    // Период, когда новый dash недоступен
    private bool canDash = true;

    // --- Параметры подката (Slide) и приседания
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;
    private bool isCrouching = false;

    // --- Параметры цепления и скольжения по стене (Wall Hang/Slide)
    public float wallHangTime = 0.5f;    // Время, прежде чем начать скользить по стене
    public float wallSlideSpeed = 2f;    // Нисходящая скорость скольжения по стене
    public float wallJumpForce = 10f;    // Сила отталкивания при wall jump
    private bool isSlidingOnWall = false;
    public float wallDetachCooldown = 0.3f;   // Период, в течение которого повторное цепление недоступно
    private float timeSinceDetached = 0f;

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
        float moveInput = Input.GetAxis("Horizontal");

        // Кэширование состояний коллизий
        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        // Если в режиме wall slide, но теперь стена не обнаруживается – прекращаем цепление
        if (isSlidingOnWall && !touchingWall)
        {
            StopWallSlide();
        }

        // --- Обычное движение
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }
        else if (isSlidingOnWall)
        {
            // Даже при цеплении отслеживаем ввод для смены направления
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Прыжок
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            if (isSlidingOnWall)
            {
                // Wall jump: отталкиваемся от стены — скорость по горизонтали задаётся так, чтобы оторваться от стены
                rb.velocity = new Vector2((facingRight ? -1 : 1) * speed, wallJumpForce);
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
        // Если персонаж касается стены, не на земле, ввод есть и он совпадает с направлением "смотра",
        // а также прошло достаточно времени с момента предыдущего wall jump, запускаем цепление.
        if (touchingWall && !grounded &&
            Mathf.Abs(moveInput) > 0.01f &&
            ((facingRight && moveInput > 0) || (!facingRight && moveInput < 0)) &&
            timeSinceDetached >= wallDetachCooldown)
        {
            StartWallHang();
        }
        // Можно добавить принудительное прекращение цепления по нажатию клавиши (например, S)
        if (Input.GetKeyDown(KeyCode.S) && isSlidingOnWall)
        {
            StopWallSlide();
        }

        // --- Рывок (Dash) – оставляем в оптимизированном виде (работает отдельно)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isSliding && !isCrouching)
        {
            StartCoroutine(Dash(moveInput));
        }

        // --- Подкат и приседание
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

        // Если не в подкате или приседе – восстанавливаем стандартный хитбокс (динамическую обработку можно расширять отдельно)
        if (!isSliding && !isCrouching)
        {
            boxCollider.size = normalSize;
            boxCollider.offset = normalOffset;
        }

        // Сообщаем CollisionController, что если цепляемся за стену, то зона проверки стены фиксирована
        collisionController.ignoreFlipForWallChecks = isSlidingOnWall;
    }

    // --- Методы для цепления за стену (Wall Hang/Slide)

    // Запускаем wall hang, если не активен wall slide
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            rb.velocity = Vector2.zero;
            jumpCount = 0;  // Сброс прыжкового счётчика для возможности двойного прыжка
            StartCoroutine(WallHangCoroutine());
        }
    }

    // Ожидаем wallHangTime секунд, после чего, если цепление всё ещё активно, запускаем wall slide
    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
        {
            BeginWallSlide();
        }
    }

    // Переход в режим wall slide: задаём постоянную нисходящую скорость (без изменения горизонтали)
    private void BeginWallSlide()
    {
        rb.velocity = new Vector2(rb.velocity.x, -wallSlideSpeed);
    }

    // Завершаем цепление
    private void StopWallSlide()
    {
        isSlidingOnWall = false;
    }

    // --- Рывок (Dash)
    private IEnumerator Dash(float moveInput)
    {
        canDash = false;
        isInvulnerable = true;

        // Если в воздухе, сбрасываем вертикальную скорость
        if (!collisionController.IsGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0);
        }

        // Если цепляемся за стену, уменьшаем effectiveDashDistance (например, вдвое)
        float effectiveDashDistance = dashDistance;
        if (isSlidingOnWall)
        {
            effectiveDashDistance = dashDistance * 0.5f;
        }

        float dashSpeed = effectiveDashDistance / dashDuration;
        // Если есть горизонтальный ввод, используем его; иначе – направление, куда смотрит персонаж
        float dashDirection = (Mathf.Abs(moveInput) > 0.01f) ? Mathf.Sign(moveInput) : (facingRight ? 1 : -1);

        // Отключаем гравитацию для чисто горизонтального перемещения
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;
        rb.velocity = new Vector2(dashDirection * dashSpeed, 0);

        yield return new WaitForSeconds(dashDuration);

        rb.velocity = Vector2.zero;
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
        rb.velocity = new Vector2(slideDirection * slideSpeed, rb.velocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.velocity = Vector2.zero;
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