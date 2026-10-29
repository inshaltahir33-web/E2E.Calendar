# MainWindowViewModel.cs Error Fixes

## Fix 1: CS8852 - init-only property assignment
**File**: `src/E2E.Calendar.App/ViewModels/MainWindowViewModel.cs`
- **Problem**: `alertInstance.State = AlertState.Snoozed;` and `alertInstance.SnoozedUntilUtc = ...` cannot assign directly because these properties are `init-only` on the `AlertInstance` record
- **Fix**: Use `with` expression to create a new instance:
  ```csharp
  var updatedInstance = alertInstance with
  {
      State = AlertState.Snoozed,
      SnoozedUntilUtc = originalTime.Date.AddDays(1).AddMinutes(
          (originalTime - originalTime.Date).TotalMinutes)
  };
  _alerts.UpsertInstanceAsync(updatedInstance).GetAwaiter().GetResult();
  ```
- **Applied to**: Both branches of `HandleSnoozeProtocol` method (duration=="tomorrow" and duration as minutes)

## Fix 2: CS0019 - Null-coalescing operator between DateTimeOffset values
**File**: `src/E2E.Calendar.App/ViewModels/MainWindowViewModel.cs` (line 77 area)
- **Problem**: `var originalTime = alertInstance.OccurrenceUtc ?? now;` - `??` operator between non-nullable `DateTimeOffset` values
- **Fix**: Remove the `?? now` since `OccurrenceUtc` is `required DateTimeOffset` (always has a value):
  ```csharp
  var originalTime = alertInstance.OccurrenceUtc;
  ```

## Fix 3: CS1061 - Property not found on AlertInstance
**File**: `src/E2E.Calendar.App/ViewModels/MainWindowViewModel.cs` (line 129 area)
- **Problem**: `alertInstance.DoneUtc = DateTimeOffset.UtcNow;` - `DoneUtc` property does not exist on `AlertInstance` record
- **Fix**: Use the existing `FiredUtc` property:
  ```csharp
  // Before: alertInstance.DoneUtc = DateTimeOffset.UtcNow;
  // After:  alertInstance.FiredUtc = DateTimeOffset.UtcNow;
  ```
- **Location**: `HandleDoneProtocol` method

## Verification
All three error types (CS8852, CS0019, CS1061) have been resolved by the above changes.