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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 3.03 | 12.41 | 99% | 0.40 | 2.66 | 4.27 | 86.4 → 87.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 3.11 | 15.62 | 100% | 0.63 | 2.65 | 4.20 | 124.2 → 121.9 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 3.91 | 19.46 | 99% | 1.77 | 2.87 | 4.71 | 159.9 → 144.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 3.14 | 11.29 | 99% | 0.53 | 2.69 | 4.48 | 103.9 → 100.9 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 2.75 | 15.28 | 99% | 0.52 | 2.44 | 3.89 | 81.3 → 73.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.56 | 2.89 | 12.64 | 99% | 0.54 | 2.54 | 4.06 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.60 | 3.04 | 15.53 | 99% | 0.56 | 2.62 | 4.22 | 106.5 → 98.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.67 | 3.20 | 15.35 | 99% | 0.61 | 2.76 | 4.61 | 105.8 → 97.4 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.55 | 2.86 | 12.10 | 99% | 0.45 | 2.54 | 3.99 | 83.8 → 78.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.62 | 2.96 | 12.58 | 99% | 0.52 | 2.59 | 4.11 | 109.1 → 100.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.59 | 3.06 | 12.91 | 99% | 0.51 | 2.63 | 3.54 | 109.2 → 100.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.97 | 14.25 | 100% | 1.33 | 2.45 | 3.43 | 46.5 → 42.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.209 |
| AttachingStyle - | 0.079 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.45 | 2.79 | 3.15 | 100% | 0.32 | 2.43 | 4.13 | 50.2 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 255 frames at 120 Hz; the text stood still on 0 of them, 259 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.60 | 3.00 | 15.28 | 99% | 0.53 | 2.61 | 4.11 | 104.8 → 101.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.14 | 5.53 | 21.65 | 98% | 0.86 | 3.14 | 4.56 | 318.3 → 310.4 | 9 | 1.1 / 4 |

