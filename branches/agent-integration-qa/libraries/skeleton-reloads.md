# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 1681 ms | 7.7 ms | 22.4 ms |
| Memory pressure | 10 ms | 0.2 ms | 0.3 ms |
