# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 2.14 | 13.37 | 99% | 0.41 | 1.82 | 4.48 | 122.3 → 129.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.78 | 2.24 | 15.43 | 99% | 0.60 | 1.78 | 4.91 | 158.4 → 156.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.90 | 2.54 | 16.11 | 99% | 0.86 | 1.86 | 5.12 | 205.1 → 190.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 3.11 | 17.06 | 99% | 0.63 | 2.32 | 4.85 | 143.8 → 145.4 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 2.61 | 11.33 | 99% | 0.63 | 1.93 | 4.92 | 119.0 → 117.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 2.21 | 18.51 | 99% | 0.60 | 1.76 | 5.07 | 143.2 → 139.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.70 | 2.20 | 10.34 | 100% | 0.60 | 1.73 | 4.74 | 145.6 → 142.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 2.34 | 14.96 | 99% | 0.60 | 1.79 | 4.71 | 148.3 → 145.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 2.88 | 11.24 | 99% | 0.55 | 2.33 | 4.81 | 108.5 → 106.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 2.25 | 14.39 | 99% | 0.60 | 1.83 | 4.55 | 148.1 → 145.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.67 | 2.22 | 15.05 | 99% | 0.60 | 1.80 | 4.23 | 148.0 → 144.7 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 1.74 | 7.81 | 100% | 0.41 | 1.46 | 4.00 | 69.2 → 68.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.275 |
| MeasuringLayoutable Grid | 0.045 |
| ArrangingLayoutable NumberDrawing | 0.015 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.85 | 3.92 | 16.18 | 99% | 2.35 | 2.51 | 5.14 | 144.7 → 145.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.16 | 4.84 | 26.47 | 95% | 0.70 | 4.04 | 5.13 | 550.6 → 570.8 | 13 | 3.5 / 31 |

