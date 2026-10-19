using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ResultsPanel : MonoBehaviour
{
    [SerializeField] private ScenarioRunner _runner;
    [SerializeField] private Text _reportText;
    [SerializeField] private Text _logText;

    public void Show()
    {
        gameObject.SetActive(true);

        var builder = new StringBuilder();
        foreach (var entry in _runner.Logger.Entries)
            builder.AppendLine(entry.ToString());
        _logText.text = builder.ToString();

        Analyze(_runner.Mode.analyzer);
    }

    public void Analyze(ScenarioAnalyzer analyzer)
    {
        var result = analyzer.Analyze(_runner.Logger.Entries);
        _reportText.text = result.ToString();

        foreach (var warning in result.warnings)
            Debug.LogWarning($"{analyzer.Title}: {warning}");
    }
}
