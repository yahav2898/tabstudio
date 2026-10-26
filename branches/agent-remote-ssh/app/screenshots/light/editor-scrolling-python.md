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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.54 | 1.93 | 8.86 | 99% | 0.36 | 1.67 | 2.78 | 123.8 → 131.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.46 | 1.49 | 9.54 | 99% | 0.40 | 1.15 | 2.77 | 159.9 → 157.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.62 | 1.84 | 26.28 | 99% | 0.71 | 1.30 | 3.17 | 207.4 → 192.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.44 | 1.45 | 10.46 | 99% | 0.39 | 1.14 | 2.90 | 144.8 → 146.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.43 | 1.52 | 11.57 | 99% | 0.46 | 1.14 | 2.92 | 119.2 → 118.0 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 3.35 | 11.34 | 99% | 0.68 | 2.40 | 3.23 | 145.2 → 141.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.50 | 1.59 | 11.47 | 99% | 0.48 | 1.23 | 3.13 | 146.8 → 143.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.54 | 1.59 | 12.89 | 99% | 0.47 | 1.24 | 3.11 | 150.1 → 146.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.50 | 1.50 | 6.97 | 100% | 0.37 | 1.21 | 2.74 | 109.0 → 107.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.55 | 1.59 | 11.32 | 99% | 0.50 | 1.24 | 3.01 | 149.5 → 146.6 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.40 | 1.46 | 11.67 | 99% | 0.44 | 1.15 | 2.77 | 149.1 → 146.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 2.28 | 8.32 | 100% | 0.40 | 1.16 | 2.56 | 70.1 → 69.4 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.271 |
| AttachingStyle - | 0.064 |
| ArrangingLayoutable CachedLineNumberMargin | 0.018 |
| ArrangingLayoutable ScrollingTextLayer | 0.017 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.51 | 1.45 | 11.55 | 99% | 0.44 | 1.18 | 2.95 | 145.5 → 146.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.15 | 2.87 | 22.85 | 96% | 0.43 | 2.55 | 2.98 | 547.1 → 567.7 | 13 | 3.5 / 31 |

