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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.84 | 2.52 | 14.73 | 99% | 0.54 | 2.08 | 4.81 | 128.8 → 136.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.02 | 2.82 | 17.33 | 99% | 0.76 | 2.20 | 5.09 | 165.7 → 163.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.18 | 3.77 | 18.84 | 99% | 1.15 | 2.58 | 5.70 | 212.5 → 198.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.97 | 3.77 | 17.26 | 99% | 0.76 | 2.63 | 5.33 | 151.7 → 152.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.77 | 2.54 | 17.25 | 99% | 0.67 | 1.91 | 5.49 | 125.4 → 124.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.84 | 2.52 | 17.37 | 99% | 0.72 | 1.99 | 5.12 | 149.9 → 146.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 2.73 | 17.40 | 99% | 0.71 | 2.05 | 5.04 | 152.4 → 149.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.12 | 4.10 | 17.77 | 99% | 0.99 | 2.62 | 5.45 | 155.8 → 153.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 2.48 | 12.44 | 99% | 0.55 | 1.98 | 4.52 | 114.8 → 113.2 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.88 | 2.63 | 19.14 | 99% | 0.72 | 2.05 | 4.99 | 155.0 → 151.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.89 | 2.47 | 17.52 | 99% | 0.67 | 2.00 | 4.49 | 154.8 → 151.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.07 | 12.42 | 99% | 0.49 | 1.66 | 4.17 | 76.0 → 75.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.297 |
| ArrangingLayoutable CachedLineNumberMargin | 0.032 |
| ArrangingLayoutable TextView | 0.030 |
| ArrangingLayoutable NumberDrawing | 0.027 |
| AttachingStyle - | 0.024 |
| FindingResource - | 0.012 |
| ArrangingLayoutable Panel | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.04 | 3.31 | 15.57 | 99% | 0.87 | 2.37 | 5.67 | 151.8 → 152.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.36 | 5.91 | 25.94 | 96% | 0.77 | 5.31 | 5.40 | 557.2 → 575.3 | 12 | 3.5 / 31 |

