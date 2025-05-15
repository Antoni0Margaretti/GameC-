//using UnityEngine;
//using System.Collections;

//public class MeleeEnemyAI : MonoBehaviour
//{
//    // ��������� ��������� ����������
//    private enum State { Pursuing, Charging, Dashing, Recovery, Stunned }
//    private State currentState = State.Pursuing;

//    [Header("Detection & Movement")]
//    [Tooltip("������������ ���������� ����������� (�� �����������) ��� �������� � �������������.")]
//    public float detectionRadius = 10f;
//    [Tooltip("���� �������������� ���������� �� ������ ������ ����� ��������, � ����� ������������ ������� ������ dashTriggerVertDistance, ���� ��������� � ����� ���������� �����.")]
//    public float dashTriggerHorizDistance = 2f;
//    [Tooltip("���� ������������ ���������� ����� ����������� � ������� ������ ����� ��������, ���������� ���������� �����.")]
//    public float dashTriggerVertDistance = 1f;

//    [Header("Movement Settings")]
//    [Tooltip("�������� �������� �������� ��� �������������.")]
//    public float moveSpeed = 3f;

//    [Header("Dash (Ryvok) Settings")]
//    [Tooltip("����� ���������� � ����� (Charging). �� ��� ����� ���� ������.")]
//    public float chargeTime = 0.5f;
//    [Tooltip("�������� �����; ����������� ������������ �������� � ������ (������������ ������������ �����������).")]
//    public float dashSpeed = 8f;
//    [Tooltip("������������ �����.")]
//    public float dashDuration = 0.3f;
//    [Tooltip("����� �������������� ����� ����� (Recovery); � ���� ������ ���� ������.")]
//    public float recoveryTime = 0.5f;
//    [Tooltip("����� ���������, ���� ����� ��� ������� ����������.")]
//    public float stunnedTime = 1f;

//    [Header("References")]
//    [Tooltip("������ �� ������. ���� �� ������, ������ ������ � ����� 'Player'.")]
//    public Transform player;

//    [Header("Invulnerability / Parry")]
//    [Tooltip("���� true, ��������� ���������� ���� (�������� �����). �������� ������������ ������������� � ����������� �� ���������.")]
//    public bool isInvulnerable { get; private set; } = true;

//    private Rigidbody2D rb;

//    void Start()
//    {
//        rb = GetComponent<Rigidbody2D>();
//        // ������� ������ �� ����, ���� ������ �� ������
//        if (player == null)
//        {
//            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
//            if (playerObj != null)
//                player = playerObj.transform;
//        }
//        currentState = State.Pursuing;
//        // �� ��������� ���� � ��������� ������������� �������� ����� (��������)
//        isInvulnerable = true;
//    }

//    void Update()
//    {
//        if (player == null)
//            return;

//        // ��� ������������� ���� �������� � ������. ����� �� ������������ �������� �� Y,
//        // ����� ��������� ��������� �� ����� (��� � ������)
//        if (currentState == State.Pursuing)
//        {
//            // ���� ����� ��������� ��� detectionRadius, ���� ����� ������ ����� (idle).
//            float horizontalDistance = Mathf.Abs(transform.position.x - player.position.x);
//            if (horizontalDistance > detectionRadius)
//                return;

//            // ���������, ��������� �� ������� ��� ����� �� ����� ����:
//            float verticalDistance = Mathf.Abs(transform.position.y - player.position.y);
//            if (horizontalDistance <= dashTriggerHorizDistance && verticalDistance <= dashTriggerVertDistance)
//            {
//                // ������� � ����� ������� �����
//                StartCoroutine(ChargeRoutine());
//            }
//        }
//    }

//    void FixedUpdate()
//    {
//        // � ��������� ������������� ��������� �������� � ������ ������ �� �����������,
//        // �������� Y ���������� (��� ��� ���� �� ����� ������)
//        if (currentState == State.Pursuing)
//        {
//            Vector2 targetPos = new Vector2(player.position.x, transform.position.y);
//            Vector2 newPos = Vector2.MoveTowards(rb.position, targetPos, moveSpeed * Time.fixedDeltaTime);
//            rb.MovePosition(newPos);
//        }
//        // ��������� �������� (����� � �.�.) ����������� ����������
//    }

//    /// <summary>
//    /// �������� ���������� ����� (Charging). �� ����� chargeTime ���� ���������� ��������.
//    /// �� ��������� chargeTime, ���� ������� ���������, ���������� �����.
//    /// </summary>
//    IEnumerator ChargeRoutine()
//    {
//        // �������� ���������� ����� � �������
//        if (currentState != State.Pursuing)
//            yield break;
//        currentState = State.Charging;
//        // �� ����� ������� ���� ������ � ����������� �� ����������
//        isInvulnerable = false;
//        // (����� ��������� �������� ����������)
//        yield return new WaitForSeconds(chargeTime);
//        // �������� �����, ���� ����� �� ��� � ���� �����
//        StartCoroutine(DashRoutine());
//    }

//    /// <summary>
//    /// �������� ����� (Dashing). �� ����� ����� ���� ������� � ����������� ������ � ��������.
//    /// ����� ���������� � ������ ��������� ���������������� (����������� �������������� �� ������).
//    /// </summary>
//    IEnumerator DashRoutine()
//    {
//        currentState = State.Dashing;
//        // �� ����� ����� ���� �������� (�� �������� ����)
//        isInvulnerable = true;

//        // ������������ ����������� �� ���������� � ������ (� ������ ���������)
//        Vector2 dashDirection = ((Vector2)player.position - rb.position).normalized;
//        float timer = 0f;
//        while (timer < dashDuration)
//        {
//            timer += Time.deltaTime;
//            rb.linearVelocity = dashDirection * dashSpeed;
//            yield return null;
//        }
//        rb.linearVelocity = Vector2.zero;
//        // ����� ����� ��������� � ���� ��������������
//        StartCoroutine(RecoveryRoutine());
//    }

//    /// <summary>
//    /// ������������ �������������� (Recovery). � ������� recoveryTime ���� �� ���������
//    /// � ������� ��������.
//    /// </summary>
//    IEnumerator RecoveryRoutine()
//    {
//        currentState = State.Recovery;
//        // �������������� � ���� ������
//        isInvulnerable = false;
//        yield return new WaitForSeconds(recoveryTime);
//        // ����� �������������� ������������ � �������������, ����� ���������� ����� ���������� (����������)
//        currentState = State.Pursuing;
//        isInvulnerable = true;
//    }

//    /// <summary>
//    /// �����, ������� ������ ���� ������, ���� ����� ����� ��� ���������� �������.
//    /// � ���� ������ ��������� ������� �������� � ��������� � ��������� ���������.
//    /// </summary>
//    public void OnParried()
//    {
//        // ���� ���� � ������ ������� ��� ����� � ��������� � ��������� ���������
//        if (currentState == State.Charging || currentState == State.Dashing)
//        {
//            StopAllCoroutines();
//            currentState = State.Stunned;
//            isInvulnerable = false; // ���������� � ������
//            rb.linearVelocity = Vector2.zero;
//            StartCoroutine(StunnedRoutine());
//        }
//    }

//    /// <summary>
//    /// �������� ��������� (Stunned). � ������� stunnedTime ���� ���������� � ������.
//    /// �� ��������� ��������� ������������ � �������������.
//    /// </summary>
//    IEnumerator StunnedRoutine()
//    {
//        yield return new WaitForSeconds(stunnedTime);
//        currentState = State.Pursuing;
//        isInvulnerable = true;
//    }

//    // ��� �������: ������������ ����, � ������� ���������� ������ ����� (dash trigger).
//    void OnDrawGizmosSelected()
//    {
//        Gizmos.color = Color.yellow;
//        Vector3 pos = transform.position;
//        // �������� �������������, �������������� � ������� ����������,
//        // ������ �������� �� X ����� dashTriggerHorizDistance*2, � �� Y � dashTriggerVertDistance*2
//        Gizmos.DrawWireCube(pos, new Vector3(dashTriggerHorizDistance * 2f, dashTriggerVertDistance * 2f, 0));
//    }
//}