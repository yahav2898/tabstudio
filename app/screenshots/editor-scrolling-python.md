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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 3.19 | 14.13 | 100% | 0.46 | 2.84 | 4.43 | 96.3 → 103.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 3.56 | 18.59 | 99% | 1.01 | 2.93 | 5.28 | 133.1 → 130.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.23 | 5.68 | 21.87 | 99% | 2.25 | 3.32 | 5.91 | 180.7 → 166.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 3.59 | 17.92 | 99% | 1.78 | 3.05 | 5.35 | 118.1 → 119.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 3.32 | 17.34 | 99% | 0.76 | 2.75 | 5.20 | 93.1 → 91.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 3.36 | 18.97 | 99% | 0.76 | 2.85 | 5.14 | 118.0 → 113.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.71 | 3.55 | 16.94 | 99% | 0.76 | 2.91 | 5.12 | 120.1 → 116.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.93 | 4.85 | 17.39 | 99% | 1.92 | 3.23 | 5.42 | 123.2 → 119.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 3.24 | 13.00 | 99% | 0.69 | 2.83 | 5.01 | 82.2 → 80.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 3.55 | 14.91 | 99% | 0.76 | 2.97 | 5.05 | 122.8 → 119.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 3.33 | 22.59 | 99% | 0.76 | 2.88 | 4.63 | 122.9 → 119.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 3.10 | 13.90 | 100% | 1.56 | 2.67 | 4.19 | 43.8 → 43.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.331 |
| ArrangingLayoutable CachedLineNumberMargin | 0.067 |
| ArrangingLayoutable ScrollingTextLayer | 0.048 |
| AttachingStyle - | 0.035 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 3.53 | 15.94 | 99% | 0.72 | 2.90 | 4.92 | 118.7 → 119.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.37 | 7.10 | 33.29 | 95% | 0.90 | 6.10 | 8.97 | 624.5 → 646.7 | 14 | 3.5 / 31 |

