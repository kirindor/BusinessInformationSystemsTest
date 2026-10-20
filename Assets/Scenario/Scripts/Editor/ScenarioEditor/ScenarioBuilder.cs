using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Операции над сценой, которые делает окно редактора. Вызываются внутри группы Undo
public static class ScenarioBuilder
{
    public static ScenarioStep CreateStep(ScenarioScene scene)
    {
        var parent = scene.steps.Count > 0 ? scene.steps[scene.steps.Count - 1].transform.parent : null;
        var number = scene.steps.Select(step => LeadingNumber(step.name)).DefaultIfEmpty(0).Max() + 1;

        var go = new GameObject($"{number:00} Новый шаг");
        go.transform.SetParent(parent, false);
        var created = go.AddComponent<ScenarioStep>();
        var so = new SerializedObject(created);
        so.FindProperty("_description").stringValue = "Новое задание";
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(go, "Новый шаг");
        return created;
    }

    public static void DeleteStep(ScenarioScene scene, ScenarioStep step)
    {
        foreach (var mode in scene.modes)
        {
            if (ScenarioScene.Contains(mode, step))
                RemoveFromMode(mode, step);
        }

        var own = new HashSet<Object>(step.GetComponents<Component>()) { step.gameObject };
        EventEditing.RemoveAll(scene.calls.Where(c => c.target != null && own.Contains(c.target) && c.owner.gameObject != step.gameObject));
        Undo.DestroyObjectImmediate(step.gameObject);
    }

    public static ScenarioMode CreateMode(ScenarioScene scene)
    {
        var parent = scene.modes.Count > 0 ? scene.modes[0].transform.parent : null;
        var go = new GameObject("Новый режим");
        go.transform.SetParent(parent, false);
        var mode = go.AddComponent<ScenarioMode>();
        mode.title = "Новый режим";
        mode.analyzer = scene.analyzers.OfType<TrainingAnalyzer>().FirstOrDefault();
        mode.groups.Add(new StepGroup { name = "Группа 1" });
        Undo.RegisterCreatedObjectUndo(go, "Новый режим");
        return mode;
    }

    public static ScenarioMode DuplicateMode(ScenarioMode mode)
    {
        var go = Object.Instantiate(mode.gameObject, mode.transform.parent);
        go.name = mode.name + " (копия)";
        go.transform.SetSiblingIndex(mode.transform.GetSiblingIndex() + 1);
        var copy = go.GetComponent<ScenarioMode>();
        copy.title = Wording.ModeTitle(mode) + " (копия)";
        Undo.RegisterCreatedObjectUndo(go, "Копия режима");
        return copy;
    }

    public static void DeleteMode(ScenarioScene scene, ScenarioMode mode)
    {
        EventEditing.RemoveAll(scene.RunCalls(mode).ToList());
        Undo.DestroyObjectImmediate(mode.gameObject);
    }

    public static void ApplyPreset(ScenarioScene scene, ScenarioMode mode, bool exam)
    {
        Undo.RecordObject(mode, exam ? "Режим как экзамен" : "Режим как тренировка");
        mode.retryAfterViolation = !exam;
        mode.violationLimit = exam ? 2 : 0;
        mode.outOfTurn = OutOfTurnAction.Violation;
        var analyzer = exam
            ? scene.analyzers.OfType<ExamAnalyzer>().FirstOrDefault()
            : (ScenarioAnalyzer)scene.analyzers.OfType<TrainingAnalyzer>().FirstOrDefault();
        if (analyzer != null)
            mode.analyzer = analyzer;
    }

    // Переносит шаги в стадию. Шаг в режиме бывает только один раз, поэтому из прежней стадии он убирается,
    // а опустевшая из-за этого стадия удаляется
    public static void PlaceSteps(ScenarioMode mode, IEnumerable<ScenarioStep> steps, StepStage target)
    {
        Undo.RecordObject(mode, "Перенос шага");
        var emptied = new List<StepStage>();

        foreach (var step in steps)
        {
            foreach (var stage in mode.groups.SelectMany(group => group.stages))
            {
                if (stage.steps.RemoveAll(s => s == step) > 0 && stage.steps.Count == 0)
                    emptied.Add(stage);
            }

            target.steps.Add(step);
        }

        foreach (var group in mode.groups)
            group.stages.RemoveAll(stage => stage != target && emptied.Contains(stage));
    }

    public static void RemoveFromMode(ScenarioMode mode, ScenarioStep step)
    {
        Undo.RecordObject(mode, "Шаг убран из режима");
        foreach (var group in mode.groups)
            group.stages.RemoveAll(stage => stage.steps.RemoveAll(s => s == step) > 0 && stage.steps.Count == 0);
    }

    public static GameObject CreateZone(ScenarioScene scene)
    {
        var sample = scene.All<TriggerZone>().FirstOrDefault();
        var number = 1;
        while (scene.All<TriggerZone>().Any(zone => zone.name == $"Зона {number}"))
            number++;

        var go = new GameObject($"Зона {number}");
        go.transform.SetParent(sample != null ? sample.transform.parent : null, false);
        go.transform.position = GroundUnderSceneView();
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0, 1, 0);
        box.size = new Vector3(2, 2, 2);
        go.AddComponent<TriggerZone>();

        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = "Plate";
        Object.DestroyImmediate(plate.GetComponent<Collider>());
        plate.transform.SetParent(go.transform, false);
        plate.transform.localPosition = new Vector3(0, 0.02f, 0);
        plate.transform.localScale = new Vector3(2, 0.04f, 2);
        var sampleRenderer = sample != null ? sample.GetComponentInChildren<MeshRenderer>() : null;
        if (sampleRenderer != null)
            plate.GetComponent<MeshRenderer>().sharedMaterial = sampleRenderer.sharedMaterial;

        Undo.RegisterCreatedObjectUndo(go, "Новая зона");
        return go;
    }

    public static TriggerZone EnsureZone(GameObject go)
    {
        return EnsureTriggerComponent<TriggerZone>(go, "Зона рядом", false);
    }

    public static Interactable EnsureInteractable(GameObject go)
    {
        return EnsureTriggerComponent<Interactable>(go, "Зона нажатия E", true);
    }

    public static CollisionSensor EnsureSensor(GameObject go)
    {
        if (go.TryGetComponent(out CollisionSensor existing))
            return existing;

        if (go.GetComponent<Collider>() == null)
            Undo.AddComponent<BoxCollider>(go);
        return Undo.AddComponent<CollisionSensor>(go);
    }

    // Объект становится переключателем: игрок включает и выключает его клавишей рядом
    public static ObjectState EnsureSwitch(GameObject go)
    {
        if (go.TryGetComponent(out ObjectState existing))
            return existing;

        var state = Undo.AddComponent<ObjectState>(go);
        var interactable = EnsureInteractable(go);
        EventEditing.Add(interactable, nameof(Interactable.onInteract), EventAction.Call(state, nameof(ObjectState.Toggle)));
        return state;
    }

    public static Button CreateButton(Button template, string label)
    {
        var go = Object.Instantiate(template.gameObject, template.transform.parent);
        go.name = label;
        go.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
        Wording.SetLabel(go, label);
        var button = go.GetComponent<Button>();
        var so = new SerializedObject(button);
        so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").ClearArray();
        so.ApplyModifiedPropertiesWithoutUndo();
        Undo.RegisterCreatedObjectUndo(go, "Новая кнопка");
        return button;
    }

    public static void SetMarker(ScenarioStep step, GameObject marker)
    {
        EventEditing.Add(step, nameof(ScenarioStep.onActivated), EventAction.WithBool(marker, nameof(GameObject.SetActive), true));
        EventEditing.Add(step, nameof(ScenarioStep.onDeactivated), EventAction.WithBool(marker, nameof(GameObject.SetActive), false));
        if (marker.activeSelf)
        {
            Undo.RecordObject(marker, "Скрыть подсказку");
            marker.SetActive(false);
        }
    }

    public static SequenceCheck AddSequence(ScenarioStep step)
    {
        var sequence = Undo.AddComponent<SequenceCheck>(step.gameObject);
        var so = new SerializedObject(sequence);
        var order = so.FindProperty("_order");
        order.ClearArray();
        order.arraySize = 2;
        order.GetArrayElementAtIndex(0).stringValue = "Действие 1";
        order.GetArrayElementAtIndex(1).stringValue = "Действие 2";
        so.ApplyModifiedProperties();

        EventEditing.Add(sequence, nameof(SequenceCheck.onCompleted), EventAction.Call(step, nameof(ScenarioStep.Complete)));
        EventEditing.Add(sequence, nameof(SequenceCheck.onWrongOrder), EventAction.WithString(step, nameof(ScenarioStep.Violate), "Нарушен порядок действий"));
        EventEditing.Add(step, nameof(ScenarioStep.onActivated), EventAction.Call(sequence, nameof(SequenceCheck.ResetProgress)));
        return sequence;
    }

    public static void RemoveSequence(ScenarioScene scene, SequenceCheck sequence)
    {
        EventEditing.RemoveAll(scene.CallsTo(sequence).Where(c => c.owner != sequence).ToList());
        Undo.DestroyObjectImmediate(sequence);
    }

    public static void RenameSequenceKey(ScenarioScene scene, SequenceCheck sequence, int index, string value)
    {
        var so = new SerializedObject(sequence);
        var key = so.FindProperty("_order").GetArrayElementAtIndex(index);
        var old = key.stringValue;
        key.stringValue = value;
        so.ApplyModifiedProperties();

        foreach (var call in scene.CallsTo(sequence, nameof(SequenceCheck.Push)).Where(c => c.stringArg == old).ToList())
            EventEditing.SetString(call, value);
    }

    public static void RemoveSequenceKey(ScenarioScene scene, SequenceCheck sequence, int index)
    {
        var so = new SerializedObject(sequence);
        var order = so.FindProperty("_order");
        var old = order.GetArrayElementAtIndex(index).stringValue;
        order.DeleteArrayElementAtIndex(index);
        so.ApplyModifiedProperties();

        if (!sequence.Order.Contains(old))
            EventEditing.RemoveAll(scene.CallsTo(sequence, nameof(SequenceCheck.Push)).Where(c => c.stringArg == old).ToList());
    }

    // Новая проверка встаёт в конец цепочки. Если проверок ещё нет, всё, что засчитывало шаг, теперь запускает проверку
    public static StateCheck AddStateCheck(ScenarioScene scene, ScenarioStep step)
    {
        var chain = scene.StateChain(step);
        var completions = scene.CallsTo(step, nameof(ScenarioStep.Complete)).ToList();

        var check = Undo.AddComponent<StateCheck>(step.gameObject);
        EventEditing.Add(check, nameof(StateCheck.onFailed), EventAction.WithString(step, nameof(ScenarioStep.Violate), "Объект в неправильном состоянии"));

        if (chain.Count == 0)
        {
            foreach (var call in completions)
                EventEditing.Replace(call, EventAction.Call(check, nameof(StateCheck.Check)));
            EventEditing.Add(check, nameof(StateCheck.onPassed), EventAction.Call(step, nameof(ScenarioStep.Complete)));
        }
        else
        {
            var last = chain[chain.Count - 1];
            var passed = EventEditing.Read(last, nameof(StateCheck.onPassed));
            EventEditing.Clear(last, nameof(StateCheck.onPassed));
            EventEditing.Add(last, nameof(StateCheck.onPassed), EventAction.Call(check, nameof(StateCheck.Check)));
            foreach (var action in passed)
                EventEditing.Add(check, nameof(StateCheck.onPassed), action);
        }

        return check;
    }

    // Проверка вынимается из цепочки: те, кто её запускал, сразу делают то, что она делала при успехе
    public static void RemoveStateCheck(ScenarioScene scene, StateCheck check)
    {
        var passed = EventEditing.Read(check, nameof(StateCheck.onPassed));
        var callers = scene.CallsTo(check, nameof(StateCheck.Check)).Where(c => c.owner != check).ToList();

        foreach (var caller in callers.OrderByDescending(c => c.index))
        {
            EventEditing.Remove(caller);
            foreach (var action in passed)
                EventEditing.Add(caller.owner, caller.eventName, action);
        }

        EventEditing.RemoveAll(scene.CallsTo(check).Where(c => c.owner != check && !callers.Contains(c)).ToList());
        Undo.DestroyObjectImmediate(check);
    }

    // Компоненту нужен коллайдер-триггер. Если объект твёрдый (или к нему надо подойти, а не зайти внутрь),
    // зона делается дочерним объектом чуть больше самого объекта
    private static T EnsureTriggerComponent<T>(GameObject go, string zoneName, bool around) where T : Component
    {
        if (go.TryGetComponent(out T existing))
            return existing;

        var collider = go.GetComponent<Collider>();
        if (collider != null && collider.isTrigger)
            return Undo.AddComponent<T>(go);

        if (collider == null && !around)
        {
            Undo.AddComponent<BoxCollider>(go).isTrigger = true;
            return Undo.AddComponent<T>(go);
        }

        var bounds = BoundsOf(go, collider);
        var host = new GameObject(zoneName);
        host.transform.SetParent(go.transform, false);
        host.transform.position = bounds.center;
        var box = host.AddComponent<BoxCollider>();
        box.isTrigger = true;
        var size = bounds.size + new Vector3(1.5f, 0.5f, 1.5f);
        size.y = Mathf.Max(size.y, 2f);
        var scale = host.transform.lossyScale;
        box.size = new Vector3(size.x / NonZero(scale.x), size.y / NonZero(scale.y), size.z / NonZero(scale.z));
        Undo.RegisterCreatedObjectUndo(host, "Новая зона");
        return Undo.AddComponent<T>(host);
    }

    private static Bounds BoundsOf(GameObject go, Collider collider)
    {
        if (collider != null)
            return collider.bounds;

        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.one);

        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static float NonZero(float value)
    {
        return Mathf.Approximately(value, 0) ? 1 : Mathf.Abs(value);
    }

    private static Vector3 GroundUnderSceneView()
    {
        var view = SceneView.lastActiveSceneView;
        var point = view != null ? view.pivot : Vector3.zero;
        return Physics.Raycast(point + Vector3.up * 50, Vector3.down, out var hit, 200, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point
            : new Vector3(point.x, 0, point.z);
    }

    private static int LeadingNumber(string name)
    {
        var digits = new string(name.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var number) ? number : 0;
    }
}
