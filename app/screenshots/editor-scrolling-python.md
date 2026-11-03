# Scrolling a long Python file

- 2211 lines; 28 visible lines; viewport 779 × 586 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.77 | 3.37 | 18.71 | 99% | 0.53 | 2.89 | 4.93 | 93.5 → 100.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 3.66 | 19.82 | 99% | 1.58 | 2.98 | 5.06 | 129.8 → 130.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.12 | 3.96 | 20.72 | 99% | 1.84 | 2.99 | 5.95 | 176.9 → 162.9 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.13 | 4.53 | 18.60 | 99% | 1.86 | 3.11 | 6.09 | 115.2 → 116.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.87 | 3.41 | 18.18 | 99% | 0.92 | 2.78 | 5.69 | 91.6 → 89.3 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.99 | 3.67 | 18.93 | 99% | 0.87 | 2.95 | 5.68 | 115.6 → 110.8 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.04 | 3.74 | 25.16 | 99% | 0.88 | 2.98 | 5.81 | 117.5 → 113.4 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.13 | 3.93 | 108.52 | 99% | 1.88 | 3.01 | 5.46 | 120.3 → 116.3 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.86 | 3.35 | 14.58 | 100% | 0.71 | 2.77 | 5.82 | 80.7 → 78.3 | 1 | 1.2 / 29 |
| without squiggles | 368 | 1.05 | 3.60 | 20.10 | 99% | 0.88 | 2.88 | 5.98 | 120.1 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.92 | 3.53 | 18.13 | 99% | 0.86 | 2.81 | 4.47 | 120.1 → 116.0 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 2.98 | 11.85 | 100% | 0.59 | 2.47 | 4.74 | 44.0 → 43.1 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.412 |
| MeasuringLayoutable Panel | 0.065 |
| AttachingStyle - | 0.033 |
| ArrangingLayoutable ScrollingTextLayer | 0.031 |
| ArrangingLayoutable TextView | 0.024 |
| ArrangingLayoutable LineDrawing | 0.023 |
| FindingResource - | 0.016 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable WorkspaceView | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.97 | 3.55 | 5.37 | 100% | 0.87 | 2.89 | 5.66 | 116.4 → 117.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.99 | 8.32 | 29.70 | 93% | 2.08 | 7.14 | 10.22 | 607.8 → 633.0 | 15 | 3.5 / 30 |

