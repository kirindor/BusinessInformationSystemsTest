using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ResultsPanel : MonoBehaviour
{
    [SerializeField] private ScenarioRunner runner;
    [SerializeField] private Text reportText;
    [SerializeField] private Text logText;

    public void Show()
    {
        gameObject.SetActive(true);

        var builder = new StringBuilder();
        foreach (var entry in runner.Logger.Entries)
            builder.AppendLine(entry.ToString());
        logText.text = builder.ToString();

        Analyze(runner.Mode.analyzer);
    }

    public void Analyze(ScenarioAnalyzer analyzer)
    {
        var result = analyzer.Analyze(runner.Logger.Entries);
        reportText.text = result.ToString();

        foreach (var warning in result.warnings)
            Debug.LogWarning($"{analyzer.Title}: {warning}");
    }
}
