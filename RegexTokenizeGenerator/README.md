# RegexTokenizeGenerator

RegexTokenizeGenerator is a high-performance, easy-to-use tokenizer (lexer) generator for .NET 10.0 and later. It allows you to define token patterns using regular expressions or exact string matches and efficiently tokenize input text.

## Features

- **Flexible Pattern Definition**: Define tokens using Regex or exact string literals.
- **High Performance**:
  - Uses a **Trie (Prefix Tree)** for O(M) lookup of keywords and operators (where M is token length), avoiding linear separate regex checks.
  - Optimized **Binary Search** for source code line/column lookups (O(log N)).
- **Detailed Diagnostics**:
  - Rust-style error reporting with code snippets and pointers to the exact location of errors.
- **Fluent/Functional API**: Define rules as a list of pattern-to-name mappings.

## Installation

Install via NuGet:

```bash
dotnet add package RegexTokenizeGenerator
```

## Usage

### Basic Tokenization

```csharp
using RegexTokenizeGenerator;

// 1. Define Patterns
var identifierRule = TokenizeGenerator.ValuePattern("foo"); // Exact match
var numberRule = TokenizeGenerator.NumberPattern(isFloat: false); // Integer
var wsRule = TokenizeGenerator.CharsPattern([' ', '\t']); // Whitespace

// 2. Create Rules Mapping
// Mapping format: (Func<string, ITokenize, string> nameGenerator, Func<ITokenize, string> patternMatcher)
// You can just use strings for names for simplicity.
var specs = new List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>
{
    ((_, _) => "identifier", identifierRule),
    ((_, _) => "number", numberRule),
    ((_, _) => "", wsRule) // Empty name means skip/ignore these tokens (e.g. whitespace)
};

// 3. Initialize Tokenizer
var tokenizer = new TokenizeGenerator(specs);

// 4. Tokenize
var code = "foo 123";
var tokens = tokenizer.Tokenize(code);

foreach (var token in tokens)
{
    Console.WriteLine($"{token.Name}: {token.Value} at {token.Line}:{token.Column}");
}
// Output:
// identifier: foo at 1:1
// number: 123 at 1:5
// eof:  at 1:8
```

### Advanced: Custom Regex

You can use any `Regex` for matching:

```csharp
var myRegex = new Regex(@"[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled);
var regexRule = TokenizeGenerator.RegexPattern(myRegex);
```

### Detailed Error Reporting

When tokenization fails, `TokenizeException` provides a `PrintDiagnostic()` method:

```csharp
try 
{
    tokenizer.Tokenize("invalid code");
}
catch (TokenizeException ex)
{
    Console.WriteLine(ex.PrintDiagnostic());
}
```

Output example:

```text
error: Unexpected token 'i' at 1:1.
  --> line:1:1
   |
 1 | invalid code
   | ^ Unexpected token 'i'
```

## License

MIT License. See [LICENSE](LICENSE) file.
