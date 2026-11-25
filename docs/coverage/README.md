# Vairas.UserManagement — code coverage

**Line coverage 81.77 %** (1009/1234 executable lines), branch coverage 57.19 % — threshold 70 % **met**. Generated 2026-11-25.

| Assembly | Line % | Lines | Branch % |
|---|---:|---:|---:|
| Vairas.UserManagement | 75.87 | 588/775 | 45.26 |
| Vairas.UserManagement.Business | 96.8 | 242/250 | 90.87 |
| Vairas.UserManagement.Domain | 100.0 | 1/1 | 100.0 |
| Vairas.UserManagement.Persistance | 85.58 | 178/208 | 54.29 |

## How it was measured

- Tool: coverlet (`XPlat Code Coverage`) driven by `Backend/local/coverage.py`; settings `Vairas.UserManagement/coverage.runsettings` (migrations and test assemblies excluded).
- Test projects: `Vairas.UserManagement.Tests`; line hits of all test projects of the service are merged (maximum per line), so shared code is never counted twice.
- Filter: `TestCategory!=Broker`. Broker tests need the Service Bus emulator and are executed (and passed) separately by `local/test_all.py`.
- The numbers are measured line coverage, not a statement about functional completeness; acceptance status is tracked in `Documentation/Final-Verification/AC-Traceability-Matrix.xlsx`.

## Reproduce

```powershell
cd Backend
py -3 local/coverage.py --services Vairas.UserManagement --filter "TestCategory!=Broker" --threshold 70
```

## Files

`coverage-summary.json` (machine-readable, per file) and `index.html` (per-file table, lowest coverage first) are in this folder.

## Least covered files

| File | Line % | Lines |
|---|---:|---:|
| `Vairas.UserManagement.Business/BadRequestException.cs` | 0.0 | 0/1 |
| `Security/LessonActions.cs` | 0.0 | 0/61 |
| `Controllers/AssistantActorController.cs` | 0.0 | 0/7 |
| `Controllers/DocumentProfileController.cs` | 0.0 | 0/2 |
| `Controllers/NotificationBindingController.cs` | 0.0 | 0/5 |
| `Vairas.UserManagement.Persistance/DataAccess/VairasDbContextFactory.cs` | 0.0 | 0/7 |
| `Security/ReadPreviewDelegation.cs` | 8.75 | 7/80 |
| `Vairas.UserManagement.Business/ServiceErrors.cs` | 30.0 | 3/10 |
