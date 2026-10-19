using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(ScenarioMode))]
public class ScenarioModeEditor : Editor
{
    private readonly Dictionary<string, ReorderableList> _stepLists = new Dictionary<string, ReorderableList>();
    private SerializedProperty _groups;

    private void OnEnable()
    {
        _groups = serializedObject.FindProperty("groups");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("title"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("retryAfterViolation"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("violationLimit"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("outOfTurn"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("analyzer"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Группы и стадии", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Группы и стадии идут по очереди, шаги одной стадии активны одновременно. Шаги можно перетаскивать в стадию прямо из иерархии.", MessageType.None);

        for (var i = 0; i < _groups.arraySize; i++)
            DrawGroup(i);

        if (GUILayout.Button("Добавить группу"))
        {
            var group = AddElement(_groups);
            group.FindPropertyRelative("name").stringValue = $"Группа {_groups.arraySize}";
            group.FindPropertyRelative("stages").ClearArray();
            ApplyStructureChange();
        }

        serializedObject.ApplyModifiedProperties();

        DrawOrder();
        DrawProblems();
    }

    private void DrawGroup(int index)
    {
        var group = _groups.GetArrayElementAtIndex(index);
        var stages = group.FindPropertyRelative("stages");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(12);
        var foldoutRect = GUILayoutUtility.GetRect(80, EditorGUIUtility.singleLineHeight, GUILayout.Width(80));
        group.isExpanded = EditorGUI.Foldout(foldoutRect, group.isExpanded, $"Группа {index + 1}", true);
        EditorGUILayout.PropertyField(group.FindPropertyRelative("name"), GUIContent.none);
        ElementButtons(_groups, index);
        EditorGUILayout.EndHorizontal();

        if (group.isExpanded)
        {
            for (var i = 0; i < stages.arraySize; i++)
                DrawStage(stages, i);

            if (GUILayout.Button("Добавить стадию"))
            {
                AddElement(stages).FindPropertyRelative("steps").ClearArray();
                ApplyStructureChange();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawStage(SerializedProperty stages, int index)
    {
        var steps = stages.GetArrayElementAtIndex(index).FindPropertyRelative("steps");

        EditorGUILayout.BeginHorizontal();
        var label = steps.arraySize > 1 ? $"Стадия {index + 1} — шаги одновременно" : $"Стадия {index + 1}";
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        ElementButtons(stages, index);
        EditorGUILayout.EndHorizontal();

        var area = EditorGUILayout.BeginVertical();
        GetStepList(steps).DoLayoutList();
        EditorGUILayout.EndVertical();
        HandleDrop(area, steps);
    }

    private void ElementButtons(SerializedProperty array, int index)
    {
        using (new EditorGUI.DisabledScope(index == 0))
        {
            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24)))
            {
                array.MoveArrayElement(index, index - 1);
                ApplyStructureChange();
            }
        }

        using (new EditorGUI.DisabledScope(index == array.arraySize - 1))
        {
            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24)))
            {
                array.MoveArrayElement(index, index + 1);
                ApplyStructureChange();
            }
        }

        if (GUILayout.Button("×", EditorStyles.miniButtonRight, GUILayout.Width(24)))
        {
            array.DeleteArrayElementAtIndex(index);
            ApplyStructureChange();
        }
    }

    private ReorderableList GetStepList(SerializedProperty steps)
    {
        if (_stepLists.TryGetValue(steps.propertyPath, out var list))
            return list;

        list = new ReorderableList(serializedObject, steps, true, false, true, true);
        list.drawElementCallback = (rect, i, active, focused) =>
        {
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(rect, list.serializedProperty.GetArrayElementAtIndex(i), GUIContent.none);
        };
        list.drawNoneElementCallback = rect => EditorGUI.LabelField(rect, "Перетащите сюда шаги из иерархии");
        list.onAddCallback = l => AddElement(l.serializedProperty).objectReferenceValue = null;
        _stepLists.Add(steps.propertyPath, list);
        return list;
    }

    private void DrawOrder()
    {
        var mode = (ScenarioMode)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Порядок активации", EditorStyles.boldLabel);
        foreach (var group in mode.groups)
        {
            var stages = group.stages.Select(stage => "[" + string.Join(", ", stage.steps.Select(StepName)) + "]");
            EditorGUILayout.LabelField($"{group.name}: {string.Join(" → ", stages)}", EditorStyles.wordWrappedLabel);
        }
    }

    private void DrawProblems()
    {
        var mode = (ScenarioMode)target;
        var used = new List<ScenarioStep>();
        var duplicates = new List<string>();
        var hasEmpty = false;

        foreach (var group in mode.groups)
        {
            hasEmpty |= group.stages.Count == 0;
            foreach (var stage in group.stages)
            {
                hasEmpty |= stage.steps.Count == 0;
                foreach (var step in stage.steps)
                {
                    if (step == null)
                        hasEmpty = true;
                    else if (used.Contains(step))
                        duplicates.Add(step.name);
                    else
                        used.Add(step);
                }
            }
        }

        var sameNames = used.GroupBy(step => step.name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        if (duplicates.Count > 0)
            EditorGUILayout.HelpBox("Шаги добавлены несколько раз: " + string.Join(", ", duplicates), MessageType.Error);

        if (sameNames.Count > 0)
            EditorGUILayout.HelpBox("У разных шагов одинаковые имена, в журнале их не различить: " + string.Join(", ", sameNames), MessageType.Error);

        if (hasEmpty)
            EditorGUILayout.HelpBox("Есть пустые группы, стадии или ячейки шагов", MessageType.Warning);

        if (!mode.gameObject.scene.IsValid())
            return;

        var unused = mode.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ScenarioStep>(true))
            .Where(step => !used.Contains(step))
            .Select(step => step.name)
            .ToList();

        if (unused.Count > 0)
            EditorGUILayout.HelpBox("Не участвуют в режиме: " + string.Join(", ", unused), MessageType.Info);
    }

    private void ApplyStructureChange()
    {
        serializedObject.ApplyModifiedProperties();
        _stepLists.Clear();
        GUIUtility.ExitGUI();
    }

    private static void HandleDrop(Rect area, SerializedProperty steps)
    {
        var e = Event.current;
        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform || !area.Contains(e.mousePosition))
            return;

        var dropped = DragAndDrop.objectReferences.Select(ToStep).Where(step => step != null).ToList();
        if (dropped.Count == 0)
            return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (var step in dropped)
                AddElement(steps).objectReferenceValue = step;
        }

        e.Use();
    }

    private static ScenarioStep ToStep(Object obj)
    {
        if (obj is GameObject go)
            return go.TryGetComponent(out ScenarioStep step) ? step : null;

        return obj as ScenarioStep;
    }

    private static SerializedProperty AddElement(SerializedProperty array)
    {
        array.arraySize++;
        return array.GetArrayElementAtIndex(array.arraySize - 1);
    }

    private static string StepName(ScenarioStep step)
    {
        return step != null ? step.name : "—";
    }
}
