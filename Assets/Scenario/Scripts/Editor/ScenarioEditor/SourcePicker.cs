using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Мастер «что должно произойти»: выбирает объект сцены и при необходимости сам добавляет ему нужные компоненты
public class SourcePicker
{
    private enum Kind
    {
        EnterZone,
        ExitZone,
        Touch,
        Interact,
        TurnOn,
        TurnOff,
        Button
    }

    private static readonly string[] KindNames =
    {
        "Игрок входит в зону",
        "Игрок выходит из зоны",
        "Игрок касается объекта",
        "Игрок нажимает E рядом с объектом",
        "Объект включается",
        "Объект выключается",
        "Игрок нажимает кнопку на экране"
    };

    private readonly Action _onSceneChanged;
    private string _slot;
    private Kind _kind;
    private GameObject _object;
    private string _text = "";
    private string _buttonLabel = "";

    public SourcePicker(Action onSceneChanged)
    {
        _onSceneChanged = onSceneChanged;
    }

    public string Text => _text;

    public bool IsOpen(string slot)
    {
        return _slot == slot;
    }

    public void Open(string slot, string text = "")
    {
        _slot = slot;
        _object = null;
        _text = text;
        _buttonLabel = "";
    }

    public void Close()
    {
        _slot = null;
    }

    // true, когда нажата кнопка «Добавить»
    public bool Draw(ScenarioScene scene, string title, string textLabel = null)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label(title, EditorStyles.boldLabel);

        _kind = (Kind)EditorGUILayout.Popup("Что происходит", (int)_kind, KindNames);

        var candidates = Candidates(scene);
        var names = new[] { "— выберите —" }.Concat(candidates.Select(go => Wording.MenuText(Wording.Name(go)))).ToArray();
        var index = candidates.IndexOf(_object) + 1;
        var picked = EditorGUILayout.Popup(_kind == Kind.Button ? "Кнопка" : "Объект", index, names);
        if (picked != index)
            _object = picked > 0 ? candidates[picked - 1] : null;

        _object = (GameObject)EditorGUILayout.ObjectField("или любой из сцены", _object, typeof(GameObject), true);

        DrawCreate(scene);

        if (textLabel != null)
            _text = EditorGUILayout.TextField(textLabel, _text);

        var valid = Explain(out var hint, out var type);
        if (hint != null)
            EditorGUILayout.HelpBox(hint, type);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Отмена", GUILayout.Width(90)))
            Close();

        bool add;
        using (new EditorGUI.DisabledScope(!valid))
            add = GUILayout.Button("Добавить", GUILayout.Width(110));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        return add;
    }

    // Вызывать внутри группы Undo: добавляет объекту недостающие компоненты и возвращает, к чьему событию подписаться
    public (Component owner, string eventName) CreateSource()
    {
        switch (_kind)
        {
            case Kind.EnterZone: return (ScenarioBuilder.EnsureZone(_object), nameof(TriggerZone.onEnter));
            case Kind.ExitZone: return (ScenarioBuilder.EnsureZone(_object), nameof(TriggerZone.onExit));
            case Kind.Touch: return (ScenarioBuilder.EnsureSensor(_object), nameof(CollisionSensor.onHit));
            case Kind.Interact: return (ScenarioBuilder.EnsureInteractable(_object), nameof(Interactable.onInteract));
            case Kind.TurnOn: return (ScenarioBuilder.EnsureSwitch(_object), nameof(ObjectState.onTurnedOn));
            case Kind.TurnOff: return (ScenarioBuilder.EnsureSwitch(_object), nameof(ObjectState.onTurnedOff));
            default: return (_object.GetComponent<Button>(), "m_OnClick");
        }
    }

    private void DrawCreate(ScenarioScene scene)
    {
        if (_kind == Kind.EnterZone || _kind == Kind.ExitZone)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(" ");
            if (GUILayout.Button("Создать новую зону"))
            {
                _object = ScenarioBuilder.CreateZone(scene);
                Selection.activeGameObject = _object;
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();
                _onSceneChanged();
            }
            EditorGUILayout.EndHorizontal();
        }
        else if (_kind == Kind.Button)
        {
            var template = scene.ActionButtonTemplate();
            EditorGUILayout.BeginHorizontal();
            _buttonLabel = EditorGUILayout.TextField("или новая с текстом", _buttonLabel);
            using (new EditorGUI.DisabledScope(template == null || string.IsNullOrWhiteSpace(_buttonLabel)))
            {
                if (GUILayout.Button("Создать", GUILayout.Width(80)))
                {
                    _object = ScenarioBuilder.CreateButton(template, _buttonLabel.Trim()).gameObject;
                    _buttonLabel = "";
                    _onSceneChanged();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private List<GameObject> Candidates(ScenarioScene scene)
    {
        IEnumerable<Component> components;
        switch (_kind)
        {
            case Kind.EnterZone:
            case Kind.ExitZone:
                components = scene.All<TriggerZone>();
                break;
            case Kind.Touch:
                components = scene.All<CollisionSensor>();
                break;
            case Kind.Interact:
                components = scene.All<Interactable>();
                break;
            case Kind.TurnOn:
            case Kind.TurnOff:
                components = scene.All<ObjectState>();
                break;
            default:
                components = scene.All<Button>();
                break;
        }

        return components.Select(c => c.gameObject).Distinct().ToList();
    }

    private bool Explain(out string hint, out MessageType type)
    {
        type = MessageType.Info;
        hint = null;

        if (_object == null)
        {
            hint = "Выберите объект из списка или перетащите его в поле из окна сцены или из списка объектов (Hierarchy).";
            return false;
        }

        if (!_object.scene.IsValid())
        {
            hint = "Нужен объект из сцены, а не из папок проекта.";
            type = MessageType.Error;
            return false;
        }

        var name = Wording.Name(_object);
        var collider = _object.GetComponent<Collider>();

        switch (_kind)
        {
            case Kind.EnterZone:
            case Kind.ExitZone:
                if (_object.TryGetComponent(out TriggerZone _))
                    return true;
                hint = collider != null && !collider.isTrigger
                    ? $"Вокруг «{name}» появится невидимая зона чуть больше самого объекта."
                    : $"«{name}» станет зоной: игрок сможет в неё зайти.";
                return true;

            case Kind.Touch:
                if (_object.TryGetComponent(out CollisionSensor _))
                    return true;
                if (collider != null && collider.isTrigger)
                {
                    hint = $"Сквозь «{name}» можно пройти, поэтому касание не сработает. Выберите условие «Игрок входит в зону».";
                    type = MessageType.Warning;
                    return false;
                }
                hint = $"«{name}» начнёт замечать, когда игрок в него упирается.";
                return true;

            case Kind.Interact:
                if (_object.TryGetComponent(out Interactable interactable))
                {
                    hint = $"Клавиша: {interactable.Key}. Поменять её можно в инспекторе объекта.";
                    return true;
                }
                hint = collider != null && collider.isTrigger
                    ? $"Стоя в «{name}», игрок сможет нажать E."
                    : $"Вокруг «{name}» появится невидимая зона: стоя в ней, игрок нажимает E.";
                return true;

            case Kind.TurnOn:
            case Kind.TurnOff:
                if (_object.TryGetComponent(out ObjectState _))
                    return true;
                hint = $"«{name}» станет переключателем: игрок включает и выключает его клавишей E, стоя рядом.";
                return true;

            default:
                if (_object.TryGetComponent(out Button _))
                    return true;
                hint = "Это не кнопка интерфейса. Выберите кнопку из списка или создайте новую.";
                type = MessageType.Error;
                return false;
        }
    }
}
