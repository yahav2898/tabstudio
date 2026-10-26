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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.85 | 2.36 | 10.45 | 99% | 0.52 | 1.94 | 4.41 | 113.3 → 114.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.02 | 2.56 | 13.38 | 99% | 0.67 | 1.96 | 4.41 | 151.1 → 148.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 3.76 | 15.49 | 99% | 0.97 | 2.38 | 4.67 | 186.9 → 171.7 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.95 | 2.96 | 11.56 | 99% | 0.62 | 2.38 | 4.40 | 131.2 → 128.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.76 | 2.16 | 14.83 | 99% | 0.55 | 1.65 | 4.15 | 108.0 → 101.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.79 | 2.21 | 13.64 | 99% | 0.57 | 1.71 | 4.17 | 133.1 → 125.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 2.29 | 12.71 | 99% | 0.61 | 1.80 | 4.27 | 133.1 → 124.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.92 | 3.20 | 74.02 | 99% | 0.70 | 2.41 | 4.35 | 132.4 → 124.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.83 | 2.29 | 10.44 | 99% | 0.52 | 1.77 | 4.12 | 110.7 → 105.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.91 | 2.38 | 12.16 | 99% | 0.63 | 1.84 | 4.38 | 135.7 → 128.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.82 | 2.28 | 12.73 | 99% | 0.59 | 1.81 | 3.74 | 135.9 → 127.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.55 | 1.97 | 10.97 | 99% | 0.53 | 1.55 | 3.16 | 71.7 → 68.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.291 |
| ArrangingLayoutable LineDrawing | 0.061 |
| ArrangingLayoutable TextView | 0.032 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.88 | 2.72 | 4.72 | 100% | 0.48 | 2.35 | 4.79 | 78.2 → 99.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 266 frames at 120 Hz; the text stood still on 0 of them, 264 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.92 | 2.44 | 16.14 | 99% | 0.61 | 1.92 | 4.28 | 131.5 → 128.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.32 | 2.97 | 17.47 | 98% | 0.74 | 2.26 | 4.33 | 343.7 → 337.1 | 8 | 1.1 / 4 |

