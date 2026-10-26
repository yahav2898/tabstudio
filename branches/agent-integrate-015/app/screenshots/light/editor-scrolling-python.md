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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.05 | 2.95 | 12.53 | 99% | 0.64 | 2.39 | 5.09 | 127.6 → 134.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.21 | 4.23 | 18.04 | 99% | 1.08 | 2.48 | 5.66 | 163.5 → 161.9 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.46 | 3.46 | 15.51 | 99% | 1.20 | 2.41 | 5.74 | 210.4 → 195.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.09 | 2.95 | 16.27 | 99% | 0.82 | 2.27 | 5.41 | 148.9 → 150.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.93 | 2.66 | 12.25 | 99% | 0.78 | 2.04 | 5.24 | 123.0 → 121.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.11 | 3.74 | 18.30 | 99% | 0.91 | 2.69 | 5.59 | 148.2 → 145.1 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.07 | 3.10 | 12.10 | 99% | 0.84 | 2.26 | 5.63 | 150.3 → 146.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.06 | 2.98 | 18.58 | 99% | 0.79 | 2.28 | 5.70 | 153.1 → 149.9 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.92 | 2.60 | 10.47 | 99% | 0.62 | 2.02 | 5.00 | 112.5 → 110.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.16 | 4.70 | 14.59 | 99% | 1.05 | 2.92 | 5.25 | 152.8 → 150.6 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.10 | 2.91 | 12.50 | 99% | 0.80 | 2.21 | 4.66 | 152.3 → 149.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.70 | 2.33 | 11.46 | 100% | 0.57 | 1.83 | 4.30 | 74.6 → 73.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.327 |
| AttachingStyle - | 0.060 |
| MeasuringLayoutable ContentPresenter | 0.037 |
| ArrangingLayoutable TextView | 0.022 |
| FindingResource - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.008 |
| ArrangingLayoutable RepeatButton | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.14 | 3.15 | 16.34 | 99% | 0.85 | 2.33 | 5.21 | 149.0 → 150.4 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.69 | 7.94 | 28.79 | 95% | 0.97 | 5.94 | 5.82 | 551.2 → 570.4 | 13 | 3.5 / 31 |

