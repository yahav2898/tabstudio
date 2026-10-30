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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 3.19 | 98.35 | 99% | 0.42 | 2.72 | 4.47 | 86.5 → 87.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 3.06 | 14.96 | 99% | 0.63 | 2.58 | 4.28 | 124.2 → 122.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.97 | 3.72 | 18.50 | 99% | 0.99 | 2.95 | 5.18 | 159.9 → 144.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.58 | 2.92 | 15.95 | 99% | 0.49 | 2.55 | 4.20 | 103.9 → 100.9 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 4.73 | 13.52 | 99% | 0.69 | 2.66 | 4.37 | 81.3 → 74.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.55 | 2.86 | 15.13 | 99% | 0.52 | 2.53 | 4.16 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.60 | 2.86 | 13.77 | 99% | 0.54 | 2.55 | 4.21 | 106.5 → 98.0 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.58 | 2.88 | 15.62 | 99% | 0.52 | 2.52 | 4.05 | 105.8 → 97.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 3.10 | 15.00 | 100% | 0.56 | 2.65 | 4.22 | 83.8 → 78.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.59 | 2.95 | 15.41 | 100% | 0.54 | 2.58 | 4.22 | 109.1 → 100.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.57 | 2.89 | 17.14 | 99% | 0.54 | 2.53 | 3.70 | 109.2 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.53 | 13.30 | 100% | 0.48 | 2.28 | 3.05 | 46.5 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.175 |
| ArrangingLayoutable CachedLineNumberMargin | 0.057 |
| AttachingStyle - | 0.054 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.46 | 2.96 | 11.74 | 98% | 0.36 | 2.51 | 4.31 | 50.2 → 72.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 216 frames at 120 Hz; the text stood still on 0 of them, 251 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 3.04 | 16.39 | 99% | 0.57 | 2.61 | 4.39 | 104.8 → 101.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.03 | 3.48 | 18.61 | 98% | 0.64 | 2.96 | 4.41 | 318.4 → 310.3 | 7 | 1.1 / 4 |

