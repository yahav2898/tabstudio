# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.53 | 2.82 | 15.56 | 99% | 0.39 | 2.54 | 4.08 | 96.3 → 103.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.72 | 3.08 | 17.46 | 99% | 1.22 | 2.56 | 4.92 | 133.1 → 130.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 5.39 | 18.81 | 99% | 2.84 | 2.95 | 5.56 | 180.7 → 165.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.63 | 2.97 | 16.52 | 99% | 0.60 | 2.55 | 4.76 | 118.0 → 119.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.50 | 2.81 | 14.94 | 99% | 0.75 | 2.40 | 4.89 | 93.0 → 91.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 2.92 | 17.30 | 99% | 0.64 | 2.52 | 4.80 | 118.0 → 113.8 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 4.65 | 17.95 | 99% | 2.41 | 2.85 | 5.31 | 120.1 → 116.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.66 | 3.01 | 17.91 | 99% | 0.71 | 2.60 | 5.10 | 123.1 → 119.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.51 | 2.77 | 13.51 | 99% | 0.47 | 2.45 | 4.69 | 82.2 → 80.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.62 | 2.99 | 15.69 | 99% | 0.71 | 2.53 | 4.73 | 122.7 → 119.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.82 | 5.04 | 82.05 | 99% | 2.23 | 2.88 | 4.77 | 122.9 → 119.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.32 | 2.49 | 11.58 | 100% | 0.42 | 2.24 | 3.95 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.245 |
| AttachingStyle - | 0.053 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.64 | 2.97 | 18.37 | 99% | 0.69 | 2.56 | 4.60 | 118.7 → 119.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.34 | 12.09 | 31.10 | 90% | 2.45 | 8.09 | 9.35 | 622.7 → 649.2 | 16 | 3.5 / 31 |

