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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 2.99 | 15.97 | 99% | 0.46 | 2.62 | 4.30 | 93.2 → 100.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 3.21 | 16.45 | 99% | 0.95 | 2.67 | 4.41 | 129.3 → 129.7 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 4.19 | 17.49 | 99% | 1.69 | 2.84 | 5.30 | 176.1 → 162.2 | 5 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 3.47 | 13.00 | 99% | 1.61 | 2.79 | 5.26 | 114.9 → 115.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 3.00 | 15.48 | 99% | 0.73 | 2.50 | 4.99 | 91.1 → 88.9 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.67 | 3.13 | 21.10 | 99% | 0.74 | 2.62 | 4.33 | 115.0 → 110.4 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.69 | 3.12 | 15.98 | 99% | 0.65 | 2.57 | 4.28 | 117.0 → 113.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.85 | 5.02 | 96.43 | 99% | 1.68 | 2.83 | 5.56 | 119.9 → 115.9 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.60 | 2.90 | 11.90 | 99% | 0.47 | 2.51 | 4.17 | 80.2 → 77.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.72 | 3.23 | 16.24 | 99% | 0.67 | 2.62 | 4.62 | 119.5 → 115.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.69 | 3.10 | 15.43 | 100% | 0.73 | 2.64 | 4.77 | 119.7 → 115.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 2.76 | 4.15 | 100% | 0.52 | 2.41 | 4.09 | 43.6 → 42.7 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.474 |
| AttachingStyle - | 0.052 |
| ArrangingLayoutable LineDrawing | 0.043 |
| ArrangingLayoutable CachedLineNumberMargin | 0.032 |
| FindingResource - | 0.026 |
| ArrangingLayoutable TextView | 0.020 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable ScrollContentPresenter | 0.014 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 3.17 | 35.92 | 99% | 0.69 | 2.67 | 4.35 | 116.0 → 117.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.30 | 7.89 | 29.08 | 95% | 1.66 | 7.19 | 9.15 | 598.0 → 625.2 | 14 | 3.5 / 30 |

