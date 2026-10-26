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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 2.03 | 12.16 | 99% | 0.43 | 1.67 | 3.77 | 128.5 → 136.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 2.23 | 15.87 | 99% | 0.61 | 1.73 | 4.12 | 164.9 → 162.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 2.44 | 15.99 | 99% | 0.79 | 1.74 | 4.40 | 211.6 → 196.9 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.92 | 4.20 | 16.25 | 99% | 0.71 | 2.68 | 4.37 | 150.7 → 151.9 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 1.99 | 11.59 | 99% | 0.56 | 1.57 | 4.17 | 125.0 → 123.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.75 | 2.14 | 15.13 | 99% | 0.58 | 1.66 | 4.06 | 149.9 → 145.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.78 | 2.16 | 13.99 | 100% | 0.57 | 1.68 | 4.21 | 151.9 → 148.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.79 | 2.26 | 14.09 | 99% | 0.59 | 1.78 | 4.07 | 155.0 → 151.8 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.80 | 2.98 | 72.98 | 99% | 0.55 | 2.48 | 4.22 | 114.6 → 112.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.81 | 2.28 | 14.96 | 99% | 0.62 | 1.75 | 4.10 | 154.4 → 151.8 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.78 | 2.18 | 13.78 | 99% | 0.60 | 1.72 | 3.65 | 154.1 → 151.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 1.84 | 10.67 | 100% | 0.45 | 1.47 | 3.47 | 75.6 → 75.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.210 |
| MeasuringLayoutable ContentPresenter | 0.040 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.012 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.83 | 2.83 | 16.32 | 99% | 0.67 | 1.94 | 4.33 | 150.8 → 151.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.12 | 8.39 | 25.38 | 95% | 0.77 | 5.30 | 5.16 | 558.6 → 578.3 | 13 | 3.5 / 31 |

