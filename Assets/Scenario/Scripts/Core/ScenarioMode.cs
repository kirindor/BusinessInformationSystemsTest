using System;
using System.Collections.Generic;
using UnityEngine;

public enum OutOfTurnAction
{
    Violation,
    Ignore
}

[Serializable]
public class StepStage
{
    public List<ScenarioStep> steps = new List<ScenarioStep>();
}

[Serializable]
public class StepGroup
{
    public string name;
    public List<StepStage> stages = new List<StepStage>();
}

public class ScenarioMode : MonoBehaviour
{
    public string title;

    [Tooltip("Включено: после нарушения шаг остаётся активным и его можно повторить. Выключено: шаг получает статус Failed")]
    public bool retryAfterViolation = true;

    [Tooltip("После стольких нарушений в группе её незавершённые шаги получают статус Skipped. 0 — без ограничения")]
    [Min(0)] public int violationLimit;

    [Tooltip("Действие шага, до которого ещё не дошла очередь: считать нарушением или игнорировать")]
    public OutOfTurnAction outOfTurn = OutOfTurnAction.Violation;

    public ScenarioAnalyzer analyzer;

    [Tooltip("Группы идут по очереди. Внутри группы этапы идут по очереди, шаги одного этапа активны одновременно")]
    public List<StepGroup> groups = new List<StepGroup>();
}
