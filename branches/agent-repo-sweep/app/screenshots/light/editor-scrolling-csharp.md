# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 3.32 | 13.60 | 99% | 0.47 | 2.85 | 4.72 | 86.7 → 87.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.96 | 4.30 | 16.30 | 99% | 1.36 | 2.97 | 4.87 | 124.6 → 122.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 3.58 | 15.65 | 99% | 0.89 | 2.91 | 4.74 | 160.2 → 144.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 3.39 | 14.95 | 99% | 0.64 | 2.83 | 4.58 | 104.2 → 101.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.61 | 3.26 | 14.51 | 99% | 0.60 | 2.68 | 4.37 | 81.1 → 73.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.81 | 3.78 | 15.01 | 99% | 1.27 | 2.88 | 4.90 | 106.8 → 98.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 3.34 | 18.11 | 99% | 0.65 | 2.84 | 4.73 | 106.7 → 98.3 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.79 | 3.34 | 15.14 | 100% | 0.65 | 2.81 | 4.62 | 106.0 → 97.7 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.76 | 3.49 | 12.56 | 99% | 0.63 | 2.86 | 4.55 | 83.9 → 78.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.85 | 3.65 | 12.21 | 99% | 0.66 | 2.93 | 4.92 | 109.4 → 101.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 3.24 | 14.77 | 99% | 0.59 | 2.75 | 4.06 | 109.4 → 101.1 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.89 | 8.81 | 100% | 1.05 | 2.49 | 3.33 | 46.1 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.307 |
| AttachingStyle - | 0.022 |
| ArrangingLayoutable TextView | 0.015 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.73 | 3.24 | 5.41 | 100% | 0.43 | 2.65 | 4.90 | 50.2 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 199 frames at 120 Hz; the text stood still on 0 of them, 232 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.81 | 3.37 | 12.88 | 99% | 0.65 | 2.83 | 4.70 | 105.1 → 101.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.29 | 3.92 | 18.93 | 98% | 0.79 | 3.20 | 4.77 | 318.7 → 310.7 | 7 | 1.1 / 4 |

