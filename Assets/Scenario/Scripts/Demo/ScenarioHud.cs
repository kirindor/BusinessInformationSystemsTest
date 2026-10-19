using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioHud : MonoBehaviour
{
    [SerializeField] private ScenarioRunner _runner;
    [SerializeField] private Text _header;
    [SerializeField] private Text _tasks;
    [SerializeField] private GameObject _messagePanel;
    [SerializeField] private Text _messageText;
    [SerializeField] private float _messageDuration = 4f;

    private readonly StringBuilder _builder = new StringBuilder();
    private float _hideTime;

    private void Start()
    {
        _messagePanel.SetActive(false);
        Refresh();
    }

    private void Update()
    {
        if (_messagePanel.activeSelf && Time.time >= _hideTime)
            _messagePanel.SetActive(false);
    }

    public void Refresh()
    {
        if (!_runner.IsRunning)
        {
            _header.text = "Сценарий не запущен";
            _tasks.text = "";
            return;
        }

        var mode = _runner.Mode;
        _builder.Clear();
        _builder.AppendLine(mode.title);
        _builder.AppendLine($"Группа {_runner.GroupIndex + 1} из {mode.groups.Count}: {_runner.CurrentGroup.name}");
        if (mode.violationLimit > 0)
            _builder.AppendLine($"Нарушений в группе: {_runner.GroupViolations} из {mode.violationLimit}");
        _header.text = _builder.ToString();

        _builder.Clear();
        foreach (var step in _runner.ActiveSteps)
        {
            _builder.Append("• ").Append(step.Description);
            var attempts = _runner.GetAttempts(step);
            if (attempts > 0)
                _builder.Append($"  (попытка {attempts + 1})");
            _builder.AppendLine();
        }
        _tasks.text = _builder.ToString();
    }

    public void ShowMessage(string text)
    {
        _messageText.text = text;
        _messagePanel.SetActive(true);
        _hideTime = Time.time + _messageDuration;
    }
}
