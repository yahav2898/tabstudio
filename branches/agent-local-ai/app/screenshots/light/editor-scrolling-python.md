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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 1.97 | 10.53 | 99% | 0.43 | 1.67 | 4.37 | 122.7 → 130.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.91 | 3.49 | 18.46 | 99% | 0.77 | 2.70 | 5.14 | 159.1 → 158.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 2.52 | 16.65 | 99% | 0.94 | 1.83 | 4.87 | 206.2 → 191.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 2.24 | 13.67 | 99% | 0.60 | 1.82 | 4.62 | 144.5 → 145.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 2.00 | 12.24 | 99% | 0.60 | 1.60 | 4.59 | 118.9 → 117.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.86 | 3.92 | 78.86 | 99% | 0.86 | 2.58 | 5.29 | 144.4 → 141.1 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.73 | 2.14 | 13.41 | 99% | 0.61 | 1.72 | 4.58 | 146.1 → 142.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 2.19 | 16.02 | 99% | 0.62 | 1.72 | 4.68 | 149.1 → 145.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.64 | 1.94 | 10.37 | 99% | 0.46 | 1.60 | 4.32 | 108.2 → 106.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.88 | 3.30 | 14.44 | 99% | 0.76 | 2.56 | 4.99 | 149.2 → 146.2 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 2.25 | 13.19 | 99% | 0.61 | 1.77 | 4.30 | 148.7 → 145.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 1.73 | 11.43 | 100% | 0.42 | 1.41 | 3.87 | 68.8 → 68.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.203 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.87 | 3.16 | 16.15 | 99% | 0.71 | 2.26 | 4.85 | 144.9 → 146.5 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.13 | 4.96 | 23.02 | 96% | 0.78 | 4.43 | 5.09 | 549.8 → 569.7 | 13 | 3.5 / 31 |

