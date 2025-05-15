using UnityEngine;

public class ParryHitbox : MonoBehaviour
{
    // ������ �� ���������� ������
    public CombatController combatController;

    // �������� ��� ��������, ������� �� ���������� �����
    public bool IsParrying => combatController != null && combatController.IsParrying;
}