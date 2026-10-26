# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.01 | 9.66 | 99% | 0.40 | 1.71 | 4.25 | 115.5 → 117.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.92 | 4.18 | 83.02 | 99% | 0.74 | 2.35 | 4.78 | 154.0 → 151.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.91 | 2.49 | 13.50 | 99% | 0.71 | 1.82 | 4.57 | 189.4 → 173.5 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 2.15 | 10.13 | 99% | 0.53 | 1.71 | 4.27 | 133.3 → 130.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 2.13 | 10.00 | 99% | 0.51 | 1.70 | 4.33 | 109.9 → 103.1 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 2.97 | 10.35 | 99% | 0.71 | 2.25 | 4.81 | 136.2 → 127.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 2.21 | 10.28 | 99% | 0.53 | 1.76 | 4.40 | 135.5 → 127.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 2.14 | 10.52 | 99% | 0.54 | 1.72 | 4.32 | 134.8 → 127.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.78 | 3.08 | 8.75 | 99% | 0.57 | 2.41 | 4.63 | 112.8 → 108.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 2.26 | 11.25 | 99% | 0.55 | 1.78 | 4.47 | 138.5 → 130.6 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 2.10 | 10.35 | 99% | 0.57 | 1.72 | 3.99 | 138.3 → 130.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 1.80 | 6.23 | 100% | 0.48 | 1.45 | 3.41 | 73.5 → 70.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.251 |
| ArrangingLayoutable TextView | 0.065 |
| ArrangingLayoutable Track | 0.028 |
| ArrangingLayoutable ScrollingTextLayer | 0.017 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.77 | 3.30 | 6.94 | 100% | 0.45 | 2.55 | 5.90 | 80.6 → 103.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 80 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 2.10 | 9.45 | 99% | 0.53 | 1.70 | 4.33 | 134.0 → 131.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.34 | 5.02 | 71.90 | 98% | 0.79 | 4.35 | 4.97 | 346.6 → 341.3 | 9 | 1.1 / 4 |

