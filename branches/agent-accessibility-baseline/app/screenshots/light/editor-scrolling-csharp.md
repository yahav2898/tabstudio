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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.52 | 2.95 | 12.95 | 100% | 0.38 | 2.60 | 4.17 | 86.0 → 86.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 3.79 | 100.57 | 99% | 0.81 | 2.80 | 4.36 | 123.4 → 121.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.78 | 3.34 | 17.41 | 99% | 0.75 | 2.69 | 4.43 | 158.5 → 142.9 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.62 | 3.15 | 17.04 | 99% | 0.56 | 2.69 | 4.29 | 103.2 → 100.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 3.09 | 13.71 | 99% | 0.58 | 2.63 | 4.41 | 80.5 → 73.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.72 | 3.49 | 17.25 | 99% | 0.66 | 2.86 | 4.82 | 105.8 → 97.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.63 | 3.15 | 15.55 | 99% | 0.56 | 2.76 | 4.54 | 105.7 → 97.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.63 | 3.10 | 14.15 | 99% | 0.54 | 2.69 | 4.18 | 104.9 → 96.7 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 3.00 | 13.50 | 100% | 0.47 | 2.61 | 4.05 | 83.3 → 77.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 3.47 | 18.25 | 99% | 0.75 | 2.77 | 4.47 | 108.3 → 100.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.60 | 3.13 | 12.49 | 99% | 0.54 | 2.68 | 3.78 | 108.4 → 100.1 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 2.71 | 14.54 | 100% | 0.66 | 2.41 | 3.20 | 46.0 → 42.1 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.192 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.54 | 3.08 | 3.39 | 100% | 0.39 | 2.58 | 4.59 | 50.0 → 72.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2248 ms over 216 frames at 120 Hz; the text stood still on 0 of them, 246 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 3.37 | 14.61 | 99% | 0.60 | 2.78 | 4.48 | 104.2 → 100.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.03 | 3.51 | 16.54 | 98% | 0.62 | 2.99 | 4.35 | 316.5 → 308.5 | 7 | 1.1 / 4 |

