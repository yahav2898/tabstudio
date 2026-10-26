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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.70 | 3.91 | 15.18 | 99% | 0.53 | 3.58 | 4.09 | 126.4 → 134.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 2.17 | 16.88 | 99% | 0.59 | 1.67 | 4.04 | 162.2 → 160.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.70 | 2.12 | 14.93 | 99% | 0.74 | 1.52 | 4.11 | 208.8 → 194.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.59 | 1.87 | 15.65 | 99% | 0.52 | 1.53 | 4.05 | 147.2 → 148.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.47 | 1.75 | 12.31 | 99% | 0.50 | 1.36 | 3.91 | 122.1 → 120.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.71 | 2.18 | 15.91 | 99% | 0.58 | 1.67 | 4.02 | 147.3 → 143.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 3.26 | 16.92 | 99% | 0.73 | 2.56 | 4.39 | 149.9 → 146.6 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 3.00 | 15.12 | 99% | 0.60 | 2.53 | 4.13 | 152.8 → 148.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 1.83 | 11.68 | 99% | 0.43 | 1.50 | 3.86 | 111.5 → 109.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.73 | 2.14 | 13.49 | 99% | 0.55 | 1.69 | 4.09 | 152.0 → 149.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.68 | 1.96 | 14.41 | 99% | 0.56 | 1.59 | 3.63 | 151.5 → 148.4 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 1.59 | 9.60 | 100% | 0.38 | 1.31 | 3.22 | 73.0 → 72.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.251 |
| AttachingStyle - | 0.056 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.93 | 3.44 | 18.06 | 99% | 0.86 | 2.55 | 4.41 | 148.7 → 149.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.91 | 4.85 | 41.88 | 96% | 0.70 | 3.78 | 4.77 | 557.3 → 577.0 | 13 | 3.5 / 31 |

