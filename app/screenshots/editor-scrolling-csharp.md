# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.68 | 2.22 | 11.11 | 99% | 0.42 | 1.87 | 4.45 | 117.6 → 119.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 2.46 | 13.06 | 99% | 0.60 | 1.95 | 4.45 | 155.5 → 153.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.06 | 4.55 | 18.04 | 99% | 2.50 | 2.65 | 4.76 | 192.2 → 176.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.94 | 2.77 | 18.40 | 99% | 0.66 | 2.16 | 4.97 | 135.7 → 133.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.74 | 2.30 | 10.75 | 99% | 0.57 | 1.90 | 4.48 | 112.3 → 105.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 2.26 | 13.13 | 99% | 0.54 | 1.87 | 4.44 | 137.8 → 129.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.88 | 3.96 | 14.74 | 99% | 0.70 | 2.58 | 4.79 | 138.1 → 130.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 2.41 | 10.03 | 99% | 0.58 | 1.95 | 4.51 | 137.1 → 129.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 2.14 | 13.27 | 99% | 0.43 | 1.78 | 4.09 | 115.0 → 110.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.70 | 2.17 | 13.04 | 99% | 0.53 | 1.81 | 4.30 | 140.2 → 132.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.79 | 2.73 | 12.48 | 99% | 0.62 | 2.00 | 4.02 | 140.3 → 132.4 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.50 | 2.34 | 15.20 | 100% | 0.50 | 1.71 | 3.45 | 76.9 → 72.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.185 |
| AttachingStyle - | 0.052 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.76 | 3.19 | 3.90 | 100% | 0.44 | 2.68 | 6.72 | 82.8 → 104.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 35 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 2.16 | 13.32 | 99% | 0.54 | 1.81 | 4.39 | 136.1 → 133.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.39 | 4.81 | 19.90 | 98% | 0.82 | 4.32 | 4.60 | 349.8 → 342.8 | 9 | 1.1 / 4 |

