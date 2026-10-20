using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Редактор сценариев для тех, кто не знаком с Unity: шаги, режимы и проверка сцены в одном окне.
// Все изменения идут через Undo, поэтому Ctrl+Z работает как обычно
public partial class ScenarioEditorWindow : EditorWindow
{
    private enum Page
    {
        Steps,
        Modes,
        Checks
    }

    private const float ListWidth = 280;
    private const string DemoScene = "Assets/Scenes/ScenarioDemo.unity";
    private const string HelpPref = "ScenarioEditor.ShowHelp";

    private static readonly Color Green = new Color(0.36f, 0.72f, 0.36f);
    private static readonly Color Red = new Color(0.87f, 0.33f, 0.31f);
    private static readonly Color Blue = new Color(0.30f, 0.56f, 0.90f);
    private static readonly Color Yellow = new Color(0.95f, 0.77f, 0.25f);
    private static readonly Color Purple = new Color(0.62f, 0.45f, 0.86f);
    private static readonly Color Orange = new Color(0.95f, 0.55f, 0.20f);
    private static readonly Color Cyan = new Color(0.25f, 0.75f, 0.80f);
    private static readonly Color Grey = new Color(0.5f, 0.5f, 0.5f);

    [SerializeField] private Page _page;
    [SerializeField] private ScenarioStep _step;
    [SerializeField] private ScenarioMode _mode;
    [SerializeField] private Vector2 _listScroll;
    [SerializeField] private Vector2 _cardScroll;

    private readonly ScenarioScene _scene = new ScenarioScene();
    private readonly Dictionary<string, StepStatus> _runStatus = new Dictionary<string, StepStatus>();
    private List<ScenarioProblem> _problems = new List<ScenarioProblem>();
    private SourcePicker _picker;
    private bool _dirty = true;
    private bool _showHelp;
    private bool _dragActive;
    private Action _pending;
    private double _nextPlayRepaint;

    [MenuItem("Tools/Редактор сценариев", priority = 0)]
    private static void OpenFromMenu()
    {
        OpenWindow();
    }

    public static void Open(ScenarioStep step)
    {
        OpenWindow().SelectStep(step, true);
    }

    public static void Open(ScenarioMode mode)
    {
        OpenWindow().SelectMode(mode, true);
    }

    private static ScenarioEditorWindow OpenWindow()
    {
        var window = GetWindow<ScenarioEditorWindow>();
        window.titleContent = new GUIContent("Редактор сценариев");
        window.minSize = new Vector2(780, 460);
        window.Show();
        return window;
    }

    private void OnEnable()
    {
        _showHelp = EditorPrefs.GetBool(HelpPref, true);
        _picker = new SourcePicker(MarkDirty);
        wantsMouseMove = true;

        ObjectChangeEvents.changesPublished += OnChangesPublished;
        Undo.undoRedoPerformed += MarkDirty;
        EditorApplication.hierarchyChanged += MarkDirty;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        MarkDirty();
    }

    private void OnDisable()
    {
        ObjectChangeEvents.changesPublished -= OnChangesPublished;
        Undo.undoRedoPerformed -= MarkDirty;
        EditorApplication.hierarchyChanged -= MarkDirty;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorSceneManager.sceneOpened -= OnSceneOpened;
    }

    private void OnChangesPublished(ref ObjectChangeEventStream stream)
    {
        MarkDirty();
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        MarkDirty();
    }

    private void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        MarkDirty();
    }

    private void MarkDirty()
    {
        _dirty = true;
        Repaint();
    }

    private void OnSelectionChange()
    {
        var go = Selection.activeGameObject;
        if (go == null)
            return;

        if (go.TryGetComponent(out ScenarioStep step) && step != _step)
            SelectStep(step);
        else if (go.TryGetComponent(out ScenarioMode mode) && mode != _mode)
            SelectMode(mode);
    }

    private void Update()
    {
        if (!Application.isPlaying || EditorApplication.timeSinceStartup < _nextPlayRepaint)
            return;

        _nextPlayRepaint = EditorApplication.timeSinceStartup + 0.25;
        Repaint();
    }

    private void OnGUI()
    {
        Styles.Init();
        EditorGUIUtility.labelWidth = 210;
        var e = Event.current;

        if (e.type == EventType.Layout)
        {
            if (_dirty)
                Rebuild();
            ApplyPending();
            ReadRunStatus();
        }

        if (e.type == EventType.MouseMove)
            Repaint();
        if (e.type == EventType.DragUpdated)
        {
            _dragActive = true;
            Repaint();
        }
        if (e.type == EventType.DragExited)
        {
            _dragActive = false;
            Repaint();
        }

        DrawToolbar();

        if (_scene.steps.Count == 0 && _scene.modes.Count == 0 && _scene.runner == null)
        {
            DrawEmptyScene();
            return;
        }

        DrawPlayBanner();

        switch (_page)
        {
            case Page.Steps:
                DrawStepsPage();
                break;
            case Page.Modes:
                DrawModesPage();
                break;
            default:
                DrawChecksPage();
                break;
        }
    }

    private void Rebuild()
    {
        _dirty = false;
        _scene.Rebuild();
        _problems = ScenarioProblems.Find(_scene);

        if (_step == null || !_scene.steps.Contains(_step))
            _step = _scene.steps.FirstOrDefault();
        if (_mode == null || !_scene.modes.Contains(_mode))
            _mode = _scene.modes.FirstOrDefault();
    }

    private void ReadRunStatus()
    {
        _runStatus.Clear();
        var logger = Application.isPlaying && _scene.runner != null ? _scene.runner.Logger : null;
        if (logger == null)
            return;

        foreach (var entry in logger.Entries)
        {
            if (string.IsNullOrEmpty(entry.stepId))
                continue;

            switch (entry.type)
            {
                case LogEvents.StepPlanned:
                    _runStatus[entry.stepId] = StepStatus.Pending;
                    break;
                case LogEvents.StepActivated:
                    _runStatus[entry.stepId] = StepStatus.Active;
                    break;
                case LogEvents.StepFinished:
                    if (Enum.TryParse(entry.result, out StepStatus status))
                        _runStatus[entry.stepId] = status;
                    break;
            }
        }
    }

    private void SelectStep(ScenarioStep step, bool openPage = false)
    {
        Later(() =>
        {
            if (openPage)
                ShowPage(Page.Steps);
            _step = step;
            _picker.Close();
            _cardScroll = Vector2.zero;
        });
    }

    private void SelectMode(ScenarioMode mode, bool openPage = false)
    {
        Later(() =>
        {
            if (openPage)
                ShowPage(Page.Modes);
            _mode = mode;
            _cardScroll = Vector2.zero;
        });
    }

    private void SetPage(Page page)
    {
        if (_page != page)
            Later(() => ShowPage(page));
    }

    private void ShowPage(Page page)
    {
        if (_page == page)
            return;

        _page = page;
        _listScroll = _cardScroll = Vector2.zero;
        _picker.Close();
    }

    // Выбор меняется только когда ни одно поле не редактируется: иначе Unity допишет набранный текст уже в новый объект
    private void Later(Action action)
    {
        _pending += action;
        Repaint();
    }

    private void ApplyPending()
    {
        if (_pending == null)
            return;

        if (GUIUtility.keyboardControl != 0)
        {
            GUI.FocusControl(null);
            Repaint();
            return;
        }

        var pending = _pending;
        _pending = null;
        pending();
    }

    // Изменение сцены одной записью в Undo. После него окно перерисовывается с новыми данными
    private void Apply(string name, Action change)
    {
        Record(name, change);
        GUIUtility.ExitGUI();
    }

    // То же для пунктов выпадающего меню: они вызываются вне OnGUI, там ExitGUI нельзя
    private void ApplyFromMenu(string name, Action change)
    {
        Record(name, change);
    }

    private void Record(string name, Action change)
    {
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        change();
        Undo.SetCurrentGroupName(name);
        Undo.CollapseUndoOperations(group);
        MarkDirty();
    }

    private void DrawToolbar()
    {
        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6);

        var issues = _problems.Count(p => p.severity != MessageType.Info);
        var checks = issues > 0
            ? new GUIContent($" Проверка ({issues})", _problems.Any(p => p.severity == MessageType.Error) ? Styles.Error.image : Styles.Warning.image)
            : new GUIContent("Проверка");
        var tabs = new[]
        {
            new GUIContent($"Шаги ({_scene.steps.Count})", "Что должен сделать игрок"),
            new GUIContent($"Режимы ({_scene.modes.Count})", "В каком порядке идут шаги и как оценивается прохождение"),
            checks
        };
        SetPage((Page)GUILayout.Toolbar((int)_page, tabs, GUILayout.Height(28), GUILayout.MaxWidth(480)));

        GUILayout.FlexibleSpace();
        EditorGUI.BeginChangeCheck();
        _showHelp = GUILayout.Toggle(_showHelp, new GUIContent("Подсказки", "Показывать пояснения под заголовками"), "Button", GUILayout.Height(28), GUILayout.Width(90));
        if (EditorGUI.EndChangeCheck())
            EditorPrefs.SetBool(HelpPref, _showHelp);

        if (File.Exists("README.md") && GUILayout.Button(new GUIContent("Инструкция", "Открыть README проекта"), GUILayout.Height(28), GUILayout.Width(90)))
            EditorUtility.OpenWithDefaultApp(Path.GetFullPath("README.md"));

        GUILayout.Space(6);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(6);
        HorizontalLine();
    }

    private void DrawEmptyScene()
    {
        GUILayout.Space(10);
        BeginSection("В этой сцене пока нет сценария", null, Blue);
        GUILayout.Label("Сценарий состоит из шагов (что должен сделать игрок) и режимов (в каком порядке идут шаги и как оценивается прохождение). Проще всего начать с демо-сцены: в ней уже есть готовый сценарий запуска насоса.", Styles.Row);
        GUILayout.Space(6);
        if (File.Exists(DemoScene) && GUILayout.Button("Открыть демо-сцену", GUILayout.Height(30), GUILayout.Width(220)))
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(DemoScene);
            GUIUtility.ExitGUI();
        }
        GUILayout.Space(4);
        GUILayout.Label("Чтобы сделать сценарий в своей сцене, скопируйте из демо-сцены объект Scenario (а если нужен готовый интерфейс, то ещё UI и EventSystem) и откройте это окно снова.", Styles.Hint);
        EndSection();
    }

    private void DrawPlayBanner()
    {
        if (!Application.isPlaying)
            return;

        var rect = EditorGUILayout.BeginVertical(Styles.Card);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + 1, 3, rect.height - 2), Yellow);

        var runner = _scene.runner;
        EditorGUILayout.BeginHorizontal();
        if (runner == null)
        {
            GUILayout.Label("Идёт игра, но в сцене нет ScenarioRunner.", EditorStyles.boldLabel);
        }
        else if (runner.IsRunning)
        {
            var mode = runner.Mode;
            GUILayout.Label($"Идёт прохождение «{Wording.ModeTitle(mode)}», группа {runner.GroupIndex + 1} из {mode.groups.Count}: «{runner.CurrentGroup.name}»", Styles.Section);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Заново", GUILayout.Width(80)))
                runner.Restart();
            if (GUILayout.Button("Прервать", GUILayout.Width(80)))
                runner.Abort();
        }
        else
        {
            GUILayout.Label("Идёт игра. Запустить режим:", Styles.Section, GUILayout.ExpandWidth(false));
            foreach (var mode in _scene.modes)
            {
                if (GUILayout.Button("▶ " + Wording.ModeTitle(mode), GUILayout.ExpandWidth(false)))
                    runner.Run(mode);
            }
            GUILayout.FlexibleSpace();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Label("Пока идёт игра, редактирование выключено: изменения всё равно пропали бы после её остановки.", Styles.Hint);
        EditorGUILayout.EndVertical();
    }

    private void DrawChecksPage()
    {
        _cardScroll = EditorGUILayout.BeginScrollView(_cardScroll);
        GUILayout.Space(4);

        var errors = _problems.Count(p => p.severity == MessageType.Error);
        var warnings = _problems.Count(p => p.severity == MessageType.Warning);
        var notes = _problems.Count(p => p.severity == MessageType.Info);

        var accent = errors > 0 ? Red : warnings > 0 ? Yellow : Green;
        BeginSection(errors + warnings == 0 ? "Проблем не найдено, сценарий можно запускать" : "Что стоит исправить", "Список обновляется сам при любом изменении сцены.", accent);
        GUILayout.Label($"Ошибок: {errors}    Предупреждений: {warnings}    Замечаний: {notes}", Styles.Row);
        Help("Ошибки мешают пройти сценарий. Предупреждения — то, что скорее всего сделано случайно. Замечания — просто к сведению.");
        EndSection();

        foreach (var problem in _problems)
        {
            EditorGUILayout.BeginHorizontal(Styles.Card);
            GUILayout.Label(Icon(problem.severity), GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label(problem.text, Styles.Row);
            if (problem.context != null && GUILayout.Button("Показать", GUILayout.Width(80)))
                Navigate(problem.context);
            if (problem.fix != null)
            {
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    if (GUILayout.Button(problem.fixLabel, GUILayout.Width(110)))
                        Apply(problem.fixLabel, problem.fix);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void Navigate(Object context)
    {
        switch (context)
        {
            case ScenarioStep step:
                SelectStep(step, true);
                break;
            case ScenarioMode mode:
                SelectMode(mode, true);
                break;
            case Component component when component.TryGetComponent(out ScenarioStep owner):
                SelectStep(owner, true);
                break;
            default:
                ShowInScene(context);
                break;
        }
    }

    // ---------- Общие элементы ----------

    private void BeginSection(string title, string hint, Color accent)
    {
        var rect = EditorGUILayout.BeginVertical(Styles.Card);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + 1, 3, rect.height - 2), accent);
        GUILayout.Label(title, Styles.Section);
        Help(hint);
        GUILayout.Space(2);
    }

    private static void EndSection()
    {
        EditorGUILayout.EndVertical();
    }

    private void Help(string text)
    {
        if (_showHelp && !string.IsNullOrEmpty(text))
            GUILayout.Label(text, Styles.Hint);
    }

    private static void Note(string text)
    {
        GUILayout.Label(text, Styles.Hint);
    }

    // Строка «• текст  [Найти] [×]». Возвращает true, если нажали ×
    private static bool Line(string text, Object find, bool removable)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("•", GUILayout.Width(10));
        GUILayout.Label(text, Styles.Row);
        if (find != null && GUILayout.Button(new GUIContent("Найти", "Выделить объект в сцене"), EditorStyles.miniButton, GUILayout.Width(50)))
            ShowInScene(find);
        var remove = removable && GUILayout.Button(Styles.Remove, EditorStyles.miniButton, GUILayout.Width(22));
        if (!removable)
            GUILayout.Space(25);
        EditorGUILayout.EndHorizontal();
        return remove;
    }

    private static void SubLine(string text)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(18);
        GUILayout.Label(text, Styles.Hint);
        EditorGUILayout.EndHorizontal();
    }

    private static void StringField(Object owner, string path, bool multiline = false)
    {
        var so = new SerializedObject(owner);
        var property = so.FindProperty(path);
        if (property == null)
            return;

        EditorGUI.BeginChangeCheck();
        var value = multiline
            ? EditorGUILayout.TextArea(property.stringValue, Styles.TextArea, GUILayout.MinHeight(42))
            : EditorGUILayout.TextField(property.stringValue);
        if (EditorGUI.EndChangeCheck())
        {
            property.stringValue = value;
            so.ApplyModifiedProperties();
        }
    }

    private void PickerSlot(string slot, string buttonText, string title, string textLabel, string defaultText, Action<string> add)
    {
        if (_picker.IsOpen(slot))
        {
            if (_picker.Draw(_scene, title, textLabel))
            {
                var text = _picker.Text;
                _picker.Close();
                Apply(title, () => add(text));
            }
        }
        else if (GUILayout.Button(buttonText, GUILayout.Width(200)))
        {
            _picker.Open(slot, defaultText);
        }
    }

    private bool ListItem(string title, string subtitle, bool selected, Color? stripe, MessageType problem)
    {
        var rect = GUILayoutUtility.GetRect(10, 40, GUILayout.ExpandWidth(true));
        var e = Event.current;

        if (selected)
            EditorGUI.DrawRect(rect, SelectedColor);
        else if (rect.Contains(e.mousePosition))
            EditorGUI.DrawRect(rect, HoverColor);
        if (stripe.HasValue)
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 3, 4, rect.height - 6), stripe.Value);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), LineColor);

        var iconWidth = problem == MessageType.None ? 0 : 22;
        GUI.Label(new Rect(rect.x + 10, rect.y + 4, rect.width - 14 - iconWidth, 18), title, Styles.ListTitle);
        GUI.Label(new Rect(rect.x + 10, rect.y + 21, rect.width - 14 - iconWidth, 16), subtitle, Styles.ListSubtitle);
        if (problem != MessageType.None)
            GUI.Label(new Rect(rect.xMax - 22, rect.y + 11, 18, 18), Icon(problem));

        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
        {
            e.Use();
            return true;
        }

        return false;
    }

    private MessageType WorstProblem(Object context)
    {
        var worst = MessageType.None;
        foreach (var problem in _problems)
        {
            if (problem.context == context && problem.severity > worst)
                worst = problem.severity;
        }
        return worst;
    }

    private static GUIContent Icon(MessageType type)
    {
        switch (type)
        {
            case MessageType.Error: return Styles.Error;
            case MessageType.Warning: return Styles.Warning;
            default: return Styles.Info;
        }
    }

    private static Color StatusColor(StepStatus status)
    {
        switch (status)
        {
            case StepStatus.Active: return Yellow;
            case StepStatus.Completed: return Green;
            case StepStatus.Failed: return Red;
            case StepStatus.Skipped: return new Color(0.35f, 0.35f, 0.35f);
            default: return Grey;
        }
    }

    private static void ShowInScene(Object obj)
    {
        var go = obj is Component component ? component.gameObject : obj as GameObject;
        if (go == null)
        {
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
            return;
        }

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        if (SceneView.lastActiveSceneView != null && !(go.transform is RectTransform))
            SceneView.lastActiveSceneView.FrameSelected();
    }

    private static bool Confirm(string title, string message, string ok)
    {
        return EditorUtility.DisplayDialog(title, message, ok, "Отмена");
    }

    private static void VerticalLine()
    {
        var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.Width(1), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(rect, LineColor);
    }

    private static void HorizontalLine()
    {
        var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.Height(1));
        EditorGUI.DrawRect(rect, LineColor);
    }

    private static void Outline(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - 2, rect.y, 2, rect.height), color);
    }

    private static Color SelectedColor => EditorGUIUtility.isProSkin ? new Color(0.17f, 0.36f, 0.53f) : new Color(0.62f, 0.76f, 0.95f);
    private static Color HoverColor => EditorGUIUtility.isProSkin ? new Color(1, 1, 1, 0.05f) : new Color(0, 0, 0, 0.05f);
    private static Color LineColor => EditorGUIUtility.isProSkin ? new Color(0, 0, 0, 0.4f) : new Color(0, 0, 0, 0.15f);

    private static class Styles
    {
        public static GUIStyle Title;
        public static GUIStyle Section;
        public static GUIStyle Hint;
        public static GUIStyle Row;
        public static GUIStyle TextArea;
        public static GUIStyle ListTitle;
        public static GUIStyle ListSubtitle;
        public static GUIStyle Card;
        public static GUIStyle Inner;
        public static GUIStyle Group;
        public static GUIStyle Stage;
        public static GUIStyle Chip;
        public static GUIStyle ChipText;
        public static GUIStyle ChipCode;
        public static GUIStyle DropZone;
        public static GUIContent Remove;
        public static GUIContent Error;
        public static GUIContent Warning;
        public static GUIContent Info;

        private static bool _ready;

        public static void Init()
        {
            if (_ready)
                return;

            _ready = true;
            var hint = EditorGUIUtility.isProSkin ? new Color(0.68f, 0.68f, 0.68f) : new Color(0.32f, 0.32f, 0.32f);

            Title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, wordWrap = true };
            Section = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, wordWrap = true };
            Hint = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11 };
            Hint.normal.textColor = hint;
            Row = new GUIStyle(EditorStyles.wordWrappedLabel);
            TextArea = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            ListTitle = new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip };
            ListSubtitle = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
            ListSubtitle.normal.textColor = hint;
            Card = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 10, 8, 10), margin = new RectOffset(6, 6, 4, 6) };
            Inner = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(8, 6, 5, 6), margin = new RectOffset(0, 0, 2, 4) };
            Group = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(8, 8, 6, 8), margin = new RectOffset(0, 0, 4, 6) };
            Stage = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(8, 6, 4, 6), margin = new RectOffset(12, 0, 3, 3) };
            Chip = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(4, 4, 3, 3), margin = new RectOffset(0, 0, 2, 2) };
            ChipText = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip };
            ChipCode = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            ChipCode.normal.textColor = hint;
            DropZone = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fontSize = 11, margin = new RectOffset(12, 0, 3, 0) };
            DropZone.normal.textColor = hint;

            Remove = new GUIContent("×", "Удалить");
            Error = EditorGUIUtility.IconContent("console.erroricon.sml");
            Warning = EditorGUIUtility.IconContent("console.warnicon.sml");
            Info = EditorGUIUtility.IconContent("console.infoicon.sml");
        }
    }
}
