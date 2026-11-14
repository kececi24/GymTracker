## Summary

<!-- Describe what this PR changes and why. -->

## Related Issue / WBS Task

<!-- Link the related issue, PR, or WBS task ID (e.g., Closes #123, WBS-1.4). -->

## Type of Change

- [ ] Feature
- [ ] Bug fix
- [ ] Refactor / chore
- [ ] Documentation
- [ ] CI / build / infrastructure

## Scope

- [ ] Backend (`backend/`)
- [ ] Mobile (`mobile/`)
- [ ] Documentation (`docs/`)
- [ ] CI / Docker (`.github/`, `compose.yaml`)

## Verification

<!-- Describe how you tested these changes locally. Check the items that apply. -->

### Backend (.NET / PostgreSQL)
- [ ] `dotnet format whitespace GymTracker.slnx --verify-no-changes`
- [ ] `dotnet test GymTracker.slnx`
- [ ] EF Core migrations added/verified (including `has-pending-model-changes` and rollback, if schema changed)

### Mobile (Flutter)
- [ ] `dart format --output=none --set-exit-if-changed lib test`
- [ ] `flutter analyze`
- [ ] `flutter test`

### General
- [ ] No secrets, credentials, or `.env` values committed
- [ ] Relevant documentation (`README.md`, `docs/`) updated if needed

## Screenshots / Additional Notes

<!-- Optional: attach screenshots for mobile UI changes or add notes for reviewers. -->
