# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.99 | 3.78 | 14.41 | 99% | 0.61 | 3.20 | 5.40 | 133.5 → 140.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.24 | 4.29 | 18.06 | 99% | 0.90 | 3.33 | 5.75 | 169.6 → 167.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.22 | 4.09 | 19.31 | 99% | 0.98 | 3.21 | 5.75 | 216.2 → 201.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.04 | 3.81 | 15.41 | 99% | 0.74 | 3.19 | 5.71 | 154.9 → 156.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.92 | 3.82 | 15.78 | 99% | 0.75 | 3.15 | 5.71 | 129.6 → 128.5 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.13 | 5.45 | 94.16 | 99% | 0.86 | 3.67 | 5.89 | 154.8 → 150.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.09 | 3.97 | 16.81 | 99% | 0.76 | 3.27 | 5.56 | 156.8 → 153.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.04 | 3.79 | 15.48 | 99% | 0.77 | 3.15 | 5.53 | 159.3 → 156.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.92 | 3.84 | 13.52 | 99% | 0.64 | 3.25 | 5.19 | 118.3 → 117.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.12 | 4.03 | 84.95 | 99% | 0.76 | 3.41 | 5.66 | 159.6 → 156.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.97 | 3.75 | 17.71 | 99% | 0.71 | 3.17 | 4.78 | 158.5 → 155.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.66 | 3.34 | 13.11 | 99% | 0.56 | 2.81 | 4.82 | 79.3 → 78.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.278 |
| RaisingRoutedEvent ScrollViewer | 0.046 |
| ArrangingLayoutable CachedLineNumberMargin | 0.034 |
| AttachingStyle - | 0.027 |
| ArrangingLayoutable TextView | 0.024 |
| ArrangingLayoutable LineDrawing | 0.023 |
| FindingResource - | 0.015 |
| ArrangingLayoutable ScrollingTextLayer | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.16 | 4.11 | 15.83 | 99% | 0.78 | 3.40 | 5.83 | 155.7 → 156.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.52 | 6.56 | 33.54 | 96% | 0.86 | 5.91 | 5.65 | 556.7 → 576.2 | 13 | 3.5 / 31 |

