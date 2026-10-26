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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.81 | 2.53 | 13.92 | 99% | 0.54 | 2.02 | 5.27 | 126.6 → 134.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.97 | 2.78 | 15.51 | 99% | 0.77 | 2.15 | 5.52 | 163.7 → 161.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.36 | 4.54 | 19.55 | 99% | 2.72 | 3.94 | 5.81 | 210.7 → 196.4 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.92 | 2.67 | 18.52 | 99% | 0.73 | 2.11 | 5.37 | 148.6 → 150.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.83 | 2.51 | 14.16 | 99% | 0.71 | 1.92 | 5.58 | 123.6 → 121.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.89 | 2.55 | 17.70 | 99% | 0.67 | 2.00 | 5.40 | 148.1 → 144.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.07 | 3.53 | 16.99 | 99% | 1.01 | 2.57 | 5.55 | 150.4 → 148.3 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.02 | 2.84 | 13.32 | 99% | 0.74 | 2.08 | 5.95 | 153.3 → 150.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.84 | 2.42 | 11.54 | 99% | 0.56 | 1.91 | 5.25 | 113.0 → 111.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.02 | 2.81 | 15.29 | 99% | 0.76 | 2.16 | 5.55 | 152.9 → 150.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.01 | 3.37 | 16.36 | 99% | 0.77 | 2.65 | 5.05 | 152.7 → 150.2 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 1.91 | 7.67 | 100% | 0.48 | 1.55 | 4.12 | 74.4 → 73.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.303 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.71 | 2.21 | 16.16 | 99% | 0.60 | 1.76 | 4.80 | 148.6 → 149.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.24 | 5.16 | 30.23 | 96% | 0.79 | 4.28 | 5.41 | 554.9 → 574.2 | 14 | 3.5 / 31 |

