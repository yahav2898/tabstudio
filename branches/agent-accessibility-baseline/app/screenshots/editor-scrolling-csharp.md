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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 3.22 | 12.56 | 100% | 0.43 | 2.75 | 4.43 | 86.5 → 87.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 3.26 | 17.43 | 99% | 0.62 | 2.75 | 4.40 | 124.2 → 122.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.84 | 3.42 | 15.04 | 99% | 0.83 | 2.79 | 4.52 | 160.0 → 144.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 3.42 | 14.34 | 99% | 0.60 | 2.87 | 4.62 | 103.9 → 101.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 3.18 | 13.91 | 99% | 0.63 | 2.70 | 4.34 | 81.4 → 73.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 3.27 | 12.69 | 99% | 0.52 | 2.74 | 4.39 | 106.6 → 98.1 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.62 | 3.11 | 16.20 | 99% | 0.56 | 2.69 | 4.32 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 3.40 | 16.12 | 99% | 0.67 | 2.81 | 4.38 | 105.8 → 97.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 3.32 | 10.90 | 99% | 0.53 | 2.77 | 4.36 | 83.9 → 78.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.64 | 3.06 | 15.04 | 99% | 0.54 | 2.68 | 4.29 | 109.1 → 100.9 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 3.03 | 15.20 | 99% | 0.55 | 2.67 | 3.79 | 109.2 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 2.65 | 12.14 | 100% | 0.60 | 2.35 | 2.97 | 46.5 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.285 |
| AttachingStyle - | 0.063 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| ArrangingLayoutable TextView | 0.015 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.71 | 3.63 | 12.08 | 98% | 0.45 | 3.30 | 4.80 | 50.3 → 72.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 219 frames at 120 Hz; the text stood still on 0 of them, 252 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.63 | 3.07 | 15.47 | 99% | 0.54 | 2.64 | 4.26 | 104.9 → 101.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.08 | 3.56 | 17.06 | 98% | 0.65 | 2.99 | 4.46 | 318.4 → 310.4 | 7 | 1.1 / 4 |

