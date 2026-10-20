using System.Linq;
using UnityEditor;
using UnityEngine;

// Вкладка «Шаги»: задание, когда шаг засчитывается, нарушения, проверки, подсказки и режимы
public partial class ScenarioEditorWindow
{
    private void DrawStepsPage()
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
        foreach (var step in _scene.steps)
        {
            var subtitle = step.name;
            Color? stripe = null;
            if (_runStatus.TryGetValue(step.name, out var status))
            {
                subtitle += "  ·  " + Wording.Status(status);
                stripe = StatusColor(status);
            }

            if (ListItem(Wording.StepTitle(step), subtitle, step == _step, stripe, WorstProblem(step)))
                SelectStep(step);
        }
        if (_scene.steps.Count == 0)
            Note("Шагов пока нет. Шаг — это одно задание для игрока: «Наденьте каску», «Откройте вентиль».");
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("+ Новый шаг", GUILayout.Height(30)))
                Apply("Новый шаг", () => SelectStep(ScenarioBuilder.CreateStep(_scene)));
        }
        GUILayout.Space(4);
        EditorGUILayout.EndVertical();

        VerticalLine();

        _cardScroll = EditorGUILayout.BeginScrollView(_cardScroll);
        GUILayout.Space(4);
        if (_step != null)
            DrawStepCard(_step);
        else
            Note("Выберите шаг слева или создайте новый.");
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawStepCard(ScenarioStep step)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6);
        GUILayout.Label(Wording.StepTitle(step, 300), Styles.Title);
        if (GUILayout.Button(new GUIContent("Показать в сцене", "Выделить объект шага в сцене и в списке объектов"), GUILayout.Width(130), GUILayout.Height(22)))
            ShowInScene(step);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Удалить шаг", GUILayout.Width(100), GUILayout.Height(22))
                && Confirm("Удалить шаг?", $"Шаг «{Wording.StepTitle(step)}» будет удалён из сцены и из всех режимов. Отменить можно через Ctrl+Z.", "Удалить"))
            {
                Apply("Удаление шага", () => ScenarioBuilder.DeleteStep(_scene, step));
            }
        }
        GUILayout.Space(6);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);

        if (Application.isPlaying)
            DrawStepTest(step);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            DrawTask(step);
            DrawCompletion(step);
            DrawViolations(step);
            var sequences = step.GetComponents<SequenceCheck>();
            for (var i = 0; i < sequences.Length; i++)
                DrawSequence(step, sequences[i], i);
            DrawStateChecks(step);
            DrawExtraChecks(step);
            DrawMarkers(step);
            DrawUsage(step);
        }

        GUILayout.Space(10);
    }

    private void DrawStepTest(ScenarioStep step)
    {
        BeginSection("Проверка в игре", "Можно засчитать шаг или вызвать нарушение, не выполняя его вручную. Если шаг ещё не начался, это будет считаться действием не по очереди.", Cyan);
        var status = _runStatus.TryGetValue(step.name, out var value) ? Wording.Status(value) : "не участвует в текущем прохождении";
        GUILayout.Label("Состояние: " + status, Styles.Row);

        var running = _scene.runner != null && _scene.runner.IsRunning;
        using (new EditorGUI.DisabledScope(!running))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Засчитать шаг", GUILayout.Height(26), GUILayout.Width(160)))
                step.Complete();
            if (GUILayout.Button("Вызвать нарушение", GUILayout.Height(26), GUILayout.Width(160)))
                step.Violate("Тестовое нарушение из редактора сценариев");
            EditorGUILayout.EndHorizontal();
        }
        if (!running)
            Note("Сначала запустите режим кнопкой вверху окна или в меню игры.");
        EndSection();
    }

    private void DrawTask(ScenarioStep step)
    {
        BeginSection("Задание для игрока", "Этот текст игрок видит в списке заданий, пока шаг активен.", Blue);
        StringField(step, "_description", true);
        if (string.IsNullOrWhiteSpace(step.Description))
            EditorGUILayout.HelpBox("Напишите, что игрок должен сделать.", MessageType.Warning);

        GUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Код шага", "Имя объекта шага в сцене"), GUILayout.Width(70));
        EditorGUI.BeginChangeCheck();
        var code = EditorGUILayout.DelayedTextField(step.name);
        if (EditorGUI.EndChangeCheck() && !string.IsNullOrWhiteSpace(code))
        {
            Apply("Код шага", () =>
            {
                Undo.RecordObject(step.gameObject, "Код шага");
                step.gameObject.name = code.Trim();
            });
        }
        EditorGUILayout.EndHorizontal();
        Help("Под этим кодом шаг записывается в журнал прохождения. У каждого шага код должен быть свой.");
        if (_scene.steps.Any(other => other != step && other.name == step.name))
            EditorGUILayout.HelpBox("Такой код уже есть у другого шага, в журнале их будет не различить.", MessageType.Error);
        EndSection();
    }

    private void DrawCompletion(ScenarioStep step)
    {
        BeginSection("Шаг засчитывается, когда…", "Достаточно любого из условий.", Green);

        var calls = _scene.CallsTo(step, nameof(ScenarioStep.Complete)).ToList();
        foreach (var call in calls)
        {
            var own = IsOwnCheck(step, call);
            var text = Wording.Trigger(call.owner, call.eventName) + (own ? "  (см. проверку ниже)" : "");
            if (Line(text, own ? null : call.owner, !own))
                Apply("Удаление условия", () => EventEditing.Remove(call));
            DrawHow(call.owner);
        }

        if (calls.Count == 0)
            EditorGUILayout.HelpBox("Пока шаг ничем не засчитывается, и выполнить его невозможно. Добавьте условие.", MessageType.Warning);

        PickerSlot("complete", "+ Добавить условие", "Шаг засчитывается, когда…", null, "", _ =>
        {
            var source = _picker.CreateSource();
            EventEditing.Add(source.owner, source.eventName, EventAction.Call(step, nameof(ScenarioStep.Complete)));
        });
        EndSection();
    }

    private void DrawViolations(ScenarioStep step)
    {
        BeginSection("Нарушение, когда…", "Что игрок может сделать неправильно. Сообщение он увидит на экране, а нарушение попадёт в журнал и повлияет на оценку.", Red);

        var calls = _scene.CallsTo(step, nameof(ScenarioStep.Violate)).ToList();
        foreach (var call in calls)
        {
            var own = IsOwnCheck(step, call);
            if (Line(Wording.Trigger(call.owner, call.eventName), own ? null : call.owner, !own))
                Apply("Удаление нарушения", () => EventEditing.Remove(call));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(18);
            GUILayout.Label("Сообщение:", GUILayout.Width(76));
            StringField(call.owner, call.StringArgPath);
            GUILayout.Space(25);
            EditorGUILayout.EndHorizontal();
            DrawHow(call.owner);
        }

        if (calls.Count == 0)
            Note("Нарушений нет: этот шаг нельзя выполнить неправильно.");

        PickerSlot("violate", "+ Добавить нарушение", "Нарушение, когда…", "Сообщение игроку", "", text =>
        {
            var source = _picker.CreateSource();
            var message = string.IsNullOrWhiteSpace(text) ? "Нарушение" : text.Trim();
            EventEditing.Add(source.owner, source.eventName, EventAction.WithString(step, nameof(ScenarioStep.Violate), message));
        });
        EndSection();
    }

    // Как включается объект-переключатель, от которого зависит шаг
    private void DrawHow(Component owner)
    {
        if (!(owner is ObjectState state))
            return;

        foreach (var call in _scene.CallsTo(state).Where(c => c.method != nameof(ObjectState.ResetState)))
            SubLine($"Как: {Wording.Trigger(call.owner, call.eventName)} → {Wording.Action(call)}");
    }

    private static bool IsOwnCheck(ScenarioStep step, EventCall call)
    {
        return (call.owner is SequenceCheck || call.owner is StateCheck) && call.owner.gameObject == step.gameObject;
    }

    private void DrawSequence(ScenarioStep step, SequenceCheck sequence, int index)
    {
        BeginSection("Проверка порядка действий", "Игрок должен сделать эти действия строго по очереди. Если ошибётся, будет нарушение (сообщение — в разделе «Нарушение»), и порядок придётся начать сначала.", Purple);

        var pushes = _scene.CallsTo(sequence, nameof(SequenceCheck.Push)).ToList();
        var order = sequence.Order;
        for (var i = 0; i < order.Count; i++)
        {
            var key = order[i];
            var keyIndex = i;

            EditorGUILayout.BeginVertical(Styles.Inner);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"{i + 1}.", EditorStyles.boldLabel, GUILayout.Width(22));
            EditorGUI.BeginChangeCheck();
            var renamed = EditorGUILayout.DelayedTextField(key);
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrWhiteSpace(renamed) && renamed != key)
                Apply("Переименование действия", () => ScenarioBuilder.RenameSequenceKey(_scene, sequence, keyIndex, renamed.Trim()));

            using (new EditorGUI.DisabledScope(i == 0))
            {
                if (GUILayout.Button(new GUIContent("▲", "Раньше"), EditorStyles.miniButtonLeft, GUILayout.Width(24)))
                    Apply("Порядок действий", () => MoveSequenceKey(sequence, keyIndex, keyIndex - 1));
            }
            using (new EditorGUI.DisabledScope(i == order.Count - 1))
            {
                if (GUILayout.Button(new GUIContent("▼", "Позже"), EditorStyles.miniButtonMid, GUILayout.Width(24)))
                    Apply("Порядок действий", () => MoveSequenceKey(sequence, keyIndex, keyIndex + 1));
            }
            if (GUILayout.Button(Styles.Remove, EditorStyles.miniButtonRight, GUILayout.Width(24)))
                Apply("Удаление действия", () => ScenarioBuilder.RemoveSequenceKey(_scene, sequence, keyIndex));
            EditorGUILayout.EndHorizontal();

            var marks = pushes.Where(c => c.stringArg == key).ToList();
            if (marks.Count == 0)
                EditorGUILayout.HelpBox("Действие пока ничем не отмечается. Укажите, когда оно считается сделанным.", MessageType.Warning);
            else
                Note("Считается сделанным, когда:");
            foreach (var call in marks)
            {
                if (Line(Wording.Trigger(call.owner, call.eventName), call.owner, true))
                    Apply("Удаление условия", () => EventEditing.Remove(call));
            }

            PickerSlot($"sequence{index}:{i}", "+ Когда", $"Действие «{key}» сделано, когда…", null, "", _ =>
            {
                var source = _picker.CreateSource();
                EventEditing.Add(source.owner, source.eventName, EventAction.WithString(sequence, nameof(SequenceCheck.Push), key));
            });
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Добавить действие", GUILayout.Width(200)))
            Apply("Новое действие", () => AddSequenceKey(sequence));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Убрать проверку порядка", GUILayout.Width(180))
            && Confirm("Убрать проверку порядка?", "Проверка и все её связи будут удалены. Отменить можно через Ctrl+Z.", "Убрать"))
        {
            Apply("Удаление проверки порядка", () => ScenarioBuilder.RemoveSequence(_scene, sequence));
        }
        EditorGUILayout.EndHorizontal();
        EndSection();
    }

    private static void MoveSequenceKey(SequenceCheck sequence, int from, int to)
    {
        var so = new SerializedObject(sequence);
        so.FindProperty("_order").MoveArrayElement(from, to);
        so.ApplyModifiedProperties();
    }

    private static void AddSequenceKey(SequenceCheck sequence)
    {
        var number = sequence.Order.Count + 1;
        while (sequence.Order.Contains($"Действие {number}"))
            number++;

        var so = new SerializedObject(sequence);
        var order = so.FindProperty("_order");
        order.arraySize++;
        order.GetArrayElementAtIndex(order.arraySize - 1).stringValue = $"Действие {number}";
        so.ApplyModifiedProperties();
    }

    private void DrawStateChecks(ScenarioStep step)
    {
        var chain = _scene.StateChain(step);
        if (chain.Count == 0)
            return;

        BeginSection("Проверка состояния объектов", "Прежде чем засчитать шаг, проверяется, что объекты включены или выключены как надо. Если нет, будет нарушение (сообщение — в разделе «Нарушение»).", Orange);

        var states = _scene.All<ObjectState>().ToList();
        var names = new[] { "— выберите объект —" }.Concat(states.Select(s => Wording.MenuText(Wording.Name(s)))).ToArray();

        for (var i = 0; i < chain.Count; i++)
        {
            var check = chain[i];
            EditorGUILayout.BeginHorizontal(Styles.Inner);
            GUILayout.Label($"{i + 1}.", EditorStyles.boldLabel, GUILayout.Width(22));

            var so = new SerializedObject(check);
            var target = so.FindProperty("_target");
            var requiredOn = so.FindProperty("_requiredOn");
            EditorGUI.BeginChangeCheck();
            var picked = EditorGUILayout.Popup(states.IndexOf(check.Target) + 1, names);
            GUILayout.Label("должен быть", GUILayout.Width(80));
            var state = EditorGUILayout.Popup(requiredOn.boolValue ? 0 : 1, new[] { "включён", "выключен" }, GUILayout.Width(90));
            if (EditorGUI.EndChangeCheck())
            {
                target.objectReferenceValue = picked > 0 ? states[picked - 1] : null;
                requiredOn.boolValue = state == 0;
                so.ApplyModifiedProperties();
            }

            if (GUILayout.Button(Styles.Remove, EditorStyles.miniButton, GUILayout.Width(22)))
                Apply("Удаление проверки", () => ScenarioBuilder.RemoveStateCheck(_scene, check));
            EditorGUILayout.EndHorizontal();

            if (check.Target == null)
                EditorGUILayout.HelpBox("Выберите, какой объект проверять. В списке только переключатели — объекты, которые бывают включены и выключены.", MessageType.Error);
        }

        GUILayout.Space(4);
        var head = chain[0];
        var triggers = _scene.CallsTo(head, nameof(StateCheck.Check)).Where(c => !(c.owner is StateCheck other && chain.Contains(other))).ToList();
        GUILayout.Label("Проверка выполняется, когда:", EditorStyles.boldLabel);
        foreach (var call in triggers)
        {
            if (Line(Wording.Trigger(call.owner, call.eventName), call.owner, true))
                Apply("Удаление условия", () => EventEditing.Remove(call));
            DrawHow(call.owner);
        }
        if (triggers.Count == 0)
            EditorGUILayout.HelpBox("Проверка ничем не запускается, поэтому шаг не засчитается. Добавьте, когда проверять.", MessageType.Warning);
        PickerSlot("check", "+ Когда", "Проверка выполняется, когда…", null, "", _ =>
        {
            var source = _picker.CreateSource();
            EventEditing.Add(source.owner, source.eventName, EventAction.Call(head, nameof(StateCheck.Check)));
        });

        var last = chain[chain.Count - 1];
        var extra = _scene.CallsFrom(last, nameof(StateCheck.onPassed))
            .Where(c => !(c.target == step && c.method == nameof(ScenarioStep.Complete)))
            .ToList();
        if (extra.Count > 0)
        {
            GUILayout.Space(4);
            GUILayout.Label("Если всё верно, ещё:", EditorStyles.boldLabel);
            foreach (var call in extra)
            {
                if (Line(Wording.Action(call), null, true))
                    Apply("Удаление действия", () => EventEditing.Remove(call));
            }
        }

        GUILayout.Space(4);
        if (GUILayout.Button("+ Ещё одна проверка", GUILayout.Width(200)))
            Apply("Проверка состояния", () => ScenarioBuilder.AddStateCheck(_scene, step));
        EndSection();
    }

    private void DrawExtraChecks(ScenarioStep step)
    {
        var hasSequence = step.GetComponent<SequenceCheck>() != null;
        var hasState = step.GetComponent<StateCheck>() != null;
        if (hasSequence && hasState)
            return;

        BeginSection("Дополнительные проверки", "Нужны, когда шаг нельзя засчитать одним событием.", Grey);
        if (!hasSequence)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Порядок действий", GUILayout.Width(200)))
                Apply("Проверка порядка", () => ScenarioBuilder.AddSequence(step));
            Note("Несколько действий строго по очереди: например, сначала пост A, потом пост B.");
            EditorGUILayout.EndHorizontal();
        }
        if (!hasState)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Состояние объектов", GUILayout.Width(200)))
                Apply("Проверка состояния", () => ScenarioBuilder.AddStateCheck(_scene, step));
            Note("Засчитать шаг, только если объекты включены или выключены как надо: например, пустить насос только при открытом вентиле.");
            EditorGUILayout.EndHorizontal();
        }
        EndSection();
    }

    private void DrawMarkers(ScenarioStep step)
    {
        BeginSection("Подсказки на время шага", "Эти объекты появляются, когда шаг начинается, и исчезают, когда он закончен. Например, жёлтый шар над местом, куда нужно подойти.", Yellow);

        var shows = _scene.CallsFrom(step, nameof(ScenarioStep.onActivated)).Where(c => IsSetActive(c, true)).ToList();
        var hides = _scene.CallsFrom(step, nameof(ScenarioStep.onDeactivated)).Where(c => IsSetActive(c, false)).ToList();
        var markers = shows.Select(c => (GameObject)c.target).Distinct().ToList();

        foreach (var marker in markers)
        {
            var related = shows.Concat(hides).Where(c => c.target == marker).ToList();
            if (Line(Wording.Name(marker), marker, true))
                Apply("Удаление подсказки", () => EventEditing.RemoveAll(related));

            if (hides.All(c => c.target != marker))
            {
                EditorGUILayout.BeginHorizontal();
                SubLine("Не исчезает после шага.");
                if (GUILayout.Button("Исправить", EditorStyles.miniButton, GUILayout.Width(80)))
                    Apply("Подсказка", () => EventEditing.Add(step, nameof(ScenarioStep.onDeactivated), EventAction.WithBool(marker, nameof(GameObject.SetActive), false)));
                EditorGUILayout.EndHorizontal();
            }

            if (marker.activeSelf && !Application.isPlaying)
            {
                EditorGUILayout.BeginHorizontal();
                SubLine("Виден с самого начала, а не только во время шага.");
                if (GUILayout.Button("Скрыть", EditorStyles.miniButton, GUILayout.Width(80)))
                {
                    Apply("Скрыть подсказку", () =>
                    {
                        Undo.RecordObject(marker, "Скрыть подсказку");
                        marker.SetActive(false);
                    });
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        if (markers.Count == 0)
            Note("Подсказок нет.");

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Добавить:", GUILayout.Width(70));
        var added = (GameObject)EditorGUILayout.ObjectField(null, typeof(GameObject), true);
        EditorGUILayout.EndHorizontal();
        Help("Перетащите сюда объект из сцены или из списка объектов (Hierarchy). До начала шага он будет скрыт.");
        if (added != null && added.scene.IsValid() && !markers.Contains(added))
            Apply("Подсказка", () => ScenarioBuilder.SetMarker(step, added));

        var other = _scene.CallsFrom(step, nameof(ScenarioStep.onActivated))
            .Where(c => !IsSetActive(c, true) && !(c.target is SequenceCheck && c.method == nameof(SequenceCheck.ResetProgress)))
            .Concat(_scene.CallsFrom(step, nameof(ScenarioStep.onDeactivated)).Where(c => !(IsSetActive(c, false) && markers.Contains(c.target as GameObject))))
            .ToList();
        if (other.Count > 0)
        {
            GUILayout.Space(4);
            GUILayout.Label("Ещё при начале и конце шага:", EditorStyles.boldLabel);
            foreach (var call in other)
            {
                var when = call.eventName == nameof(ScenarioStep.onActivated) ? "В начале" : "В конце";
                if (Line($"{when}: {Wording.Action(call)}", null, true))
                    Apply("Удаление действия", () => EventEditing.Remove(call));
            }
        }
        EndSection();
    }

    private static bool IsSetActive(EventCall call, bool value)
    {
        return call.target is GameObject && call.method == nameof(GameObject.SetActive) && call.boolArg == value;
    }

    private void DrawUsage(ScenarioStep step)
    {
        BeginSection("В каких режимах", "Один и тот же шаг можно включить в несколько режимов: например, в тренировку и в экзамен.", Grey);

        var used = false;
        foreach (var mode in _scene.modes)
        {
            foreach (var group in mode.groups)
            {
                for (var i = 0; i < group.stages.Count; i++)
                {
                    var stage = group.stages[i];
                    if (!stage.steps.Contains(step))
                        continue;

                    used = true;
                    var others = stage.steps.Count(s => s != step);
                    var text = $"«{Wording.ModeTitle(mode)}»: группа «{group.name}», стадия {i + 1}";
                    if (others > 0)
                        text += $", вместе с ещё {Wording.Count(others, "шагом", "шагами", "шагами")}";

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("•", GUILayout.Width(10));
                    GUILayout.Label(text, Styles.Row);
                    if (GUILayout.Button("Открыть", EditorStyles.miniButton, GUILayout.Width(64)))
                        Navigate(mode);
                    if (GUILayout.Button(new GUIContent("×", "Убрать из режима"), EditorStyles.miniButton, GUILayout.Width(22)))
                        Apply("Шаг убран из режима", () => ScenarioBuilder.RemoveFromMode(mode, step));
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        if (!used)
            EditorGUILayout.HelpBox("Шаг не входит ни в один режим, поэтому в игре он не появится.", MessageType.Info);

        if (GUILayout.Button("+ Добавить в режим", GUILayout.Width(200)))
            ShowAddToModeMenu(step, null);
        EndSection();
    }

    private void ShowAddToModeMenu(ScenarioStep step, ScenarioMode onlyMode)
    {
        var menu = new GenericMenu();
        var modes = onlyMode != null ? new[] { onlyMode } : _scene.modes.ToArray();

        foreach (var mode in modes)
        {
            var prefix = onlyMode != null ? "" : Wording.MenuText(Wording.ModeTitle(mode)) + "/";
            if (mode.groups.Count == 0)
            {
                menu.AddItem(new GUIContent(prefix + "Новая группа"), false, () => ApplyFromMenu("Шаг в режиме", () =>
                {
                    Undo.RecordObject(mode, "Шаг в режиме");
                    var stage = new StepStage();
                    mode.groups.Add(new StepGroup { name = "Группа 1", stages = { stage } });
                    ScenarioBuilder.PlaceSteps(mode, new[] { step }, stage);
                }));
                continue;
            }

            for (var g = 0; g < mode.groups.Count; g++)
            {
                var group = mode.groups[g];
                var path = prefix + Wording.MenuText($"Группа {g + 1}. {group.name}") + "/";
                for (var s = 0; s < group.stages.Count; s++)
                {
                    var stage = group.stages[s];
                    var label = $"Стадия {s + 1}";
                    if (stage.steps.Count > 0)
                        label += $" (с шагом «{Wording.MenuText(Wording.StepTitle(stage.steps.FirstOrDefault(other => other != step) ?? stage.steps[0]))}»)";

                    if (stage.steps.Contains(step))
                        menu.AddDisabledItem(new GUIContent(path + label + " — уже здесь"));
                    else
                        menu.AddItem(new GUIContent(path + label), false, () => ApplyFromMenu("Шаг в режиме", () => ScenarioBuilder.PlaceSteps(mode, new[] { step }, stage)));
                }

                menu.AddItem(new GUIContent(path + "Новая стадия в конце"), false, () => ApplyFromMenu("Шаг в режиме", () =>
                {
                    Undo.RecordObject(mode, "Шаг в режиме");
                    var stage = new StepStage();
                    group.stages.Add(stage);
                    ScenarioBuilder.PlaceSteps(mode, new[] { step }, stage);
                }));
            }
        }

        if (menu.GetItemCount() == 0)
            menu.AddDisabledItem(new GUIContent("В сцене нет режимов"));
        menu.ShowAsContext();
    }
}
