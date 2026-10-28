using SessionLab.Components.Docs;

namespace SessionLab.Tests;

/// <summary>The guide and architecture pages quote the source; fail when a quote drifts.</summary>
public class SnippetTests
{
    static readonly string Root = FindRoot();

    public static TheoryData<string> Names() => [.. typeof(Snippets).GetFields().Select(f => f.Name)];

    [Theory]
    [MemberData(nameof(Names))]
    public void Snippet_appears_verbatim_in_its_source_file(string name)
    {
        var snippet = (Snippet)typeof(Snippets).GetField(name)!.GetValue(null)!;
        var source = File.ReadAllText(Path.Combine(Root, snippet.File));
        Assert.Contains(Normalize(snippet.Code), Normalize(source));
    }

    // Indentation differs between the source and the dedented excerpt; compare trimmed lines.
    static string Normalize(string text) =>
        string.Join('\n', text.Replace("\r", "").Split('\n').Select(line => line.Trim()));

    static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SessionLab.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("SessionLab.slnx not found above the test output folder.");
    }
}
