# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.72 | 16.04 | 99% | 0.43 | 2.35 | 3.78 | 85.4 → 86.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.86 | 3.24 | 17.08 | 99% | 0.73 | 2.55 | 4.12 | 123.2 → 123.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 5.18 | 125.22 | 99% | 2.74 | 2.73 | 4.70 | 159.8 → 144.5 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.84 | 16.46 | 99% | 0.55 | 2.35 | 3.79 | 103.1 → 100.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 2.96 | 19.04 | 99% | 0.66 | 2.45 | 3.86 | 81.0 → 74.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.70 | 2.98 | 13.51 | 99% | 0.56 | 2.48 | 3.77 | 106.0 → 97.5 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.70 | 2.97 | 15.60 | 99% | 0.59 | 2.46 | 3.75 | 105.8 → 97.4 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.70 | 3.15 | 16.59 | 100% | 0.58 | 2.51 | 3.91 | 105.1 → 96.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.75 | 3.04 | 98.44 | 99% | 0.54 | 2.50 | 3.86 | 83.1 → 77.8 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.75 | 3.15 | 16.42 | 99% | 0.64 | 2.53 | 3.92 | 108.3 → 100.2 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.60 | 2.77 | 16.74 | 99% | 0.55 | 2.37 | 3.26 | 108.5 → 100.1 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.59 | 10.92 | 100% | 1.33 | 2.16 | 2.71 | 46.6 → 42.9 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.293 |
| AttachingStyle - | 0.062 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.60 | 2.67 | 2.80 | 100% | 0.42 | 2.23 | 3.57 | 49.5 → 73.8 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 226 frames at 120 Hz; the text stood still on 0 of them, 249 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.78 | 3.22 | 21.97 | 99% | 0.64 | 2.58 | 3.99 | 104.1 → 100.7 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.05 | 3.34 | 19.21 | 98% | 0.63 | 2.76 | 3.77 | 310.9 → 303.0 | 7 | 1.1 / 4 |

