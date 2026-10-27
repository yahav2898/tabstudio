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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.10 | 11.85 | 99% | 0.42 | 1.74 | 4.49 | 128.8 → 136.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 2.32 | 14.67 | 99% | 0.65 | 1.80 | 4.61 | 165.3 → 163.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.95 | 2.63 | 15.48 | 99% | 0.88 | 1.88 | 5.16 | 211.8 → 197.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 2.93 | 14.64 | 99% | 0.67 | 2.31 | 5.07 | 150.7 → 152.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 2.29 | 15.83 | 99% | 0.63 | 1.70 | 4.93 | 125.9 → 123.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.72 | 2.22 | 15.49 | 99% | 0.59 | 1.80 | 4.77 | 150.1 → 146.3 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.71 | 2.20 | 14.44 | 99% | 0.59 | 1.77 | 4.85 | 152.4 → 148.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.75 | 2.31 | 13.60 | 99% | 0.62 | 1.81 | 4.99 | 155.0 → 151.9 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 2.97 | 26.35 | 99% | 0.59 | 2.45 | 4.94 | 115.1 → 113.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.73 | 2.24 | 11.68 | 99% | 0.61 | 1.77 | 5.00 | 155.0 → 151.9 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 2.18 | 15.14 | 99% | 0.62 | 1.75 | 4.45 | 154.7 → 151.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 1.71 | 10.38 | 100% | 0.41 | 1.42 | 3.93 | 75.7 → 75.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.250 |
| ArrangingLayoutable CachedLineNumberMargin | 0.054 |
| ArrangingLayoutable LineDrawing | 0.051 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.88 | 3.78 | 19.59 | 99% | 2.25 | 2.57 | 5.21 | 151.1 → 152.4 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.23 | 5.03 | 28.07 | 96% | 0.75 | 4.09 | 5.39 | 558.8 → 580.6 | 13 | 3.5 / 31 |

