# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 803 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.41 | 1.59 | 10.49 | 100% | 0.31 | 1.34 | 3.67 | 77.5 → 78.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.60 | 1.83 | 12.18 | 99% | 0.51 | 1.46 | 3.86 | 115.4 → 113.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.79 | 2.93 | 15.83 | 99% | 0.81 | 1.67 | 4.43 | 151.5 → 135.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.49 | 1.63 | 11.77 | 100% | 0.45 | 1.36 | 3.75 | 95.0 → 92.1 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.37 | 1.48 | 13.82 | 99% | 0.45 | 1.21 | 3.54 | 71.9 → 64.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.55 | 2.76 | 14.58 | 99% | 0.58 | 1.95 | 4.33 | 97.7 → 89.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.48 | 1.63 | 12.20 | 100% | 0.46 | 1.32 | 3.72 | 97.6 → 89.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.47 | 1.65 | 14.09 | 99% | 0.46 | 1.32 | 3.65 | 96.8 → 88.6 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 1.66 | 9.46 | 100% | 0.40 | 1.35 | 3.65 | 74.5 → 69.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.58 | 2.18 | 11.83 | 100% | 0.59 | 1.48 | 4.43 | 100.3 → 92.1 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.48 | 1.67 | 11.91 | 99% | 0.46 | 1.34 | 3.36 | 100.4 → 91.9 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.25 | 1.33 | 9.74 | 100% | 0.35 | 1.09 | 2.74 | 36.0 → 32.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 43.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.187 |
| ArrangingLayoutable ScrollingTextLayer | 0.016 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| ArrangingLayoutable TextView | 0.010 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable Grid | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.60 | 1.90 | 2.60 | 100% | 0.41 | 1.46 | 4.14 | 40.8 → 63.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 90 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.48 | 1.66 | 10.58 | 99% | 0.44 | 1.34 | 3.77 | 95.9 → 92.6 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.05 | 4.59 | 61.56 | 98% | 0.74 | 3.93 | 5.10 | 309.6 → 301.5 | 8 | 1.1 / 4 |

