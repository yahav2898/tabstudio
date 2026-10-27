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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.68 | 2.12 | 8.98 | 100% | 0.43 | 1.79 | 4.34 | 130.8 → 138.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 2.35 | 14.06 | 99% | 0.64 | 1.82 | 4.77 | 167.0 → 164.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.26 | 4.72 | 17.95 | 99% | 1.09 | 4.15 | 5.83 | 214.1 → 200.4 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.88 | 2.49 | 14.55 | 99% | 0.65 | 1.95 | 5.00 | 152.8 → 153.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.70 | 2.36 | 13.32 | 99% | 0.65 | 1.80 | 5.19 | 127.3 → 125.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.93 | 2.73 | 17.82 | 99% | 0.78 | 2.11 | 5.54 | 152.3 → 148.7 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.99 | 3.33 | 17.19 | 99% | 0.81 | 2.58 | 5.38 | 154.1 → 152.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.96 | 3.07 | 14.77 | 99% | 0.72 | 2.26 | 5.36 | 158.0 → 153.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.64 | 2.08 | 10.57 | 99% | 0.46 | 1.73 | 4.77 | 116.6 → 115.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.92 | 2.64 | 16.52 | 99% | 0.70 | 2.01 | 5.36 | 156.9 → 154.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.93 | 3.08 | 14.88 | 99% | 0.80 | 2.28 | 4.72 | 157.0 → 154.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 2.57 | 85.11 | 99% | 0.58 | 1.96 | 4.35 | 79.0 → 77.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.272 |
| AttachingStyle - | 0.050 |
| ArrangingLayoutable Thumb | 0.042 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.89 | 2.53 | 18.01 | 99% | 0.69 | 1.96 | 5.00 | 153.0 → 153.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.26 | 6.40 | 21.61 | 95% | 0.76 | 4.67 | 5.29 | 555.8 → 573.6 | 13 | 3.5 / 31 |

