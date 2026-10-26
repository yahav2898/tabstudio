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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 1.91 | 2.29 | 100% | 0.37 | 1.62 | 3.73 | 128.2 → 135.8 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.66 | 1.96 | 51.15 | 100% | 0.55 | 1.53 | 4.37 | 164.1 → 161.8 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.77 | 2.33 | 50.83 | 100% | 0.83 | 1.71 | 4.75 | 210.8 → 195.8 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.60 | 2.14 | 49.00 | 100% | 0.51 | 1.63 | 4.25 | 149.3 → 151.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.52 | 3.58 | 5.87 | 100% | 2.35 | 2.36 | 4.50 | 124.8 → 122.8 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.58 | 1.75 | 36.26 | 100% | 0.51 | 1.46 | 4.20 | 148.6 → 145.2 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.60 | 1.89 | 50.04 | 100% | 0.53 | 1.53 | 4.34 | 150.9 → 148.0 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.58 | 1.90 | 2.77 | 100% | 0.52 | 1.54 | 4.38 | 153.5 → 150.8 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.51 | 1.75 | 47.49 | 100% | 0.39 | 1.52 | 4.30 | 113.2 → 111.7 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.59 | 3.01 | 88.09 | 100% | 0.58 | 2.43 | 4.55 | 153.4 → 151.0 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.59 | 1.90 | 10.90 | 100% | 0.52 | 1.30 | 3.87 | 153.5 → 149.9 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 1.26 | 1.73 | 100% | 0.35 | 1.02 | 3.48 | 74.4 → 74.1 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| AttachingStyle - | 0.210 |
| MeasuringLayoutable TextView | 0.167 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.58 | 1.59 | 47.17 | 100% | 0.50 | 1.23 | 4.24 | 149.2 → 150.5 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.66 | 3.21 | 65.37 | 99% | 0.58 | 2.82 | 4.66 | 548.5 → 570.2 | 3 | 3.5 / 31 |

