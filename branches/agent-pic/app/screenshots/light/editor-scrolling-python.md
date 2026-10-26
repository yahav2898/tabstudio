# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 1.84 | 13.11 | 99% | 0.47 | 1.40 | 3.18 | 121.7 → 129.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 1.87 | 13.00 | 99% | 0.55 | 1.46 | 3.14 | 158.2 → 155.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.58 | 1.83 | 13.48 | 99% | 0.66 | 1.29 | 3.19 | 204.9 → 189.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.48 | 1.61 | 11.60 | 99% | 0.47 | 1.28 | 3.08 | 142.9 → 144.6 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.52 | 2.60 | 12.08 | 99% | 0.57 | 2.26 | 3.08 | 117.6 → 116.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.57 | 2.65 | 14.18 | 99% | 0.58 | 1.78 | 3.27 | 143.1 → 138.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.46 | 1.40 | 11.94 | 99% | 0.39 | 1.14 | 2.90 | 144.5 → 141.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.53 | 1.65 | 12.45 | 99% | 0.55 | 1.30 | 3.16 | 148.2 → 144.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.51 | 1.69 | 10.17 | 99% | 0.52 | 1.35 | 3.04 | 107.2 → 104.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.57 | 1.64 | 11.60 | 99% | 0.48 | 1.30 | 3.15 | 147.8 → 144.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.77 | 3.48 | 69.58 | 99% | 2.30 | 2.51 | 3.23 | 148.0 → 144.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 1.41 | 10.03 | 100% | 0.41 | 1.13 | 2.65 | 68.0 → 67.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.170 |
| ArrangingLayoutable TextView | 0.010 |
| AttachingStyle - | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.50 | 1.55 | 12.32 | 99% | 0.45 | 1.25 | 2.97 | 143.4 → 144.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.38 | 3.60 | 23.32 | 96% | 0.63 | 2.87 | 3.33 | 549.0 → 561.6 | 12 | 3.5 / 31 |

