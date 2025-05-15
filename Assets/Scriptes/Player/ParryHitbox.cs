using UnityEngine;

public class ParryHitbox : MonoBehaviour
{
    // —сылка на контроллер игрока
    public CombatController combatController;

    // —войство дл€ проверки, активен ли парирующий режим
    public bool IsParrying => combatController != null && combatController.IsParrying;
}