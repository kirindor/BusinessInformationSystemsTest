using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Описания объектов и связей обычными словами, без терминов Unity
public static class Wording
{
    public static string Name(Object obj)
    {
        if (obj == null)
            return "—";

        if (obj is ScenarioStep step)
            return StepTitle(step);

        var go = obj is Component component ? component.gameObject : obj as GameObject;
        if (go == null)
            return obj.name;

        if (go.TryGetComponent(out Button button))
            return Label(button.gameObject);

        var parent = go.transform.parent;
        return parent != null && parent.parent != null ? $"{parent.name} / {go.name}" : go.name;
    }

    public static string StepTitle(ScenarioStep step, int maxLength = 70)
    {
        if (step == null)
            return "—";

        var text = step.Description;
        if (string.IsNullOrWhiteSpace(text))
            return step.name;

        text = text.Trim().Split('\n')[0].Trim();
        return text.Length > maxLength ? text.Substring(0, maxLength - 3) + "…" : text;
    }

    public static string ModeTitle(ScenarioMode mode)
    {
        if (mode == null)
            return "—";

        return string.IsNullOrWhiteSpace(mode.title) ? mode.name : mode.title;
    }

    public static string Label(GameObject go)
    {
        var text = go.GetComponentInChildren<Text>(true);
        if (text != null && !string.IsNullOrWhiteSpace(text.text))
            return text.text.Trim();

        // TextMeshPro и другие подписи с полем text, без прямой зависимости от пакета
        foreach (var component in go.GetComponentsInChildren<Component>(true))
        {
            var property = component.GetType().GetProperty("text", typeof(string));
            if (property != null && !(component is InputField) && property.GetValue(component) is string value && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return go.name;
    }

    public static void SetLabel(GameObject go, string value)
    {
        var text = go.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = value;
            return;
        }

        foreach (var component in go.GetComponentsInChildren<Component>(true))
        {
            var property = component.GetType().GetProperty("text", typeof(string));
            if (property != null && property.CanWrite)
            {
                property.SetValue(component, value);
                return;
            }
        }
    }

    public static string State(bool on)
    {
        return on ? "включён" : "выключен";
    }

    public static string Condition(StateCheck check)
    {
        return check.Target == null ? "объект не выбран" : $"«{Name(check.Target)}» {State(check.RequiredOn)}";
    }

    // Что произошло: «Игрок входит в зону…»
    public static string Trigger(Component owner, string eventName)
    {
        var name = Name(owner);
        switch (owner)
        {
            case TriggerZone _:
                return eventName == nameof(TriggerZone.onExit) ? $"Игрок выходит из зоны «{name}»" : $"Игрок входит в зону «{name}»";
            case CollisionSensor _:
                return $"Игрок касается «{name}»";
            case Interactable interactable:
                return $"Игрок нажимает {interactable.Key} рядом с «{name}»";
            case ObjectState _:
                return eventName == nameof(ObjectState.onTurnedOff) ? $"«{name}» выключается" : $"«{name}» включается";
            case SequenceCheck sequence:
                return eventName == nameof(SequenceCheck.onWrongOrder)
                    ? "Действия выполнены не по порядку"
                    : $"Действия выполнены по порядку: {string.Join(" → ", sequence.Order)}";
            case StateCheck check:
                if (check.Target == null)
                    return "Проверка состояния (объект не выбран)";
                var actual = eventName == nameof(StateCheck.onPassed) ? check.RequiredOn : !check.RequiredOn;
                return $"При проверке «{Name(check.Target)}» {State(actual)}";
            case ScenarioStep step:
                return eventName == nameof(ScenarioStep.onDeactivated) ? $"Заканчивается шаг «{StepTitle(step)}»" : $"Начинается шаг «{StepTitle(step)}»";
            case ScenarioRunner _:
                switch (eventName)
                {
                    case nameof(ScenarioRunner.onStarted): return "Начинается прохождение";
                    case nameof(ScenarioRunner.onFinished): return "Прохождение закончено";
                    case nameof(ScenarioRunner.onViolation): return "Игрок допускает нарушение";
                    default: return "Меняется ход прохождения";
                }
            case Button _:
                return $"Нажата кнопка «{name}»";
        }

        return $"«{name}»: {ObjectNames.NicifyVariableName(eventName)}";
    }

    // Что сделать в ответ: «Шаг выполнен», «Показать маркер»…
    public static string Action(EventCall call)
    {
        if (call.IsBroken)
            return "Пустая или сломанная связь: объект или действие удалены";

        switch (call.target)
        {
            case ScenarioStep step when call.method == nameof(ScenarioStep.Complete):
                return $"Шаг «{StepTitle(step)}» выполнен";
            case ScenarioStep step when call.method == nameof(ScenarioStep.Violate):
                return $"Нарушение в шаге «{StepTitle(step)}»: {call.stringArg}";
            case SequenceCheck _ when call.method == nameof(SequenceCheck.Push):
                return $"Отметить действие «{call.stringArg}»";
            case SequenceCheck _ when call.method == nameof(SequenceCheck.ResetProgress):
                return "Начать проверку порядка заново";
            case StateCheck check when call.method == nameof(StateCheck.Check):
                return $"Проверить, что {Condition(check)}";
            case ObjectState state:
                switch (call.method)
                {
                    case nameof(ObjectState.TurnOn): return $"Включить «{Name(state)}»";
                    case nameof(ObjectState.TurnOff): return $"Выключить «{Name(state)}»";
                    case nameof(ObjectState.Toggle): return $"Переключить «{Name(state)}»";
                    case nameof(ObjectState.ResetState): return $"Вернуть «{Name(state)}» в начальное состояние";
                }
                break;
            case GameObject go when call.method == nameof(GameObject.SetActive):
                return call.boolArg ? $"Показать «{Name(go)}»" : $"Скрыть «{Name(go)}»";
            case ScenarioRunner _:
                switch (call.method)
                {
                    case nameof(ScenarioRunner.Run): return $"Запустить режим «{ModeTitle(call.objectArg as ScenarioMode)}»";
                    case nameof(ScenarioRunner.Restart): return "Начать прохождение заново";
                    case nameof(ScenarioRunner.Abort): return "Прервать прохождение";
                }
                break;
        }

        var argument = Argument(call);
        return $"«{Name(call.target)}»: {ObjectNames.NicifyVariableName(call.method)}{(argument != null ? $" ({argument})" : "")}";
    }

    public static string Status(StepStatus status)
    {
        switch (status)
        {
            case StepStatus.Active: return "Активен";
            case StepStatus.Completed: return "Выполнен";
            case StepStatus.Failed: return "Провален";
            case StepStatus.Skipped: return "Пропущен";
            default: return "Ожидает";
        }
    }

    public static string Count(int n, string one, string few, string many)
    {
        var mod100 = n % 100;
        var mod10 = n % 10;
        if (mod100 >= 11 && mod100 <= 14)
            return $"{n} {many}";
        if (mod10 == 1)
            return $"{n} {one}";
        return mod10 >= 2 && mod10 <= 4 ? $"{n} {few}" : $"{n} {many}";
    }

    public static string MenuText(string text)
    {
        return text.Replace('/', '∕');
    }

    private static string Argument(EventCall call)
    {
        switch (call.mode)
        {
            case PersistentListenerMode.String: return call.stringArg;
            case PersistentListenerMode.Bool: return call.boolArg ? "да" : "нет";
            case PersistentListenerMode.Object: return Name(call.objectArg);
            default: return null;
        }
    }
}
