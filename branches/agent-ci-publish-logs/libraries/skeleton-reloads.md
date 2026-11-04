# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 496 ms | 32.2 ms | 10.1 ms |
| Memory pressure | 21 ms | 0.1 ms | 5.3 ms |
