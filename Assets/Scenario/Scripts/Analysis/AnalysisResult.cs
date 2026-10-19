using System.Collections.Generic;
using System.Text;

public class AnalysisResult
{
    public readonly string analyzer;
    public readonly List<string> lines = new List<string>();
    public readonly List<string> warnings = new List<string>();
    public int score;
    public int maxScore;

    public AnalysisResult(string analyzer)
    {
        this.analyzer = analyzer;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Анализатор: {analyzer}");
        builder.AppendLine($"Итог: {score} из {maxScore}");
        builder.AppendLine();

        foreach (var line in lines)
            builder.AppendLine(line);

        if (warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Предупреждения:");
            foreach (var warning in warnings)
                builder.AppendLine("• " + warning);
        }

        return builder.ToString();
    }
}
