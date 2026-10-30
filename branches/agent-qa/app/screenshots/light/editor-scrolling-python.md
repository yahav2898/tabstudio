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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.86 | 3.67 | 18.90 | 99% | 0.58 | 3.14 | 4.76 | 96.2 → 103.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.05 | 3.96 | 18.89 | 99% | 1.89 | 3.22 | 5.52 | 132.7 → 130.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.29 | 5.42 | 20.07 | 98% | 2.70 | 3.28 | 5.97 | 180.0 → 165.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.90 | 3.71 | 20.16 | 99% | 0.87 | 3.05 | 5.35 | 117.7 → 119.1 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.75 | 3.49 | 4.99 | 100% | 0.84 | 2.90 | 5.41 | 92.7 → 90.7 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.88 | 3.69 | 19.67 | 99% | 0.95 | 3.04 | 5.29 | 117.6 → 113.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.12 | 5.26 | 21.49 | 99% | 2.05 | 3.33 | 5.89 | 119.8 → 116.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.87 | 3.72 | 21.50 | 99% | 0.78 | 3.10 | 5.27 | 122.7 → 119.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.76 | 3.47 | 14.35 | 100% | 0.59 | 2.98 | 5.08 | 81.8 → 79.8 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.91 | 3.72 | 19.11 | 99% | 1.17 | 3.11 | 5.25 | 122.4 → 119.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.03 | 5.42 | 93.96 | 98% | 2.36 | 3.24 | 4.94 | 122.5 → 119.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 3.28 | 13.93 | 100% | 0.65 | 2.77 | 4.40 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.296 |
| ArrangingLayoutable LineDrawing | 0.063 |
| ArrangingLayoutable TextView | 0.063 |
| FindingResource - | 0.015 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.94 | 3.69 | 19.89 | 99% | 0.83 | 3.08 | 4.95 | 118.5 → 119.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.97 | 11.22 | 36.10 | 87% | 2.95 | 8.56 | 10.10 | 619.1 → 640.9 | 15 | 3.5 / 31 |

