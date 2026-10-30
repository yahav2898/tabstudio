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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.83 | 4.61 | 16.15 | 100% | 1.37 | 2.90 | 5.20 | 95.3 → 102.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.00 | 3.57 | 15.86 | 99% | 0.94 | 2.94 | 5.13 | 131.3 → 129.0 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.10 | 3.75 | 16.92 | 99% | 1.57 | 2.89 | 5.49 | 178.0 → 163.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.97 | 3.72 | 18.85 | 99% | 1.50 | 2.99 | 5.26 | 116.6 → 118.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.71 | 3.35 | 14.06 | 99% | 0.79 | 2.73 | 5.29 | 92.0 → 90.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 3.45 | 14.44 | 99% | 0.78 | 2.84 | 5.17 | 116.5 → 112.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.83 | 3.39 | 17.89 | 100% | 0.78 | 2.77 | 5.24 | 118.6 → 115.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.94 | 4.54 | 28.13 | 99% | 1.86 | 3.00 | 5.23 | 121.5 → 118.0 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 3.14 | 12.32 | 99% | 0.57 | 2.71 | 4.75 | 81.3 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.85 | 3.61 | 15.41 | 99% | 1.12 | 2.84 | 5.27 | 121.2 → 118.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.74 | 3.32 | 16.24 | 99% | 0.71 | 2.78 | 4.54 | 121.3 → 117.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 3.05 | 7.75 | 100% | 1.28 | 2.53 | 4.35 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.258 |
| ArrangingLayoutable TextView | 0.019 |
| AttachingStyle - | 0.016 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.81 | 3.33 | 16.78 | 99% | 0.73 | 2.78 | 5.11 | 117.2 → 118.0 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.40 | 11.72 | 33.76 | 92% | 2.65 | 7.99 | 9.77 | 610.1 → 638.5 | 15 | 3.5 / 31 |

