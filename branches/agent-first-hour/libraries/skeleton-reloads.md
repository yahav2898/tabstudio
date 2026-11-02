# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 1444 ms | 90.2 ms | 20.7 ms |
| Memory pressure | 47 ms | 0.3 ms | 30.7 ms |
