# Contributing to PhotoC

Thanks for your interest in improving PhotoC.

## How to Contribute

1. Fork the repository.
2. Create a feature branch.
3. Make focused changes with clear commit messages.
4. Run build and basic validation locally.
5. Open a pull request with a concise summary.

## Local Setup

```powershell
dotnet restore .\PhotoC\PhotoC.csproj
dotnet build .\PhotoC\PhotoC.csproj --configuration Debug
```

## Pull Request Guidelines

- Keep PRs small and scoped.
- Include rationale and testing notes.
- Update documentation when behavior changes.
- Avoid committing generated files (`bin/`, `obj/`, logs).

## Code Style

- Follow existing C# conventions and naming.
- Prefer readable, maintainable code over clever patterns.
- Add comments only when intent is not obvious.

## Reporting Issues

Please include:

- Steps to reproduce
- Expected vs actual behavior
- Environment details (OS, .NET SDK version)
- Relevant logs or screenshots
