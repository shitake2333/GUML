namespace GUML.SourceGenerator;
/// <summary>
/// Delegate to resolve imported Guml documents.
/// </summary>
public delegate GumlDoc? ImportResolver(string path);

/// <summary>
/// Holds information about a named alias node declared via @name syntax in .guml.
/// </summary>
internal readonly struct AliasInfo(string aliasKey, string propertyName)
{
    /// <summary>The original alias key (e.g., "@hello").</summary>
    public string AliasKey { get; } = aliasKey;

    /// <summary>The PascalCase property name (e.g., "Hello").</summary>
    public string PropertyName { get; } = propertyName;
}

/// <summary>
/// Transforms a parsed <see cref="GumlDoc"/> AST into a C# source code string
/// representing a strongly-typed view class.
/// Each call to <see cref="Emit"/> creates a fresh instance to ensure thread-safety.
/// </summary>
internal sealed class GumlCodeEmitter
{
    private int _varCounter;
    private int _bindingCounter;
    private int _eachCounter;
    private string _currentNodeVar = "null";

    /// <summary>
    /// Tracks each-loop scope variable names for nested each code generation.
    /// Each entry maps loop variable names (IndexName, ValueName) to the C# scope variable
    /// (e.g., "__scope_0") so that <see cref="BuildRefChain"/> can emit
    /// <c>__scope_N.Lookup("varName")</c> instead of a bare variable reference.
    /// </summary>
    private readonly Stack<(string ScopeVar, HashSet<string> VarNames)> _eachScopeStack = new();

    /// <summary>Maps alias key ("@hello") to PascalCase property name ("Hello").</summary>
    private readonly Dictionary<string, string> _aliasMap = new Dictionary<string, string>();

    /// <summary>Reverse map from GumlSyntaxNode instance to alias info, for assignment generation.</summary>
    private readonly Dictionary<GumlSyntaxNode, AliasInfo> _nodeAliasMap = new Dictionary<GumlSyntaxNode, AliasInfo>();

    /// <summary>Whether a controller type name is explicitly provided (enables controller alias assignment).</summary>
    private bool _hasControllerTypeName;

    private CompilationApiScanner? _scanner;
    private string? _activeControllerTypeName;
    private GumlDoc? _currentDoc;
    private ImportResolver? _importResolver;

    /// <summary>
    /// Emits C# source code for the given GUML document.
    /// </summary>
    /// <param name="filePath">The original .guml file path (used to derive class name).</param>
    /// <param name="doc">The parsed GUML document AST.</param>
    /// <param name="additionalNamespaces">
    /// Additional namespaces to include as <c>using</c> directives in the generated code.
    /// This corresponds to the runtime <c>Guml.ControllerNamespaces</c> list and ensures that
    /// user-defined GUI component types can be resolved at compile time.
    /// </param>
    /// <param name="scanner">
    /// Optional <see cref="CompilationApiScanner"/> for resolving property types at compile time.
    /// When provided and the property type is resolved, the emitter generates a direct setter
    /// delegate (zero-reflection path). When <c>null</c> or the type cannot be resolved, the
    /// emitter falls back to the string property name constructor (reflection path).
    /// </param>
    /// <param name="controllerTypeName">
    /// When provided, overrides the controller type name derived from the file name.
    /// Used by the [GumlController]-driven pipeline to supply the exact controller type.
    /// </param>
    /// <param name="gumlRegistryKey">
    /// When provided, generates a [ModuleInitializer] Register() method that registers
    /// this view's factory into Guml.ViewRegistry with this key.
    /// </param>
    /// <param name="importResolver">
    /// Optional delegate to resolve imported Guml documents for strict parameter checking.
    /// </param>
    /// <returns>Complete C# source code string for the generated view class.</returns>
    public static string Emit(string filePath, GumlDoc doc, IReadOnlyList<string>? additionalNamespaces = null,
        CompilationApiScanner? scanner = null, string? controllerTypeName = null, string? gumlRegistryKey = null,
        ImportResolver? importResolver = null)
    {
        var emitter = new GumlCodeEmitter();
        emitter._scanner = scanner;
        emitter._importResolver = importResolver;
        return emitter.EmitInternal(filePath, doc, additionalNamespaces ?? Array.Empty<string>(),
            scanner, controllerTypeName, gumlRegistryKey);
    }

    /// <summary>
    /// Generates a partial class for the controller with strongly-typed
    /// named node properties derived from @alias declarations and import controller
    /// properties derived from import declarations in the .guml file.
    /// Properties that already exist on the controller class (user-defined) are skipped.
    /// </summary>
    /// <param name="controllerTypeName">Simple type name of the controller class.</param>
    /// <param name="controllerNamespace">Namespace of the controller class, or null for global.</param>
    /// <param name="doc">The parsed GUML document containing alias and import declarations.</param>
    /// <param name="existingMembers">Set of member names already defined on the controller class. May be null.</param>
    /// <returns>C# source code for the controller partial class, or null if nothing to generate.</returns>
    public static string? EmitControllerPartial(string controllerTypeName, string? controllerNamespace, GumlDoc doc,
        ISet<string>? existingMembers = null)
    {
        if (doc.LocalAlias.Count == 0 && doc.Imports.Count == 0 && doc.RootNode.ParameterNodes.Count == 0 && doc.RootNode.EventNodes.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// This file was generated by GUML.SourceGenerator. Do not edit manually.");
        sb.AppendLine();
        sb.AppendLine("using Godot;");
        sb.AppendLine("using GUML;");
        sb.AppendLine("using System;");
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

        // Generate named node properties from @alias declarations
        foreach (var kvp in doc.LocalAlias)
        {
            string? aliasKey = kvp.Key;
            var node = kvp.Value;
            string propName = KeyConverter.ToPascalCase(aliasKey.TrimStart('@'));

            if (existingMembers != null && existingMembers.Contains(propName))
                continue;

            // Determine type: check if node.Name matches an import
            string propType = node.Name;
            foreach (var import in doc.Imports)
            {
                 // Robust path parsing
                 string fileName = import.Key.Replace('\\', '/').TrimEnd('/');
                 int lastSlash = fileName.LastIndexOf('/');
                 if (lastSlash >= 0) fileName = fileName.Substring(lastSlash + 1);
                 if (fileName.EndsWith(".guml", StringComparison.OrdinalIgnoreCase))
                     fileName = fileName.Substring(0, fileName.Length - 5);

                 string nameInGuml = import.Value.Alias ?? KeyConverter.ToPascalCase(fileName);

                 if (nameInGuml == node.Name)
                 {
                     // Use the Controller type for imported components
                     propType = KeyConverter.ToPascalCase(fileName) + "Controller";
                     break;
                 }
            }

            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Alias reference '{kvp.Key}' pointing to a {node.Name} node.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public {propType} {propName} {{ get; internal set; }}");
            sb.AppendLine();
        }

        // Generate import controller properties from import declarations
        foreach (var import in doc.Imports)
        {
            string importKey = import.Key; // e.g., "panel/setting"
            string importFileName = importKey.Contains("/")
                ? importKey.Substring(importKey.LastIndexOf('/') + 1)
                : importKey;
            string importControllerTypeName = KeyConverter.ToPascalCase(importFileName) + "Controller";

            if (existingMembers != null && existingMembers.Contains(importControllerTypeName))
                continue;

            sb.AppendLine($"{indent}    /// <summary>Import controller for '{importKey}.guml', auto-generated from import declaration.</summary>");
            sb.AppendLine($"{indent}    public {importControllerTypeName} {importControllerTypeName} {{ get; set; }}");
            sb.AppendLine();
            anyGenerated = true;
        }

        // Generate Parameter Properties (param Type Name: DefaultValue)
        foreach (var param in doc.RootNode.ParameterNodes)
        {
            string propName = KeyConverter.ToPascalCase(param.ParameterName);
            string typeName = param.TypeName;
            if (typeName == "string") typeName = "string"; // normalize? generic types?

            if (existingMembers != null && existingMembers.Contains(propName))
                continue;

            string defaultVal = "";
            if (param.DefaultValue != null)
            {
                // We'll create a temporary emitter instance to emit expression string
                var tempEmitter = new GumlCodeEmitter();
                defaultVal = " = " + tempEmitter.EmitExpression(param.DefaultValue);
            }

            sb.AppendLine($"{indent}    private {typeName} _{propName}{defaultVal};");
            sb.AppendLine($"{indent}    /// <summary>Parameter property '{param.ParameterName}'.</summary>");
            sb.AppendLine($"{indent}    [GumlParameter]");
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

        // Generate Events (event Name(Type arg...))
        foreach (var evt in doc.RootNode.EventNodes)
        {
             string evtName = KeyConverter.ToPascalCase(evt.EventName);

             if (existingMembers != null && existingMembers.Contains(evtName))
                continue;

             string delegateType;
             if (evt.Arguments.Count == 0)
             {
                 delegateType = "Action";
             }
             else
             {
                 var typeList = string.Join(", ", evt.Arguments.Select(a => a.Type));
                 delegateType = $"Action<{typeList}>";
             }

             sb.AppendLine($"{indent}    /// <summary>Event '{evt.EventName}'.</summary>");
             sb.AppendLine($"{indent}    public event {delegateType}? {evtName};");

             // Raise method
             var argsDecl = string.Join(", ", evt.Arguments.Select((a, i) => $"{a.Type} arg{i}"));
             var argsCall = string.Join(", ", evt.Arguments.Select((_, i) => $"arg{i}"));

             sb.AppendLine($"{indent}    internal void Raise{evtName}({argsDecl}) => {evtName}?.Invoke({argsCall});");
             sb.AppendLine();
             anyGenerated = true;
        }

        sb.AppendLine($"{indent}}}");

        // If all properties were skipped (already exist), return null
        if (!anyGenerated)
        {
            return null;
        }

        return sb.ToString();
    }

    private string EmitInternal(string filePath, GumlDoc doc, IReadOnlyList<string> additionalNamespaces,
        CompilationApiScanner? scanner, string? controllerTypeName, string? gumlRegistryKey)
    {
        // 1. Determine class names
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string className = KeyConverter.ToPascalCase(fileName) + "GumlView";
        string controllerName = controllerTypeName ?? (KeyConverter.ToPascalCase(fileName) + "Controller");
        _activeControllerTypeName = controllerName;

        _hasControllerTypeName = !string.IsNullOrEmpty(controllerTypeName);
        _currentDoc = doc;

        // Collect alias names from the doc and build reverse map
        foreach (var kvp in doc.LocalAlias)
        {
            string aliasVarName = KeyConverter.ToPascalCase(kvp.Key.TrimStart('@'));
            _aliasMap[kvp.Key] = aliasVarName;
            _nodeAliasMap[kvp.Value] = new AliasInfo(kvp.Key, aliasVarName);
        }

        var bodyBuilder = new StringBuilder();
        string rootVarName = EmitNode(bodyBuilder, doc.RootNode, null, "        ", scanner);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// This file was generated by GUML.SourceGenerator. Do not edit manually.");
        sb.AppendLine();
        sb.AppendLine("using Godot;");
        sb.AppendLine("using GUML;");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");

        // Additional namespaces from GumlNamespaces MSBuild property
        // (compile-time equivalent of Guml.ControllerNamespaces)
        foreach (string? ns in additionalNamespaces)
        {
            if (!string.IsNullOrWhiteSpace(ns))
            {
                sb.AppendLine($"using {ns.Trim()};");
            }
        }

        sb.AppendLine();

        // Generate import comments
        if (doc.Imports.Count > 0)
        {
            sb.AppendLine("// GUML Imports (require runtime resolution):");
            foreach (var import in doc.Imports)
            {
                sb.AppendLine($"//   {(import.Value.IsTopLevel ? "import_top" : "import")} \"{import.Key}\"");
            }
            sb.AppendLine();
        }

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Auto-generated GUML view for {fileName}.guml.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public partial class {className}");
        sb.AppendLine("{");

        // Controller field
        sb.AppendLine($"    private {controllerName} _controller;");
        sb.AppendLine($"    private BindingScope _rootScope;");
        sb.AppendLine($"    internal BindingScope RootScope => _rootScope;");
        sb.AppendLine();

        // Alias properties
        foreach (var kvp in _aliasMap)
        {
            var node = doc.LocalAlias[kvp.Key];

            // Determine type: check if node.Name matches an import
            string propType = node.Name;
            foreach (var import in doc.Imports)
            {
                 // Robust path parsing
                 string importFileName = import.Key.Replace('\\', '/').TrimEnd('/');
                 int lastSlash = importFileName.LastIndexOf('/');
                 if (lastSlash >= 0) importFileName = importFileName.Substring(lastSlash + 1);
                 if (importFileName.EndsWith(".guml", StringComparison.OrdinalIgnoreCase))
                     importFileName = importFileName.Substring(0, importFileName.Length - 5);

                 string nameInGuml = import.Value.Alias ?? KeyConverter.ToPascalCase(importFileName);

                 if (nameInGuml == node.Name)
                 {
                     // Use the Controller type for imported components
                     propType = KeyConverter.ToPascalCase(importFileName) + "Controller";
                     break;
                 }
            }

            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Alias reference '{kvp.Key}' pointing to a {node.Name} node.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public {propType} {kvp.Value} {{ get; private set; }}");
            sb.AppendLine();
        }

        // Build method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Builds the UI tree from the GUML definition.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"controller\">The controller instance to bind to.</param>");
        sb.AppendLine("    /// <returns>The root control node of the UI tree.</returns>");
        sb.AppendLine($"    public Control Build({controllerName} controller)");
        sb.AppendLine("    {");
        sb.AppendLine("        _controller = controller;");
        sb.AppendLine();
        sb.Append(bodyBuilder);
        sb.AppendLine();
        sb.AppendLine($"        _rootScope = scope_{rootVarName};");
        sb.AppendLine($"        return {rootVarName};");
        sb.AppendLine("    }");

        // Generate [ModuleInitializer] Register() method when registry key is provided
        if (!string.IsNullOrEmpty(gumlRegistryKey))
        {
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Registers this view's factory into the GUML runtime registry.");
            sb.AppendLine("    /// Called automatically via module initializer.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    [System.Runtime.CompilerServices.ModuleInitializer]");
            sb.AppendLine("    internal static void Register()");
            sb.AppendLine("    {");
            sb.AppendLine($"        Guml.ViewRegistry[\"{EscapeString(gumlRegistryKey!)}\"] = (root) =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            var ctrl = new {controllerName}();");
            sb.AppendLine($"            var view = new {className}();");
            sb.AppendLine("            var rootNode = view.Build(ctrl);");

            // Emit import resolution code
            if (doc.Imports.Count > 0)
            {
                // Compute the directory of the current .guml file relative to the project
                string gumlDir = Path.GetDirectoryName(gumlRegistryKey!)?.Replace('\\', '/') ?? "";
                if (gumlDir.Length > 0 && !gumlDir.EndsWith("/"))
                {
                    gumlDir += "/";
                }

                sb.AppendLine();
                sb.AppendLine("            // Resolve imports");
                foreach (var import in doc.Imports)
                {
                    string importKey = import.Key;   // e.g., "panel/setting" or "../components/MyButton"
                    bool isTop = import.Value.IsTopLevel;

                    // Resolve relative path segments (..)
                    string importGumlPath = ResolveRelativePath($"{gumlDir}{importKey}.guml");

                    // Derive controller property name: "panel/setting" → file name "setting" → PascalCase "Setting" → "SettingController"
                    string importFileName = importKey.Replace('\\', '/').TrimEnd('/');
                    int lastSlash = importFileName.LastIndexOf('/');
                    if (lastSlash >= 0) importFileName = importFileName.Substring(lastSlash + 1);

                    string importControllerTypeName = KeyConverter.ToPascalCase(importFileName) + "Controller";

                    if (isTop)
                    {
                        sb.AppendLine($"            var import_{importControllerTypeName} = Guml.LoadGuml(root, \"{EscapeString(importGumlPath)}\");");
                    }
                    else
                    {
                        sb.AppendLine($"            var import_{importControllerTypeName} = Guml.LoadGuml(rootNode, \"{EscapeString(importGumlPath)}\");");
                    }
                    sb.AppendLine($"            ctrl.{importControllerTypeName} = ({importControllerTypeName})import_{importControllerTypeName};");
                }
            }

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
    /// Emits code for a single AST node (component declaration) and its children.
    /// </summary>
    /// <returns>The variable name assigned to this node.</returns>
    private string EmitNode(StringBuilder sb, GumlSyntaxNode node, string? parentVarName, string indent,
        CompilationApiScanner? scanner)
    {
        string varName = GetVariableName(node);
        _currentNodeVar = varName;

        // 1. Check if this node matches an import
        string? importKey = null;
        string? importedControllerType = null;
        string? importedViewType = null;
        GumlDoc? importedDoc = null;

        foreach (var kvp in _currentDoc.Imports)
        {
            string path = kvp.Key;
            var info = kvp.Value;

            // Robust path parsing
            string fileName = path.Replace('\\', '/').TrimEnd('/');
            int lastSlash = fileName.LastIndexOf('/');
            if (lastSlash >= 0) fileName = fileName.Substring(lastSlash + 1);
            if (fileName.EndsWith(".guml", StringComparison.OrdinalIgnoreCase))
                fileName = fileName.Substring(0, fileName.Length - 5);

            string nameInGuml = info.Alias ?? KeyConverter.ToPascalCase(fileName);

            if (nameInGuml == node.Name)
            {
                importKey = path;
                importedControllerType = KeyConverter.ToPascalCase(fileName) + "Controller";
                importedViewType = KeyConverter.ToPascalCase(fileName) + "GumlView";
                break;
            }
        }

        if (importKey != null && _importResolver != null)
        {
            importedDoc = _importResolver(importKey);
        }

        bool isImported = importKey != null;

        sb.AppendLine($"{indent}// Node: {node.Name} ({varName})");

        string? controllerVarName = null;

        if (isImported)
        {
            // Imported Component Instantiation
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
            // Native Godot Node Instantiation
            sb.AppendLine($"{indent}var {varName} = new {node.Name}();");
        }

        // Create BindingScope for this node
        sb.AppendLine($"{indent}var scope_{varName} = new BindingScope({varName});");
        if (parentVarName != null)
        {
            sb.AppendLine($"{indent}scope_{parentVarName}.Add(scope_{varName});");
        }

        // 2. Properties
        foreach (var kvp in node.Properties)
        {
            string propName = kvp.Key; // e.g., "Text"

            if (propName == "ThemeOverrides")
            {
                // Theme overrides apply to the Godot Control node (View), even for imported components
                EmitThemeOverrides(sb, varName, kvp.Value.Item2, indent);
                continue;
            }

            bool useController = false;

            if (isImported && importedDoc != null)
            {
                // Check if propName is actually an alias in the imported component
                string aliasKey = "@" + KeyConverter.ToCamelCase(propName);
                if (importedDoc.LocalAlias.TryGetValue(aliasKey, out _))
                {
                   propName = KeyConverter.ToPascalCase(aliasKey.TrimStart('@'));
                   useController = true;
                }
                // Check if propName refers to a parameter
                else if (importedDoc.RootNode.ParameterNodes.Any(p => KeyConverter.ToPascalCase(p.ParameterName) == KeyConverter.ToPascalCase(propName)))
                {
                    propName = KeyConverter.ToPascalCase(propName);
                    useController = true;
                }
            }

            // Fallback: Check scanner for explicit controller properties
            if (isImported && !useController && scanner != null && importedControllerType != null)
            {
                 // Check if the controller type actually has this property
                 if (scanner.HasProperty(importedControllerType, propName))
                 {
                     useController = true;
                 }
            }

            if (isImported)
            {
                 if (useController)
                 {
                     EmitPropertyOrBinding(sb, controllerVarName!, importedControllerType!, propName, kvp.Value, indent, scanner);
                 }
                 else
                 {
                     // Fallback to setting on the view (Control)
                     EmitPropertyOrBinding(sb, varName, "Control", propName, kvp.Value, indent, scanner);
                 }
            }
            else
            {
                EmitPropertyOrBinding(sb, varName, node.Name, propName, kvp.Value, indent, scanner);
            }
        }

        // 3. Signals / Events
        foreach (var kvp in node.Signals)
        {
            string signalName = kvp.Key; // raw string from parser (e.g. "pressed" from "#pressed")
            string evtName = KeyConverter.ToPascalCase(signalName);
            string handlerExpr = EmitExpression(kvp.Value);

            bool isEvent = false;
            if (isImported && importedDoc != null)
            {
                isEvent = importedDoc.RootNode.EventNodes.Any(e => KeyConverter.ToPascalCase(e.EventName) == evtName);
            }

            if (isImported && isEvent)
            {
                sb.AppendLine($"{indent}{controllerVarName}.{evtName} += {handlerExpr};");
            }
            else
            {
                sb.AppendLine($"{indent}{varName}.{evtName} += {handlerExpr};");
            }
        }

        // Common: Metadata
        sb.AppendLine($"{indent}{varName}.SetMeta(\"GumlNodeName\", \"{node.Name}\");");

        // Hierarchy (Add Children)
        if (parentVarName != null)
        {
            sb.AppendLine($"{indent}{parentVarName}.AddChild({varName});");
        }

        // Process Children
        foreach (var childNode in node.Children)
        {
            EmitNode(sb, childNode, varName, indent, scanner);
        }

        // Process Each Nodes
        foreach (var eachNode in node.EachNodes)
        {
            EmitEach(sb, eachNode, varName, indent, scanner);
        }

        // Handle Alias Assignment
        if (_nodeAliasMap.TryGetValue(node, out var aliasInfo))
        {
            if (isImported)
            {
                sb.AppendLine($"{indent}this.{aliasInfo.PropertyName} = {controllerVarName};");
            }
            else
            {
                sb.AppendLine($"{indent}this.{aliasInfo.PropertyName} = {varName};");
            }
        }

        return varName;
    }

    private void EmitPropertyOrBinding(StringBuilder sb, string destVar, string typeName, string propName, (bool isBind, GumlExprNode expr) value, string indent, CompilationApiScanner? scanner)
    {
        if (value.isBind)
        {
            // Binding
            EmitBinding(sb, destVar, typeName, propName, value.expr, indent, scanner);
        }
        else
        {
            // Static Assignment
            string valExpr;

            // Special handling for Enum values to prepend Type Name (e.g. Center -> LayoutPreset.Center)
            if (scanner != null && scanner.IsAvailable
                && value.expr is GumlValueNode { ValueType: GumlValueType.Enum } enumVal)
            {
                 var propType = scanner.ResolvePropertyType(typeName, propName);
                 if (propType != null && propType.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum)
                 {
                     string enumFullName = propType.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat);
                     valExpr = $"{enumFullName}.{enumVal.EnumMemberName}";
                 }
                 else
                 {
                     valExpr = EmitExpression(value.expr);
                 }
            }
            else
            {
                 valExpr = EmitExpression(value.expr);
            }

            // Type casting for other types (int -> Enum, float -> int, etc.)
            string? castExpr = null;
            if (scanner != null && scanner.IsAvailable)
            {
                var propType = scanner.ResolvePropertyType(typeName, propName);
                if (propType != null)
                {
                    castExpr = CompilationApiScanner.GetCastExpression(propType);
                }
            }

            if (castExpr != null)
            {
                 // Avoid double casting if valExpr already contains the type (for enums handled above)
                 // But wait, if valExpr is "Enum.Center", casting to (Enum) is fine.

                 // Special check: if we just resolved an enum and prepended the type, we might not need the cast or the cast is redundant but harmless.
                 // However, "Godot.LayoutPreset.Center" is arguably an expression of that type.

                 if (castExpr.StartsWith("Convert."))
                     valExpr = $"{castExpr}({valExpr})";
                 else
                     valExpr = $"{castExpr}{valExpr}";
            }

            sb.AppendLine($"{indent}{destVar}.{propName} = {valExpr};");
        }
    }


    /// <summary>
    /// Generates a unique variable name for a component node.
    /// Root node gets "root"; others get lowercaseType + counter.
    /// </summary>
    private string GetVariableName(GumlSyntaxNode node)
    {
        if (_varCounter == 0)
        {
            _varCounter++;
            return "root";
        }

        string baseName;
        if (_nodeAliasMap.TryGetValue(node, out var aliasInfo))
        {
            // Use alias name as variable name (camelCase)
            baseName = KeyConverter.ToCamelCase(aliasInfo.AliasKey.TrimStart('@'));
        }
        else
        {
           // Fallback to TypeName
           baseName = char.ToLowerInvariant(node.Name[0]) + node.Name.Substring(1);
        }

        string name = $"{baseName}_{_varCounter}";
        _varCounter++;
        return name;
    }

    /// <summary>
    /// Emits a data binding using <c>BindingExpression</c> and registers it with the node's <c>BindingScope</c>.
    /// When a <see cref="CompilationApiScanner"/> is available and the property type can be resolved,
    /// generates a direct setter delegate (zero-reflection). Otherwise falls back to string property name (reflection).
    /// </summary>
    private void EmitBinding(StringBuilder sb, string varName, string componentType,
        string propertyName, GumlExprNode exprNode, string indent, CompilationApiScanner? scanner)
    {
        int bindingId = _bindingCounter++;
        string valueExpr = EmitExpression(exprNode);

        // Resolve enum values to fully qualified names
        if (scanner != null && scanner.IsAvailable
            && exprNode is GumlValueNode { ValueType: GumlValueType.Enum } bindEnumVal)
        {
            var propType = scanner.ResolvePropertyType(componentType, propertyName);
            if (propType != null && propType.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum)
            {
                string enumFullName = propType.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat);
                valueExpr = $"{enumFullName}.{bindEnumVal.EnumMemberName}";
            }
        }

        // Collect controller property dependencies from the expression
        var deps = new HashSet<string>();
        CollectControllerDependencies(exprNode, deps);

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

        // Try to resolve property type for zero-reflection binding
        string? castExpr = null;
        if (scanner != null && scanner.IsAvailable)
        {
            var propType = scanner.ResolvePropertyType(componentType, propertyName);
            if (propType != null)
            {
                castExpr = CompilationApiScanner.GetCastExpression(propType);
            }
        }

        sb.AppendLine($"{indent}// Binding: {propertyName}:= ...");
        sb.AppendLine($"{indent}var binding{bindingId} = new BindingExpression(");
        sb.AppendLine($"{indent}    {varName},");

        if (castExpr != null)
        {
            // Zero-reflection path: direct setter with type cast
            if (castExpr.StartsWith("Convert."))
            {
                // Convert.ToSingle(val) style
                sb.AppendLine($"{indent}    (val) => {varName}.{propertyName} = {castExpr}(val),");
            }
            else
            {
                // (type)val style
                sb.AppendLine($"{indent}    (val) => {varName}.{propertyName} = {castExpr}val,");
            }
        }
        else
        {
            // Reflection fallback: string property name
            sb.AppendLine($"{indent}    \"{propertyName}\",");
        }

        sb.AppendLine($"{indent}    () => {valueExpr},");
        sb.AppendLine($"{indent}    _controller,");
        sb.AppendLine($"{indent}    {depsExpr});");
        sb.AppendLine($"{indent}binding{bindingId}.Activate();");
        sb.AppendLine($"{indent}scope_{varName}.Add(binding{bindingId});");
    }

    /// <summary>
    /// Emits code for an 'each' block (list rendering) with dynamic ListBinding support.
    /// <para>
    /// Uses <c>EachScope</c> to pass loop variables through a chained scope,
    /// avoiding nested closure capture bugs. Each iteration creates its own <c>EachScope</c>
    /// whose parent points to the enclosing each's scope.
    /// </para>
    /// <para>
    /// For <c>ListChangedType.Add</c> events, only the new item is rendered (incremental).
    /// For other change types (Remove, Insert, Clear), a full re-render is performed.
    /// </para>
    /// </summary>
    private void EmitEach(StringBuilder sb, GumlEachNode each, string parentVarName, string indent,
        CompilationApiScanner? scanner)
    {
        int eachId = _eachCounter++;
        string dataSourceExpr = EmitExpression(each.DataSource);
        string parentScopeExpr = _eachScopeStack.Count > 0 ? _eachScopeStack.Peek().ScopeVar : "null";

        sb.AppendLine($"{indent}// each {dataSourceExpr}");

        // Use an EachScope as the manager of this each-block's items
        GumlExprNode? cacheSizeExpr = null;
        if (each.Params != null)
        {
            if (!each.Params.TryGetValue("cache", out cacheSizeExpr))
            {
                each.Params.TryGetValue("Cache", out cacheSizeExpr);
            }
        }
        string cacheCountArg = cacheSizeExpr != null ? EmitExpression(cacheSizeExpr) : "0";

        string managerScopeVar = $"__each_{eachId}";
        sb.AppendLine($"{indent}var {managerScopeVar} = new EachListManager({cacheCountArg});");

        // Capture static child count as offset for this each block
        sb.AppendLine($"{indent}int __offset_{eachId} = {parentVarName}.GetChildCount();");

        // --- createItem: creates a fresh node LIST for a single data datum ---
        sb.AppendLine($"{indent}Func<int, object, List<Node>> createItem_{eachId} = (__idx_{eachId}, __val_{eachId}) =>");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    var __itemScope_{eachId} = new EachScope({parentScopeExpr});");
        sb.AppendLine($"{indent}    __itemScope_{eachId}[\"{each.IndexName}\"] = __idx_{eachId};");
        sb.AppendLine($"{indent}    __itemScope_{eachId}[\"{each.ValueName}\"] = __val_{eachId};");
        sb.AppendLine($"{indent}    var __nodes = new List<Node>();");

        _eachScopeStack.Push(($"__itemScope_{eachId}", new HashSet<string> { each.IndexName, each.ValueName }));

        // Emit child components
        for (int i = 0; i < each.Children.Count; i++)
        {
            var child = each.Children[i];
            // We pass null for cacheStackVar because EachScope/EachListManager now handles caching
            // Pass null for parentVarName because EachListManager controls the hierarchy (Reconcile)
            string childVarName = EmitNode(sb, child, null, indent + "    ", scanner);
            sb.AppendLine($"{indent}    __nodes.Add({childVarName});");
        }

        // Set scope metadata on all root nodes of the item
        sb.AppendLine($"{indent}    foreach (var n in __nodes)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        n.SetMeta(\"GumlEachScope\", __itemScope_{eachId});");
        sb.AppendLine($"{indent}    }}");

        _eachScopeStack.Pop();
        sb.AppendLine($"{indent}    return __nodes;");
        sb.AppendLine($"{indent}}};");

        // --- updateItem: updates an existing node with new data ---
        sb.AppendLine($"{indent}Action<Node, int, object> updateItem_{eachId} = (__node, __idx_{eachId}, __val_{eachId}) =>");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    if (__node.HasMeta(\"GumlEachScope\"))");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        var __es = __node.GetMeta(\"GumlEachScope\").As<EachScope>();");
        sb.AppendLine($"{indent}        __es[\"{each.IndexName}\"] = __idx_{eachId};");
        sb.AppendLine($"{indent}        __es[\"{each.ValueName}\"] = __val_{eachId};");
        sb.AppendLine($"{indent}        BindingScope.UpdateRecursive(__node);");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}};");

        // --- Perform initial reconcile ---
        sb.AppendLine($"{indent}{managerScopeVar}.Reconcile({dataSourceExpr}, {parentVarName}, createItem_{eachId}, updateItem_{eachId}, __offset_{eachId});");

        // --- Watch for changes ---
        sb.AppendLine($"{indent}var listBinding_{eachId} = new ListBinding(() => (INotifyListChanged){dataSourceExpr},");
        sb.AppendLine($"{indent}    (_, _, _, _) =>");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        {managerScopeVar}.Reconcile({dataSourceExpr}, {parentVarName}, createItem_{eachId}, updateItem_{eachId}, __offset_{eachId});");
        sb.AppendLine($"{indent}    }});");
        sb.AppendLine($"{indent}scope_{parentVarName}.Add(listBinding_{eachId});");
    }

    /// <summary>
    /// Emits theme override property assignments.
    /// </summary>
    private void EmitThemeOverrides(StringBuilder sb, string varName, GumlExprNode exprNode, string indent)
    {
        if (exprNode is GumlValueNode { ValueType: GumlValueType.Dictionary, DictionaryValue: not null } valueNode)
        {
            foreach (var kvp in valueNode.DictionaryValue)
            {
                // Unpack nested categories if the user grouped them (e.g. constants: { ... })
                if (kvp.Value is GumlValueNode { ValueType: GumlValueType.Dictionary } nestedDict)
                {
                    // Recursively emit overrides from the nested dictionary
                    EmitThemeOverrides(sb, varName, nestedDict, indent);
                    continue;
                }

                string overrideName = KeyConverter.FromCamelCase(kvp.Key);
                string valueExpr = EmitExpression(kvp.Value);
                // Theme overrides use AddTheme*Override methods
                sb.AppendLine($"{indent}// ThemeOverride: {overrideName}");

                // Choose override method by the expression node type when possible.
                switch (kvp.Value)
                {
                    case GumlValueNode gv when gv.ValueType == GumlValueType.Color:
                        sb.AppendLine($"{indent}{varName}.AddThemeColorOverride(\"{overrideName}\", {valueExpr});");
                        break;

                    case GumlValueNode gv when gv.ValueType == GumlValueType.Font:
                        sb.AppendLine($"{indent}{varName}.AddThemeFontOverride(\"{overrideName}\", {valueExpr});");
                        break;

                    case GumlValueNode gv when gv.ValueType == GumlValueType.Image:
                        // Image -> icon/texture override
                        sb.AppendLine($"{indent}{varName}.AddThemeIconOverride(\"{overrideName}\", {valueExpr});");
                        break;

                    case GumlValueNode gv when gv.ValueType == GumlValueType.StyleBox:
                        sb.AppendLine($"{indent}{varName}.AddThemeStyleboxOverride(\"{overrideName}\", {valueExpr});");
                        break;

                    case GumlValueNode gv when gv.ValueType == GumlValueType.Int || gv.ValueType == GumlValueType.Float:
                        // Heuristic: font size keys often contain "font_size" or "size"
                        if (overrideName.IndexOf("font_size", StringComparison.OrdinalIgnoreCase) >= 0 || overrideName.IndexOf("size", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                             // Ensure separation uses Constant override, not FontSize
                             if (overrideName.Contains("separation"))
                             {
                                 sb.AppendLine($"{indent}{varName}.AddThemeConstantOverride(\"{overrideName}\", Convert.ToInt32({valueExpr}));");
                             }
                             else
                             {
                                 sb.AppendLine($"{indent}{varName}.AddThemeFontSizeOverride(\"{overrideName}\", Convert.ToInt32({valueExpr}));");
                             }
                        }
                        else
                        {
                            sb.AppendLine($"{indent}{varName}.AddThemeConstantOverride(\"{overrideName}\", Convert.ToInt32({valueExpr}));");
                        }
                        break;

                    default:
                        // Fallback: try constant override first, leave TODO comment for manual adjustment
                        sb.AppendLine($"{indent}// Fallback: emit constant override (adjust if this key expects color/font/style)");
                        sb.AppendLine($"{indent}{varName}.AddThemeConstantOverride(\"{overrideName}\", Convert.ToInt32({valueExpr}));");
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Converts an AST expression node into a C# expression string.
    /// </summary>
    private string EmitExpression(GumlExprNode node)
    {
        switch (node)
        {
            case GumlStructNode structNode:
                return EmitStructExpression(structNode);

            case GumlValueNode valueNode:
                return EmitValueExpression(valueNode);

            case InfixOpNode infixNode:
                string left = EmitExpression(infixNode.Left);
                string right = EmitExpression(infixNode.Right);
                return $"({left} {infixNode.Op} {right})";

            case PrefixOpNode prefixNode:
                string operand = EmitExpression(prefixNode.Right);
                return $"({prefixNode.Op}{operand})";

            default:
                return "/* unsupported expression */";
        }
    }

    /// <summary>
    /// Converts a value node into a C# expression string.
    /// </summary>
    private string EmitValueExpression(GumlValueNode node)
    {
        switch (node.ValueType)
        {
            case GumlValueType.String:
                return $"\"{EscapeString(node.StringValue)}\"";

            case GumlValueType.Int:
                return node.IntValue.ToString();

            case GumlValueType.Float:
                return node.FloatValue.ToString("G") + "f";

            case GumlValueType.Boolean:
                return node.BooleanValue ? "true" : "false";

            case GumlValueType.Null:
                return "null";

            case GumlValueType.Vector2:
                if (node.Vector2XNode != null && node.Vector2YNode != null)
                {
                    string x = EmitExpression(node.Vector2XNode);
                    string y = EmitExpression(node.Vector2YNode);
                    return $"new Vector2({x}, {y})";
                }
                return "Vector2.Zero";

            case GumlValueType.Color:
                if (node.ColorRNode != null && node.ColorGNode != null &&
                    node.ColorBNode != null && node.ColorANode != null)
                {
                    string r = EmitExpression(node.ColorRNode);
                    string g = EmitExpression(node.ColorGNode);
                    string b = EmitExpression(node.ColorBNode);
                    string a = EmitExpression(node.ColorANode);
                    return $"new Color({r}, {g}, {b}, {a})";
                }
                return "new Color()";

            case GumlValueType.StyleBox:
                return EmitStyleBox(node);

            case GumlValueType.Image:
                if (node.ResourceNode != null)
                {
                    string path = EmitExpression(node.ResourceNode);
                    return $"Guml.ResourceProvider.LoadImage({path}, {_currentNodeVar})";
                }
                return "null";
            case GumlValueType.Font:
                if (node.ResourceNode != null)
                {
                    string path = EmitExpression(node.ResourceNode);
                    return $"Guml.ResourceProvider.LoadFont({path}, {_currentNodeVar})";
                }
                return "null";
            case GumlValueType.Audio:
                if (node.ResourceNode != null)
                {
                    string path = EmitExpression(node.ResourceNode);
                    return $"Guml.ResourceProvider.LoadAudio({path}, {_currentNodeVar})";
                }
                return "null";
            case GumlValueType.Video:
                if (node.ResourceNode != null)
                {
                    string path = EmitExpression(node.ResourceNode);
                    return $"Guml.ResourceProvider.LoadVideo({path}, {_currentNodeVar})";
                }
                return "null";

            case GumlValueType.Ref:
                return EmitRef(node);

            case GumlValueType.Dictionary:
                // Dictionary literals - emit inline
                if (node.DictionaryValue != null)
                {
                    string[] entries = node.DictionaryValue
                        .Select(kvp => $"{{ \"{kvp.Key}\", {EmitExpression(kvp.Value)} }}")
                        .ToArray();
                    return $"new System.Collections.Generic.Dictionary<string, object> {{ {string.Join(", ", entries)} }}";
                }
                return "null";

            case GumlValueType.Enum:
                // Emit bare member name; the caller applies the enum type cast/qualification.
                return node.EnumMemberName!;
            case GumlValueType.NewObj:
                return EmitNewObj(node);

            default:
                return "/* unsupported value type */";
        }
    }

    /// <summary>
    /// Emits a StyleBox construction expression.
    /// </summary>
    private string EmitStyleBox(GumlValueNode node)
    {
        switch (node.StyleNodeType)
        {
            case StyleNodeType.Empty:
                return "new StyleBoxEmpty()";

            case StyleNodeType.Flat:
                if (node.StyleNode != null)
                {
                    return EmitStyleBoxWithProperties("StyleBoxFlat", node.StyleNode);
                }
                return "new StyleBoxFlat()";

            case StyleNodeType.Line:
                if (node.StyleNode != null)
                {
                    return EmitStyleBoxWithProperties("StyleBoxLine", node.StyleNode);
                }
                return "new StyleBoxLine()";

            case StyleNodeType.Texture:
                if (node.StyleNode != null)
                {
                    return EmitStyleBoxWithProperties("StyleBoxTexture", node.StyleNode);
                }
                return "new StyleBoxTexture()";

            default:
                return "new StyleBoxEmpty()";
        }
    }

    /// <summary>
    /// Emits a StyleBox with property initializer syntax.
    /// </summary>
    private string EmitStyleBoxWithProperties(string typeName, GumlExprNode propsNode)
    {
        if (propsNode is GumlValueNode { ValueType: GumlValueType.Dictionary, DictionaryValue: not null } objNode)
        {
            var props = new List<string>();
            foreach (var kvp in objNode.DictionaryValue)
            {
                string key = kvp.Key;
                string val = EmitExpression(kvp.Value);

                if (typeName == "StyleBoxFlat")
                {
                    // Expand shorthands (permissive matching)
                    string keyLower = key.Replace("_", "").ToLowerInvariant();

                    if (keyLower == "borderwidth")
                    {
                        props.Add($"BorderWidthLeft = {val}");
                        props.Add($"BorderWidthTop = {val}");
                        props.Add($"BorderWidthRight = {val}");
                        props.Add($"BorderWidthBottom = {val}");
                        continue;
                    }
                    if (keyLower == "contentmargin")
                    {
                        props.Add($"ContentMarginLeft = {val}");
                        props.Add($"ContentMarginTop = {val}");
                        props.Add($"ContentMarginRight = {val}");
                        props.Add($"ContentMarginBottom = {val}");
                        continue;
                    }
                    if (keyLower == "cornerradius")
                    {
                        props.Add($"CornerRadiusTopLeft = {val}");
                        props.Add($"CornerRadiusTopRight = {val}");
                        props.Add($"CornerRadiusBottomRight = {val}");
                        props.Add($"CornerRadiusBottomLeft = {val}");
                        continue;
                    }

                    // Specific mapping or PascalCase conversion
                    props.Add($"{KeyConverter.ToPascalCase(key)} = {val}");
                }
                else
                {
                    props.Add($"{KeyConverter.ToPascalCase(key)} = {val}");
                }
            }
            return $"new {typeName}() {{ {string.Join(", ", props)} }}";
        }
        return $"new {typeName}()";
    }

    /// <summary>
    /// Emits a struct constructor expression (e.g., new Vector2I(10, 20)).
    /// </summary>
    private string EmitStructExpression(GumlStructNode node)
    {
        string[] args = node.Args.Select(EmitExpression).ToArray();
        string argsStr = string.Join(", ", args);
        return node.TypeName switch
        {
            "vec2i" => $"new Vector2I({argsStr})",
            "vec3" => $"new Vector3({argsStr})",
            "vec3i" => $"new Vector3I({argsStr})",
            "vec4" => $"new Vector4({argsStr})",
            "vec4i" => $"new Vector4I({argsStr})",
            "rect2" => $"new Rect2({argsStr})",
            "rect2i" => $"new Rect2I({argsStr})",
            _ => throw new NotSupportedException($"Unknown struct type: {node.TypeName}")
        };
    }

    /// <summary>
    /// Emits a new object creation expression with property initializer syntax.
    /// </summary>
    private string EmitNewObj(GumlValueNode node)
    {
        if (node.DictionaryValue == null || node.DictionaryValue.Count == 0)
            return $"new {node.NewObjTypeName}()";

        string[] props = node.DictionaryValue
            .Select(kvp => $"{kvp.Key} = {EmitExpression(kvp.Value)}")
            .ToArray();
        return $"new {node.NewObjTypeName}() {{ {string.Join(", ", props)} }}";
    }

    /// <summary>
    /// Emits a reference expression (variable/property chain).
    /// </summary>
    private string EmitRef(GumlValueNode node)
    {
        var parts = new List<string>();
        BuildRefChain(node, parts);
        return string.Join(".", parts);
    }

    /// <summary>
    /// Recursively builds a reference chain (e.g., $controller.Property.SubProperty).
    /// </summary>
    private void BuildRefChain(GumlValueNode node, List<string> parts)
    {
        if (node.RefNode != null)
        {
            BuildRefChain(node.RefNode, parts);
        }

        switch (node.RefType)
        {
            case RefType.GlobalRef:
                if (node.RefName == "$controller")
                {
                    parts.Add("_controller");
                }
                else
                {
                    // Global refs: Guml.GlobalRefs["$name"]
                    parts.Add($"Guml.GlobalRefs[\"{node.RefName}\"]");
                }
                break;

            case RefType.LocalAliasRef:
                string aliasName = node.RefName;
                if (_aliasMap.TryGetValue(aliasName, out string? mappedName))
                {
                    parts.Add(mappedName);
                }
                else
                {
                    parts.Add(aliasName.TrimStart('@'));
                }
                break;

            case RefType.LocalRef:
                // Check if this variable is defined in any enclosing each scope
                string? foundScopeVar = null;
                foreach ((string scopeVar, HashSet<string> varNames) in _eachScopeStack)
                {
                    if (varNames.Contains(node.RefName))
                    {
                        foundScopeVar = scopeVar;
                        break; // Innermost scope wins
                    }
                }
                if (foundScopeVar != null)
                {
                    // Emit EachScope lookup instead of bare variable name
                    parts.Add($"((dynamic){foundScopeVar}.Lookup(\"{node.RefName}\"))");
                }
                else
                {
                    // If not found in local scope, check if it's a property on the controller.
                    // This supports implicit controller property access like { Text: MyProp } instead of { Text: $controller.MyProp }
                    string propName = node.RefName;

                    // The property name might be camelCase in GUML but PascalCase in C# controller.
                    // We try to match both or rely on standard conversion.
                    // Usually GUML identifiers are same case as C# unless strictly enforced otherwise.
                    // However, standard C# properties are PascalCase.

                    // If we have scanner and controller type, we can check existence.
                    bool isControllerProp = false;
                    if (_scanner != null && _activeControllerTypeName != null)
                    {
                          // Check exact name or converted PascalCase name
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
                    } else if (_hasControllerTypeName) {
                         // Fallback without scanner: assume it is a controller property if we have a controller.
                         // This is risky for static types like "Math", but "Math" is unlikely to be a valid property name on controller anyway.
                         // But we should probably prefer PascalCase conversion for properties.
                         propName = KeyConverter.ToPascalCase(propName);
                         isControllerProp = true;
                    }

                    if (isControllerProp)
                    {
                        parts.Add($"_controller.{propName}");
                    }
                    else
                    {
                        parts.Add(node.RefName);
                    }
                }
                break;

            case RefType.PropertyRef:
                parts.Add(KeyConverter.ToPascalCase(node.RefName));
                break;
        }
    }

    /// <summary>
    /// Collects controller property names referenced in an expression
    /// (used to determine which PropertyChanged events to subscribe to).
    /// </summary>
    private void CollectControllerDependencies(GumlExprNode node, HashSet<string> deps)
    {
        switch (node)
        {
            case GumlValueNode valueNode when valueNode.ValueType == GumlValueType.Ref:
                // Walk the ref chain to find $controller.PropertyName patterns or implicit property refs
                CollectRefDependencies(valueNode, deps, false);
                break;

            case GumlStructNode structNode:
                foreach (var arg in structNode.Args)
                {
                    CollectControllerDependencies(arg, deps);
                }
                break;

            case InfixOpNode infixNode:
                CollectControllerDependencies(infixNode.Left, deps);
                CollectControllerDependencies(infixNode.Right, deps);
                break;

            case PrefixOpNode prefixNode:
                CollectControllerDependencies(prefixNode.Right, deps);
                break;
        }
    }

    /// <summary>
    /// Walks a reference chain to find direct controller property dependencies.
    /// Only the first property after $controller is tracked (e.g., $controller.Foo.Bar tracks "Foo").
    /// </summary>
    private void CollectRefDependencies(GumlValueNode node, HashSet<string> deps, bool isControllerChild)
    {
        // If my parent in the ref chain is $controller, then I am a controller property
        if (isControllerChild && node.RefType == RefType.PropertyRef)
        {
            deps.Add(KeyConverter.ToPascalCase(node.RefName));
            return; // Only track the first-level property
        }

        // Implicit controller property access (bare Identifier that isn't a loop variable)
        if (node.RefType == RefType.LocalRef)
        {
             bool isLoopVar = false;
             foreach ((string _, HashSet<string> varNames) in _eachScopeStack)
             {
                 if (varNames.Contains(node.RefName))
                 {
                     isLoopVar = true;
                     break;
                 }
             }

             if (!isLoopVar)
             {
                 string propName = node.RefName;
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
                 {
                     deps.Add(propName);
                 }
             }
        }

        if (node.RefNode != null)
        {
            bool parentIsController = node.RefNode.RefType == RefType.GlobalRef &&
                                      node.RefNode.RefName == "$controller";
            // Recurse into the parent ref first
            CollectRefDependencies(node.RefNode, deps, parentIsController);
            // If parent is $controller and current node is a property ref, add it
            if (parentIsController && node.RefType == RefType.PropertyRef)
            {
                deps.Add(KeyConverter.ToPascalCase(node.RefName));
            }
        }
    }

    /// <summary>
    /// Resolves parent directory references (..) in a path string.
    /// Example: "a/b/../c" -> "a/c"
    /// </summary>
    private static string ResolveRelativePath(string path)
    {
        var parts = path.Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var stack = new Stack<string>();

        foreach (var part in parts)
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (stack.Count > 0) stack.Pop();
                continue;
            }
            stack.Push(part);
        }

        // Reconstruct path
        var result = string.Join("/", stack.Reverse());
        return result;
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
