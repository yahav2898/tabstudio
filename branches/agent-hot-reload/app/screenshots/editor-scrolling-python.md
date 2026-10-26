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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.68 | 1.95 | 11.01 | 100% | 0.46 | 1.52 | 3.84 | 122.2 → 130.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.84 | 2.22 | 12.95 | 99% | 0.67 | 1.65 | 4.04 | 159.6 → 157.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.96 | 3.40 | 14.09 | 99% | 0.95 | 2.45 | 4.43 | 207.0 → 192.3 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 2.49 | 11.30 | 99% | 0.65 | 1.82 | 4.29 | 145.0 → 145.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.67 | 2.00 | 12.34 | 99% | 0.57 | 1.55 | 3.85 | 118.6 → 117.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.83 | 2.23 | 12.76 | 99% | 0.59 | 1.68 | 4.02 | 144.3 → 140.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.03 | 2.54 | 11.81 | 99% | 0.74 | 1.83 | 4.39 | 146.5 → 143.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.10 | 4.48 | 85.21 | 99% | 0.84 | 2.86 | 4.88 | 149.7 → 146.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.94 | 2.41 | 11.10 | 99% | 0.60 | 1.85 | 4.20 | 108.6 → 106.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.06 | 2.64 | 18.11 | 99% | 0.77 | 1.98 | 4.63 | 149.2 → 146.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.82 | 2.25 | 13.76 | 99% | 0.64 | 1.74 | 4.11 | 149.3 → 145.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.36 | 8.30 | 100% | 0.37 | 1.09 | 3.05 | 68.4 → 68.2 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.263 |
| AttachingStyle - | 0.081 |
| MeasuringLayoutable CachedLineNumberMargin | 0.038 |
| ArrangingLayoutable ScrollingTextLayer | 0.028 |
| ArrangingLayoutable TextView | 0.026 |
| ArrangingLayoutable CachedLineNumberMargin | 0.019 |
| ArrangingLayoutable LineDrawing | 0.019 |
| ArrangingLayoutable NumberDrawing | 0.015 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable Track | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 2.30 | 14.52 | 99% | 0.57 | 1.63 | 3.96 | 145.0 → 145.5 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.80 | 4.51 | 19.92 | 96% | 0.64 | 3.68 | 4.17 | 551.6 → 579.0 | 12 | 3.5 / 31 |

