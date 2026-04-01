using GUML.Shared.Api.FrameworkPlugin;
using GUML.Shared.Converter;
using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;
using GUML.Shared.Syntax.Nodes.Expressions;
using GUML.SourceGenerator.FrameworkPlugin;

namespace GUML.SourceGenerator;

/// <summary>
/// Delegate to resolve imported Guml documents.
/// </summary>
public delegate GumlDocumentSyntax? ImportResolver(string path);

/// <summary>
/// Transforms a parsed <see cref="GumlDocumentSyntax"/> CST into a C# source code string
/// representing a strongly-typed view class.
/// Each call to <see cref="Emit"/> creates a fresh instance to ensure thread-safety.
/// </summary>
internal sealed class GumlCodeEmitter
{
    /// <summary>
    /// Maps GUML type names to their C# equivalents.
    /// </summary>
    private static readonly Dictionary<string, string> s_gumlTypeToCSharp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["boolean"] = "bool",
        ["integer"] = "int",
        ["number"] = "double",
    };

    /// <summary>
    /// Ensures <paramref name="name"/> is a valid C# identifier.
    /// Leading digit characters are prefixed with <c>_</c> rather than stripped,
    /// so that "1button" becomes "_1button" instead of silently losing the digit.
    /// </summary>
    internal static string SanitizeIdentifier(string name)
    {
        if (name.Length > 0 && char.IsDigit(name[0]))
            return "_" + name;
        return name.Length > 0 ? name : "_";
    }

    /// <summary>
    /// Maps a GUML type name to its C# equivalent, or returns it unchanged.
    /// </summary>
    internal static string MapGumlType(string typeName)
    {
        return s_gumlTypeToCSharp.TryGetValue(typeName, out string? mapped) ? mapped : typeName;
    }
    private int _varCounter;
    private int _bindingCounter;
    private int _eachCounter;
    private string _currentNodeVar = "null";

    /// <summary>
    /// Tracks each-loop scope variable names for nested each code generation.
    /// Includes the resolved element type name for typed code generation.
    /// </summary>
    private readonly Stack<(string ScopeVar, string IndexName, string ValueName, string? ElementTypeName)> _eachScopeStack = new();

    /// <summary>Maps alias key ("@hello") to PascalCase property name ("Hello").</summary>
    private readonly Dictionary<string, string> _aliasMap = new();

    /// <summary>Reverse map from ComponentDeclarationSyntax to alias info (key, PascalCase property name).</summary>
    private readonly Dictionary<ComponentDeclarationSyntax, (string AliasKey, string PropertyName)> _nodeAliasMap = new();

    /// <summary>Whether a controller type name is explicitly provided.</summary>
    private bool _hasControllerTypeName;

    private CompilationApiScanner? _scanner;
    private string? _activeControllerTypeName;
    private GumlDocumentSyntax? _currentDoc;
    private ImportResolver? _importResolver;

    // Framework plugin fields — default to Godot adapter for backward compatibility.
    private readonly IFrameworkTypeProvider _typeProvider = GodotFrameworkPlugin.Instance;
    private readonly IFrameworkEventProvider _eventProvider = GodotFrameworkPlugin.Instance;
    private readonly IFrameworkPseudoPropProvider _pseudoPropProvider = GodotFrameworkPlugin.Instance;

    /// <summary>
    /// Emits C# source code for the given GUML document.
    /// </summary>
    internal static string Emit(string filePath, GumlDocumentSyntax doc,
        IReadOnlyList<string>? additionalNamespaces = null,
        CompilationApiScanner? scanner = null, string? controllerTypeName = null,
        string? gumlRegistryKey = null, ImportResolver? importResolver = null)
    {
        var emitter = new GumlCodeEmitter
        {
            _scanner = scanner,
            _importResolver = importResolver
        };
        return emitter.EmitInternal(filePath, doc,
            additionalNamespaces ?? Array.Empty<string>(),
            scanner, controllerTypeName, gumlRegistryKey);
    }

    /// <summary>
    /// Generates a partial class for the controller with strongly-typed
    /// named node properties, import controller properties, parameter properties, and events.
    /// </summary>
    /// <param name="controllerTypeName">The simple name of the controller class.</param>
    /// <param name="controllerNamespace">The namespace of the controller class, or null.</param>
    /// <param name="doc">The parsed GUML document.</param>
    /// <param name="existingMembers">Members already declared on the controller type.</param>
    /// <param name="typeProvider">Optional framework type provider for namespace usings. Defaults to Godot.</param>
    /// <param name="additionalNamespaces">Optional extra namespaces to inject as using directives.</param>
    internal static string? EmitControllerPartial(string controllerTypeName,
        string? controllerNamespace, GumlDocumentSyntax doc,
        ISet<string>? existingMembers = null,
        IFrameworkTypeProvider? typeProvider = null,
        IReadOnlyList<string>? additionalNamespaces = null)
    {
        typeProvider ??= GodotFrameworkPlugin.Instance;

        var root = doc.RootComponent;
        var aliases = CollectAliases(root);
        var parameters = GetParameters(root).ToList();
        var events = GetEvents(root).ToList();

        if (aliases.Count == 0 && parameters.Count == 0 && events.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// This file was generated by GUML.SourceGenerator. Do not edit manually.");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        var emittedNamespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (string ns in typeProvider.GetRequiredUsings())
        {
            sb.AppendLine($"using {ns};");
            emittedNamespaces.Add(ns);
        }
        sb.AppendLine("using GUML;");
        emittedNamespaces.Add("GUML");
        sb.AppendLine("using System;");
        emittedNamespaces.Add("System");
        sb.AppendLine("using System.Collections;");
        emittedNamespaces.Add("System.Collections");
        sb.AppendLine("using System.Collections.Generic;");
        emittedNamespaces.Add("System.Collections.Generic");

        if (additionalNamespaces != null)
        {
            foreach (string ns in additionalNamespaces)
            {
                if (!string.IsNullOrWhiteSpace(ns) && emittedNamespaces.Add(ns.Trim()))
                    sb.AppendLine($"using {ns.Trim()};");
            }
        }

        sb.AppendLine();

        bool hasNamespace = !string.IsNullOrEmpty(controllerNamespace);
        string indent = hasNamespace ? "    " : "";

        if (hasNamespace)
        {
            sb.AppendLine($"namespace {controllerNamespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Auto-generated properties for {controllerTypeName}.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}public partial class {controllerTypeName}");
        sb.AppendLine($"{indent}{{");

        bool anyGenerated = false;
        var generatedNames = new HashSet<string>();

        // Generate named node properties from @alias declarations
        foreach (var kvp in aliases)
        {
            string aliasKey = kvp.Key;
            var comp = kvp.Value;
            string propName = KeyConverter.ToPascalCase(aliasKey.TrimStart('@'));

            if ((existingMembers != null && existingMembers.Contains(propName)) || !generatedNames.Add(propName))
                continue;

            string propType = comp.TypeName.Text;
            // Check if this type matches an import
            foreach (var import in doc.Imports)
            {
                string importFileName = GetImportFileName(import);
                string nameInGuml = import.Alias != null
                    ? import.Alias.Name.Text
                    : KeyConverter.ToPascalCase(importFileName);

                if (nameInGuml == propType)
                {
                    propType = KeyConverter.ToPascalCase(importFileName) + "Controller";
                    break;
                }
            }

            sb.AppendLine($"{indent}    /// <summary>Named node '{aliasKey}' pointing to a {comp.TypeName.Text} node.</summary>");
            sb.AppendLine($"{indent}    public {propType} {propName} {{ get; internal set; }} = null!;");
            sb.AppendLine();
            anyGenerated = true;
        }

        // Generate Parameter Properties
        foreach (var param in parameters)
        {
            string propName = KeyConverter.ToPascalCase(param.Name.Text);
            string typeName = MapGumlType(param.TypeName.Text);

            if ((existingMembers != null && existingMembers.Contains(propName)) || !generatedNames.Add(propName))
                continue;

            string defaultVal = " = default!";
            if (param.DefaultValue != null)
            {
                var tempEmitter = new GumlCodeEmitter();
                defaultVal = " = " + tempEmitter.EmitExpression(param.DefaultValue);
            }

            sb.AppendLine($"{indent}    private {typeName} _{propName}{defaultVal};");
            sb.AppendLine($"{indent}    /// <summary>Parameter property '{param.Name.Text}'.</summary>");
            sb.AppendLine($"{indent}    public {typeName} {propName}");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        get => _{propName};");
            sb.AppendLine($"{indent}        set");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            if (!System.Collections.Generic.EqualityComparer<{typeName}>.Default.Equals(_{propName}, value))");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                _{propName} = value;");
            sb.AppendLine($"{indent}                OnPropertyChanged();");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            anyGenerated = true;
        }

        // Generate Events
        foreach (var evt in events)
        {
            string evtName = KeyConverter.ToPascalCase(evt.Name.Text);

            if (existingMembers != null && existingMembers.Contains(evtName))
                continue;

            var args = evt.Arguments != null ? AsList(evt.Arguments) : new List<EventArgumentSyntax>();

            string delegateType;
            if (args.Count == 0)
            {
                delegateType = "Action";
            }
            else
            {
                string typeList = string.Join(", ", args.Select(a => MapGumlType(a.TypeName.Text)));
                delegateType = $"Action<{typeList}>";
            }

            sb.AppendLine($"{indent}    /// <summary>Event '{evt.Name.Text}'.</summary>");
            sb.AppendLine($"{indent}    public event {delegateType}? {evtName};");

            string argsDecl = string.Join(", ", args.Select((a, i) => $"{MapGumlType(a.TypeName.Text)} arg{i}"));
            string argsCall = string.Join(", ", args.Select((_, i) => $"arg{i}"));

            sb.AppendLine($"{indent}    internal void Raise{evtName}({argsDecl}) => {evtName}?.Invoke({argsCall});");
            sb.AppendLine();
            anyGenerated = true;
        }

        sb.AppendLine($"{indent}}}");

        if (!anyGenerated)
            return null;

        return sb.ToString();
    }

    private string EmitInternal(string filePath, GumlDocumentSyntax doc,
        IReadOnlyList<string> additionalNamespaces,
        CompilationApiScanner? scanner, string? controllerTypeName, string? gumlRegistryKey)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string className = SanitizeIdentifier(KeyConverter.ToPascalCase(fileName)) + "GumlView";
        string controllerName = controllerTypeName ?? (SanitizeIdentifier(KeyConverter.ToPascalCase(fileName)) + "Controller");
        _activeControllerTypeName = controllerName;
        _hasControllerTypeName = !string.IsNullOrEmpty(controllerTypeName);
        _currentDoc = doc;

        // Collect aliases
        var aliases = CollectAliases(doc.RootComponent);
        foreach (var kvp in aliases)
        {
            string aliasVarName = KeyConverter.ToPascalCase(kvp.Key.TrimStart('@'));
            _aliasMap[kvp.Key] = aliasVarName;
            _nodeAliasMap[kvp.Value] = (kvp.Key, aliasVarName);
        }

        var bodyBuilder = new StringBuilder();
        string rootVarName = EmitNode(bodyBuilder, doc.RootComponent, null, "        ", scanner);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// This file was generated by GUML.SourceGenerator. Do not edit manually.");
        sb.AppendLine();
        var emittedNamespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (string ns in _typeProvider.GetRequiredUsings())
        {
            sb.AppendLine($"using {ns};");
            emittedNamespaces.Add(ns);
        }
        sb.AppendLine("using GUML;");
        emittedNamespaces.Add("GUML");
        sb.AppendLine("using GUML.Binding;");
        emittedNamespaces.Add("GUML.Binding");
        sb.AppendLine("using System;");
        emittedNamespaces.Add("System");
        sb.AppendLine("using System.Collections.Generic;");
        emittedNamespaces.Add("System.Collections.Generic");

        foreach (string? ns in additionalNamespaces)
        {
            if (!string.IsNullOrWhiteSpace(ns) && emittedNamespaces.Add(ns.Trim()))
                sb.AppendLine($"using {ns.Trim()};");
        }

        // Auto-inject namespaces for custom component types found by the scanner
        if (scanner != null)
        {
            var componentNamespaces = new HashSet<string>();
            CollectComponentNamespaces(doc.RootComponent, doc, scanner, componentNamespaces);
            foreach (string ns in componentNamespaces)
            {
                if (emittedNamespaces.Add(ns))
                    sb.AppendLine($"using {ns};");
            }
        }

        sb.AppendLine();

        // Import comments
        if (doc.Imports.Count > 0)
        {
            sb.AppendLine("// GUML Imports (require runtime resolution):");
            foreach (var import in doc.Imports)
            {
                string importPath = StripQuotes(import.Path.Text);
                sb.AppendLine($"//   import \"{importPath}\"");
            }
            sb.AppendLine();
        }

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Auto-generated GUML view for {fileName}.guml.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public partial class {className}");
        sb.AppendLine("{");

        sb.AppendLine($"    private {controllerName} _controller;");
        sb.AppendLine("    private BindingScope _rootScope;");
        sb.AppendLine("    internal BindingScope RootScope => _rootScope;");
        sb.AppendLine();

        // Alias properties on the view class
        foreach (var kvp in _aliasMap)
        {
            var comp = aliases[kvp.Key];
            string propType = comp.TypeName.Text;

            foreach (var import in doc.Imports)
            {
                string importFileName = GetImportFileName(import);
                string nameInGuml = import.Alias != null
                    ? import.Alias.Name.Text
                    : KeyConverter.ToPascalCase(importFileName);

                if (nameInGuml == comp.TypeName.Text)
                {
                    propType = KeyConverter.ToPascalCase(importFileName) + "Controller";
                    break;
                }
            }

            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Alias reference '{kvp.Key}' pointing to a {comp.TypeName.Text} node.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public {propType} {kvp.Value} {{ get; private set; }}");
            sb.AppendLine();
        }

        // Build method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Builds the UI tree from the GUML definition.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public Control Build({controllerName} controller)");
        sb.AppendLine("    {");
        sb.AppendLine("        _controller = controller;");
        sb.AppendLine();
        sb.Append(bodyBuilder);
        sb.AppendLine();
        sb.AppendLine($"        _rootScope = scope_{rootVarName};");
        sb.AppendLine($"        return {rootVarName};");
        sb.AppendLine("    }");

        // Generate [ModuleInitializer] Register()
        if (!string.IsNullOrEmpty(gumlRegistryKey))
        {
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Registers this view's factory into the GUML runtime registry.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    [System.Runtime.CompilerServices.ModuleInitializer]");
            sb.AppendLine("    internal static void Register()");
            sb.AppendLine("    {");
            sb.AppendLine($"        Guml.ControllerRegistry[typeof({controllerName})] = (root) =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            var ctrl = new {controllerName}();");
            sb.AppendLine($"            var view = new {className}();");
            sb.AppendLine("            var rootNode = view.Build(ctrl);");

            sb.AppendLine("            root.AddChild(rootNode);");
            sb.AppendLine("            ctrl.GumlRootNode = rootNode;");
            sb.AppendLine("            ctrl.RootBindingScope = view._rootScope;");
            sb.AppendLine("            ctrl.Created();");
            sb.AppendLine("            return ctrl;");
            sb.AppendLine("        };");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Emits code for a single component node and its children.
    /// </summary>
    private string EmitNode(StringBuilder sb, ComponentDeclarationSyntax comp,
        string? parentVarName, string indent, CompilationApiScanner? scanner)
    {
        string typeName = comp.TypeName.Text;
        string varName = GetVariableName(comp);
        _currentNodeVar = varName;

        // 1. Check if this node matches an import
        string? importedControllerType = null;
        string? importedViewType = null;
        GumlDocumentSyntax? importedDoc = null;

        if (_currentDoc != null)
        {
            foreach (var import in _currentDoc.Imports)
            {
                string importFileName = GetImportFileName(import);
                string nameInGuml = import.Alias != null
                    ? import.Alias.Name.Text
                    : KeyConverter.ToPascalCase(importFileName);

                if (nameInGuml == typeName)
                {
                    importedControllerType = KeyConverter.ToPascalCase(importFileName) + "Controller";
                    importedViewType = KeyConverter.ToPascalCase(importFileName) + "GumlView";
                    if (_importResolver != null)
                    {
                        string importPath = StripQuotes(import.Path.Text);
                        importedDoc = _importResolver(importPath);
                    }
                    break;
                }
            }
        }

        bool isImported = importedControllerType != null;

        sb.AppendLine($"{indent}// Node: {typeName} ({varName})");

        string? controllerVarName = null;

        if (isImported)
        {
            controllerVarName = varName + "_ctrl";
            string viewVarName = varName + "_view";

            sb.AppendLine($"{indent}var {controllerVarName} = new {importedControllerType}();");
            sb.AppendLine($"{indent}var {viewVarName} = new {importedViewType}();");
            sb.AppendLine($"{indent}Control {varName} = {viewVarName}.Build({controllerVarName});");
            sb.AppendLine($"{indent}{controllerVarName}.GumlRootNode = {varName};");
            sb.AppendLine($"{indent}{controllerVarName}.RootBindingScope = {viewVarName}.RootScope;");
            sb.AppendLine($"{indent}{controllerVarName}.Created();");
        }
        else
        {
            sb.AppendLine($"{indent}var {varName} = new {typeName}();");
        }

        // Create BindingScope
        sb.AppendLine($"{indent}var scope_{varName} = new BindingScope({varName});");
        if (parentVarName != null)
        {
            sb.AppendLine($"{indent}scope_{parentVarName}.Add(scope_{varName});");
        }

        // 2. Process members
        foreach (var member in comp.Members)
        {
            switch (member)
            {
                case PropertyAssignmentSyntax prop:
                {
                    string propName = KeyConverter.ToPascalCase(prop.Name.Text);

                    if (_pseudoPropProvider.PseudoPropertyNames.Contains(prop.Name.Text))
                    {
                        var pseudoLines = _pseudoPropProvider.EmitPseudoProperty(
                            varName, typeName, prop.Name.Text, prop.Value,
                            node => EmitExpression((ExpressionSyntax)node), indent);
                        foreach (string line in pseudoLines)
                            sb.AppendLine(line);
                        continue;
                    }

                    string targetVar = varName;
                    string targetType = typeName;

                    if (isImported)
                    {
                        bool useController = ShouldTargetController(propName, importedDoc,
                            importedControllerType!, scanner);
                        if (useController)
                        {
                            targetVar = controllerVarName!;
                            targetType = importedControllerType!;
                        }
                    }

                    string valExpr = EmitExpressionWithEnumQualification(prop.Value, targetType,
                        propName, scanner);

                    string? castExpr = GetPropertyCast(targetType, propName, scanner);
                    if (castExpr != null)
                    {
                        valExpr = castExpr.StartsWith("Convert.")
                            ? $"{castExpr}({valExpr})"
                            : $"{castExpr}{valExpr}";
                    }

                    sb.AppendLine($"{indent}{targetVar}.{propName} = {valExpr};");
                    break;
                }

                case MappingAssignmentSyntax mapping:
                {
                    string propName = KeyConverter.ToPascalCase(mapping.Name.Text);

                    string targetVar = varName;
                    string targetType = typeName;

                    if (isImported)
                    {
                        bool useController = ShouldTargetController(propName, importedDoc,
                            importedControllerType!, scanner);
                        if (useController)
                        {
                            targetVar = controllerVarName!;
                            targetType = importedControllerType!;
                        }
                    }

                    EmitBinding(sb, targetVar, targetType, propName, mapping.Value, indent, scanner);
                    break;
                }

                case EventSubscriptionSyntax evt:
                {
                    string signalText = evt.EventRef.Text; // e.g. "#pressed"
                    string signalName = KeyConverter.ToPascalCase(signalText.TrimStart('#'));
                    string handlerExpr = EmitEventHandler(evt.Handler);

                    bool isGumlEvent = isImported && importedDoc != null
                        && GetEvents(importedDoc.RootComponent)
                            .Any(e => KeyConverter.ToPascalCase(e.Name.Text) == signalName);

                    if (isGumlEvent)
                    {
                        sb.AppendLine($"{indent}{controllerVarName}.{signalName} += {handlerExpr};");
                    }
                    else
                    {
                        sb.AppendLine($"{indent}{_eventProvider.EmitEventSubscription(varName, signalName, handlerExpr)}");
                    }
                    break;
                }

                case ComponentDeclarationSyntax child:
                    EmitNode(sb, child, varName, indent, scanner);
                    break;

                case EachBlockSyntax each:
                    EmitEach(sb, each, varName, indent, scanner);
                    break;

                case TemplateParamAssignmentSyntax templateParam:
                {
                    string paramName = KeyConverter.ToPascalCase(templateParam.Name.Text);
                    string childVarName = EmitNode(sb, templateParam.Component, null, indent, scanner);
                    sb.AppendLine(isImported
                        ? $"{indent}{controllerVarName}.{paramName} = {childVarName};"
                        : $"{indent}{varName}.{paramName} = {childVarName};");
                    break;
                }
            }
        }

        // Metadata
        sb.AppendLine($"{indent}{varName}.SetMeta(\"GumlNodeName\", \"{typeName}\");");

        // Add to parent
        if (parentVarName != null)
        {
            sb.AppendLine($"{indent}{parentVarName}.AddChild({varName});");
        }

        // Handle Alias Assignment
        if (_nodeAliasMap.TryGetValue(comp, out var aliasInfo))
        {
            sb.AppendLine(isImported
                ? $"{indent}this.{aliasInfo.PropertyName} = {controllerVarName};"
                : $"{indent}this.{aliasInfo.PropertyName} = {varName};");

            if (_hasControllerTypeName)
            {
                sb.AppendLine(isImported
                    ? $"{indent}_controller.{aliasInfo.PropertyName} = {controllerVarName};"
                    : $"{indent}_controller.{aliasInfo.PropertyName} = {varName};");
            }
        }

        return varName;
    }

    /// <summary>
    /// Emits a data binding using BindingExpression.
    /// </summary>
    private void EmitBinding(StringBuilder sb, string varName, string componentType,
        string propertyName, ExpressionSyntax exprNode, string indent, CompilationApiScanner? scanner)
    {
        int bindingId = _bindingCounter++;
        string valueExpr = EmitExpressionWithEnumQualification(exprNode, componentType, propertyName, scanner);

        // Collect dependencies
        var deps = CollectControllerDependencies(exprNode);

        string depsExpr;
        if (deps.Count > 0)
        {
            string depsArray = string.Join(", ", deps.Select(d => $"\"{d}\""));
            depsExpr = $"new HashSet<string> {{ {depsArray} }}";
        }
        else
        {
            depsExpr = "new HashSet<string>()";
        }

        string? castExpr = GetPropertyCast(componentType, propertyName, scanner);

        sb.AppendLine($"{indent}// Binding: {propertyName}:= ...");
        sb.AppendLine($"{indent}var binding{bindingId} = new BindingExpression(");
        sb.AppendLine($"{indent}    {varName},");

        if (castExpr != null)
        {
            sb.AppendLine(castExpr.StartsWith("Convert.")
                ? $"{indent}    (val) => {varName}.{propertyName} = {castExpr}(val),"
                : $"{indent}    (val) => {varName}.{propertyName} = {castExpr}val,");
        }
        else
        {
            // Property type could not be resolved at source-generation time; fall back to dynamic
            // dispatch so that the binding is still active at runtime.
            sb.AppendLine($"{indent}    // WARNING: type of '{propertyName}' on '{componentType}' could not be resolved.");
            sb.AppendLine($"{indent}    (val) => ((dynamic){varName}).{propertyName} = val,");
        }

        sb.AppendLine($"{indent}    () => {valueExpr},");
        sb.AppendLine($"{indent}    _controller,");
        sb.AppendLine($"{indent}    {depsExpr});");
        sb.AppendLine($"{indent}binding{bindingId}.Activate();");
        sb.AppendLine($"{indent}scope_{varName}.Add(binding{bindingId});");
    }

    /// <summary>
    /// Emits code for an 'each' block.
    /// </summary>
    private void EmitEach(StringBuilder sb, EachBlockSyntax each, string parentVarName,
        string indent, CompilationApiScanner? scanner)
    {
        int eachId = _eachCounter++;
        string dataSourceExpr = EmitExpression(each.DataSource);
        string parentScopeExpr = _eachScopeStack.Count > 0 ? _eachScopeStack.Peek().ScopeVar : "null";

        string indexName = each.IndexName?.Text ?? "index";
        string valueName = each.ValueName?.Text ?? "value";

        sb.AppendLine($"{indent}// each {dataSourceExpr}");

        // Cache param
        string cacheCountArg = "0";
        if (each.Params != null)
        {
            foreach (var prop in each.Params.Properties)
            {
                string paramName = prop.Name.Text.ToLowerInvariant();
                if (paramName == "cache")
                {
                    cacheCountArg = EmitExpression(prop.Value);
                    break;
                }
            }
        }

        string managerScopeVar = $"__each_{eachId}";
        sb.AppendLine($"{indent}var {managerScopeVar} = new EachListManager({cacheCountArg});");

        sb.AppendLine($"{indent}int __offset_{eachId} = {parentVarName}.GetChildCount();");

        // createItem delegate
        sb.AppendLine($"{indent}Func<int, object, List<Node>> createItem_{eachId} = (__idx_{eachId}, __val_{eachId}) =>");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    var __itemScope_{eachId} = new EachScope({parentScopeExpr});");
        sb.AppendLine($"{indent}    __itemScope_{eachId}[\"{indexName}\"] = __idx_{eachId};");
        sb.AppendLine($"{indent}    __itemScope_{eachId}[\"{valueName}\"] = __val_{eachId};");
        sb.AppendLine($"{indent}    var __nodes = new List<Node>();");

        // Resolve collection element type for typed code generation
        string? elementType = ResolveEachElementType(each.DataSource, scanner);
        _eachScopeStack.Push(($"__itemScope_{eachId}", indexName, valueName, elementType));

        // Emit body children
        if (each.Body != null)
        {
            foreach (var child in each.Body)
            {
                if (child is ComponentDeclarationSyntax childComp)
                {
                    string childVarName = EmitNode(sb, childComp, null, indent + "    ", scanner);
                    sb.AppendLine($"{indent}    __nodes.Add({childVarName});");
                }
            }
        }

        sb.AppendLine($"{indent}    foreach (var n in __nodes)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        n.SetMeta(\"GumlEachScope\", __itemScope_{eachId});");
        sb.AppendLine($"{indent}    }}");

        _eachScopeStack.Pop();
        sb.AppendLine($"{indent}    return __nodes;");
        sb.AppendLine($"{indent}}};");

        // updateItem delegate
        sb.AppendLine($"{indent}Action<Node, int, object> updateItem_{eachId} = (__node, __idx_{eachId}, __val_{eachId}) =>");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    if (__node.HasMeta(\"GumlEachScope\"))");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        var __es = __node.GetMeta(\"GumlEachScope\").As<EachScope>();");
        sb.AppendLine($"{indent}        __es[\"{indexName}\"] = __idx_{eachId};");
        sb.AppendLine($"{indent}        __es[\"{valueName}\"] = __val_{eachId};");
        sb.AppendLine($"{indent}        BindingScope.UpdateRecursive(__node);");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}};");

        // Initial reconcile
        sb.AppendLine($"{indent}{managerScopeVar}.Reconcile((System.Collections.IList){dataSourceExpr}, {parentVarName}, createItem_{eachId}, updateItem_{eachId}, __offset_{eachId});");

        // ListBinding for change notification
        sb.AppendLine($"{indent}var listBinding_{eachId} = new ListBinding(() => (System.Collections.Specialized.INotifyCollectionChanged){dataSourceExpr},");
        sb.AppendLine($"{indent}    () =>");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        {managerScopeVar}.Reconcile((System.Collections.IList){dataSourceExpr}, {parentVarName}, createItem_{eachId}, updateItem_{eachId}, __offset_{eachId});");
        sb.AppendLine($"{indent}    }},");
        sb.AppendLine($"{indent}    (__vidx_{eachId}, __vval_{eachId}) =>");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        if (__vval_{eachId} != null)");
        sb.AppendLine($"{indent}            {managerScopeVar}.UpdateSingleItem(__vidx_{eachId}, __vval_{eachId}, updateItem_{eachId});");
        sb.AppendLine($"{indent}    }});");
        sb.AppendLine($"{indent}scope_{parentVarName}.Add(listBinding_{eachId});");
    }

    /// <summary>
    /// Attempts to resolve the element type of an each-block data source expression.
    /// Supports <c>$controller.PropertyName</c> and bare <c>propertyName</c> patterns.
    /// Returns <c>null</c> when the type cannot be resolved (falls back to dynamic).
    /// </summary>
    private string? ResolveEachElementType(ExpressionSyntax dataSource, CompilationApiScanner? scanner)
    {
        if (scanner == null || _activeControllerTypeName == null)
            return null;

        string? propName = null;

        // Pattern 1: $controller.Items (MemberAccess on $controller)
        if (dataSource is MemberAccessExpressionSyntax ma &&
            ma.Expression is ReferenceExpressionSyntax { Identifier.Kind: SyntaxKind.GlobalRefToken, Identifier.Text: "$controller" or "$root" })
        {
            propName = KeyConverter.ToPascalCase(ma.Name.Text);
        }
        // Pattern 2: bare identifier (camelCase controller property)
        else if (dataSource is ReferenceExpressionSyntax { Identifier.Kind: SyntaxKind.IdentifierToken } ident)
        {
            string name = ident.Identifier.Text;
            string pascal = KeyConverter.ToPascalCase(name);
            if (scanner.HasProperty(_activeControllerTypeName, pascal))
                propName = pascal;
            else if (scanner.HasProperty(_activeControllerTypeName, name))
                propName = name;
        }

        if (propName == null) return null;

        return scanner.ResolveCollectionElementType(_activeControllerTypeName, propName);
    }

    // ========================================
    // Expression emission
    // ========================================

    /// <summary>
    /// Converts a CST expression node into a C# expression string.
    /// </summary>
    private string EmitExpression(ExpressionSyntax node)
    {
        switch (node)
        {
            case LiteralExpressionSyntax literal:
                return EmitLiteral(literal);

            case ReferenceExpressionSyntax reference:
                return EmitReference(reference);

            case MemberAccessExpressionSyntax memberAccess:
                return EmitMemberAccess(memberAccess);

            case BinaryExpressionSyntax binary:
                return $"({EmitExpression(binary.Left)} {binary.OperatorToken.Text} {EmitExpression(binary.Right)})";

            case PrefixUnaryExpressionSyntax prefix:
                return $"({prefix.OperatorToken.Text}{EmitExpression(prefix.Operand)})";

            case ConditionalExpressionSyntax cond:
                return $"({EmitExpression(cond.Condition)} ? {EmitExpression(cond.WhenTrue)} : {EmitExpression(cond.WhenFalse)})";

            case CallExpressionSyntax call:
                return EmitCallExpression(call);

            case StructExpressionSyntax structExpr:
                return EmitStructExpression(structExpr);

            case ResourceExpressionSyntax resource:
                return EmitResourceExpression(resource);

            case EnumValueExpressionSyntax enumValue:
                // .Center → just emit the member name (caller applies enum type)
                return enumValue.Token.Text.TrimStart('.');

            case ObjectLiteralExpressionSyntax objLit:
                return EmitObjectLiteral(objLit);

            case ObjectCreationExpressionSyntax objCreate:
                return EmitNewObj(objCreate);

            case ArrayLiteralExpressionSyntax array:
                return EmitArrayLiteral(array);

            case DictionaryLiteralExpressionSyntax dict:
                return EmitDictionaryLiteral(dict);

            case ParenthesizedExpressionSyntax paren:
                return $"({EmitExpression(paren.Expression)})";

            case TemplateStringExpressionSyntax templateStr:
                return EmitTemplateString(templateStr);

            default:
                return "/* unsupported expression */";
        }
    }

    private string EmitLiteral(LiteralExpressionSyntax literal)
    {
        switch (literal.Token.Kind)
        {
            case SyntaxKind.StringLiteralToken:
                return $"\"{EscapeString(StripQuotes(literal.Token.Text))}\"";

            case SyntaxKind.IntegerLiteralToken:
                return literal.Token.Text;

            case SyntaxKind.FloatLiteralToken:
                string floatText = literal.Token.Text;
                if (!floatText.EndsWith("f", StringComparison.OrdinalIgnoreCase))
                    floatText += "f";
                return floatText;

            case SyntaxKind.TrueLiteralToken:
                return "true";

            case SyntaxKind.FalseLiteralToken:
                return "false";

            case SyntaxKind.NullLiteralToken:
                return "null";

            default:
                return literal.Token.Text;
        }
    }

    private string EmitReference(ReferenceExpressionSyntax reference)
    {
        switch (reference.Identifier.Kind)
        {
            case SyntaxKind.GlobalRefToken:
                string globalName = reference.Identifier.Text; // e.g. "$controller"
                if (globalName is "$controller" or "$root")
                    return "_controller";
                if (globalName == "$item")
                {
                    if (_eachScopeStack.Count > 0)
                    {
                        (string scopeVar, _, _, _) = _eachScopeStack.Peek();
                        return scopeVar;
                    }
                    // Template projection: $item used outside each scope — dynamic placeholder
                    return "((dynamic)null!)";
                }
                // Unknown global reference — emit as comment to surface the issue at compile time
                return $"/* ERROR: unknown global ref '{globalName}' */ default(object)";

            case SyntaxKind.AliasRefToken:
                string aliasName = reference.Identifier.Text; // e.g. "@hello"
                if (_aliasMap.TryGetValue(aliasName, out string? mappedName))
                    return mappedName;
                return aliasName.TrimStart('@');

            case SyntaxKind.IdentifierToken:
                return EmitIdentifierRef(reference.Identifier.Text);

            default:
                return reference.Identifier.Text;
        }
    }

    private string EmitIdentifierRef(string name)
    {
        // Check if this is an each-scope variable
        foreach ((string scopeVar, string idxName, string valName, string? elementType) in _eachScopeStack)
        {
            if (name == idxName)
                return $"((int){scopeVar}.Lookup(\"{name}\")!)";

            if (name == valName)
            {
                if (elementType != null)
                    return $"(({elementType}){scopeVar}.Lookup(\"{name}\")!)";
                return $"((dynamic){scopeVar}.Lookup(\"{name}\"))";
            }
        }

        // Check if this is a controller property
        string propName = name;
        bool isControllerProp = false;

        if (_scanner != null && _activeControllerTypeName != null)
        {
            if (_scanner.HasProperty(_activeControllerTypeName, propName))
            {
                isControllerProp = true;
            }
            else
            {
                string pascalName = KeyConverter.ToPascalCase(propName);
                if (_scanner.HasProperty(_activeControllerTypeName, pascalName))
                {
                    propName = pascalName;
                    isControllerProp = true;
                }
            }
        }
        else if (_hasControllerTypeName)
        {
            propName = KeyConverter.ToPascalCase(propName);
            isControllerProp = true;
        }

        if (isControllerProp)
            return $"_controller.{propName}";

        return name;
    }

    /// <summary>
    /// Emits a signal handler expression, handling declared events and call expressions.
    /// </summary>
    private string EmitEventHandler(ExpressionSyntax handler)
    {
        // Call expressions must be wrapped in a lambda
        if (handler is CallExpressionSyntax)
            return $"() => {EmitExpression(handler)}";

        // References to declared events should use the Raise method
        string? eventName = GetDeclaredEventName(handler);
        if (eventName != null)
            return $"_controller.Raise{KeyConverter.ToPascalCase(eventName)}";

        return EmitExpression(handler);
    }

    /// <summary>
    /// Checks if the expression references a declared event on the current document.
    /// Returns the event name (snake_case) if found, null otherwise.
    /// </summary>
    private string? GetDeclaredEventName(ExpressionSyntax expr)
    {
        if (_currentDoc == null) return null;

        if (expr is MemberAccessExpressionSyntax { Expression: ReferenceExpressionSyntax { Identifier.Kind: SyntaxKind.GlobalRefToken } refExpr } memberAccess
            && (refExpr.Identifier.Text == "$root" || refExpr.Identifier.Text == "$controller"))
        {
            string memberName = memberAccess.Name.Text;
            var events = GetEvents(_currentDoc.RootComponent);
            return events.Any(e => e.Name.Text == memberName) ? memberName : null;
        }

        return null;
    }

    private string EmitMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        // Handle $item.xxx for each-scope variable lookup
        if (memberAccess.Expression is ReferenceExpressionSyntax { Identifier: { Kind: SyntaxKind.GlobalRefToken, Text: "$item" } })
        {
            string member = memberAccess.Name.Text;
            if (_eachScopeStack.Count > 0)
            {
                (string scopeVar, _, _, string? elementType) = _eachScopeStack.Peek();
                if (elementType != null)
                    return $"(({elementType}){scopeVar}.Lookup(\"{member}\")!)";
                return $"((dynamic){scopeVar}.Lookup(\"{member}\"))";
            }
            // Template projection: dynamic placeholder
            return "((dynamic)null!)";
        }

        string expr = EmitExpression(memberAccess.Expression);
        string memberName = KeyConverter.ToPascalCase(memberAccess.Name.Text);
        return $"{expr}.{memberName}";
    }

    private string EmitCallExpression(CallExpressionSyntax call)
    {
        // Check if this is a framework shorthand constructor call like vec2(...), color(...)
        if (call.Expression is ReferenceExpressionSyntax { Identifier.Kind: SyntaxKind.IdentifierToken } refExpr)
        {
            string callName = refExpr.Identifier.Text;

            // i18n: tr("msgid") / tr("msgid", { context: "ctx", key: val, ... })
            if (callName == "tr")
                return EmitTrCall(call);

            // i18n: ntr("singular", "plural", count) / ntr(..., { context: "ctx", count: val })
            if (callName == "ntr")
                return EmitNtrCall(call);

            if (_typeProvider.ResolveTypeShorthand(callName) != null)
            {
                var args = AsList(call.Arguments);
                // Named args form: vec2({ x: 200, y: 100 }) → via EmitShorthandNamedConstruction
                if (args.Count == 1 && args[0] is ObjectLiteralExpressionSyntax objLit)
                {
                    var namedArgs = AsList(objLit.Properties)
                        .Select(p => (p.Name.Text, EmitExpression(p.Value)))
                        .ToList();
                    string? namedResult = _typeProvider.EmitShorthandNamedConstruction(callName, namedArgs);
                    if (namedResult != null)
                        return namedResult;
                }
                var positionalArgs = args.Select(EmitExpression).ToList();
                string? posResult = _typeProvider.EmitShorthandConstruction(callName, positionalArgs);
                if (posResult != null)
                    return posResult;
            }
        }

        string callee = EmitExpression(call.Expression);
        string[] callArgs = AsList(call.Arguments).Select(EmitExpression).ToArray();
        return $"{callee}({string.Join(", ", callArgs)})";
    }

    /// <summary>
    /// Emits a <c>tr(msgid [, options])</c> call as
    /// <c>(Guml.StringProvider?.Tr(msgid [, context [, args]]) ?? msgid)</c>.
    /// The <c>options</c> argument is an object literal where the reserved key
    /// <c>context</c> maps to the gettext context and all other keys become
    /// the named <c>args</c> dictionary.
    /// </summary>
    private string EmitTrCall(CallExpressionSyntax call)
    {
        var argList = AsList(call.Arguments);
        if (argList.Count == 0)
            return "\"\"";

        string msgid = EmitExpression(argList[0]);
        (string? contextExpr, string? argsDictExpr) = ExtractTranslateOptions(argList, optionsIndex: 1);

        string trArgs = BuildTrArgList(msgid, contextExpr, argsDictExpr);
        return $"(Guml.StringProvider?.Tr({trArgs}) ?? {msgid})";
    }

    /// <summary>
    /// Emits a <c>ntr(singular, plural, count [, options])</c> call as
    /// <c>(Guml.StringProvider?.Ntr(...) ?? (count == 1 ? singular : plural))</c>.
    /// </summary>
    private string EmitNtrCall(CallExpressionSyntax call)
    {
        var argList = AsList(call.Arguments);
        if (argList.Count < 3)
            return "\"\"";

        string singular = EmitExpression(argList[0]);
        string plural   = EmitExpression(argList[1]);
        string count    = EmitExpression(argList[2]);
        (string? contextExpr, string? argsDictExpr) = ExtractTranslateOptions(argList, optionsIndex: 3);

        string ntrArgs = BuildTrArgList($"{singular}, {plural}, {count}", contextExpr, argsDictExpr);
        return $"(Guml.StringProvider?.Ntr({ntrArgs}) ?? ({count} == 1 ? {singular} : {plural}))";
    }

    /// <summary>
    /// Reads the translate-options object literal at <paramref name="optionsIndex"/> in
    /// <paramref name="argList"/> and returns separate expressions for
    /// <c>context</c> (nullable) and the args dictionary (nullable).
    /// </summary>
    private (string? contextExpr, string? argsDictExpr) ExtractTranslateOptions(
        List<ExpressionSyntax> argList, int optionsIndex)
    {
        if (argList.Count <= optionsIndex || argList[optionsIndex] is not ObjectLiteralExpressionSyntax opts)
            return (null, null);

        var props = AsList(opts.Properties);
        var contextProp = props.FirstOrDefault(p => p.Name.Text == "context");
        var otherProps  = props.Where(p => p.Name.Text != "context").ToList();

        string? contextExpr  = contextProp != null ? EmitExpression(contextProp.Value) : null;
        string? argsDictExpr = null;

        if (otherProps.Count > 0)
        {
            string entries = string.Join(", ",
                otherProps.Select(p => $"{{ \"{p.Name.Text}\", (object){EmitExpression(p.Value)} }}"));
            argsDictExpr =
                $"new System.Collections.Generic.Dictionary<string, object> {{ {entries} }}";
        }

        return (contextExpr, argsDictExpr);
    }

    /// <summary>
    /// Builds the C# argument list string for Tr/Ntr, omitting trailing null parameters.
    /// </summary>
    private static string BuildTrArgList(string firstArgs, string? contextExpr, string? argsDictExpr)
    {
        if (argsDictExpr != null)
            return $"{firstArgs}, {contextExpr ?? "null"}, {argsDictExpr}";
        if (contextExpr != null)
            return $"{firstArgs}, {contextExpr}";
        return firstArgs;
    }

    private string EmitStructExpression(StructExpressionSyntax structExpr)
    {
        string structType = structExpr.TypeName.Text;

        // Positional args
        if (structExpr.PositionalArgs != null)
        {
            var args = AsList(structExpr.PositionalArgs).Select(EmitExpression).ToList();
            string? posResult = _typeProvider.EmitShorthandConstruction(structType, args);
            if (posResult != null)
                return posResult;
            return $"new {structType}({string.Join(", ", args)})";
        }

        // Named args
        if (structExpr.NamedArgs != null)
        {
            var namedArgs = AsList(structExpr.NamedArgs.Properties)
                .Select(p => (p.Name.Text, EmitExpression(p.Value)))
                .ToList();
            string? namedResult = _typeProvider.EmitShorthandNamedConstruction(structType, namedArgs);
            if (namedResult != null)
                return namedResult;
            // Generic fallback: object initializer with PascalCase keys
            string fallbackProps = string.Join(", ",
                namedArgs.Select(p => $"{KeyConverter.ToPascalCase(p.Item1)} = {p.Item2}"));
            return $"new {structType}() {{ {fallbackProps} }}";
        }

        // Zero-value
        string? zeroResult = _typeProvider.EmitShorthandConstruction(structType, null);
        if (zeroResult != null)
            return zeroResult;
        return $"new {structType}()";
    }

    private string EmitResourceExpression(ResourceExpressionSyntax resource)
    {
        string pathExpr = EmitExpression(resource.Path);
        return resource.Keyword.Kind switch
        {
            SyntaxKind.ImageKeyword => $"Guml.ResourceProvider.LoadImage({pathExpr}, {_currentNodeVar})",
            SyntaxKind.FontKeyword => $"Guml.ResourceProvider.LoadFont({pathExpr}, {_currentNodeVar})",
            SyntaxKind.AudioKeyword => $"Guml.ResourceProvider.LoadAudio({pathExpr}, {_currentNodeVar})",
            SyntaxKind.VideoKeyword => $"Guml.ResourceProvider.LoadVideo({pathExpr}, {_currentNodeVar})",
            _ => "/* unsupported resource type */"
        };
    }

    private string EmitObjectLiteral(ObjectLiteralExpressionSyntax objLit)
    {
        string[] entries = AsList(objLit.Properties)
            .Select(p => $"{{ \"{p.Name.Text}\", {EmitExpression(p.Value)} }}")
            .ToArray();
        return $"new System.Collections.Generic.Dictionary<string, object> {{ {string.Join(", ", entries)} }}";
    }

    private string EmitNewObj(ObjectCreationExpressionSyntax objCreate)
    {
        if (objCreate.Properties.Count == 0)
            return $"new {objCreate.TypeName.Text}()";

        string[] props = AsList(objCreate.Properties)
            .Select(p => $"{KeyConverter.ToPascalCase(p.Name.Text)} = {EmitExpression(p.Value)}")
            .ToArray();
        return $"new {objCreate.TypeName.Text}() {{ {string.Join(", ", props)} }}";
    }

    private string EmitArrayLiteral(ArrayLiteralExpressionSyntax array)
    {
        string[] elements = AsList(array.Elements).Select(EmitExpression).ToArray();
        return $"new {array.TypeName.Text}[] {{ {string.Join(", ", elements)} }}";
    }

    private string EmitDictionaryLiteral(DictionaryLiteralExpressionSyntax dict)
    {
        string keyType = dict.KeyType.Text;
        string valueType = dict.ValueType.Text;
        string[] entries = AsList(dict.Entries)
            .Select(e => $"{{ {EmitExpression(e.Key)}, {EmitExpression(e.Value)} }}")
            .ToArray();
        return $"new System.Collections.Generic.Dictionary<{keyType}, {valueType}> {{ {string.Join(", ", entries)} }}";
    }

    private string EmitTemplateString(TemplateStringExpressionSyntax templateStr)
    {
        var sb = new StringBuilder();
        sb.Append("$\"");
        foreach (var part in templateStr.Parts)
        {
            switch (part)
            {
                case TemplateStringTextSyntax text:
                    sb.Append(EscapeString(text.TextToken.Text));
                    break;
                case TemplateStringInterpolationSyntax interp:
                    sb.Append('{');
                    sb.Append(EmitExpression(interp.Expression));
                    sb.Append('}');
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    // ========================================
    // Helper methods
    // ========================================

    /// <summary>
    /// Emits an expression with enum qualification when applicable.
    /// </summary>
    private string EmitExpressionWithEnumQualification(ExpressionSyntax expr,
        string componentType, string propertyName, CompilationApiScanner? scanner)
    {
        if (scanner is { IsAvailable: true } && expr is EnumValueExpressionSyntax enumVal)
        {
            var propType = scanner.ResolvePropertyType(componentType, propertyName);
            if (propType is { TypeKind: Microsoft.CodeAnalysis.TypeKind.Enum })
            {
                string enumFullName = propType.ToDisplayString(
                    Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat);
                return $"{enumFullName}.{enumVal.Token.Text.TrimStart('.')}";
            }
        }

        return EmitExpression(expr);
    }

    /// <summary>
    /// Gets the cast expression for a property type, or null if no cast needed.
    /// </summary>
    private static string? GetPropertyCast(string componentType, string propertyName,
        CompilationApiScanner? scanner)
    {
        if (scanner is not { IsAvailable: true })
            return null;

        var propType = scanner.ResolvePropertyType(componentType, propertyName);
        if (propType == null)
            return null;

        return scanner.GetCastExpression(propType);
    }

    /// <summary>
    /// Determines if a property should target the imported controller vs the view node.
    /// </summary>
    private bool ShouldTargetController(string propName, GumlDocumentSyntax? importedDoc,
        string importedControllerType, CompilationApiScanner? scanner)
    {
        if (importedDoc != null)
        {
            // Check aliases
            string aliasKey = "@" + KeyConverter.ToCamelCase(propName);
            var aliases = CollectAliases(importedDoc.RootComponent);
            if (aliases.ContainsKey(aliasKey))
                return true;

            // Check parameters
            if (GetParameters(importedDoc.RootComponent)
                .Any(p => KeyConverter.ToPascalCase(p.Name.Text) == KeyConverter.ToPascalCase(propName)))
                return true;
        }

        // Check scanner
        if (scanner != null && scanner.HasProperty(importedControllerType, propName))
            return true;

        return false;
    }

    /// <summary>
    /// Generates a unique variable name for a component node.
    /// </summary>
    private string GetVariableName(ComponentDeclarationSyntax comp)
    {
        if (_varCounter == 0)
        {
            _varCounter++;
            return "root";
        }

        string baseName;
        if (_nodeAliasMap.TryGetValue(comp, out var aliasInfo))
        {
            baseName = KeyConverter.ToCamelCase(aliasInfo.AliasKey.TrimStart('@'));
        }
        else
        {
            string typeName = comp.TypeName.Text;
            baseName = char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
        }

        string name = $"{baseName}_{_varCounter}";
        _varCounter++;
        return name;
    }

    /// <summary>
    /// Collects controller property dependencies from a CST expression.
    /// </summary>
    private HashSet<string> CollectControllerDependencies(ExpressionSyntax node)
    {
        var deps = new HashSet<string>();
        CollectDepsRecursive(node, deps);
        return deps;
    }

    private void CollectDepsRecursive(ExpressionSyntax node, HashSet<string> deps)
    {
        switch (node)
        {
            case MemberAccessExpressionSyntax memberAccess:
            {
                var root = GetMemberAccessRoot(memberAccess);
                if (root is ReferenceExpressionSyntax { Identifier: { Kind: SyntaxKind.GlobalRefToken, Text: "$controller" } })
                {
                    string? firstMember = GetFirstMemberName(memberAccess);
                    if (firstMember != null)
                        deps.Add(KeyConverter.ToPascalCase(firstMember));
                }
                CollectDepsRecursive(memberAccess.Expression, deps);
                break;
            }

            case ReferenceExpressionSyntax { Identifier.Kind: SyntaxKind.IdentifierToken } reference:
            {
                // Implicit controller property access
                string name = reference.Identifier.Text;
                bool isLoopVar = false;
                foreach ((_, string idxN, string valN, _) in _eachScopeStack)
                {
                    if (name == idxN || name == valN)
                    {
                        isLoopVar = true;
                        break;
                    }
                }

                if (!isLoopVar)
                {
                    string propName = name;
                    bool isControllerProp = false;

                    if (_scanner != null && _activeControllerTypeName != null)
                    {
                        if (_scanner.HasProperty(_activeControllerTypeName, propName))
                            isControllerProp = true;
                        else
                        {
                            string pascalName = KeyConverter.ToPascalCase(propName);
                            if (_scanner.HasProperty(_activeControllerTypeName, pascalName))
                            {
                                propName = pascalName;
                                isControllerProp = true;
                            }
                        }
                    }
                    else if (_hasControllerTypeName)
                    {
                        propName = KeyConverter.ToPascalCase(propName);
                        isControllerProp = true;
                    }

                    if (isControllerProp)
                        deps.Add(propName);
                }
                break;
            }

            case BinaryExpressionSyntax binary:
                CollectDepsRecursive(binary.Left, deps);
                CollectDepsRecursive(binary.Right, deps);
                break;

            case PrefixUnaryExpressionSyntax prefix:
                CollectDepsRecursive(prefix.Operand, deps);
                break;

            case ConditionalExpressionSyntax cond:
                CollectDepsRecursive(cond.Condition, deps);
                CollectDepsRecursive(cond.WhenTrue, deps);
                CollectDepsRecursive(cond.WhenFalse, deps);
                break;

            case CallExpressionSyntax call:
                if (call.Expression is ReferenceExpressionSyntax { Identifier.Kind: SyntaxKind.IdentifierToken } callId &&
                    (callId.Identifier.Text == "tr" || callId.Identifier.Text == "ntr"))
                    deps.Add("_locale");
                CollectDepsRecursive(call.Expression, deps);
                foreach (var arg in call.Arguments)
                    CollectDepsRecursive(arg, deps);
                break;

            case StructExpressionSyntax structExpr:
                if (structExpr.PositionalArgs != null)
                    foreach (var arg in structExpr.PositionalArgs)
                        CollectDepsRecursive(arg, deps);
                if (structExpr.NamedArgs != null)
                    foreach (var prop in structExpr.NamedArgs.Properties)
                        CollectDepsRecursive(prop.Value, deps);
                break;

            case ParenthesizedExpressionSyntax paren:
                CollectDepsRecursive(paren.Expression, deps);
                break;

            case TemplateStringExpressionSyntax templateStr:
                foreach (var part in templateStr.Parts)
                    if (part is TemplateStringInterpolationSyntax interp)
                        CollectDepsRecursive(interp.Expression, deps);
                break;

            case ObjectLiteralExpressionSyntax objLit:
                foreach (var prop in objLit.Properties)
                    CollectDepsRecursive(prop.Value, deps);
                break;

            case ObjectCreationExpressionSyntax objCreate:
                foreach (var prop in objCreate.Properties)
                    CollectDepsRecursive(prop.Value, deps);
                break;

            case ResourceExpressionSyntax resource:
                CollectDepsRecursive(resource.Path, deps);
                break;

            case ArrayLiteralExpressionSyntax array:
                foreach (var elem in array.Elements)
                    CollectDepsRecursive(elem, deps);
                break;

            case DictionaryLiteralExpressionSyntax dict:
                foreach (var entry in dict.Entries)
                {
                    CollectDepsRecursive(entry.Key, deps);
                    CollectDepsRecursive(entry.Value, deps);
                }
                break;
        }
    }

    private static ExpressionSyntax GetMemberAccessRoot(MemberAccessExpressionSyntax memberAccess)
    {
        ExpressionSyntax current = memberAccess;
        while (current is MemberAccessExpressionSyntax m)
            current = m.Expression;
        return current;
    }

    private static string? GetFirstMemberName(MemberAccessExpressionSyntax memberAccess)
    {
        if (memberAccess.Expression is MemberAccessExpressionSyntax inner)
            return GetFirstMemberName(inner);
        return memberAccess.Name.Text;
    }

    // ========================================
    // Static helper methods
    // ========================================

    /// <summary>
    /// Collects all alias declarations from a component tree recursively.
    /// </summary>
    internal static Dictionary<string, ComponentDeclarationSyntax> CollectAliases(
        ComponentDeclarationSyntax root)
    {
        var aliases = new Dictionary<string, ComponentDeclarationSyntax>();
        CollectAliasesRecursive(root, aliases);
        return aliases;
    }

    private static void CollectAliasesRecursive(ComponentDeclarationSyntax comp,
        Dictionary<string, ComponentDeclarationSyntax> aliases)
    {
        foreach (var member in comp.Members)
        {
            if (member is ComponentDeclarationSyntax child)
            {
                if (child.AliasPrefix != null)
                {
                    aliases[child.AliasPrefix.AliasRef.Text] = child;
                }
                CollectAliasesRecursive(child, aliases);
            }
            else if (member is EachBlockSyntax { Body: not null } each)
            {
                foreach (var bodyMember in each.Body)
                {
                    if (bodyMember is ComponentDeclarationSyntax eachChild)
                    {
                        if (eachChild.AliasPrefix != null)
                        {
                            aliases[eachChild.AliasPrefix.AliasRef.Text] = eachChild;
                        }
                        CollectAliasesRecursive(eachChild, aliases);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets parameter declarations from a component's members.
    /// </summary>
    internal static IEnumerable<ParameterDeclarationSyntax> GetParameters(
        ComponentDeclarationSyntax comp)
    {
        var result = new List<ParameterDeclarationSyntax>();
        foreach (var member in comp.Members)
            if (member is ParameterDeclarationSyntax param)
                result.Add(param);
        return result;
    }

    /// <summary>
    /// Gets event declarations from a component's members.
    /// </summary>
    internal static IEnumerable<EventDeclarationSyntax> GetEvents(
        ComponentDeclarationSyntax comp)
    {
        var result = new List<EventDeclarationSyntax>();
        foreach (var member in comp.Members)
            if (member is EventDeclarationSyntax evt)
                result.Add(evt);
        return result;
    }

    /// <summary>
    /// Extracts the file name (without extension) from an import directive.
    /// </summary>
    internal static string GetImportFileName(ImportDirectiveSyntax import)
    {
        string path = StripQuotes(import.Path.Text); // e.g. "panel/setting"
        string fileName = path.Replace('\\', '/').TrimEnd('/');
        int lastSlash = fileName.LastIndexOf('/');
        if (lastSlash >= 0) fileName = fileName.Substring(lastSlash + 1);
        if (fileName.EndsWith(".guml", StringComparison.OrdinalIgnoreCase))
            fileName = fileName.Substring(0, fileName.Length - 5);
        return fileName;
    }

    /// <summary>
    /// Recursively collects namespaces of all component types referenced in the document
    /// that can be resolved by the <see cref="CompilationApiScanner"/>.
    /// Imported components are skipped (their controller namespaces are resolved separately).
    /// </summary>
    private static void CollectComponentNamespaces(
        ComponentDeclarationSyntax comp,
        GumlDocumentSyntax doc,
        CompilationApiScanner scanner,
        HashSet<string> namespaces)
    {
        string typeName = comp.TypeName.Text;

        // Skip imported components (their namespaces are handled separately)
        bool isImported = false;
        foreach (var import in doc.Imports)
        {
            string importFileName = GetImportFileName(import);
            string nameInGuml = import.Alias != null
                ? import.Alias.Name.Text
                : KeyConverter.ToPascalCase(importFileName);
            if (nameInGuml == typeName)
            {
                isImported = true;
                break;
            }
        }

        if (!isImported)
        {
            string? ns = scanner.ResolveComponentNamespace(typeName);
            if (ns != null)
                namespaces.Add(ns);
        }

        // Recurse into children
        foreach (var member in comp.Members)
        {
            if (member is ComponentDeclarationSyntax child)
                CollectComponentNamespaces(child, doc, scanner, namespaces);
            else if (member is EachBlockSyntax each && each.Body != null)
            {
                foreach (var bodyChild in each.Body)
                {
                    if (bodyChild is ComponentDeclarationSyntax eachChild)
                        CollectComponentNamespaces(eachChild, doc, scanner, namespaces);
                }
            }
            else if (member is TemplateParamAssignmentSyntax templateParam)
                CollectComponentNamespaces(templateParam.Component, doc, scanner, namespaces);
        }
    }

    // ========================================
    // Collection conversion helpers
    // ========================================

    /// <summary>
    /// Converts a SyntaxList to a standard List for LINQ compatibility.
    /// </summary>
    private static List<T> AsList<T>(SyntaxList<T> source) where T : SyntaxNode
    {
        var list = new List<T>(source.Count);
        foreach (var item in source)
            list.Add(item);
        return list;
    }

    /// <summary>
    /// Converts a SeparatedSyntaxList to a standard List for LINQ compatibility.
    /// </summary>
    private static List<T> AsList<T>(SeparatedSyntaxList<T> source) where T : SyntaxNode
    {
        var list = new List<T>(source.Count);
        foreach (var item in source)
            list.Add(item);
        return list;
    }

    /// <summary>
    /// Strips surrounding quotes from a string literal token text.
    /// </summary>
    internal static string StripQuotes(string text)
    {
        // ReSharper disable once MergeIntoPattern
        if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
            return text.Substring(1, text.Length - 2);
        return text;
    }

    /// <summary>
    /// Escapes special characters in a string literal.
    /// </summary>
    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
