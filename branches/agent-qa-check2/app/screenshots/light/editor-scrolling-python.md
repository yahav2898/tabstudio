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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 2.89 | 15.99 | 99% | 0.41 | 2.59 | 4.21 | 96.1 → 103.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 3.47 | 15.90 | 99% | 1.19 | 2.83 | 4.93 | 132.6 → 130.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.86 | 4.32 | 18.60 | 99% | 2.62 | 2.92 | 5.89 | 180.0 → 165.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.63 | 3.11 | 16.21 | 99% | 0.70 | 2.66 | 4.87 | 117.7 → 119.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.50 | 2.91 | 14.76 | 99% | 0.65 | 2.46 | 4.80 | 92.6 → 90.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 3.27 | 15.29 | 99% | 1.35 | 2.67 | 4.62 | 117.6 → 113.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 3.25 | 19.78 | 99% | 1.20 | 2.74 | 4.97 | 119.8 → 116.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.65 | 3.00 | 13.75 | 100% | 0.67 | 2.61 | 4.62 | 122.6 → 119.1 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.54 | 2.90 | 11.96 | 99% | 0.45 | 2.54 | 4.63 | 81.8 → 79.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 3.64 | 16.36 | 99% | 1.34 | 2.75 | 4.87 | 122.4 → 119.1 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 3.48 | 17.61 | 99% | 1.16 | 2.77 | 4.73 | 122.6 → 118.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.59 | 7.47 | 100% | 0.43 | 2.32 | 3.99 | 43.4 → 42.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.260 |
| ArrangingLayoutable CachedLineNumberMargin | 0.069 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
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
| this editor | 368 | 0.66 | 3.05 | 14.47 | 99% | 0.72 | 2.63 | 4.75 | 118.3 → 119.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.33 | 11.55 | 87.32 | 93% | 1.56 | 9.22 | 9.13 | 620.3 → 636.3 | 15 | 3.5 / 31 |

