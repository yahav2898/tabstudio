# TabStudio's start

## This test process's own start (cold)

| Part | Took | At |
| --- | --- | --- |
| styles | 449 ms | 449 ms |
| services | 70 ms | 519 ms |
| theme | 33 ms | 553 ms |
| view models | 241 ms | 794 ms |

## Again, warm

- Building the service provider: 0 ms.
- Every service, created one at a time: 20 ms.
- The main view's first frame on the start screen: 64 ms.

| Service (with what it needs that was not created before it) | Took |
| --- | --- |
| SwarmMemberService | 17.6 ms |
| ExtensionsService | 1.3 ms |
| LanguageServerServices | 0.2 ms |
| ToolboxCatalog | 0.1 ms |
| LocalAIService | 0.0 ms |
| AppearanceService | 0.0 ms |
| DesktopModeViewModel | 0.0 ms |
| MainViewModel | 0.0 ms |
| GitHubAccount | 0.0 ms |
| SwarmCoordinatorService | 0.0 ms |
| StartViewModel | 0.0 ms |
| LinkViewModel | 0.0 ms |
| CompilerServices | 0.0 ms |
| WindowsBridgeService | 0.0 ms |
| CrashLog | 0.0 ms |
| LinkService | 0.0 ms |
| RemoteSshServices | 0.0 ms |
| SwarmJoinViewModel | 0.0 ms |
| IDatabaseSecretStore | 0.0 ms |
| SwarmInputMirror | 0.0 ms |
