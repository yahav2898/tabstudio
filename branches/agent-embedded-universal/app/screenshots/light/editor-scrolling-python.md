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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 2.17 | 12.83 | 99% | 0.46 | 1.86 | 4.55 | 128.8 → 136.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.40 | 14.72 | 99% | 0.65 | 1.92 | 4.96 | 164.9 → 162.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 2.66 | 15.45 | 99% | 0.96 | 1.93 | 5.21 | 211.2 → 196.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.90 | 4.39 | 18.16 | 99% | 0.92 | 3.95 | 5.33 | 150.8 → 152.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 2.16 | 15.04 | 99% | 0.60 | 1.72 | 4.97 | 125.0 → 123.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 2.28 | 14.80 | 99% | 0.61 | 1.81 | 4.47 | 149.7 → 145.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.73 | 2.34 | 14.63 | 99% | 0.61 | 1.86 | 4.70 | 151.9 → 148.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.78 | 2.67 | 16.13 | 99% | 0.65 | 2.05 | 4.80 | 154.6 → 151.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 2.94 | 14.91 | 99% | 0.57 | 2.42 | 4.84 | 114.9 → 112.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.73 | 2.34 | 11.86 | 99% | 0.62 | 1.87 | 4.71 | 154.4 → 151.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 2.29 | 24.32 | 99% | 0.62 | 1.83 | 4.34 | 154.4 → 151.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.84 | 9.18 | 99% | 0.43 | 1.53 | 4.06 | 75.5 → 74.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.198 |
| ArrangingLayoutable NumberDrawing | 0.045 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.96 | 4.14 | 14.74 | 99% | 0.97 | 2.59 | 5.49 | 151.1 → 152.6 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.12 | 5.38 | 25.04 | 96% | 0.74 | 4.40 | 5.12 | 548.1 → 569.7 | 12 | 3.5 / 31 |

