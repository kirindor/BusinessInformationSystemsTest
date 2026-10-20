using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Вкладка «Режимы»: правила, кнопка запуска и порядок шагов по группам и стадиям с перетаскиванием
public partial class ScenarioEditorWindow
{
    private void DrawModesPage()
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
        foreach (var mode in _scene.modes)
        {
            var subtitle = $"{Wording.Count(mode.groups.Count, "группа", "группы", "групп")}  ·  {Wording.Count(ScenarioScene.StepsOf(mode).Count(), "шаг", "шага", "шагов")}";
            Color? stripe = null;
            if (_scene.runner != null && _scene.runner.IsRunning && _scene.runner.Mode == mode)
            {
                subtitle += "  ·  идёт";
                stripe = Yellow;
            }

            if (ListItem(Wording.ModeTitle(mode), subtitle, mode == _mode, stripe, WorstProblem(mode)))
                SelectMode(mode);
        }
        if (_scene.modes.Count == 0)
            Note("Режимов пока нет. Режим — это набор шагов с правилами: например, тренировка или экзамен.");
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("+ Новый режим", GUILayout.Height(30)))
                Apply("Новый режим", () => SelectMode(ScenarioBuilder.CreateMode(_scene)));
        }
        GUILayout.Space(4);
        EditorGUILayout.EndVertical();

        VerticalLine();

        _cardScroll = EditorGUILayout.BeginScrollView(_cardScroll);
        GUILayout.Space(4);
        if (_mode != null)
            DrawModeCard(_mode);
        else
            Note("Выберите режим слева или создайте новый.");
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawModeCard(ScenarioMode mode)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6);
        GUILayout.Label(Wording.ModeTitle(mode), Styles.Title);
        if (GUILayout.Button(new GUIContent("Найти", "Выделить объект режима в списке объектов"), GUILayout.Width(70), GUILayout.Height(22)))
            ShowInScene(mode);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button(new GUIContent("Копия", "Создать такой же режим, чтобы поменять в нём правила"), GUILayout.Width(70), GUILayout.Height(22)))
                Apply("Копия режима", () => SelectMode(ScenarioBuilder.DuplicateMode(mode)));
            if (GUILayout.Button("Удалить", GUILayout.Width(80), GUILayout.Height(22))
                && Confirm("Удалить режим?", $"Режим «{Wording.ModeTitle(mode)}» будет удалён, его кнопки запуска перестанут работать. Шаги останутся в сцене. Отменить можно через Ctrl+Z.", "Удалить"))
            {
                Apply("Удаление режима", () => ScenarioBuilder.DeleteMode(_scene, mode));
            }
        }
        GUILayout.Space(6);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);

        if (Application.isPlaying && _scene.runner != null)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6);
            if (GUILayout.Button("▶  Запустить этот режим", GUILayout.Height(28), GUILayout.Width(220)))
                _scene.runner.Run(mode);
            EditorGUILayout.EndHorizontal();
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            DrawModeRules(mode);
            DrawModeLaunch(mode);
            DrawFlow(mode);
            DrawUnused(mode);
        }

        GUILayout.Space(10);
    }

    private void DrawModeRules(ScenarioMode mode)
    {
        BeginSection("Название и правила", null, Blue);
        var so = new SerializedObject(mode);
        so.Update();

        EditorGUILayout.PropertyField(so.FindProperty("title"), new GUIContent("Название"));
        Help("Название видно игроку вверху экрана во время прохождения.");
        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Быстрая настройка");
        if (GUILayout.Button("Как тренировка"))
            Apply("Режим как тренировка", () => ScenarioBuilder.ApplyPreset(_scene, mode, false));
        if (GUILayout.Button("Как экзамен"))
            Apply("Режим как экзамен", () => ScenarioBuilder.ApplyPreset(_scene, mode, true));
        EditorGUILayout.EndHorizontal();
        Help("Тренировка: после ошибки шаг можно повторить, баллы зависят от числа попыток. Экзамен: ошибка сразу проваливает шаг, после двух ошибок в группе её остаток пропускается. Ниже каждое правило можно поменять отдельно.");
        GUILayout.Space(6);

        var retry = so.FindProperty("retryAfterViolation");
        EditorGUI.BeginChangeCheck();
        var retryValue = EditorGUILayout.ToggleLeft("После ошибки шаг можно повторить", retry.boolValue);
        if (EditorGUI.EndChangeCheck())
            retry.boolValue = retryValue;
        Note(retry.boolValue
            ? "Сейчас: после нарушения шаг остаётся активным, и игрок пробует ещё раз."
            : "Сейчас: после нарушения шаг сразу считается проваленным.");
        GUILayout.Space(4);

        var limit = so.FindProperty("violationLimit");
        EditorGUI.BeginChangeCheck();
        var limitValue = EditorGUILayout.IntField("Ошибок до пропуска группы", limit.intValue);
        if (EditorGUI.EndChangeCheck())
            limit.intValue = Mathf.Max(0, limitValue);
        Note(limit.intValue == 0
            ? "Сейчас: сколько бы ни было ошибок, группа проходится до конца. Число больше нуля включит пропуск."
            : $"Сейчас: после {limit.intValue}-й ошибки в группе её оставшиеся шаги пропускаются. 0 — не пропускать.");
        GUILayout.Space(4);

        var outOfTurn = so.FindProperty("outOfTurn");
        EditorGUI.BeginChangeCheck();
        var outOfTurnValue = EditorGUILayout.Popup("Шаг сделан раньше времени", outOfTurn.enumValueIndex, new[] { "Считать нарушением", "Не обращать внимания" });
        if (EditorGUI.EndChangeCheck())
            outOfTurn.enumValueIndex = outOfTurnValue;
        Note(outOfTurn.enumValueIndex == (int)OutOfTurnAction.Violation
            ? "Сейчас: если игрок делает шаг, до которого ещё не дошла очередь, это нарушение."
            : "Сейчас: действия для шагов, до которых не дошла очередь, просто не учитываются.");
        GUILayout.Space(4);

        var analyzer = so.FindProperty("analyzer");
        var analyzers = _scene.analyzers;
        var names = new[] { "— не выбрана —" }
            .Concat(analyzers.Select(a => Wording.MenuText(string.IsNullOrWhiteSpace(a.Title) ? a.name : a.Title)))
            .ToArray();
        var index = analyzers.IndexOf(analyzer.objectReferenceValue as ScenarioAnalyzer) + 1;
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        var picked = EditorGUILayout.Popup("Как оценивать", index, names);
        if (EditorGUI.EndChangeCheck())
            analyzer.objectReferenceValue = picked > 0 ? analyzers[picked - 1] : null;
        using (new EditorGUI.DisabledScope(analyzer.objectReferenceValue == null))
        {
            if (GUILayout.Button(new GUIContent("Баллы…", "Открыть настройки оценки в инспекторе"), EditorStyles.miniButton, GUILayout.Width(64)))
            {
                Selection.activeObject = analyzer.objectReferenceValue;
                EditorGUIUtility.PingObject(analyzer.objectReferenceValue);
            }
        }
        EditorGUILayout.EndHorizontal();
        Help("Баллы и штрафы задаются в самой оценке: кнопка «Баллы…» откроет её в инспекторе.");
        if (analyzer.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Выберите оценку, иначе окно результатов не откроется.", MessageType.Error);

        so.ApplyModifiedProperties();
        EndSection();
    }

    private void DrawModeLaunch(ScenarioMode mode)
    {
        BeginSection("Как запускается", "Режим начинается, когда игрок нажимает его кнопку в меню.", Green);

        if (_scene.runner == null)
        {
            EditorGUILayout.HelpBox("В сцене нет ScenarioRunner, запускать режим нечем.", MessageType.Error);
            EndSection();
            return;
        }

        var runs = _scene.RunCalls(mode).ToList();
        foreach (var call in runs)
        {
            if (Line(Wording.Trigger(call.owner, call.eventName), call.owner, true))
                Apply("Удаление запуска", () => EventEditing.Remove(call));
        }

        if (runs.Count == 0)
        {
            EditorGUILayout.HelpBox("Режим пока ничем не запускается.", MessageType.Warning);
            var template = _scene.RunButtonTemplate();
            if (template != null && GUILayout.Button($"Создать кнопку «{Wording.ModeTitle(mode)}» в меню", GUILayout.Width(300)))
            {
                Apply("Кнопка запуска", () =>
                {
                    var button = ScenarioBuilder.CreateButton(template, Wording.ModeTitle(mode));
                    EventEditing.Add(button, "m_OnClick", EventAction.WithObject(_scene.runner, nameof(ScenarioRunner.Run), mode));
                });
            }
        }
        EndSection();
    }

    private void DrawFlow(ScenarioMode mode)
    {
        BeginSection("Порядок прохождения", "Группы и стадии идут сверху вниз. Шаги одной стадии доступны одновременно, их можно делать в любом порядке. Шаги можно перетаскивать мышкой между стадиями (и прямо из списка объектов), двойной щелчок открывает шаг.", Purple);

        for (var g = 0; g < mode.groups.Count; g++)
        {
            var group = mode.groups[g];
            EditorGUILayout.BeginVertical(Styles.Group);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Группа {g + 1}", EditorStyles.boldLabel, GUILayout.Width(70));
            EditorGUI.BeginChangeCheck();
            var name = EditorGUILayout.DelayedTextField(group.name);
            if (EditorGUI.EndChangeCheck())
            {
                Apply("Название группы", () =>
                {
                    Undo.RecordObject(mode, "Название группы");
                    group.name = name;
                });
            }
            var hasSteps = group.stages.Any(stage => stage.steps.Count > 0);
            OrderButtons(mode, mode.groups, g, "Порядок групп", hasSteps ? $"Удалить группу «{group.name}» со всеми её стадиями? Сами шаги останутся в сцене." : null);
            EditorGUILayout.EndHorizontal();

            for (var s = 0; s < group.stages.Count; s++)
                DrawStage(mode, group, s);

            var dragging = _dragActive && DraggedSteps().Count > 0;
            var zone = GUILayoutUtility.GetRect(new GUIContent("+"), Styles.DropZone, GUILayout.Height(26), GUILayout.ExpandWidth(true));
            if (GUI.Button(zone, dragging ? "Отпустите здесь, чтобы создать новую стадию" : "+ Новая стадия", Styles.DropZone))
            {
                Apply("Новая стадия", () =>
                {
                    Undo.RecordObject(mode, "Новая стадия");
                    group.stages.Add(new StepStage());
                });
            }
            HandleDrop(zone, steps =>
            {
                Undo.RecordObject(mode, "Новая стадия");
                var stage = new StepStage();
                group.stages.Add(stage);
                ScenarioBuilder.PlaceSteps(mode, steps, stage);
            });

            EditorGUILayout.EndVertical();
        }

        if (mode.groups.Count == 0)
            EditorGUILayout.HelpBox("В режиме нет ни одной группы. Добавьте группу, а в неё стадии с шагами.", MessageType.Warning);

        if (GUILayout.Button("+ Новая группа", GUILayout.Width(200)))
        {
            Apply("Новая группа", () =>
            {
                Undo.RecordObject(mode, "Новая группа");
                mode.groups.Add(new StepGroup { name = $"Группа {mode.groups.Count + 1}" });
            });
        }
        Help("Группа — крупная часть сценария (подготовка, запуск, контроль). Лимит ошибок из правил считается для каждой группы отдельно.");
        EndSection();
    }

    private void DrawStage(ScenarioMode mode, StepGroup group, int index)
    {
        var stage = group.stages[index];
        var rect = EditorGUILayout.BeginVertical(Styles.Stage);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(stage.steps.Count > 1 ? $"Стадия {index + 1}  ·  шаги в любом порядке" : $"Стадия {index + 1}", EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("+ шаг", "Выбрать шаг из списка"), EditorStyles.miniButton, GUILayout.Width(52)))
            ShowStageMenu(mode, stage);
        GUILayout.Space(4);
        OrderButtons(mode, group.stages, index, "Порядок стадий", stage.steps.Count > 0 ? $"Удалить стадию {index + 1}? Сами шаги останутся в сцене." : null);
        EditorGUILayout.EndHorizontal();

        for (var i = 0; i < stage.steps.Count; i++)
        {
            var position = i;
            StepChip(stage.steps[i], () => Apply("Шаг убран из стадии", () =>
            {
                Undo.RecordObject(mode, "Шаг убран из стадии");
                stage.steps.RemoveAt(position);
            }), null);
        }

        if (stage.steps.Count == 0)
            Note("Пустая стадия: перетащите сюда шаги или нажмите «+ шаг».");

        EditorGUILayout.EndVertical();
        HandleDrop(rect, steps => ScenarioBuilder.PlaceSteps(mode, steps, stage));
    }

    private void DrawUnused(ScenarioMode mode)
    {
        var unused = _scene.steps.Where(step => !ScenarioScene.Contains(mode, step)).ToList();
        if (unused.Count == 0)
            return;

        BeginSection("Шаги, которых нет в этом режиме", "Перетащите шаг в нужную стадию или в «+ Новая стадия». Так бывает и специально: некоторые шаги нужны только в другом режиме.", Grey);
        foreach (var step in unused)
            StepChip(step, null, () => ShowAddToModeMenu(step, mode));
        EndSection();
    }

    private void StepChip(ScenarioStep step, Action onRemove, Action onAdd)
    {
        var rect = EditorGUILayout.BeginHorizontal(Styles.Chip);

        var handle = GUILayoutUtility.GetRect(12, 18, GUILayout.Width(12));
        for (var i = 0; i < 3; i++)
            EditorGUI.DrawRect(new Rect(handle.x + 2, handle.y + 5 + i * 4, 8, 1), Grey);

        if (step == null)
        {
            GUILayout.Label("— пустая ячейка —", Styles.ChipCode, GUILayout.ExpandWidth(true));
        }
        else
        {
            if (_runStatus.TryGetValue(step.name, out var status))
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2, 3, rect.height - 4), StatusColor(status));
            GUILayout.Label(Wording.StepTitle(step), Styles.ChipText, GUILayout.ExpandWidth(true));
            GUILayout.Label(_runStatus.TryGetValue(step.name, out status) ? $"{step.name} · {Wording.Status(status)}" : step.name, Styles.ChipCode, GUILayout.ExpandWidth(false));
        }

        if (onAdd != null && GUILayout.Button(new GUIContent("В режим", "Выбрать, в какую стадию добавить"), EditorStyles.miniButton, GUILayout.Width(64)))
            onAdd();
        if (onRemove != null && GUILayout.Button(new GUIContent("×", "Убрать из стадии (шаг останется в сцене)"), EditorStyles.miniButton, GUILayout.Width(22)))
            onRemove();
        EditorGUILayout.EndHorizontal();

        if (step != null)
            HandleChipMouse(rect, step);
    }

    // Перетаскивание шага и двойной щелчок по нему
    private void HandleChipMouse(Rect rect, ScenarioStep step)
    {
        var id = GUIUtility.GetControlID(FocusType.Passive);
        var e = Event.current;
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Pan);

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (e.button != 0 || !rect.Contains(e.mousePosition))
                    break;
                if (e.clickCount == 2)
                {
                    e.Use();
                    Navigate(step);
                    GUIUtility.ExitGUI();
                }
                GUIUtility.hotControl = id;
                e.Use();
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl != id)
                    break;
                GUIUtility.hotControl = 0;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { step.gameObject };
                DragAndDrop.StartDrag(Wording.StepTitle(step));
                e.Use();
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl != id)
                    break;
                GUIUtility.hotControl = 0;
                e.Use();
                break;
        }
    }

    private void HandleDrop(Rect rect, Action<List<ScenarioStep>> drop)
    {
        var e = Event.current;
        if (!rect.Contains(e.mousePosition))
            return;

        if (e.type == EventType.Repaint)
        {
            if (_dragActive && DraggedSteps().Count > 0)
                Outline(rect, Blue);
            return;
        }

        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)
            return;

        var steps = DraggedSteps();
        if (steps.Count == 0)
            return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Move;
        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            _dragActive = false;
            e.Use();
            Apply("Перенос шага", () => drop(steps));
        }
        e.Use();
    }

    private static List<ScenarioStep> DraggedSteps()
    {
        return DragAndDrop.objectReferences
            .Select(obj => obj is GameObject go ? go.GetComponent<ScenarioStep>() : obj as ScenarioStep)
            .Where(step => step != null && step.gameObject.scene.IsValid())
            .Distinct()
            .ToList();
    }

    private void OrderButtons<T>(ScenarioMode mode, List<T> list, int index, string undo, string deleteQuestion)
    {
        using (new EditorGUI.DisabledScope(index == 0))
        {
            if (GUILayout.Button(new GUIContent("▲", "Выше"), EditorStyles.miniButtonLeft, GUILayout.Width(24)))
                Apply(undo, () => Swap(mode, list, index, index - 1, undo));
        }
        using (new EditorGUI.DisabledScope(index == list.Count - 1))
        {
            if (GUILayout.Button(new GUIContent("▼", "Ниже"), EditorStyles.miniButtonMid, GUILayout.Width(24)))
                Apply(undo, () => Swap(mode, list, index, index + 1, undo));
        }
        if (GUILayout.Button(Styles.Remove, EditorStyles.miniButtonRight, GUILayout.Width(24))
            && (deleteQuestion == null || Confirm("Удалить?", deleteQuestion + " Отменить можно через Ctrl+Z.", "Удалить")))
        {
            Apply("Удаление", () =>
            {
                Undo.RecordObject(mode, "Удаление");
                list.RemoveAt(index);
            });
        }
    }

    private static void Swap<T>(ScenarioMode mode, List<T> list, int a, int b, string undo)
    {
        Undo.RecordObject(mode, undo);
        (list[a], list[b]) = (list[b], list[a]);
    }

    private void ShowStageMenu(ScenarioMode mode, StepStage stage)
    {
        var menu = new GenericMenu();
        var unused = _scene.steps.Where(step => !ScenarioScene.Contains(mode, step)).ToList();
        var used = _scene.steps.Where(step => ScenarioScene.Contains(mode, step)).ToList();

        foreach (var step in unused)
        {
            menu.AddItem(new GUIContent(Wording.MenuText($"{Wording.StepTitle(step)}  ({step.name})")), false,
                () => ApplyFromMenu("Шаг в стадии", () => ScenarioBuilder.PlaceSteps(mode, new[] { step }, stage)));
        }

        if (unused.Count > 0 && used.Count > 0)
            menu.AddSeparator("");

        foreach (var step in used)
        {
            var label = Wording.MenuText($"{Wording.StepTitle(step)}  ({step.name})");
            if (stage.steps.Contains(step))
                menu.AddDisabledItem(new GUIContent(label + " — уже здесь"));
            else
                menu.AddItem(new GUIContent(label + " — перенести сюда"), false, () => ApplyFromMenu("Шаг в стадии", () => ScenarioBuilder.PlaceSteps(mode, new[] { step }, stage)));
        }

        if (_scene.steps.Count == 0)
            menu.AddDisabledItem(new GUIContent("В сцене нет шагов"));
        menu.ShowAsContext();
    }
}
