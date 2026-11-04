# Scrolling a long Python file

- 2211 lines; 28 visible lines; viewport 779 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.52 | 2.67 | 14.99 | 99% | 0.39 | 2.27 | 3.69 | 93.4 → 100.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 3.14 | 18.95 | 99% | 0.77 | 2.45 | 4.30 | 129.7 → 130.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.91 | 3.56 | 22.20 | 99% | 1.93 | 2.68 | 4.59 | 176.9 → 162.8 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.62 | 2.66 | 15.38 | 99% | 0.62 | 2.26 | 3.82 | 115.2 → 116.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 2.85 | 16.92 | 99% | 0.97 | 2.24 | 4.40 | 91.5 → 89.3 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.78 | 4.34 | 108.52 | 99% | 2.22 | 2.53 | 4.48 | 115.5 → 110.8 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.58 | 2.60 | 18.80 | 99% | 0.64 | 2.20 | 3.85 | 117.4 → 113.4 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.57 | 2.73 | 19.71 | 99% | 0.85 | 2.27 | 3.82 | 120.2 → 116.2 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.46 | 2.47 | 15.07 | 99% | 0.42 | 2.13 | 3.95 | 80.7 → 78.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.63 | 3.00 | 16.61 | 99% | 0.72 | 2.38 | 4.34 | 120.0 → 116.2 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.54 | 2.60 | 16.92 | 99% | 0.53 | 2.21 | 3.45 | 120.1 → 116.0 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.44 | 10.13 | 100% | 0.80 | 2.08 | 3.49 | 44.0 → 43.1 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.414 |
| AttachingStyle - | 0.048 |
| ArrangingLayoutable NumberDrawing | 0.027 |
| FindingResource - | 0.025 |
| ArrangingLayoutable LineDrawing | 0.021 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable TextView | 0.020 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| MeasuringLayoutable NumberDrawing | 0.011 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 2.75 | 41.72 | 99% | 0.63 | 2.33 | 4.10 | 116.4 → 117.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.84 | 5.99 | 26.11 | 95% | 0.66 | 4.80 | 7.01 | 582.7 → 607.7 | 13 | 3.5 / 30 |

