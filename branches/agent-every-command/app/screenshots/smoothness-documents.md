# Twenty long tabs

- The sample with 20 more C# files of about 1,500 lines each, in the IDE's window at 1280 × 800.
- The tab in view colored by Roslyn 4.4 s after the first was opened.

| Action | Cost |
| --- | --- |
| Opening a tab, to its first frame | median 179.7 ms, worst 1010.0 ms, 20 KB each |
| Switching to another tab, to its first frame | median 11.3 ms, worst 52.9 ms, 362 KB each |
| A keystroke with 21 tabs open | median 9.8 ms, worst 31.5 ms, 313 KB each |
| Closing a tab | median 15.4 ms, worst 43.1 ms, 454 KB each |

After closing them: 2 of 21 documents and 2 of 21 editors still in memory.

The UI thread: 18 over 50 ms (1 over 250 ms, 0 over 1 s), 1.6 s in all, longest 314 ms during opening the tabs, 0.7 s of it garbage collection; answer p50 2.2 ms, p95 38.3 ms, p99 85.0 ms over 514 questions in 15 s.

| Stall | Of it, GC | At | During |
| --- | --- | --- | --- |
| 314 ms | 83 ms | 0.5 s | opening the tabs |
| 145 ms | 30 ms | 0.8 s | opening the tabs |
| 113 ms | 41 ms | 2.8 s | opening the tabs |
| 102 ms | 22 ms | 0.3 s | opening the tabs |
| 86 ms | 27 ms | 1.1 s | opening the tabs |
| 85 ms | 35 ms | 4.1 s | opening the tabs |
| 80 ms | 42 ms | 2.6 s | opening the tabs |
| 80 ms | 37 ms | 3.8 s | opening the tabs |
| 69 ms | 43 ms | 2.1 s | opening the tabs |
| 69 ms | 30 ms | 1.3 s | opening the tabs |
| 67 ms | 46 ms | 2.4 s | opening the tabs |
| 63 ms | 28 ms | 3.9 s | opening the tabs |
