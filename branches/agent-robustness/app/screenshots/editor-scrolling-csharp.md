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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 3.33 | 32.48 | 99% | 0.47 | 2.79 | 4.74 | 87.1 → 88.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 3.01 | 15.04 | 100% | 0.61 | 2.54 | 4.13 | 125.3 → 123.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.76 | 3.13 | 16.81 | 99% | 0.89 | 2.63 | 4.23 | 161.6 → 145.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.58 | 2.98 | 15.53 | 99% | 0.53 | 2.55 | 4.15 | 104.8 → 101.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.95 | 10.36 | 100% | 0.64 | 2.56 | 4.10 | 81.8 → 74.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 2.99 | 80.86 | 99% | 0.61 | 2.63 | 4.55 | 107.6 → 99.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.55 | 2.86 | 16.25 | 99% | 0.53 | 2.53 | 4.05 | 107.4 → 98.9 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.57 | 2.84 | 15.35 | 99% | 0.53 | 2.48 | 3.98 | 106.7 → 98.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.55 | 2.79 | 10.07 | 100% | 0.44 | 2.44 | 3.92 | 84.3 → 78.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 3.47 | 14.27 | 99% | 0.64 | 2.81 | 4.55 | 110.1 → 101.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.55 | 2.96 | 15.06 | 99% | 0.51 | 2.57 | 3.61 | 110.1 → 101.7 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.55 | 12.44 | 100% | 0.53 | 2.26 | 2.93 | 46.6 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.169 |
| ArrangingLayoutable LineDrawing | 0.053 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
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
| touch drag and flick (real touch input) | 46 | 0.46 | 2.74 | 3.06 | 100% | 0.36 | 2.43 | 4.07 | 50.4 → 72.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2246 ms over 252 frames at 120 Hz; the text stood still on 0 of them, 267 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 3.54 | 81.76 | 99% | 0.60 | 2.67 | 4.52 | 105.8 → 102.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.00 | 3.40 | 17.81 | 98% | 0.61 | 2.85 | 4.15 | 320.5 → 312.3 | 7 | 1.1 / 4 |

