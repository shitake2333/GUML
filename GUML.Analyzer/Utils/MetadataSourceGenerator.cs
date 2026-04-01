using System.Text;
using Microsoft.CodeAnalysis;
using Serilog;

namespace GUML.Analyzer.Utils;

/// <summary>
/// Result of metadata source generation, including the file path and member line positions.
/// </summary>
/// <param name="FilePath">Absolute path to the generated file.</param>
/// <param name="TypeDeclarationLine">Zero-based line number of the type declaration.</param>
/// <param name="MemberLines">Maps C# member names (PascalCase) to zero-based line numbers.</param>
internal record MetadataSourceResult(string FilePath, int TypeDeclarationLine, Dictionary<string, int> MemberLines);

/// <summary>
/// Generates "metadata-as-source" C# files for types loaded from compiled assemblies.
/// The generated files contain only public API declarations (no method bodies),
/// similar to Visual Studio's metadata view.
/// </summary>
internal static class MetadataSourceGenerator
{
    private static readonly Dictionary<string, MetadataSourceResult> s_cache = new(StringComparer.Ordinal);
    private static string? s_cacheDir;

    /// <summary>
    /// Returns the path and member positions for a generated metadata-as-source file.
    /// Files are cached — subsequent calls for the same type return the existing result.
    /// </summary>
    public static MetadataSourceResult GenerateMetadataSource(INamedTypeSymbol typeSymbol)
    {
        string key = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (s_cache.TryGetValue(key, out var cached) && File.Exists(cached.FilePath))
            return cached;

        EnsureCacheDir();

        string assemblyName = typeSymbol.ContainingAssembly?.Name ?? "Unknown";
        string fileName = $"{typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}.cs";
        // Sanitize file name
        foreach (char c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c, '_');

        string dir = Path.Combine(s_cacheDir!, assemblyName);
        Directory.CreateDirectory(dir);
        string filePath = Path.Combine(dir, fileName);

        var buildResult = BuildSourceContent(typeSymbol, assemblyName);
        File.WriteAllText(filePath, buildResult.Content, Encoding.UTF8);

        var result = new MetadataSourceResult(filePath, buildResult.TypeDeclarationLine, buildResult.MemberLines);
        s_cache[key] = result;
        Log.Logger.Debug("Generated metadata source for {Type} at {Path}", key, filePath);
        return result;
    }

    /// <summary>
    /// Clears all cached metadata-as-source files.
    /// </summary>
    public static void ClearCache()
    {
        s_cache.Clear();
        if (s_cacheDir != null && Directory.Exists(s_cacheDir))
        {
            try { Directory.Delete(s_cacheDir, true); }
            catch { /* best effort */ }
        }
    }

    private static void EnsureCacheDir()
    {
        if (s_cacheDir != null) return;
        s_cacheDir = Path.Combine(Path.GetTempPath(), "guml-metadata-source");
        Directory.CreateDirectory(s_cacheDir);
    }

    private record BuildResult(string Content, int TypeDeclarationLine, Dictionary<string, int> MemberLines);

    private static BuildResult BuildSourceContent(INamedTypeSymbol typeSymbol, string assemblyName)
    {
        var sb = new StringBuilder();
        int lineNum = 0;
        var memberLines = new Dictionary<string, int>(StringComparer.Ordinal);

        void AppendLine(string text = "")
        {
            sb.AppendLine(text);
            lineNum++;
        }

        AppendLine("// Generated metadata source — do not edit");
        AppendLine($"// Assembly: {assemblyName}, Version={typeSymbol.ContainingAssembly?.Identity.Version}");
        AppendLine();

        string? ns = typeSymbol.ContainingNamespace?.IsGlobalNamespace == true
            ? null
            : typeSymbol.ContainingNamespace?.ToDisplayString();

        if (ns != null)
        {
            AppendLine($"namespace {ns}");
            AppendLine("{");
        }

        string indent = ns != null ? "    " : "";

        // Doc comment
        lineNum += AppendDocCommentLines(sb, typeSymbol, indent);

        // Type declaration
        string typeKind = typeSymbol.TypeKind switch
        {
            TypeKind.Interface => "interface",
            TypeKind.Struct => "struct",
            TypeKind.Enum => "enum",
            _ => "class"
        };

        string baseTypes = BuildBaseTypeList(typeSymbol);
        string modifier = typeSymbol is { IsAbstract: true, TypeKind: TypeKind.Class } ? "abstract " : "";
        string partial = typeSymbol.TypeKind is TypeKind.Class or TypeKind.Struct ? "partial " : "";

        int typeDeclarationLine = lineNum;
        AppendLine($"{indent}public {modifier}{partial}{typeKind} {typeSymbol.Name}{baseTypes}");
        AppendLine($"{indent}{{");

        if (typeSymbol.TypeKind == TypeKind.Enum)
        {
            AppendEnumMembersTracked(sb, typeSymbol, indent + "    ", ref lineNum, memberLines);
        }
        else
        {
            AppendMembersTracked(sb, typeSymbol, indent + "    ", ref lineNum, memberLines);
        }

        AppendLine($"{indent}}}");

        if (ns != null)
            AppendLine("}");

        return new BuildResult(sb.ToString(), typeDeclarationLine, memberLines);
    }

    private static string BuildBaseTypeList(INamedTypeSymbol typeSymbol)
    {
        var bases = new List<string>();

        if (typeSymbol.BaseType != null
            && typeSymbol.BaseType.SpecialType != SpecialType.System_Object
            && typeSymbol.BaseType.SpecialType != SpecialType.System_ValueType
            && typeSymbol.BaseType.SpecialType != SpecialType.System_Enum)
        {
            bases.Add(typeSymbol.BaseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }

        foreach (var iface in typeSymbol.Interfaces)
        {
            if (iface.DeclaredAccessibility == Accessibility.Public)
                bases.Add(iface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }

        return bases.Count > 0 ? " : " + string.Join(", ", bases) : "";
    }

    private static void AppendEnumMembersTracked(
        StringBuilder sb, INamedTypeSymbol typeSymbol, string indent,
        ref int lineNum, Dictionary<string, int> memberLines)
    {
        var fields = typeSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue)
            .ToList();

        for (int i = 0; i < fields.Count; i++)
        {
            var f = fields[i];
            lineNum += AppendDocCommentLines(sb, f, indent);
            memberLines[f.Name] = lineNum;
            string comma = i < fields.Count - 1 ? "," : "";
            sb.AppendLine($"{indent}{f.Name} = {f.ConstantValue}{comma}");
            lineNum++;
        }
    }

    private static void AppendMembersTracked(
        StringBuilder sb, INamedTypeSymbol typeSymbol, string indent,
        ref int lineNum, Dictionary<string, int> memberLines)
    {
        bool first = true;

        // Properties
        foreach (var prop in typeSymbol.GetMembers().OfType<IPropertySymbol>()
                     .Where(p => p is { DeclaredAccessibility: Accessibility.Public, IsStatic: false }))
        {
            if (!first) { sb.AppendLine(); lineNum++; }
            first = false;
            lineNum += AppendDocCommentLines(sb, prop, indent);

            memberLines[prop.Name] = lineNum;
            string getter = prop.GetMethod != null ? " get;" : "";
            string setter = prop.SetMethod != null ? " set;" : "";
            string typeName = prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            sb.AppendLine($"{indent}public {typeName} {prop.Name} {{{getter}{setter} }}");
            lineNum++;
        }

        // Events
        foreach (var evt in typeSymbol.GetMembers().OfType<IEventSymbol>()
                     .Where(e => e is { DeclaredAccessibility: Accessibility.Public, IsStatic: false }))
        {
            if (!first) { sb.AppendLine(); lineNum++; }
            first = false;
            lineNum += AppendDocCommentLines(sb, evt, indent);

            memberLines[evt.Name] = lineNum;
            string typeName = evt.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            sb.AppendLine($"{indent}public event {typeName} {evt.Name};");
            lineNum++;
        }

        // Methods
        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>()
                     .Where(m => m is { DeclaredAccessibility: Accessibility.Public, IsStatic: false, MethodKind: MethodKind.Ordinary }))
        {
            if (!first) { sb.AppendLine(); lineNum++; }
            first = false;
            lineNum += AppendDocCommentLines(sb, method, indent);

            memberLines[method.Name] = lineNum;
            string returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var parameters = method.Parameters.Select(p =>
            {
                string paramType = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                string defaultVal = p.HasExplicitDefaultValue
                    ? $" = {FormatDefaultValue(p)}"
                    : "";
                return $"{paramType} {p.Name}{defaultVal}";
            });
            string paramList = string.Join(", ", parameters);
            sb.AppendLine($"{indent}public {returnType} {method.Name}({paramList});");
            lineNum++;
        }
    }

    private static string FormatDefaultValue(IParameterSymbol param)
    {
        if (!param.HasExplicitDefaultValue) return "";

        object? value = param.ExplicitDefaultValue;
        return value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? "default"
        };
    }

    /// <summary>
    /// Appends XML doc comment lines to the StringBuilder and returns the number of lines written.
    /// </summary>
    private static int AppendDocCommentLines(StringBuilder sb, ISymbol symbol, string indent)
    {
        string? xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml)) return 0;

        // Extract <summary> content
        int start = xml.IndexOf("<summary>", StringComparison.Ordinal);
        int end = xml.IndexOf("</summary>", StringComparison.Ordinal);
        if (start < 0 || end < 0) return 0;

        string inner = xml[(start + "<summary>".Length)..end].Trim();
        if (string.IsNullOrWhiteSpace(inner)) return 0;

        int count = 0;
        sb.AppendLine($"{indent}/// <summary>");
        count++;
        foreach (string line in inner.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                sb.AppendLine($"{indent}/// {trimmed}");
                count++;
            }
        }
        sb.AppendLine($"{indent}/// </summary>");
        count++;
        return count;
    }
}
