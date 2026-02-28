using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using ConsoleAppFramework;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
[assembly: InternalsVisibleTo("GUML.ApiGenerator.Tests")]

var app = ConsoleApp.Create();
app.Add<ApiGeneratorCommands>();
app.Run(args);

public class ApiGeneratorCommands
{
    private readonly ApiModelBuilder _modelBuilder = new();

    /// <summary>
    /// Downloads a Godot NuGet package and generates a GUML API definition cache.
    /// </summary>
    /// <param name="packageId">-p, The NuGet package ID (default: GodotSharp).</param>
    /// <param name="version">-v, The specific version to download (e.g., 4.2.1-stable).</param>
    /// <param name="outputDir">-o, The output dir for the JSON cache file.</param>
    /// <param name="source">-s, Custom NuGet source URL (optional).</param>
    [Command("")]
    public async Task Root(
        string outputDir,
        string version,
        string packageId = "GodotSharp",
        string source = "https://api.nuget.org/v3/index.json")
    {
        Console.WriteLine($"Starting generation from NuGet Package: {packageId} v{version}...");

        // 1. Download and Extract NuGet Package
        var extractionPath = Path.Combine(Path.GetTempPath(), "GumlApiGen", $"{packageId}.{version}");
        await DownloadAndExtractPackageAsync(packageId, version, source, extractionPath);

        try
        {
            // 2. Locate DLL and XML
            var (dllPath, xmlPath) = FindDllAndXml(extractionPath, packageId);

            Console.WriteLine($"Located DLL: {dllPath}");
            if (xmlPath != null) Console.WriteLine($"Located XML keys: {xmlPath}");

            // 3. Analyze with Mono.Cecil
            Dictionary<string, string> docMap = new();
            if (xmlPath != null) docMap = LoadXmlDocumentation(xmlPath);

            using var assembly = AssemblyDefinition.ReadAssembly(dllPath);
            var apiModel = _modelBuilder.Build(assembly, version, docMap);

            // 4. Save
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(apiModel, jsonOptions);

            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, $"{packageId}.{version}.json");
            await File.WriteAllTextAsync(outputPath, json);
            Console.WriteLine($"Success! API cache generated at: {outputPath}");
        }
        finally
        {
            Directory.Delete(extractionPath, true);
        }
    }

    private async Task DownloadAndExtractPackageAsync(string packageId, string versionString, string sourceUrl, string destPath)
    {
        Console.WriteLine($"Downloading {packageId} {versionString} from {sourceUrl}...");

        var cache = new SourceCacheContext();
        var repository = Repository.Factory.GetCoreV3(sourceUrl);
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>();

        if (!NuGetVersion.TryParse(versionString, out var nugetVersion))
        {
            throw new ArgumentException($"Invalid version string: {versionString}");
        }

        using var memoryStream = new MemoryStream();
        if (!await resource.CopyNupkgToStreamAsync(
            packageId,
            nugetVersion,
            memoryStream,
            cache,
            NullLogger.Instance,
            CancellationToken.None))
        {
            throw new Exception($"Failed to download package {packageId} v{versionString}");
        }

        Console.WriteLine("Download complete. Extracting...");

        if (Directory.Exists(destPath)) Directory.Delete(destPath, true);
        Directory.CreateDirectory(destPath);

        await using var archive = new ZipArchive(memoryStream);
        await archive.ExtractToDirectoryAsync(destPath);
    }

    private (string dllPath, string? xmlPath) FindDllAndXml(string rootPath, string packageId)
    {
        // NuGet structure usually: lib/netX.Y/GodotSharp.dll
        var libDir = Path.Combine(rootPath, "lib");
        if (!Directory.Exists(libDir)) throw new DirectoryNotFoundException($"'lib' directory not found in package at {rootPath}");

        // Find the best matching framework folder (e.g., net6.0, netstandard2.1)
        // We just take the last one alphabetically (usually highest version) for simplicity
        var frameworkDir = Directory.GetDirectories(libDir).OrderByDescending(d => d).FirstOrDefault();
        if (frameworkDir == null) throw new DirectoryNotFoundException("No framework directory found in package.");

        var dllName = $"{packageId}.dll";
        var dllPath = Path.Combine(frameworkDir, dllName);

        if (!File.Exists(dllPath))
        {
            // Fallback: search recursively if standard structure isn't followed
            var files = Directory.GetFiles(rootPath, dllName, SearchOption.AllDirectories);
            if (files.Length == 0) throw new FileNotFoundException($"DLL {dllName} not found in package.");
            dllPath = files[0];
        }

        var xmlPath = Path.ChangeExtension(dllPath, ".xml");
        if (!File.Exists(xmlPath)) xmlPath = null;

        return (dllPath, xmlPath);
    }

    private Dictionary<string, string> LoadXmlDocumentation(string xmlPath)
    {
        var result = new Dictionary<string, string>();
        try
        {
            // Use XDocument to parse .xml (Standard IXmlDocumentationProvider format)
            var doc = XDocument.Load(xmlPath);
            foreach (var member in doc.Root?.Element("members")?.Elements("member")!)
            {
                var name = member.Attribute("name")?.Value;
                var summary = member.Element("summary")?.Value?.Trim();
                if (name != null && !string.IsNullOrEmpty(summary))
                {
                    result[name] = Regex.Replace(summary, @"\s+", " ");
                }
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }
}

internal sealed class ApiModelBuilder
{
    private static readonly HashSet<string> PrimitiveClrTypeNames = new(StringComparer.Ordinal)
    {
        nameof(Boolean),
        nameof(Int32),
        nameof(Int64),
        nameof(Single),
        nameof(Double),
        nameof(String)
    };

    private static readonly HashSet<string> GodotStructTypeNames = new(StringComparer.Ordinal)
    {
        "Vector2",
        "Vector2I",
        "Vector3",
        "Vector3I",
        "Vector4",
        "Vector4I",
        "Rect2",
        "Rect2I",
        "Transform2D",
        "Transform3D",
        "Color",
        "Plane",
        "Quaternion",
        "Basis",
        "Rid",
        "Aabb",
        "Projection",
        "Margins"
    };

    private static readonly HashSet<string> GodotReferenceTypeFullNames = new(StringComparer.Ordinal)
    {
        "Godot.NodePath",
        "Godot.StringName",
        "Godot.Collections.Array",
        "Godot.Collections.Dictionary"
    };

    private static readonly string[] GodotSupportedBaseTypes =
    {
        "Godot.Node",
        "Godot.Resource",
        "Godot.StyleBox",
        "Godot.Theme",
        "Godot.Font",
        "Godot.FontFile",
        "Godot.Texture",
        "Godot.Texture2D",
        "Godot.AudioStream",
        "Godot.InputEvent",
        "Godot.PackedScene"
    };

    public GumlApiCache Build(AssemblyDefinition assembly, string version, Dictionary<string, string> docMap)
    {
        var cache = new GumlApiCache
        {
            Version = version,
            GeneratedAt = DateTime.UtcNow
        };

        var allTypes = assembly.MainModule.Types
            .Where(t => t.IsPublic &&
                        t is { IsAbstract: false, Namespace: not null } &&
                        t.Namespace.StartsWith("Godot") &&
                        IsSubclassOf(t, "Godot.Control"));

        foreach (var type in allTypes)
        {
            if (type.IsEnum || type.IsInterface) continue;

            var classInfo = new GumlClassInfo
            {
                Name = type.Name,
                BaseType = type.BaseType?.Name
            };

            string classDocId = $"T:{type.FullName}";
            if (docMap.TryGetValue(classDocId, out var classDoc)) classInfo.Description = classDoc;

            classInfo.Properties = type.Properties
                .Where(p => p.GetMethod != null && p.GetMethod.IsPublic)
                .Where(p => IsTypeSupported(p.PropertyType))
                .Select(p =>
                {
                    string propDocId = $"P:{type.FullName}.{p.Name}";
                    docMap.TryGetValue(propDocId, out var propDoc);
                    return new GumlPropertyInfo
                    {
                        Name = ToSnakeCase(p.Name),
                        Type = p.PropertyType.Name,
                        Description = propDoc ?? ""
                    };
                })
                .ToDictionary(p => p.Name, p => p);

            if (classInfo.Properties.Count > 0)
            {
                cache.Classes[type.Name] = classInfo;
            }
        }

        return cache;
    }

    public bool IsTypeSupported(TypeReference type)
    {
        if (type == null) return false;

        if (type.IsPrimitive || PrimitiveClrTypeNames.Contains(type.Name))
        {
            return true;
        }

        if (GodotStructTypeNames.Contains(type.Name))
        {
            return true;
        }

        var fullName = type.FullName ?? type.Name;
        if (GodotReferenceTypeFullNames.Contains(fullName))
        {
            return true;
        }

        TypeDefinition? definition;
        try
        {
            definition = type.Resolve();
        }
        catch
        {
            return false;
        }

        if (definition == null)
        {
            return false;
        }

        if (definition.IsEnum && definition.Namespace == "Godot")
        {
            return true;
        }

        if (definition.IsValueType && definition.Namespace == "Godot")
        {
            return true;
        }

        foreach (var supportedBase in GodotSupportedBaseTypes)
        {
            if (IsSubclassOf(definition, supportedBase))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSubclassOf(TypeDefinition type, string baseClassName)
    {
        var current = type;
        while (current != null && current.FullName != "System.Object")
        {
            if (current.FullName == baseClassName)
            {
                return true;
            }

            try
            {
                var baseTypeRef = current.BaseType;
                if (baseTypeRef == null) return false;

                current = baseTypeRef.Resolve();
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    private string ToSnakeCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (text.Length < 2) return text.ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        sb.Append(char.ToLowerInvariant(text[0]));
        for (int i = 1; i < text.Length; ++i)
        {
            char c = text[i];
            if (char.IsUpper(c)) { sb.Append('_'); sb.Append(char.ToLowerInvariant(c)); }
            else sb.Append(c);
        }
        return sb.ToString();
    }
}

// --- Data Models (DTOs) ---

public class GumlApiCache
{
    public string Version { get; set; } = "";
    public DateTime GeneratedAt { get; set; }

    public Dictionary<string, GumlClassInfo> Classes { get; set; } = new();
}

public class GumlClassInfo
{
    public string Name { get; set; } = "";
    public string? BaseType { get; set; }
    public string Description { get; set; } = "";
    public Dictionary<string, GumlPropertyInfo> Properties { get; set; } = new();
}

public class GumlPropertyInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
}
