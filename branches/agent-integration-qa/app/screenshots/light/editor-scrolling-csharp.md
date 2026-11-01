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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.80 | 4.58 | 14.06 | 99% | 2.22 | 2.54 | 3.86 | 85.0 → 85.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.84 | 2.88 | 18.12 | 99% | 0.60 | 2.33 | 3.35 | 122.4 → 122.9 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 3.02 | 20.27 | 99% | 0.86 | 2.35 | 3.50 | 158.3 → 143.2 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 2.68 | 14.92 | 100% | 0.60 | 2.22 | 3.29 | 102.5 → 99.6 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 2.65 | 14.51 | 99% | 0.57 | 2.12 | 3.03 | 80.3 → 73.4 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.78 | 2.77 | 18.35 | 99% | 0.61 | 2.27 | 3.43 | 105.2 → 96.9 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.88 | 3.24 | 17.18 | 99% | 0.84 | 2.51 | 3.82 | 105.1 → 96.8 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.88 | 3.14 | 98.79 | 99% | 1.18 | 2.47 | 3.65 | 104.5 → 96.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.77 | 2.75 | 3.23 | 100% | 0.56 | 2.26 | 3.25 | 82.7 → 77.4 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.76 | 2.76 | 17.85 | 99% | 0.59 | 2.27 | 3.34 | 107.6 → 99.6 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.78 | 2.77 | 15.10 | 99% | 0.59 | 2.28 | 3.02 | 107.7 → 99.5 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.54 | 2.56 | 13.17 | 100% | 1.23 | 2.12 | 2.49 | 46.2 → 42.5 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.347 |
| AttachingStyle - | 0.019 |
| FindingResource - | 0.015 |
| ArrangingLayoutable TextView | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
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
| touch drag and flick (real touch input) | 46 | 0.69 | 2.82 | 4.68 | 100% | 0.51 | 2.15 | 3.69 | 49.4 → 73.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 261 frames at 120 Hz; the text stood still on 0 of them, 230 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 2.63 | 22.68 | 99% | 0.53 | 2.16 | 3.29 | 103.5 → 100.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.00 | 2.91 | 20.93 | 98% | 0.63 | 2.29 | 3.41 | 309.6 → 301.7 | 7 | 1.1 / 4 |

