using TaxClaw.Calc.Form;

namespace TaxClaw.Calc.Tests;

public class Form255405Tests
{
    [Fact]
    public void Has_the_form_code_and_pinned_version()
    {
        FormDefinition form = Form255405.Definition("2027.1");

        Assert.Equal("25 5405", form.FormCode);
        Assert.Equal("2027.1", form.Version);
    }

    [Fact]
    public void Every_line_is_grounded_in_a_provision()
    {
        FormDefinition form = Form255405.Definition("2027.1");

        Assert.All(form.Lines, line => Assert.False(string.IsNullOrWhiteSpace(line.LawRef)));
        Assert.All(form.Lines, line => Assert.False(string.IsNullOrWhiteSpace(line.Description)));
    }

    [Fact]
    public void Is_acyclic_and_orders_dependencies_before_dependents()
    {
        FormDefinition form = Form255405.Definition("2027.1");

        IReadOnlyList<FormLineDefinition> order = form.EvaluationOrder();
        var seen = new HashSet<string>();

        foreach (FormLineDefinition line in order)
        {
            foreach (string dependency in line.DependsOn)
            {
                Assert.Contains(dependency, seen); // dependency computed earlier
            }
            seen.Add(line.Id);
        }

        Assert.Equal(form.Lines.Count, order.Count);
    }

    [Fact]
    public void Tax_base_aggregates_all_five_partial_bases()
    {
        FormDefinition form = Form255405.Definition("2027.1");

        FormLineDefinition zakladDane = form.Lines.Single(l => l.Id == "zaklad_dane");

        Assert.Equal(
            new HashSet<string> { "dzd_p6", "dzd_p7", "dzd_p8", "dzd_p9", "dzd_p10" },
            zakladDane.DependsOn.ToHashSet());
        Assert.Equal("§ 5", zakladDane.LawRef);
    }

    [Fact]
    public void Foreign_tax_credit_is_grounded_in_paragraph_38f()
    {
        FormDefinition form = Form255405.Definition("2027.1");

        FormLineDefinition credit = form.Lines.Single(l => l.Id == "zapocet_zahranicni");

        Assert.Equal("§ 38f", credit.LawRef);
        Assert.Contains("dan", credit.DependsOn);
    }
}
