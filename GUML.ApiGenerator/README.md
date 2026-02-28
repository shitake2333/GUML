# GUML.ApiGenerator

`GUML.ApiGenerator` is a .NET CLI tool that downloads a Godot C# NuGet package, extracts the public UI-related API surface via Mono.Cecil, and produces a JSON cache consumed by the GUML LSP/IDE tooling.

## Installation

```bash
# After packing/publishing the tool
dotnet tool install -g GUML.ApiGenerator --version <version>
```

## Usage

```bash
guml-apigen -v 4.6.1 -o ./api-cache -p GodotSharp
```

| Option | Description |
| --- | --- |
| `-v, --version` | Godot package version (e.g. `4.6.1`). |
| `-o, --output-dir` | Destination folder for the generated JSON. |
| `-p, --package-id` | NuGet package ID (default `GodotSharp`). |
| `-s, --source` | Custom NuGet feed URL (default `https://api.nuget.org/v3/index.json`). |

The tool will download the specified package, locate the DLL/XML doc pair, and write `<PackageId>.<Version>.json` inside the output directory.

## Building & Packing

```bash
# Run tests
dotnet test GUML.ApiGenerator.Tests

# Pack as a NuGet CLI tool
dotnet pack GUML.ApiGenerator -c Release
```

## License

Distributed under the MIT License. See `LICENSE` for details.
