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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.18 | 4.65 | 12.96 | 100% | 0.64 | 4.09 | 7.64 | 141.0 → 148.6 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.31 | 3.12 | 14.77 | 99% | 0.84 | 2.39 | 7.14 | 177.3 → 175.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.31 | 3.27 | 16.81 | 99% | 1.05 | 2.34 | 7.05 | 223.5 → 208.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.19 | 3.44 | 16.10 | 99% | 0.85 | 2.51 | 7.32 | 162.8 → 164.1 | 5 | 1.1 / 4 |
| without line numbers | 368 | 1.08 | 3.14 | 89.97 | 99% | 0.82 | 2.47 | 7.20 | 138.1 → 135.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.12 | 2.75 | 17.83 | 99% | 0.78 | 2.17 | 6.90 | 162.2 → 158.1 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.16 | 2.79 | 13.70 | 99% | 0.74 | 2.21 | 7.02 | 164.0 → 160.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.29 | 3.84 | 18.95 | 99% | 0.91 | 2.78 | 7.54 | 167.4 → 164.3 | 5 | 1.2 / 30 |
| without coloring | 368 | 1.02 | 2.76 | 10.15 | 99% | 0.59 | 2.19 | 6.77 | 126.4 → 124.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.16 | 2.98 | 14.72 | 99% | 0.76 | 2.30 | 6.95 | 167.0 → 163.8 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.24 | 3.40 | 16.67 | 99% | 0.80 | 2.53 | 6.65 | 166.4 → 163.8 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.78 | 2.71 | 9.54 | 99% | 0.59 | 2.11 | 6.20 | 87.9 → 85.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.296 |
| AttachingStyle - | 0.055 |
| ArrangingLayoutable TextView | 0.020 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.01 | 2.73 | 16.17 | 99% | 0.67 | 2.19 | 6.92 | 162.9 → 162.4 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.33 | 11.25 | 30.50 | 93% | 2.43 | 8.32 | 8.49 | 578.3 → 599.2 | 14 | 3.5 / 31 |

