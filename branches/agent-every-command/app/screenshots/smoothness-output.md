# A program printing 20,000 lines as fast as it can

- The Output panel shown in a 1280 × 400 window, keeping its last 1,000,000 characters; a frame asked for every 16 ms meanwhile.

| Appending | Every line shown after | Frames drawn meanwhile | The UI thread meanwhile |
| --- | --- | --- | --- |
| As before 0.21.7: everything queued in one job at normal priority, one insertion per write | 11.61 s | 1 | 1 over 50 ms (1 over 250 ms, 1 over 1 s), 11.6 s in all, longest 11604 ms during the end of the run, 3.9 s of it garbage collection; answer p50 3.3 ms, p95 11603.6 ms, p99 11603.6 ms over 2 questions in 12 s |
| In batches of 64 KB behind input and drawing | 0.12 s | 5 | 0 over 50 ms (0 over 250 ms, 0 over 1 s), 0.0 s in all; answer p50 1.1 ms, p95 22.8 ms, p99 22.8 ms over 16 questions in 0 s |
