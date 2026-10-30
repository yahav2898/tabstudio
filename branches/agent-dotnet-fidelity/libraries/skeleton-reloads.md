# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 396 ms | 1.9 ms | 3.4 ms |
| Memory pressure | 13 ms | 0.0 ms | 1.3 ms |
