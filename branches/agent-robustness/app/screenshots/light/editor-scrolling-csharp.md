# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 3.03 | 29.96 | 100% | 0.44 | 2.65 | 4.25 | 86.7 → 87.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.65 | 2.95 | 25.50 | 99% | 0.57 | 2.51 | 4.04 | 124.5 → 122.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.72 | 3.14 | 16.23 | 99% | 0.83 | 2.55 | 4.21 | 160.2 → 144.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.54 | 2.89 | 15.78 | 99% | 0.48 | 2.49 | 4.02 | 104.1 → 101.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 3.26 | 13.66 | 99% | 1.00 | 2.50 | 4.40 | 81.1 → 73.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.51 | 2.81 | 16.27 | 99% | 0.50 | 2.46 | 4.00 | 106.7 → 98.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.54 | 2.89 | 13.74 | 99% | 0.48 | 2.50 | 4.02 | 106.7 → 98.3 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.54 | 2.88 | 15.19 | 99% | 0.52 | 2.49 | 4.00 | 106.0 → 97.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 2.94 | 13.13 | 99% | 0.47 | 2.55 | 3.98 | 83.9 → 78.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.62 | 3.18 | 5.76 | 100% | 0.56 | 2.65 | 4.68 | 109.4 → 101.2 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.55 | 2.87 | 14.82 | 99% | 0.48 | 2.50 | 3.53 | 109.4 → 101.1 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 2.48 | 13.12 | 100% | 0.76 | 2.21 | 2.90 | 46.2 → 42.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.165 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.47 | 2.76 | 3.01 | 100% | 0.40 | 2.35 | 4.11 | 50.4 → 72.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 226 frames at 120 Hz; the text stood still on 0 of them, 250 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.57 | 2.90 | 15.84 | 99% | 0.51 | 2.54 | 4.11 | 105.1 → 101.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.99 | 3.34 | 18.37 | 98% | 0.59 | 2.86 | 4.08 | 318.7 → 310.6 | 7 | 1.1 / 4 |

