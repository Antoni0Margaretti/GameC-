using UnityEngine;

public class EnemyParryHitbox : MonoBehaviour
{
    // ������ �� AI �����
    public MeleeEnemyAI enemyAI;

    // �������� ��� ��������, ����� �� ���� ���������� ������� � ������ ������
    public bool CanParryProjectile
    {
        get
        {
            // ������� ������� ������ ����� ������� (enabled)
            return enemyAI != null && enabled && enemyAI.enabled;
        }
    }
}