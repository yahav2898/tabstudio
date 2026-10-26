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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 1.95 | 12.40 | 99% | 0.38 | 1.70 | 4.21 | 126.4 → 133.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.91 | 3.45 | 13.85 | 99% | 0.75 | 2.53 | 4.96 | 162.6 → 161.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.89 | 2.53 | 15.88 | 99% | 0.83 | 1.84 | 4.98 | 208.9 → 194.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.12 | 14.28 | 99% | 0.58 | 1.73 | 5.02 | 148.0 → 149.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 2.01 | 14.11 | 99% | 0.57 | 1.57 | 4.70 | 122.8 → 121.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 2.69 | 17.35 | 99% | 0.69 | 1.91 | 4.74 | 147.3 → 143.9 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 3.23 | 15.47 | 99% | 0.66 | 2.29 | 5.17 | 150.1 → 146.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 2.22 | 12.03 | 99% | 0.63 | 1.78 | 4.64 | 152.5 → 149.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.59 | 2.01 | 9.36 | 99% | 0.45 | 1.67 | 4.63 | 112.2 → 110.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 2.20 | 12.64 | 99% | 0.57 | 1.81 | 4.92 | 152.1 → 149.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.85 | 3.96 | 14.36 | 99% | 0.73 | 2.74 | 4.52 | 152.5 → 149.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 1.66 | 7.47 | 100% | 0.42 | 1.41 | 3.90 | 73.2 → 72.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.231 |
| FindingResource - | 0.069 |
| ArrangingLayoutable NumberDrawing | 0.047 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 2.14 | 13.57 | 99% | 0.56 | 1.76 | 4.55 | 148.2 → 149.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.90 | 8.28 | 30.40 | 95% | 1.42 | 7.11 | 6.73 | 551.2 → 574.0 | 14 | 3.5 / 31 |

