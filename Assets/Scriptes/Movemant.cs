using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Переменные для движения
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // Переменные для стены
    public float wallHangTime = 0.5f;    // Время висения на стене
    public float wallSlideSpeed = 2f;   // Скорость скольжения по стене
    public float wallSlideAcceleration = 5f; // Ускорение при скольжении
    private bool isTouchingWall;
    private bool isHangingOnWall;
    private bool isSlidingFromWall;

    // Прочие компоненты
    private Rigidbody2D rb;

    // Слои для проверки
    public LayerMask groundLayer;
    public LayerMask wallLayer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Бег
        float moveInput = Input.GetAxis("Horizontal");
        if (!isHangingOnWall && !isSlidingFromWall) // Если персонаж не на стене, движение включено
        {
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);

            // Поворот персонажа
            if (moveInput > 0)
            {
                transform.localScale = new Vector3(1, 1, 1);
            }
            else if (moveInput < 0)
            {
                transform.localScale = new Vector3(-1, 1, 1);
            }
        }

        // Прыжок
        if (Input.GetButtonDown("Jump") && (IsGrounded() || jumpCount < maxJumps || isHangingOnWall))
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);

            // Если персонаж висит на стене, отпускаем её
            if (isHangingOnWall || isSlidingFromWall)
            {
                isHangingOnWall = false;
                isSlidingFromWall = false;
            }

            jumpCount++;
        }

        // Сброс прыжков при касании земли
        if (IsGrounded())
        {
            jumpCount = 0;
        }

        // Логика стены: цепляние, висение и скольжение
        isTouchingWall = IsTouchingWall();
        if (isTouchingWall && !IsGrounded() && moveInput != 0) // Проверка касания стены и направления
        {
            StartCoroutine(HangOnWall());
        }
        else if (isSlidingFromWall)
        {
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y + wallSlideAcceleration * Time.deltaTime);
        }

        // Отцепление от стены при нажатии S
        if (Input.GetKeyDown(KeyCode.S) && (isHangingOnWall || isSlidingFromWall))
        {
            isHangingOnWall = false;
            isSlidingFromWall = false;
        }
    }

    // Цепляние за стену
    private System.Collections.IEnumerator HangOnWall()
    {
        isHangingOnWall = true;
        rb.velocity = new Vector2(0, 0); // Персонаж полностью останавливается на стене
        yield return new WaitForSeconds(wallHangTime); // Время висения на стене

        isHangingOnWall = false;
        isSlidingFromWall = true; // Персонаж начинает скользить
        rb.velocity = new Vector2(rb.velocity.x, -wallSlideSpeed); // Начальная скорость скольжения
    }

    // Проверка на землю
    private bool IsGrounded()
    {
        Vector2 position = transform.position;
        float radius = 0.2f;
        return Physics2D.OverlapCircle(position, radius, groundLayer);
    }

    // Проверка на стену
    private bool IsTouchingWall()
    {
        Vector2 position = transform.position;
        float radius = 0.2f;
        return Physics2D.OverlapCircle(position, radius, wallLayer);
    }
}

