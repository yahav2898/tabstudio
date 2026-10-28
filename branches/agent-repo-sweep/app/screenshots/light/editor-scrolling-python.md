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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.46 | 1.64 | 12.95 | 100% | 0.40 | 1.34 | 3.81 | 96.1 → 103.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.79 | 2.24 | 16.20 | 99% | 0.85 | 1.62 | 4.19 | 132.6 → 130.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.80 | 2.51 | 16.72 | 99% | 1.57 | 1.61 | 4.50 | 179.9 → 165.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 3.77 | 15.35 | 99% | 1.71 | 2.44 | 4.23 | 117.7 → 119.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.42 | 1.61 | 10.18 | 99% | 0.56 | 1.18 | 3.95 | 92.6 → 90.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.48 | 1.71 | 14.73 | 99% | 0.57 | 1.26 | 3.80 | 117.6 → 113.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.49 | 1.70 | 15.02 | 99% | 0.55 | 1.31 | 3.77 | 119.7 → 116.2 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.50 | 1.70 | 13.31 | 99% | 0.58 | 1.30 | 3.97 | 122.6 → 119.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 1.79 | 10.99 | 99% | 0.44 | 1.35 | 3.79 | 81.8 → 79.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.61 | 3.78 | 14.84 | 99% | 1.56 | 2.38 | 4.29 | 122.4 → 119.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.49 | 1.67 | 14.39 | 99% | 0.54 | 1.31 | 3.40 | 122.5 → 118.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.28 | 1.34 | 8.99 | 100% | 0.47 | 1.06 | 3.38 | 43.3 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.184 |
| AttachingStyle - | 0.080 |
| MeasuringLayoutable MainWindow | 0.038 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.49 | 1.69 | 13.70 | 99% | 0.50 | 1.30 | 3.76 | 118.3 → 119.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.77 | 7.16 | 95.41 | 96% | 1.87 | 6.30 | 8.11 | 589.6 → 630.9 | 15 | 3.5 / 31 |

