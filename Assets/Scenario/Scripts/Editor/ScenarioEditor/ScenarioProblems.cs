using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ScenarioProblem
{
    public MessageType severity;
    public string text;
    public Object context;
    public string fixLabel;
    public Action fix;
}

// Всё, из-за чего сценарий не запустится или пройдёт не так, как задумано
public static class ScenarioProblems
{
    public static List<ScenarioProblem> Find(ScenarioScene scene)
    {
        var problems = new List<ScenarioProblem>();

        void Add(MessageType severity, string text, Object context, string fixLabel = null, Action fix = null)
        {
            problems.Add(new ScenarioProblem { severity = severity, text = text, context = context, fixLabel = fixLabel, fix = fix });
        }

        if (scene.steps.Count == 0 && scene.modes.Count == 0 && scene.runner == null)
            return problems;

        if (scene.runner == null)
            Add(MessageType.Error, "В сцене нет ScenarioRunner, сценарий не запустится. Скопируйте объект Scenario из демо-сцены.", null);
        else if (scene.runner.Logger == null)
            Add(MessageType.Error, "У Runner не выбран журнал (поле Logger), прохождение остановится с ошибкой.", scene.runner);

        if (scene.modes.Count == 0)
            Add(MessageType.Warning, "Нет ни одного режима. Создайте его на вкладке «Режимы».", null);

        foreach (var step in scene.steps)
            FindStepProblems(scene, step, Add);

        foreach (var name in scene.steps.GroupBy(step => step.name).Where(g => g.Count() > 1).Select(g => g.Key))
            Add(MessageType.Error, $"Несколько шагов с кодом «{name}», в журнале их не различить.", scene.steps.First(step => step.name == name));

        foreach (var mode in scene.modes)
            FindModeProblems(scene, mode, Add);

        foreach (var call in scene.calls.Where(c => c.IsBroken))
        {
            var broken = call;
            Add(MessageType.Error, $"{Wording.Trigger(call.owner, call.eventName)}: связь пустая или ведёт к удалённому объекту.", call.owner,
                "Удалить связь", () => EventEditing.Remove(broken));
        }

        return problems
            .OrderBy(p => p.severity == MessageType.Error ? 0 : p.severity == MessageType.Warning ? 1 : 2)
            .ToList();
    }

    private static void FindStepProblems(ScenarioScene scene, ScenarioStep step, Action<MessageType, string, Object, string, Action> add)
    {
        var title = Wording.StepTitle(step);

        if (string.IsNullOrWhiteSpace(step.Description))
            add(MessageType.Warning, $"Шаг «{step.name}»: не написан текст задания, игрок не поймёт, что делать.", step, null, null);

        if (!scene.CallsTo(step, nameof(ScenarioStep.Complete)).Any())
            add(MessageType.Error, $"Шаг «{title}» ничем не засчитывается, выполнить его невозможно.", step, null, null);

        if (!scene.IsUsed(step))
            add(MessageType.Info, $"Шаг «{title}» не входит ни в один режим.", step, null, null);

        foreach (var sequence in step.GetComponents<SequenceCheck>())
        {
            if (sequence.Order.Count == 0)
                add(MessageType.Error, $"Шаг «{title}»: в проверке порядка нет ни одного действия.", step, null, null);

            foreach (var key in sequence.Order.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key))
                add(MessageType.Warning, $"Шаг «{title}»: действие «{key}» записано в порядке несколько раз.", step, null, null);

            var pushes = scene.CallsTo(sequence, nameof(SequenceCheck.Push)).ToList();
            foreach (var key in sequence.Order.Distinct().Where(k => pushes.All(c => c.stringArg != k)))
                add(MessageType.Warning, $"Шаг «{title}»: действие «{key}» ничем не отмечается.", step, null, null);

            foreach (var push in pushes.Where(c => !sequence.Order.Contains(c.stringArg)))
                add(MessageType.Warning, $"Шаг «{title}»: отмечается действие «{push.stringArg}», которого нет в списке порядка.", step, null, null);
        }

        var chain = scene.StateChain(step);
        foreach (var check in chain.Where(c => c.Target == null))
            add(MessageType.Error, $"Шаг «{title}»: в проверке состояния не выбран объект.", step, null, null);

        if (chain.Count > 0 && !scene.CallsTo(chain[0], nameof(StateCheck.Check)).Any(c => c.owner != chain[0]))
            add(MessageType.Warning, $"Шаг «{title}»: проверка состояния ничем не запускается.", step, null, null);

        // Во время игры подсказки активных шагов видны, это нормально
        if (Application.isPlaying)
            return;

        foreach (var marker in Markers(scene, step).Where(go => go.activeSelf))
        {
            var target = marker;
            add(MessageType.Warning, $"Шаг «{title}»: «{Wording.Name(marker)}» виден с самого начала, а должен появляться только на время шага.", step,
                "Скрыть", () =>
                {
                    Undo.RecordObject(target, "Скрыть подсказку");
                    target.SetActive(false);
                });
        }
    }

    private static void FindModeProblems(ScenarioScene scene, ScenarioMode mode, Action<MessageType, string, Object, string, Action> add)
    {
        var title = Wording.ModeTitle(mode);

        if (string.IsNullOrWhiteSpace(mode.title))
            add(MessageType.Warning, $"Режим «{mode.name}»: нет названия.", mode, null, null);

        if (mode.analyzer == null)
            add(MessageType.Error, $"Режим «{title}»: не выбрана оценка, окно результатов не откроется.", mode, null, null);

        if (mode.groups.Count == 0)
            add(MessageType.Error, $"Режим «{title}»: нет ни одной группы шагов.", mode, null, null);

        foreach (var group in mode.groups)
        {
            if (group.stages.Count == 0)
                add(MessageType.Warning, $"Режим «{title}»: группа «{group.name}» пустая.", mode, null, null);

            for (var i = 0; i < group.stages.Count; i++)
            {
                if (group.stages[i].steps.Count == 0)
                    add(MessageType.Warning, $"Режим «{title}»: в группе «{group.name}» пустая стадия {i + 1}.", mode, null, null);
                else if (group.stages[i].steps.Any(step => step == null))
                    add(MessageType.Warning, $"Режим «{title}»: в группе «{group.name}», стадии {i + 1} есть пустая ячейка вместо шага.", mode, null, null);
            }
        }

        foreach (var step in ScenarioScene.StepsOf(mode).GroupBy(step => step).Where(g => g.Count() > 1).Select(g => g.Key))
            add(MessageType.Error, $"Режим «{title}»: шаг «{Wording.StepTitle(step)}» добавлен несколько раз.", mode, null, null);

        if (scene.runner != null && !scene.RunCalls(mode).Any())
            add(MessageType.Warning, $"Режим «{title}» ничем не запускается: нет кнопки запуска.", mode, null, null);
    }

    public static IEnumerable<GameObject> Markers(ScenarioScene scene, ScenarioStep step)
    {
        return scene.CallsFrom(step, nameof(ScenarioStep.onActivated))
            .Where(c => c.target is GameObject && c.method == nameof(GameObject.SetActive) && c.boolArg)
            .Select(c => (GameObject)c.target)
            .Distinct();
    }
}
