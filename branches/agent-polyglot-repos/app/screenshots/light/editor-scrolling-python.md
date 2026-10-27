# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 2.00 | 53.95 | 100% | 0.45 | 1.67 | 4.72 | 128.6 → 136.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 2.25 | 3.22 | 100% | 0.68 | 1.67 | 5.33 | 164.7 → 162.6 | 0 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.15 | 4.25 | 4.93 | 100% | 1.41 | 3.66 | 5.88 | 211.8 → 197.5 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 2.51 | 27.06 | 100% | 0.68 | 1.75 | 5.39 | 150.4 → 151.6 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 1.96 | 2.56 | 100% | 0.61 | 1.51 | 5.39 | 125.0 → 123.4 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.76 | 2.14 | 43.03 | 100% | 0.67 | 1.65 | 5.40 | 149.9 → 145.9 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.80 | 2.20 | 47.60 | 100% | 0.66 | 1.73 | 5.24 | 151.7 → 148.6 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.90 | 4.28 | 24.65 | 100% | 2.54 | 2.55 | 5.61 | 155.0 → 152.4 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 1.90 | 2.31 | 100% | 0.50 | 1.54 | 4.34 | 114.4 → 112.7 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.76 | 2.13 | 3.17 | 100% | 0.64 | 1.65 | 5.20 | 154.3 → 151.7 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 2.05 | 3.22 | 100% | 0.65 | 1.64 | 4.86 | 154.3 → 151.2 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 1.69 | 46.29 | 100% | 0.47 | 1.33 | 4.53 | 75.5 → 75.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.298 |
| AttachingStyle - | 0.026 |
| ArrangingLayoutable TextView | 0.023 |
| ArrangingLayoutable LineDrawing | 0.021 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| MeasuringLayoutable DockPanel | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.82 | 2.60 | 90.37 | 99% | 0.70 | 1.84 | 5.45 | 151.0 → 151.5 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.18 | 4.31 | 69.03 | 98% | 0.72 | 3.61 | 5.76 | 549.2 → 570.0 | 3 | 3.5 / 31 |

