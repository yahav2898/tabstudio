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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.95 | 2.34 | 11.45 | 99% | 0.56 | 1.87 | 3.71 | 112.7 → 113.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.98 | 2.41 | 11.75 | 99% | 0.63 | 1.84 | 3.59 | 150.0 → 148.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.15 | 2.79 | 16.06 | 99% | 0.85 | 1.96 | 3.76 | 185.4 → 169.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.91 | 2.52 | 11.94 | 99% | 0.62 | 1.86 | 3.92 | 130.1 → 127.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.79 | 2.49 | 16.54 | 99% | 0.63 | 1.72 | 3.50 | 106.4 → 100.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.98 | 3.22 | 72.73 | 99% | 0.73 | 2.08 | 3.77 | 132.9 → 124.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.91 | 2.37 | 13.55 | 99% | 0.63 | 1.85 | 3.60 | 132.2 → 124.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.90 | 2.34 | 12.52 | 99% | 0.59 | 1.79 | 3.56 | 131.5 → 123.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.85 | 2.35 | 10.30 | 99% | 0.55 | 1.82 | 3.59 | 109.6 → 104.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.97 | 2.64 | 11.97 | 99% | 0.65 | 1.97 | 3.73 | 135.0 → 127.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.01 | 4.07 | 13.97 | 99% | 0.77 | 2.67 | 3.33 | 134.7 → 127.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.61 | 1.86 | 6.30 | 100% | 0.51 | 1.45 | 2.57 | 71.1 → 67.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.236 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.014 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.90 | 2.25 | 2.49 | 100% | 0.47 | 1.75 | 3.74 | 78.1 → 98.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 8 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.89 | 2.31 | 11.42 | 99% | 0.58 | 1.76 | 3.53 | 130.4 → 127.6 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.30 | 4.70 | 18.85 | 98% | 0.79 | 4.02 | 3.71 | 341.7 → 335.4 | 8 | 1.1 / 4 |

