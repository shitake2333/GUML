#nullable enable
using Markdig;
using MdSyntax = Markdig.Syntax;
using MdInlines = Markdig.Syntax.Inlines;
using MdTables = Markdig.Extensions.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestApplication.scripts.notebook;

/// <summary>
/// Parses a Markdown string into a list of <see cref="IMarkdownBlock"/> values
/// suitable for direct consumption by <c>MarkdownCellNode._Draw()</c>.
/// Only a subset of Markdown is supported (see remarks).
/// </summary>
/// <remarks>
/// Supported: paragraphs, ATX headings (H1–H3), bullet/ordered lists,
/// thematic breaks, fenced code blocks (<c>guml</c>, <c>guml-live</c>, etc.), pipe tables,
/// inline bold, italic, and inline code.
/// Unsupported constructs are silently skipped.
/// </remarks>
public static class MarkdownParser
{
    private static readonly MarkdownPipeline s_pipeline =
        new MarkdownPipelineBuilder().UseEmphasisExtras().UsePipeTables().Build();

    /// <summary>Parses <paramref name="markdown"/> into an ordered block list.</summary>
    public static IReadOnlyList<IMarkdownBlock> Parse(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return [];

        var doc = Markdown.Parse(markdown, s_pipeline);
        var result = new List<IMarkdownBlock>();

        foreach (var block in doc)
            result.AddRange(ConvertBlock(block));

        return result;
    }

    // -----------------------------------------------------------------------

    private static IEnumerable<ListItemBlock> _ConvertListBlock(MdSyntax.ListBlock list, int depth)
    {
        foreach (var item in list)
        {
            if (item is not MdSyntax.ListItemBlock li) continue;

            // Collect the immediate paragraph text of this item.
            var inlines = new List<InlineRun>();
            foreach (var child in li)
            {
                if (child is MdSyntax.ParagraphBlock pb)
                    inlines.AddRange(ParseInlines(pb.Inline));
            }
            if (inlines.Count > 0)
                yield return new ListItemBlock(inlines, depth);

            // Recurse into any nested list blocks.
            foreach (var child in li)
            {
                if (child is MdSyntax.ListBlock subList)
                {
                    foreach (var nested in _ConvertListBlock(subList, depth + 1))
                        yield return nested;
                }
            }
        }
    }

    private static IEnumerable<IMarkdownBlock> ConvertBlock(MdSyntax.Block block)
    {
        switch (block)
        {
            case MdSyntax.HeadingBlock h:
            {
                int level = Math.Clamp(h.Level, 1, 3);
                yield return new HeadingBlock(level, ParseInlines(h.Inline));
                break;
            }
            case MdSyntax.ParagraphBlock p:
                yield return new ParagraphBlock(ParseInlines(p.Inline));
                break;
            case MdSyntax.ListBlock list:
            {
                foreach (var item in _ConvertListBlock(list, 0))
                    yield return item;
                break;
            }
            case MdSyntax.ThematicBreakBlock:
                yield return new HRuleBlock();
                break;
            case MdSyntax.FencedCodeBlock fcb:
            {
                // Markdig splits the info string: Info = first word (language id),
                // Arguments = everything after the first space.  Rejoin them.
                string info = ((fcb.Info ?? "") + " " + (fcb.Arguments ?? "")).Trim();
                string code = ExtractCodeText(fcb);

                if (info.StartsWith("guml-live", StringComparison.OrdinalIgnoreCase))
                {
                    // "guml-live ControllerTypeName Height"
                    string[] parts = info.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string typeName = parts.Length > 1 ? parts[1] : "UnknownController";
                    float height = parts.Length > 2 && float.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out float h2) ? h2 : 260f;
                    yield return new GumlLiveBlock(typeName, height);
                }
                else
                {
                    yield return new CodeBlock(info, code);
                }
                break;
            }
            case MdTables.Table table:
            {
                var headers = new List<string>();
                var rows = new List<IReadOnlyList<string>>();

                foreach (var item in table)
                {
                    if (item is MdTables.TableRow row)
                    {
                        var cells = row.Select(c => c is MdTables.TableCell tc ? GetCellText(tc) : "").ToList();
                        if (row.IsHeader)
                            headers.AddRange(cells);
                        else
                            rows.Add(cells);
                    }
                }
                yield return new TableBlock(headers, rows);
                break;
            }
        }
    }

    private static string ExtractCodeText(MdSyntax.FencedCodeBlock fcb)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < fcb.Lines.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(fcb.Lines.Lines[i].Slice.ToString());
        }
        return sb.ToString();
    }

    private static string GetCellText(MdTables.TableCell cell)
    {
        var sb = new StringBuilder();
        foreach (var blk in cell)
        {
            if (blk is MdSyntax.ParagraphBlock p)
            {
                foreach (var run in ParseInlines(p.Inline))
                    sb.Append(run.Text);
            }
        }
        return sb.ToString();
    }

    private static IReadOnlyList<InlineRun> ParseInlines(MdInlines.ContainerInline? container)
    {
        if (container is null) return [];
        var result = new List<InlineRun>();
        CollectInlines(container, InlineStyle.Normal, result);
        return result;
    }

    private static void CollectInlines(MdInlines.ContainerInline container, InlineStyle parentStyle, List<InlineRun> result)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case MdInlines.LiteralInline lit:
                    if (lit.Content.Length > 0)
                        result.Add(new InlineRun(lit.Content.ToString(), parentStyle));
                    break;

                case MdInlines.EmphasisInline em:
                {
                    var style = parentStyle;
                    if (em.DelimiterCount >= 2) style |= InlineStyle.Bold;
                    else                        style |= InlineStyle.Italic;
                    CollectInlines(em, style, result);
                    break;
                }

                case MdInlines.CodeInline code:
                    result.Add(new InlineRun(code.Content, parentStyle | InlineStyle.InlineCode));
                    break;

                case MdInlines.LinkInline link:
                {
                    // Render the link label text with Link style, storing the URL.
                    var linkStyle = (parentStyle | InlineStyle.Link) & ~InlineStyle.InlineCode;
                    var linkText = new List<InlineRun>();
                    CollectInlines(link, linkStyle, linkText);
                    foreach (var run in linkText)
                        result.Add(run with { Url = link.Url ?? "" });
                    break;
                }

                case MdInlines.LineBreakInline:
                    // Soft/hard line breaks become a space in our single-flow renderer.
                    result.Add(new InlineRun(" ", parentStyle));
                    break;

                case MdInlines.ContainerInline nested:
                    CollectInlines(nested, parentStyle, result);
                    break;
            }
        }
    }
}

