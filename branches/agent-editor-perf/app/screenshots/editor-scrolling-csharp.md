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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.83 | 2.31 | 6.10 | 100% | 0.48 | 1.85 | 4.73 | 124.0 → 123.3 | 5 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.96 | 2.93 | 15.87 | 99% | 0.73 | 1.97 | 5.03 | 149.4 → 147.2 | 7 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.10 | 3.43 | 13.96 | 99% | 0.95 | 2.02 | 5.15 | 176.4 → 167.2 | 7 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.89 | 2.58 | 12.77 | 99% | 0.60 | 1.90 | 4.89 | 136.9 → 132.5 | 6 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 2.22 | 8.09 | 100% | 0.66 | 1.62 | 4.75 | 71.0 → 64.2 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.89 | 2.46 | 12.23 | 100% | 0.64 | 1.78 | 4.74 | 138.8 → 129.6 | 6 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 2.33 | 8.52 | 100% | 0.64 | 1.76 | 4.69 | 138.7 → 129.7 | 5 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.89 | 2.67 | 13.90 | 99% | 0.66 | 1.84 | 4.77 | 138.0 → 129.0 | 6 | 1.2 / 30 |
| without coloring | 368 | 0.82 | 2.43 | 12.12 | 100% | 0.59 | 1.76 | 4.57 | 116.5 → 110.1 | 5 | 1.2 / 30 |
| without squiggles | 368 | 0.91 | 2.99 | 15.45 | 99% | 0.71 | 1.94 | 4.86 | 141.4 → 132.5 | 6 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.88 | 2.98 | 13.16 | 99% | 0.74 | 1.86 | 4.48 | 141.6 → 132.3 | 6 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 1.91 | 10.78 | 100% | 0.54 | 1.43 | 3.75 | 35.9 → 32.4 | 4 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 43.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.259 |
| ArrangingLayoutable ContentPresenter | 0.051 |
| ArrangingLayoutable TextView | 0.022 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable CachedLineNumberMargin | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.91 | 3.38 | 4.84 | 100% | 0.56 | 2.54 | 6.22 | 102.7 → 117.1 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2248 ms over 251 frames at 120 Hz; the text stood still on 0 of them, 259 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.89 | 2.40 | 14.77 | 99% | 0.66 | 1.82 | 4.89 | 137.9 → 133.0 | 6 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.28 | 3.72 | 15.34 | 98% | 0.80 | 2.84 | 4.94 | 307.2 → 299.2 | 10 | 1.1 / 4 |

