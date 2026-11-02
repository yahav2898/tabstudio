# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 2.93 | 14.77 | 99% | 0.45 | 2.65 | 4.49 | 84.4 → 85.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.78 | 3.42 | 32.23 | 99% | 0.74 | 2.79 | 4.52 | 121.3 → 121.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.97 | 4.61 | 111.66 | 99% | 1.65 | 2.97 | 5.06 | 156.8 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.59 | 2.96 | 16.95 | 99% | 0.55 | 2.55 | 4.22 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 3.19 | 14.77 | 100% | 0.68 | 2.61 | 4.84 | 79.8 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.82 | 3.37 | 13.27 | 100% | 0.81 | 2.76 | 5.31 | 104.3 → 96.1 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 1.10 | 4.72 | 98.28 | 99% | 1.23 | 3.60 | 7.60 | 104.2 → 96.1 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.69 | 3.29 | 18.04 | 99% | 0.69 | 2.74 | 4.81 | 103.5 → 95.4 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.63 | 3.06 | 10.84 | 100% | 0.52 | 2.62 | 4.60 | 82.2 → 76.9 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.60 | 3.00 | 15.64 | 99% | 0.57 | 2.58 | 4.27 | 106.7 → 98.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.58 | 2.91 | 17.49 | 99% | 0.53 | 2.54 | 3.64 | 106.8 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 2.86 | 11.66 | 100% | 1.40 | 2.41 | 3.34 | 46.1 → 42.4 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.339 |
| ArrangingLayoutable NumberDrawing | 0.053 |
| AttachingStyle - | 0.039 |
| ArrangingLayoutable LineDrawing | 0.019 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.51 | 2.74 | 3.13 | 100% | 0.39 | 2.47 | 4.27 | 49.2 → 72.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 249 frames at 120 Hz; the text stood still on 0 of them, 259 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 2.98 | 17.02 | 99% | 0.56 | 2.56 | 4.26 | 102.6 → 99.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.31 | 5.76 | 94.16 | 98% | 2.54 | 5.07 | 4.78 | 307.6 → 299.9 | 8 | 1.1 / 4 |

