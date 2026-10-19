using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioHud : MonoBehaviour
{
    [SerializeField] private ScenarioRunner runner;
    [SerializeField] private Text header;
    [SerializeField] private Text tasks;
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private Text messageText;
    [SerializeField] private float messageDuration = 4f;

    private readonly StringBuilder builder = new StringBuilder();
    private float hideTime;

    private void Start()
    {
        messagePanel.SetActive(false);
        Refresh();
    }

    private void Update()
    {
        if (messagePanel.activeSelf && Time.time >= hideTime)
            messagePanel.SetActive(false);
    }

    public void Refresh()
    {
        if (!runner.IsRunning)
        {
            header.text = "Сценарий не запущен";
            tasks.text = "";
            return;
        }

        var mode = runner.Mode;
        builder.Clear();
        builder.AppendLine(mode.title);
        builder.AppendLine($"Группа {runner.GroupIndex + 1} из {mode.groups.Count}: {runner.CurrentGroup.name}");
        if (mode.violationLimit > 0)
            builder.AppendLine($"Нарушений в группе: {runner.GroupViolations} из {mode.violationLimit}");
        header.text = builder.ToString();

        builder.Clear();
        foreach (var step in runner.ActiveSteps)
        {
            builder.Append("• ").Append(step.Description);
            var attempts = runner.GetAttempts(step);
            if (attempts > 0)
                builder.Append($"  (попытка {attempts + 1})");
            builder.AppendLine();
        }
        tasks.text = builder.ToString();
    }

    public void ShowMessage(string text)
    {
        messageText.text = text;
        messagePanel.SetActive(true);
        hideTime = Time.time + messageDuration;
    }
}
