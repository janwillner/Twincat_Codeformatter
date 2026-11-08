using TcFormatter.Configuration;
using TcFormatter.Formatting;

namespace TcFormatter.Core.Tests;

/// <summary>Import of a STweep settings file (exported by the user from STweep, JSON format, version 4.5).</summary>
public class StweepImportTests
{
    private static string UserFile()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "TcFormatter.sln")))
            dir = Path.GetDirectoryName(dir)!;
        return File.ReadAllText(Path.Combine(dir, "presets", "STSweepConfig.STweep"));
    }

    [Fact]
    public void Maps_the_settings_of_the_users_stweep_file()
    {
        var config = StweepImport.Convert(UserFile(), out var notes);
        var f = config.Format;

        // TabsIndentAlignment
        Assert.Equal(IndentStyle.Spaces, f.IndentStyle);
        Assert.Equal(4, f.IndentSize);
        Assert.Equal(1, f.ContinuationIndent);
        Assert.True(f.IndentCaseLabels);
        Assert.True(f.IndentCaseBody);
        Assert.False(f.IndentCaseElse);
        Assert.True(f.IndentThen);
        Assert.False(f.IndentDo);
        Assert.True(f.IndentTypeDeclaration);
        Assert.False(f.AlignDeclarationColons);
        Assert.False(f.AlignDeclarationInitializers);
        Assert.False(f.AlignEnumValues);
        Assert.False(f.AlignConsecutiveAssignments);
        Assert.Equal(TrailingCommentAlignment.None, f.DeclarationCommentAlignment);
        Assert.False(f.AlignWrappedArguments);

        // LineBreaksAndWrapping
        Assert.Equal(LineBreakMode.LongOrMultiline, f.CallLineBreakMode);
        Assert.Equal(LineBreakMode.LongOrMultiline, f.InitializerLineBreakMode);
        Assert.Equal(LineBreakMode.LongOrMultiline, f.EnumLineBreakMode);
        Assert.Equal(5, f.MaxCallArgumentsOnSingleLine);
        Assert.Equal(5, f.MaxInitializerArgumentsOnSingleLine);
        Assert.Equal(5, f.MaxEnumValuesOnSingleLine);
        Assert.Equal(150, f.MaxLineLength);
        Assert.True(f.KeepExistingLineBreaks);
        Assert.False(f.SimpleIfOnSingleLine);
        Assert.False(f.StructOnNewLine);
        Assert.True(f.LineBreakAfterTypeKeyword);
        Assert.False(f.LineBreakBeforeThen);
        Assert.False(f.LineBreakBeforeDo);
        Assert.False(f.CaseStatementOnNewLine);
        Assert.Equal(CallWrapStyle.AlignWithOpeningParenthesis, f.CallWrapStyle);
        Assert.False(f.LineBreakAfterInitializerOpenParenthesis);
        Assert.False(f.LineBreakBeforeEnumOpenParenthesis);
        Assert.False(f.LineBreakAfterEnumOpenParenthesis);
        Assert.False(f.LineBreakBeforeEnumCloseParenthesis);

        // BlankLines
        Assert.Equal(1, f.MaxConsecutiveBlankLines);
        Assert.Equal(1, f.BlankLinesAfterMultiLineStatement);
        Assert.Equal(1, f.BlankLinesBeforeStatementWithBody);
        Assert.Equal(1, f.BlankLinesAfterStatementWithBody);
        Assert.Equal(0, f.BlankLinesAfterSingleLineCase);
        Assert.Equal(0, f.BlankLinesAtFileStart);

        // Spaces and casing
        Assert.True(f.SpaceAfterComma);
        Assert.True(f.SpaceBeforeColon);
        Assert.True(f.SpaceAroundAssignment);
        Assert.True(f.SpaceAroundOperators);
        Assert.True(f.SpaceBeforeArgumentAssignment);
        Assert.False(f.SpaceInsideParentheses);
        Assert.False(f.SpaceBetweenNestedParentheses);
        Assert.Equal(CasingStyle.Upper, f.KeywordCasing);

        // settings without counterpart are listed, not silently dropped ...
        Assert.Contains(notes, n => n.Contains("ChainedMethodsLineBreakMode"));
        // ... unless TcFormatter behaves exactly like the chosen value
        Assert.DoesNotContain(notes, n => n.Contains("WrapBeforeComma"));
    }

    [Fact]
    public void Unknown_settings_and_versions_are_reported()
    {
        var json = """{ "Settings": { "Spaces": { "AfterComma": false, "SomethingNew": true } }, "Version": "9.0.0.0" }""";
        var config = StweepImport.Convert(json, out var notes);

        Assert.False(config.Format.SpaceAfterComma);
        Assert.Contains(notes, n => n.Contains("SomethingNew"));
        Assert.Contains(notes, n => n.Contains("9.0.0.0"));
    }

    [Fact]
    public void Imported_configuration_formats_code_like_stweep_settings()
    {
        var config = StweepImport.Convert(UserFile(), out _);
        var input = "x:=1;\nIF a THEN\ny:=2;\nEND_IF\nz:=3;";
        var result = StFormatter.Format(input, CodePart.Implementation, config.Format);

        Assert.Null(result.Error);
        Assert.Equal("x := 1;\n\nIF a THEN\n    y := 2;\nEND_IF\n\nz := 3;", result.Text);
    }

    [Fact]
    public void Invalid_files_are_rejected()
    {
        Assert.Throws<ConfigException>(() => StweepImport.Convert("not json", out _));
        Assert.Throws<ConfigException>(() => StweepImport.Convert("""{ "NoSettings": 1 }""", out _));
    }
}
