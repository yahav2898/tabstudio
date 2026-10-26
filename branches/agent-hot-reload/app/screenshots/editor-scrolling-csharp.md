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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.13 | 10.06 | 100% | 0.40 | 1.80 | 4.26 | 113.1 → 114.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 2.52 | 13.61 | 99% | 0.63 | 1.96 | 4.52 | 151.1 → 149.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 2.67 | 13.76 | 99% | 0.75 | 1.96 | 4.55 | 186.8 → 171.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.84 | 3.55 | 15.95 | 99% | 0.64 | 2.68 | 4.71 | 131.1 → 128.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.14 | 9.64 | 99% | 0.54 | 1.68 | 4.19 | 108.2 → 100.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 2.16 | 15.23 | 99% | 0.52 | 1.80 | 4.33 | 133.3 → 125.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.70 | 2.18 | 11.45 | 99% | 0.52 | 1.80 | 4.28 | 133.4 → 125.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.81 | 3.11 | 76.69 | 99% | 0.64 | 2.39 | 4.58 | 132.7 → 124.9 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 2.10 | 8.91 | 100% | 0.44 | 1.71 | 4.03 | 110.5 → 105.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 2.17 | 11.53 | 99% | 0.51 | 1.75 | 4.30 | 135.7 → 128.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 2.11 | 11.64 | 99% | 0.51 | 1.73 | 3.64 | 135.5 → 127.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.95 | 10.84 | 100% | 0.44 | 1.55 | 3.17 | 71.7 → 68.4 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.290 |
| AttachingStyle - | 0.054 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.010 |
| RaisingRoutedEvent Thumb | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.77 | 2.32 | 2.37 | 100% | 0.43 | 1.86 | 4.87 | 78.2 → 99.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 266 frames at 120 Hz; the text stood still on 0 of them, 250 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 2.39 | 11.92 | 99% | 0.58 | 1.91 | 4.55 | 131.6 → 128.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.43 | 5.05 | 20.37 | 98% | 2.34 | 4.30 | 4.95 | 344.2 → 338.8 | 9 | 1.1 / 4 |

