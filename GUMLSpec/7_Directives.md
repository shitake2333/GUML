# §7 Directives

Directives appear at the beginning of a GUML document, before the root component declaration. They control how the document integrates with external files and the controller system.

```antlr
guml_document
    : import_directive* root_component_declaration EOF
    ;
```

## §7.1 Import Directive

The import directive includes another GUML file, making its root component available for use within the current document.

```antlr
import_directive
    : 'import'
      string_literal
      ('as' import_alias)?
    ;

import_alias
    : component_name
    | identifier
    ;
```

- **`import`**: Loads the referenced GUML file and introduces its exported component type into the current document scope.

The *string_literal* specifies the path to the GUML file. It may be an absolute resource path (e.g., `"res://components/MyButton.guml"`) or a relative path from the current file (e.g., `"../components/MyButton"`).

The optional `as` clause assigns an alias name to the imported component type, allowing it to be referenced by that name instead of the file name.

Each path may only be imported once. Duplicate imports are ill-formed and shall produce a diagnostic.

> *Example:*
> ```guml
> import "components/header.guml"
> import "../shared/footer.guml" as Footer
>
> Panel {
>     Header {}
>     Footer {}
>     Label { text: "Content" }
> }
> ```
> *end example*

> *Note:* The imported file path does not need to include the `.guml` extension; implementations may resolve both with and without the extension. *end note*

Controller association is specified by `[GumlController]` (§9.1), not by directives in `.guml` source.

