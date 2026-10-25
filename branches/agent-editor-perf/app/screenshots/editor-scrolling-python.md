# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 827 × 614 dip.
- 0 classified spans, 0 diagnostics, 683 folds, 0 breakpoints, 0 Live Unit Testing markers.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.48 | 1.78 | 5.04 | 100% | 0.42 | 1.43 | 4.41 | 83.9 → 95.4 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.65 | 2.01 | 6.60 | 100% | 0.61 | 1.47 | 4.35 | 122.8 → 120.5 | 7 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.75 | 2.34 | 10.95 | 99% | 0.87 | 1.53 | 4.56 | 170.4 → 154.2 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.57 | 1.93 | 8.62 | 99% | 0.54 | 1.44 | 4.47 | 106.2 → 106.4 | 6 | 1.1 / 4 |
| without line numbers | 368 | 0.41 | 1.74 | 7.04 | 100% | 0.61 | 1.28 | 4.39 | 81.1 → 77.9 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.52 | 2.31 | 10.46 | 99% | 0.64 | 1.50 | 4.42 | 106.1 → 100.8 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.58 | 1.90 | 10.98 | 99% | 0.52 | 1.47 | 4.44 | 108.3 → 103.6 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.57 | 1.93 | 5.99 | 100% | 0.58 | 1.45 | 4.42 | 111.1 → 106.3 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.43 | 1.76 | 5.81 | 100% | 0.48 | 1.36 | 4.15 | 70.5 → 67.2 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.58 | 1.96 | 10.52 | 99% | 0.56 | 1.45 | 4.44 | 111.1 → 106.4 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.53 | 1.93 | 9.35 | 100% | 0.65 | 1.44 | 3.98 | 110.9 → 106.2 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.27 | 1.38 | 6.99 | 100% | 0.35 | 1.10 | 3.55 | 32.3 → 29.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 43.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.240 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| ArrangingLayoutable TextView | 0.012 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| FindingResource - | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.58 | 1.93 | 9.68 | 99% | 0.56 | 1.49 | 4.38 | 106.9 → 106.3 | 6 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.24 | 9.99 | 24.86 | 94% | 0.84 | 5.68 | 8.18 | 580.3 → 605.6 | 17 | 3.5 / 31 |

