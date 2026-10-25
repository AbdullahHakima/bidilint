using System.Text;

namespace BidiLint;

internal sealed class IsolateInfo
{
    public required int Opener { get; init; }
    public required char Kind { get; init; }
    public required int Depth { get; init; }
    public int Closer { get; set; }
    public bool Closed { get; set; }
    public bool HasVisibleContent { get; set; }
}

internal sealed record EmptyIsolate(int Opener, int Closer);

internal sealed record IsolateDepthIssue(int Opener, int Depth);

// Structural scan of explicit bidi controls. It mirrors the stack behavior
// that matters for lint findings: isolate matching, PDFs that are ignored
// inside an isolate, unmatched legacy controls, depth limits, and empty
// isolates. It does not resolve text levels; BidiResolver owns that.
internal sealed class BidiStructure
{
    public const int RecommendedIsolateDepth = 16;
    public const int UnicodeExplicitDepth = 125;

    private readonly List<IsolateInfo> _isolates = new();
    private readonly List<int> _unclosedIsolates = new();
    private readonly List<int> _strayPdis = new();
    private readonly List<int> _unclosedEmbeddings = new();
    private readonly List<int> _strayPdfs = new();
    private readonly List<int> _ineffectivePdfs = new();
    private readonly List<int> _overflowControls = new();
    private readonly List<EmptyIsolate> _emptyIsolates = new();
    private readonly List<IsolateDepthIssue> _depthIssues = new();

    public IReadOnlyList<IsolateInfo> Isolates => _isolates;
    public IReadOnlyList<int> UnclosedIsolates => _unclosedIsolates;
    public IReadOnlyList<int> StrayPdis => _strayPdis;
    public IReadOnlyList<int> UnclosedEmbeddings => _unclosedEmbeddings;
    public IReadOnlyList<int> StrayPdfs => _strayPdfs;
    public IReadOnlyList<int> IneffectivePdfs => _ineffectivePdfs;
    public IReadOnlyList<int> OverflowControls => _overflowControls;
    public IReadOnlyList<EmptyIsolate> EmptyIsolates => _emptyIsolates;
    public IReadOnlyList<IsolateDepthIssue> DepthIssues => _depthIssues;
    public bool HasControls { get; private set; }
    public bool HasIsolateControls { get; private set; }

    public static BidiStructure Scan(string value)
    {
        var structure = new BidiStructure();
        structure.Build(value);
        return structure;
    }

    private void Build(string value)
    {
        var stack = new List<Frame>();
        var isolateDepth = 0;

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (!BidiScanner.IsBidiControl(current))
            {
                if (current is >= '\uD800' and <= '\uDBFF' &&
                    index + 1 < value.Length &&
                    value[index + 1] is >= '\uDC00' and <= '\uDFFF')
                {
                    index++;
                }

                continue;
            }

            HasControls = true;
            if (BidiScanner.IsIsolateControl(current)) HasIsolateControls = true;
            if (BidiScanner.IsDirectionalMark(current)) continue;

            if (BidiScanner.IsLegacyControl(current) && current != BidiScanner.Pdf)
            {
                stack.Add(new Frame(
                    current,
                    index,
                    Isolate: false,
                    IsolateDepth: isolateDepth,
                    Overflow: stack.Count >= UnicodeExplicitDepth));
                if (stack[^1].Overflow) _overflowControls.Add(index);
                continue;
            }

            if (current == BidiScanner.Pdf)
            {
                if (stack.Count == 0)
                {
                    _strayPdfs.Add(index);
                    continue;
                }

                if (stack.Any(frame => frame.Isolate))
                {
                    // UAX #9 X6a: a PDF has no effect while any isolate is open.
                    _ineffectivePdfs.Add(index);
                }
                else
                {
                    stack.RemoveAt(stack.Count - 1);
                }

                continue;
            }

            if (current == BidiScanner.Pdi)
            {
                var isolateIndex = FindLastIsolate(stack);
                if (isolateIndex < 0)
                {
                    _strayPdis.Add(index);
                    continue;
                }

                for (var i = stack.Count - 1; i >= isolateIndex; i--)
                {
                    var frame = stack[i];
                    if (!frame.Isolate) continue;

                    isolateDepth--;
                    var isolate = _isolates.First(info => info.Opener == frame.Opener);
                    isolate.Closer = index;
                    isolate.Closed = true;
                    isolate.HasVisibleContent = HasVisibleContent(
                        value,
                        frame.Opener + 1,
                        index);
                    if (!isolate.HasVisibleContent)
                        _emptyIsolates.Add(new EmptyIsolate(frame.Opener, index));
                }

                stack.RemoveRange(isolateIndex, stack.Count - isolateIndex);
                continue;
            }

            // FSI, LRI, or RLI.
            isolateDepth++;
            var depth = isolateDepth;
            if (depth > RecommendedIsolateDepth)
                _depthIssues.Add(new IsolateDepthIssue(index, depth));

            var isolateInfo = new IsolateInfo
            {
                Opener = index,
                Kind = current,
                Depth = depth,
                Closer = value.Length,
                Closed = false,
                HasVisibleContent = false,
            };
            _isolates.Add(isolateInfo);

            var overflow = stack.Count >= UnicodeExplicitDepth;
            stack.Add(new Frame(
                current,
                index,
                Isolate: true,
                IsolateDepth: depth,
                Overflow: overflow));
            if (overflow) _overflowControls.Add(index);
        }

        foreach (var frame in stack)
        {
            if (frame.Isolate)
            {
                _unclosedIsolates.Add(frame.Opener);
                var isolate = _isolates.First(info => info.Opener == frame.Opener);
                isolate.HasVisibleContent = HasVisibleContent(
                    value,
                    frame.Opener + 1,
                    value.Length);
            }
            else
            {
                _unclosedEmbeddings.Add(frame.Opener);
            }
        }
    }

    private static int FindLastIsolate(List<Frame> stack)
    {
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            if (stack[i].Isolate) return i;
        }

        return -1;
    }

    private static bool HasVisibleContent(string value, int start, int end)
    {
        for (var index = start; index < end;)
        {
            if (Rune.DecodeFromUtf16(value.AsSpan(index, end - index), out var rune, out var consumed) !=
                System.Buffers.OperationStatus.Done)
            {
                return true;
            }

            if (!Rune.IsWhiteSpace(rune) &&
                Rune.GetUnicodeCategory(rune) != System.Globalization.UnicodeCategory.Format)
            {
                return true;
            }

            index += consumed;
        }

        return false;
    }

    private sealed record Frame(
        char Kind,
        int Opener,
        bool Isolate,
        int IsolateDepth,
        bool Overflow);
}
