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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.86 | 3.10 | 13.72 | 100% | 0.86 | 2.43 | 3.49 | 86.4 → 87.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 3.01 | 15.45 | 99% | 0.70 | 2.31 | 3.48 | 124.2 → 122.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.94 | 2.98 | 16.56 | 99% | 0.78 | 2.36 | 3.57 | 159.9 → 144.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 2.86 | 12.56 | 99% | 0.57 | 2.30 | 3.37 | 103.9 → 100.9 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 2.72 | 13.28 | 99% | 0.58 | 2.16 | 3.31 | 81.3 → 73.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.75 | 2.93 | 18.44 | 99% | 0.61 | 2.33 | 3.48 | 106.5 → 98.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.87 | 3.32 | 14.01 | 100% | 1.41 | 2.47 | 3.58 | 106.5 → 98.1 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.77 | 2.70 | 13.44 | 99% | 0.60 | 2.19 | 3.39 | 105.7 → 97.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 2.64 | 12.61 | 99% | 0.49 | 2.15 | 3.09 | 83.8 → 78.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 2.65 | 13.79 | 99% | 0.52 | 2.13 | 3.25 | 109.0 → 101.0 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 2.64 | 14.83 | 99% | 0.55 | 2.15 | 2.91 | 109.2 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 2.47 | 12.02 | 100% | 1.05 | 1.99 | 2.44 | 46.5 → 42.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.394 |
| AttachingStyle - | 0.045 |
| ArrangingLayoutable ScrollingTextLayer | 0.028 |
| ArrangingLayoutable TextView | 0.017 |
| RaisingRoutedEvent ScrollBar | 0.016 |
| ArrangingLayoutable RepeatButton | 0.014 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.80 | 4.75 | 79.44 | 98% | 0.57 | 2.52 | 3.57 | 50.7 → 72.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 162 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 2.71 | 14.94 | 99% | 0.54 | 2.16 | 3.52 | 104.8 → 101.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.02 | 2.93 | 17.21 | 98% | 0.57 | 2.40 | 3.30 | 318.3 → 310.2 | 7 | 1.1 / 4 |

