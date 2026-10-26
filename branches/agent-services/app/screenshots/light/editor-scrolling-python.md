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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.91 | 2.29 | 16.69 | 99% | 0.57 | 1.74 | 4.35 | 124.9 → 132.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.07 | 2.60 | 16.89 | 99% | 0.75 | 1.90 | 4.48 | 161.7 → 159.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.15 | 2.63 | 16.38 | 99% | 0.92 | 1.81 | 4.48 | 208.5 → 193.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 3.51 | 18.96 | 99% | 0.78 | 2.44 | 4.54 | 146.9 → 148.4 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.87 | 2.76 | 11.16 | 99% | 0.72 | 2.28 | 4.27 | 121.4 → 119.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.83 | 2.23 | 18.92 | 99% | 0.64 | 1.67 | 4.26 | 145.8 → 142.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.97 | 2.44 | 16.07 | 99% | 0.72 | 1.82 | 4.47 | 148.2 → 145.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.99 | 2.38 | 13.58 | 99% | 0.71 | 1.81 | 4.46 | 151.2 → 148.0 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.92 | 2.97 | 76.58 | 99% | 0.62 | 2.41 | 4.25 | 110.4 → 108.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.94 | 2.27 | 14.72 | 99% | 0.70 | 1.71 | 4.33 | 151.0 → 148.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.95 | 2.32 | 15.51 | 99% | 0.70 | 1.72 | 4.01 | 151.1 → 147.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.60 | 1.81 | 11.41 | 99% | 0.53 | 1.40 | 3.52 | 71.2 → 70.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.250 |
| AttachingStyle - | 0.057 |
| ArrangingLayoutable TextView | 0.021 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.10 | 4.31 | 78.45 | 99% | 0.85 | 3.69 | 4.64 | 147.5 → 148.6 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.25 | 5.12 | 24.59 | 96% | 0.86 | 3.70 | 4.83 | 553.5 → 571.8 | 12 | 3.5 / 31 |

