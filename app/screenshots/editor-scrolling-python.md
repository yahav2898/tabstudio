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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.47 | 2.45 | 15.27 | 99% | 0.37 | 2.20 | 3.37 | 96.3 → 103.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.64 | 2.65 | 16.15 | 100% | 0.68 | 2.21 | 4.03 | 133.0 → 130.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.74 | 3.13 | 15.88 | 99% | 1.47 | 2.36 | 4.28 | 180.7 → 165.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.54 | 2.56 | 14.92 | 99% | 0.58 | 2.17 | 3.89 | 118.1 → 119.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 3.20 | 15.63 | 99% | 1.64 | 2.30 | 4.28 | 93.0 → 91.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.52 | 2.66 | 11.65 | 99% | 0.56 | 2.24 | 4.14 | 118.0 → 113.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.53 | 2.60 | 15.33 | 99% | 0.54 | 2.20 | 3.90 | 120.1 → 116.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.63 | 2.77 | 15.74 | 99% | 0.69 | 2.30 | 3.88 | 123.0 → 119.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 2.43 | 9.20 | 100% | 0.41 | 2.12 | 3.84 | 82.2 → 80.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.61 | 2.85 | 19.10 | 99% | 1.46 | 2.35 | 4.07 | 122.7 → 119.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.68 | 2.94 | 15.97 | 99% | 1.51 | 2.42 | 3.76 | 122.9 → 119.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.34 | 10.15 | 100% | 0.41 | 2.05 | 3.31 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.203 |
| ArrangingLayoutable TextView | 0.012 |
| AttachingStyle - | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 2.96 | 13.09 | 99% | 0.76 | 2.40 | 4.32 | 118.7 → 119.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.87 | 11.46 | 98.46 | 93% | 2.51 | 7.45 | 8.11 | 609.5 → 647.9 | 14 | 3.5 / 31 |

