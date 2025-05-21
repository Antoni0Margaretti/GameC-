using UnityEngine;
using System.Collections.Generic;

public enum EnemyActionType { Walk, Jump, AirControl, EvasionDash, StepOver }

public class EnemyAction
{
    public EnemyActionType Type;
    public Vector2 Direction;
    public float Duration;
    public float Force;
}

public class EnemyState
{
    public Vector2 Position;
    public Vector2 Velocity;
    public bool IsGrounded;
    public bool DashUsed;
    public bool JumpUsed;
}

public class ActionBasedPathfinder : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float jumpForce = 7f;
    public float evasionDashSpeed = 14f;
    public float evasionDashDuration = 0.12f;
    public float stepHeight = 0.3f;
    public float stepUpSpeed = 10f;
    public LayerMask groundLayer;

    public List<EnemyAction> FindActionPath(Vector2 start, Vector2 target, EnemyState initialState)
    {
        // Пример: BFS по состояниям (для реального проекта лучше A*)
        Queue<(EnemyState, List<EnemyAction>)> queue = new Queue<(EnemyState, List<EnemyAction>)>();
        HashSet<string> visited = new HashSet<string>();
        queue.Enqueue((initialState, new List<EnemyAction>()));

        while (queue.Count > 0)
        {
            var (state, actions) = queue.Dequeue();
            if (Vector2.Distance(state.Position, target) < 0.5f)
                return actions;

            foreach (var action in GenerateActions(state))
            {
                EnemyState next = SimulateAction(state, action);


                string stateKey = $"{state.Position.x:F2},{state.Position.y:F2},{state.Velocity.x:F2},{state.Velocity.y:F2},{state.IsGrounded},{state.DashUsed},{state.JumpUsed}";
                if (!visited.Contains(stateKey))
                {
                    visited.Add(stateKey);
                    var newActions = new List<EnemyAction>(actions) { action };
                    queue.Enqueue((next, newActions));
                }

            }
        }
        return null;
    }

    public List<EnemyAction> GenerateActions(EnemyState state)
    {
        var actions = new List<EnemyAction>();
        if (state.IsGrounded)
        {
            actions.Add(new EnemyAction { Type = EnemyActionType.Walk, Direction = Vector2.right, Duration = 0.1f, Force = moveSpeed });
            actions.Add(new EnemyAction { Type = EnemyActionType.Walk, Direction = Vector2.left, Duration = 0.1f, Force = moveSpeed });
        }
        if (state.IsGrounded && !state.JumpUsed)
            actions.Add(new EnemyAction { Type = EnemyActionType.Jump, Direction = Vector2.up, Duration = 0.1f, Force = jumpForce });
        if (!state.IsGrounded)
        {
            actions.Add(new EnemyAction { Type = EnemyActionType.AirControl, Direction = Vector2.right, Duration = 0.1f, Force = moveSpeed });
            actions.Add(new EnemyAction { Type = EnemyActionType.AirControl, Direction = Vector2.left, Duration = 0.1f, Force = moveSpeed });
        }
        if (!state.DashUsed)
        {
            actions.Add(new EnemyAction { Type = EnemyActionType.EvasionDash, Direction = Vector2.right, Duration = evasionDashDuration, Force = evasionDashSpeed });
            actions.Add(new EnemyAction { Type = EnemyActionType.EvasionDash, Direction = Vector2.left, Duration = evasionDashDuration, Force = evasionDashSpeed });
        }
        if (state.IsGrounded)
            actions.Add(new EnemyAction { Type = EnemyActionType.StepOver, Direction = Vector2.right, Duration = stepHeight / stepUpSpeed, Force = stepUpSpeed });
        return actions;
    }

    public EnemyState SimulateAction(EnemyState from, EnemyAction action)
    {
        EnemyState next = new EnemyState
        {
            Position = from.Position,
            Velocity = from.Velocity,
            IsGrounded = from.IsGrounded,
            DashUsed = from.DashUsed,
            JumpUsed = from.JumpUsed
        };

        switch (action.Type)
        {
            case EnemyActionType.Walk:
                next.Position += action.Direction * action.Force * action.Duration;
                break;
            case EnemyActionType.Jump:
                if (!from.JumpUsed && from.IsGrounded)
                {
                    next.Velocity = new Vector2(next.Velocity.x, action.Force);
                    next.IsGrounded = false;
                    next.JumpUsed = true;
                }
                break;
            case EnemyActionType.AirControl:
                if (!from.IsGrounded)
                {
                    next.Velocity = new Vector2(
                        Mathf.MoveTowards(next.Velocity.x, action.Direction.x * action.Force, action.Force * action.Duration),
                        next.Velocity.y
                    );
                }
                break;
            case EnemyActionType.EvasionDash:
                if (!from.DashUsed)
                {
                    next.Velocity = action.Direction * action.Force;
                    next.DashUsed = true;
                }
                break;
            case EnemyActionType.StepOver:
                next.Position += Vector2.up * action.Force * action.Duration;
                break;
        }
        next.Position += next.Velocity * action.Duration;
        next.Velocity += Physics2D.gravity * action.Duration;
        // Здесь можно добавить raycast/overlap для проверки IsGrounded и коллизий
        return next;
    }
}