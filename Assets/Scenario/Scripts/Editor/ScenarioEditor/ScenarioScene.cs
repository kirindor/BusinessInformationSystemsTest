using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Снимок открытых сцен: шаги, режимы и все связи UnityEvent между объектами
public class ScenarioScene
{
    public readonly List<ScenarioStep> steps = new List<ScenarioStep>();
    public readonly List<ScenarioMode> modes = new List<ScenarioMode>();
    public readonly List<MonoBehaviour> behaviours = new List<MonoBehaviour>();
    public readonly List<EventCall> calls = new List<EventCall>();
    public readonly List<ScenarioAnalyzer> analyzers = new List<ScenarioAnalyzer>();
    public ScenarioRunner runner;

    public void Rebuild()
    {
        steps.Clear();
        modes.Clear();
        behaviours.Clear();
        calls.Clear();
        analyzers.Clear();
        runner = null;

        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            foreach (var root in scene.GetRootGameObjects())
                behaviours.AddRange(root.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b != null));
        }

        foreach (var behaviour in behaviours)
        {
            if (behaviour is ScenarioStep step)
                steps.Add(step);
            else if (behaviour is ScenarioMode mode)
                modes.Add(mode);
            else if (behaviour is ScenarioRunner found && runner == null)
                runner = found;

            EventEditing.ReadAll(behaviour, calls);
        }

        foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(ScenarioAnalyzer)))
        {
            var analyzer = AssetDatabase.LoadAssetAtPath<ScenarioAnalyzer>(AssetDatabase.GUIDToAssetPath(guid));
            if (analyzer != null)
                analyzers.Add(analyzer);
        }
    }

    public IEnumerable<T> All<T>() where T : Component
    {
        return behaviours.OfType<T>().Where(c => c != null);
    }

    public IEnumerable<EventCall> CallsTo(Object target, string method)
    {
        return calls.Where(c => c.owner != null && c.target == target && c.method == method);
    }

    public IEnumerable<EventCall> CallsTo(Object target)
    {
        return calls.Where(c => c.owner != null && c.target == target);
    }

    public IEnumerable<EventCall> CallsFrom(Component owner, string eventName)
    {
        return calls.Where(c => c.owner == owner && c.eventName == eventName);
    }

    public IEnumerable<EventCall> RunCalls(ScenarioMode mode)
    {
        return runner == null ? Enumerable.Empty<EventCall>() : CallsTo(runner, nameof(ScenarioRunner.Run)).Where(c => c.objectArg == mode);
    }

    // Кнопка-образец для новых кнопок действий: последняя кнопка, которая что-то делает с шагами
    public Button ActionButtonTemplate()
    {
        return calls
            .Where(c => c.owner is Button && (c.target is ScenarioStep || c.target is StateCheck || c.target is SequenceCheck))
            .Select(c => (Button)c.owner)
            .LastOrDefault();
    }

    public Button RunButtonTemplate()
    {
        return runner == null ? null : CallsTo(runner, nameof(ScenarioRunner.Run)).Select(c => c.owner).OfType<Button>().LastOrDefault();
    }

    public bool IsUsed(ScenarioStep step)
    {
        return modes.Any(mode => Contains(mode, step));
    }

    public static bool Contains(ScenarioMode mode, ScenarioStep step)
    {
        return mode.groups.Any(group => group.stages.Any(stage => stage.steps.Contains(step)));
    }

    public static IEnumerable<ScenarioStep> StepsOf(ScenarioMode mode)
    {
        return mode.groups.SelectMany(group => group.stages).SelectMany(stage => stage.steps).Where(step => step != null);
    }

    // Проверки состояния шага в порядке цепочки: onPassed одной вызывает Check следующей
    public List<StateCheck> StateChain(ScenarioStep step)
    {
        var checks = step.GetComponents<StateCheck>().ToList();
        var ordered = new List<StateCheck>();

        StateCheck Next(StateCheck check)
        {
            return CallsFrom(check, nameof(StateCheck.onPassed))
                .Where(c => c.method == nameof(StateCheck.Check))
                .Select(c => c.target as StateCheck)
                .FirstOrDefault(next => checks.Contains(next));
        }

        var heads = checks.Where(check => !checks.Any(other => Next(other) == check)).ToList();
        foreach (var head in heads)
        {
            for (var check = head; check != null && !ordered.Contains(check); check = Next(check))
                ordered.Add(check);
        }

        ordered.AddRange(checks.Where(check => !ordered.Contains(check)));
        return ordered;
    }
}
