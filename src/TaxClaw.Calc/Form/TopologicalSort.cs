namespace TaxClaw.Calc.Form;

internal static class TopologicalSort
{
    public static IReadOnlyList<FormLineDefinition> Order(IReadOnlyList<FormLineDefinition> lines)
    {
        var byId = lines.ToDictionary(l => l.Id);
        var visited = new Dictionary<string, bool>(); // false = in progress, true = done
        var result = new List<FormLineDefinition>();

        void Visit(FormLineDefinition line)
        {
            if (visited.TryGetValue(line.Id, out bool done))
            {
                if (!done)
                {
                    throw new InvalidOperationException($"Dependency cycle detected at line '{line.Id}'.");
                }
                return;
            }

            visited[line.Id] = false;
            foreach (string dep in line.DependsOn)
            {
                if (byId.TryGetValue(dep, out var depLine))
                {
                    Visit(depLine);
                }
            }
            visited[line.Id] = true;
            result.Add(line);
        }

        foreach (var line in lines)
        {
            Visit(line);
        }

        return result;
    }
}
