# A program printing 20,000 lines as fast as it can

- The Output panel shown in a 1280 × 400 window, keeping its last 1,000,000 characters; a frame asked for every 16 ms meanwhile.

| Appending | Every line shown after | Frames drawn meanwhile | The UI thread meanwhile |
| --- | --- | --- | --- |
| As before 0.21.11: everything queued in one job at normal priority, one insertion per write | 53.34 s | 1 | 1 over 50 ms (1 over 250 ms, 1 over 1 s), 53.3 s in all, longest 53285 ms during the end of the run, 0.8 s of it garbage collection; answer p50 2.2 ms, p95 53284.5 ms, p99 53284.5 ms over 2 questions in 53 s |
| In batches of 64 KB behind input and drawing | 0.26 s | 10 | 0 over 50 ms (0 over 250 ms, 0 over 1 s), 0.0 s in all; answer p50 3.2 ms, p95 22.7 ms, p99 25.8 ms over 22 questions in 0 s |
