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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.43 | 1.84 | 8.76 | 99% | 0.28 | 1.60 | 2.91 | 112.1 → 113.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.57 | 2.05 | 9.50 | 99% | 0.42 | 1.75 | 3.02 | 150.5 → 148.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.68 | 2.21 | 12.64 | 99% | 0.54 | 1.80 | 3.25 | 186.3 → 170.2 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.64 | 2.97 | 13.06 | 100% | 0.52 | 2.41 | 3.26 | 130.5 → 127.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.36 | 1.73 | 8.09 | 100% | 0.35 | 1.54 | 2.65 | 106.1 → 99.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 2.08 | 12.85 | 99% | 0.43 | 1.77 | 3.25 | 132.5 → 124.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.46 | 1.91 | 9.42 | 99% | 0.39 | 1.61 | 3.26 | 132.4 → 124.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.43 | 1.85 | 9.66 | 99% | 0.36 | 1.60 | 2.82 | 131.5 → 123.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 2.64 | 9.70 | 100% | 0.46 | 2.02 | 3.17 | 109.6 → 105.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.73 | 2.91 | 64.90 | 99% | 0.48 | 2.38 | 3.60 | 135.6 → 127.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.62 | 1.79 | 12.01 | 99% | 0.41 | 1.40 | 2.93 | 135.3 → 126.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 1.27 | 8.01 | 100% | 0.35 | 1.06 | 2.15 | 70.4 → 66.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.181 |
| RaisingRoutedEvent RepeatButton | 0.032 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable TextView | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.52 | 1.65 | 11.70 | 98% | 0.35 | 1.34 | 2.98 | 76.3 → 99.2 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 2 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.64 | 3.32 | 12.15 | 99% | 0.60 | 2.60 | 3.42 | 131.0 → 128.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.82 | 1.97 | 14.22 | 98% | 0.47 | 1.56 | 3.10 | 342.7 → 336.5 | 8 | 1.1 / 4 |

