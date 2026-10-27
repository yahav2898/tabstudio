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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.92 | 2.68 | 14.90 | 99% | 0.58 | 2.15 | 4.86 | 131.6 → 138.7 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.12 | 2.90 | 17.13 | 99% | 0.78 | 2.23 | 5.13 | 168.3 → 165.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.20 | 3.11 | 18.82 | 99% | 1.05 | 2.26 | 5.73 | 215.4 → 200.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.22 | 4.80 | 22.31 | 99% | 1.07 | 3.95 | 5.85 | 154.2 → 155.8 | 5 | 1.1 / 4 |
| without line numbers | 368 | 0.90 | 2.64 | 12.50 | 99% | 0.74 | 2.07 | 5.53 | 128.0 → 126.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.01 | 2.85 | 18.98 | 99% | 0.82 | 2.21 | 5.53 | 152.7 → 149.1 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.04 | 2.90 | 17.67 | 99% | 0.77 | 2.23 | 5.41 | 155.1 → 151.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.05 | 3.02 | 17.47 | 99% | 0.81 | 2.36 | 5.40 | 158.7 → 154.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.00 | 4.50 | 15.10 | 99% | 0.85 | 2.58 | 5.32 | 117.9 → 116.7 | 4 | 1.2 / 30 |
| without squiggles | 368 | 1.04 | 2.94 | 15.73 | 99% | 0.79 | 2.31 | 5.54 | 157.9 → 154.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.11 | 3.04 | 29.51 | 99% | 0.86 | 2.33 | 4.82 | 157.7 → 154.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.65 | 2.31 | 10.43 | 100% | 0.57 | 1.82 | 4.49 | 79.2 → 78.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.269 |
| ArrangingLayoutable TextView | 0.023 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.06 | 3.27 | 18.82 | 99% | 0.81 | 2.50 | 5.64 | 153.9 → 155.3 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.80 | 7.14 | 30.22 | 96% | 0.93 | 5.90 | 6.04 | 559.8 → 582.6 | 13 | 3.5 / 31 |

