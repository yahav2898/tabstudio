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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 2.22 | 12.34 | 99% | 0.44 | 1.89 | 4.37 | 128.8 → 136.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 2.43 | 15.12 | 99% | 0.65 | 1.87 | 4.64 | 165.2 → 163.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.01 | 2.75 | 16.31 | 99% | 0.94 | 2.00 | 5.30 | 212.0 → 197.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.02 | 4.94 | 16.60 | 99% | 1.12 | 3.13 | 5.43 | 151.5 → 153.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 2.35 | 13.45 | 99% | 0.62 | 1.90 | 4.95 | 125.6 → 123.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 2.88 | 17.78 | 99% | 0.65 | 2.49 | 4.97 | 150.1 → 146.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 3.06 | 14.93 | 99% | 0.64 | 2.63 | 4.91 | 152.4 → 149.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.81 | 2.98 | 13.88 | 99% | 0.64 | 2.57 | 4.89 | 155.1 → 152.2 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.76 | 4.37 | 15.11 | 100% | 0.59 | 2.83 | 4.96 | 115.4 → 113.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 3.00 | 15.61 | 99% | 0.65 | 2.57 | 4.84 | 154.9 → 152.1 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.74 | 2.92 | 15.75 | 99% | 0.63 | 2.51 | 4.18 | 154.8 → 151.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.67 | 10.85 | 99% | 0.47 | 2.32 | 4.36 | 76.6 → 76.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.247 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.92 | 4.78 | 21.04 | 99% | 2.51 | 3.48 | 5.27 | 151.7 → 152.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.12 | 5.76 | 24.99 | 96% | 0.75 | 4.86 | 5.18 | 555.0 → 576.0 | 13 | 3.5 / 31 |

