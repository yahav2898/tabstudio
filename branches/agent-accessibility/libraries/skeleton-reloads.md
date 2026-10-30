# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 655 ms | 2.8 ms | 37.1 ms |
| Memory pressure | 23 ms | 0.1 ms | 4.2 ms |
