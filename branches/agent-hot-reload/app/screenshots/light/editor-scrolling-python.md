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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.95 | 3.15 | 12.51 | 99% | 0.60 | 2.64 | 4.91 | 123.3 → 130.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.21 | 3.68 | 77.23 | 99% | 0.89 | 2.85 | 5.50 | 160.7 → 158.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.40 | 3.68 | 16.74 | 99% | 1.11 | 2.80 | 5.62 | 207.9 → 192.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.11 | 3.45 | 13.70 | 99% | 0.79 | 2.77 | 5.14 | 145.8 → 147.1 | 5 | 1.1 / 4 |
| without line numbers | 368 | 0.97 | 3.30 | 11.68 | 99% | 0.84 | 2.63 | 5.15 | 120.0 → 118.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.06 | 3.39 | 16.36 | 99% | 0.80 | 2.78 | 5.18 | 145.0 → 141.2 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.11 | 3.45 | 13.79 | 99% | 0.81 | 2.81 | 5.52 | 147.3 → 144.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.21 | 3.87 | 14.85 | 99% | 0.89 | 2.99 | 5.68 | 151.0 → 147.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.02 | 3.29 | 12.54 | 99% | 0.66 | 2.66 | 5.00 | 108.7 → 107.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.19 | 3.49 | 15.63 | 99% | 0.85 | 2.80 | 5.49 | 150.4 → 147.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.16 | 4.91 | 13.95 | 99% | 1.11 | 2.86 | 5.19 | 149.8 → 146.9 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.77 | 2.87 | 3.26 | 100% | 0.59 | 2.37 | 4.35 | 69.8 → 69.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.348 |
| ArrangingLayoutable Track | 0.050 |
| ArrangingLayoutable TextView | 0.025 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.11 | 3.39 | 17.65 | 99% | 0.80 | 2.75 | 5.14 | 146.5 → 146.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.87 | 9.12 | 37.07 | 94% | 1.00 | 6.65 | 5.76 | 547.5 → 567.7 | 13 | 3.5 / 31 |

