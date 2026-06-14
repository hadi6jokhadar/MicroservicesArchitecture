# Terminal Rules

## Documentation First — Mandatory Both Directions

**BEFORE starting any task:** Read every relevant `Doc/*.md` file. State which files you read.

**AFTER completing any task:** Update every doc file that was affected. This is not optional — a task is NOT complete until documentation is current. Specifically:

- If code behavior changed → update the doc that describes that behavior
- If a new pattern was used → add it to the relevant `Doc/*.md` or `.claude/instructions/*.md`
- If a new pitfall was hit → record it under "Common Pitfalls" in the relevant CLAUDE.md
- If a file was added, removed, or renamed → update `DOCUMENTATION_INDEX.md` (backend) or the docs table in `MicroservicesArchitecture-Web/CLAUDE.md` (frontend)
- If a CLAUDE.md table is stale (wrong ports, wrong file paths, outdated patterns) → fix it immediately

## Self-Correcting Documentation

If you make a mistake caused by reading incorrect or misleading documentation:

1. Immediately acknowledge the mistake.
2. Update the affected documentation file(s) with correct information.
3. Add clarification or a warning to prevent repeating the mistake.
4. Update the relevant CLAUDE.md or `.claude/instructions/*.md` if the gap is a missing rule rather than a factual error.

## No Ampersand (`&`) in Commands

`&` is reserved in Windows PowerShell and cmd. Never chain commands with `&`.

```powershell
# WRONG
dotnet build & dotnet run

# CORRECT
dotnet build; dotnet run
```

Run commands sequentially or in separate terminals.

## Python: Always Use the Project Virtual Environment

Never rely on system Python when a project-local `venv` folder exists.

From a Python service folder, use `venv\Scripts\python.exe` for all scripts, tests, migrations, and package commands.

This is required for `src/Services/AI/AI.API` so editable local dependencies such as `ihsandev_shared` resolve correctly.
