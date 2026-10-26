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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.87 | 2.47 | 11.68 | 99% | 0.57 | 1.78 | 3.61 | 128.3 → 135.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.15 | 4.54 | 21.67 | 99% | 2.34 | 2.68 | 4.21 | 165.1 → 163.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.16 | 2.58 | 16.30 | 99% | 0.86 | 1.82 | 4.06 | 211.8 → 196.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.01 | 2.41 | 15.28 | 99% | 0.66 | 1.79 | 3.88 | 150.2 → 151.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.87 | 2.25 | 13.53 | 99% | 0.67 | 1.66 | 3.83 | 125.3 → 122.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.96 | 2.66 | 17.12 | 99% | 0.71 | 2.09 | 4.03 | 149.9 → 146.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.01 | 2.74 | 17.97 | 99% | 0.66 | 2.18 | 3.88 | 152.1 → 148.8 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.18 | 4.23 | 13.78 | 99% | 2.46 | 2.79 | 4.47 | 155.4 → 152.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.83 | 2.47 | 9.86 | 99% | 0.55 | 1.98 | 3.44 | 114.4 → 112.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.03 | 2.99 | 15.15 | 99% | 0.72 | 2.34 | 4.02 | 154.7 → 151.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.99 | 2.76 | 13.58 | 99% | 0.66 | 2.30 | 3.32 | 154.7 → 150.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.71 | 2.33 | 10.71 | 99% | 0.57 | 1.98 | 2.98 | 75.6 → 75.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.261 |
| ArrangingLayoutable CachedLineNumberMargin | 0.053 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.01 | 2.58 | 11.86 | 99% | 0.71 | 1.91 | 3.95 | 150.5 → 151.9 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.02 | 7.63 | 92.09 | 95% | 1.12 | 5.90 | 4.29 | 557.1 → 579.0 | 12 | 3.5 / 31 |

