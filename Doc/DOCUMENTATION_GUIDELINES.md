# Documentation Guidelines for AI Agents

**Purpose:** This file teaches AI agents how to create, update, and maintain documentation in this project.

**Version:** 1.0  
**Last Updated:** January 27, 2026

---

## 🎯 Core Principles

### 1. **ONE FILE PER TOPIC** - No Exceptions

- ❌ **NEVER** create separate "Quick Reference", "Guide", "Overview", "Stage 1/2/3" files
- ✅ **ALWAYS** consolidate all information about a topic into ONE comprehensive file
- ✅ Use markdown sections (##, ###) to organize content within the file

**Example:**

```
❌ BAD:
- JWT_AUTHENTICATION_GUIDE.md
- JWT_AUTHENTICATION_QUICK_REFERENCE.md
- JWT_AUTHENTICATION_FLOW.md

✅ GOOD:
- JWT_AUTHENTICATION.md (contains everything: guide, reference, flow, examples)
```

### 2. **NO TEMPORARY DOCUMENTATION**

- ❌ **NEVER** create files with names like:
  - `*_SUMMARY.md` (implementation summaries)
  - `*_FIX.md` (bug fix documentation)
  - `*_MIGRATION.md` (migration tracking)
  - `*_VERIFICATION.md` (verification logs)
  - `*_IMPLEMENTATION.md` (implementation logs)
  - `*_UPDATE.md` (update logs)
  - `*_PROGRESS.md` (progress tracking)
- ✅ **INSTEAD:** Update the main topic file directly

**Reason:** Summaries and logs become outdated. The code is the source of truth.

### 3. **DOCUMENTATION = PERMANENT REFERENCE ONLY**

Documentation should answer: "How does this work?" not "How did we build this?"

**Keep:**

- ✅ Architecture explanations
- ✅ API endpoint references
- ✅ Configuration guides
- ✅ Usage examples
- ✅ Design patterns

**Remove:**

- ❌ "We added feature X on date Y"
- ❌ "Migration steps 1, 2, 3"
- ❌ "Fixed bug Z"
- ❌ "Implementation summary"
- ❌ Version history (use git commits instead)

---

## 📝 Creating New Documentation

### When to Create a File

✅ **CREATE** a new file when:

1. A new **service** is added (e.g., `PAYMENT_SERVICE.md`)
2. A new **major feature** spans multiple services (e.g., `AUDIT_LOGGING.md`)
3. A new **architectural pattern** is introduced (e.g., `EVENT_SOURCING.md`)

❌ **DO NOT CREATE** a file for:

1. Bug fixes → Update existing file or just fix the code
2. Feature enhancements → Update existing file
3. Configuration changes → Update existing file
4. Code refactoring → No documentation needed (code should be self-explanatory)

### File Naming Convention

```
{TOPIC_NAME}.md
```

**Rules:**

- UPPERCASE with underscores
- Descriptive, not generic
- Singular form (e.g., `FILE_MANAGER.md` not `FILE_MANAGERS.md`)
- Topic-focused, not task-focused

**Examples:**

```
✅ GOOD:
- MULTI_TENANCY.md
- JWT_AUTHENTICATION.md
- FILE_MANAGER.md
- NOTIFICATIONS.md
- REDIS_CACHING.md

❌ BAD:
- MULTI_TENANCY_QUICK_START.md (don't split into quick/full)
- HOW_TO_SETUP_JWT.md (task-focused, not topic-focused)
- NOTIFICATION_GUIDE.md (redundant "GUIDE" suffix)
- CACHE.md (too generic, which cache?)
```

### File Structure Template

Every documentation file must follow this structure:

```markdown
# {Topic Name}

**Purpose:** One sentence explaining what this is  
**Last Updated:** {Date}  
**Status:** ✅ Production Ready | ⚠️ In Development | ❌ Deprecated

---

## Overview

Brief explanation of the topic (2-3 paragraphs)

## Architecture

How it works, diagrams, flow charts

## Configuration

- Required settings
- Optional settings
- Environment variables

## API Reference

- Endpoints
- Request/Response examples
- Error codes

## Usage Examples

Code samples showing how to use it

## Common Patterns

Best practices and design patterns

## Troubleshooting

Common issues and solutions

## Related Documentation

Links to other relevant docs (use relative paths)
```

**Notes:**

- Not every section is required (skip if not applicable)
- Keep it concise but complete
- Use code blocks for examples
- Use tables for API references

---

## 🔄 Updating Documentation

### When to Update

✅ **UPDATE** immediately when:

1. API endpoints change (added, modified, removed)
2. Configuration options change
3. Architecture changes
4. Breaking changes occur
5. New usage patterns emerge

### How to Update

1. **Find the correct file** - Use `DOCUMENTATION_INDEX.md` to locate it
2. **Read the entire file** - Understand existing content
3. **Update inline** - Modify the relevant section
4. **Update "Last Updated" date** - Change to current date
5. **Remove outdated information** - Delete what's no longer true
6. **Do NOT add version history** - Just update the content

**Example Update:**

```markdown
❌ BAD:

## API Endpoints

...existing endpoints...

### Update January 2026

- Added new endpoint POST /api/users/bulk

✅ GOOD:

## API Endpoints

...existing endpoints...

- **POST /api/users/bulk** - Create multiple users at once
```

---

## 🗑️ Removing Documentation

### When to Remove

✅ **REMOVE** a file when:

1. The feature is completely removed from codebase
2. The service is deprecated and deleted
3. The file is a duplicate of another file
4. The file is a temporary summary/migration doc

### How to Remove

1. **Verify** the feature/service is truly gone from code
2. **Check references** - Search all .md files for links to this file
3. **Remove the file**
4. **Update DOCUMENTATION_INDEX.md** - Remove the entry
5. **Fix broken links** in other files

**Command to find references:**

```powershell
Get-ChildItem *.md | Select-String "FILENAME_TO_REMOVE.md"
```

---

## 📚 Special Files

### DOCUMENTATION_INDEX.md

- **Purpose:** Single entry point for AI to understand what documentation exists
- **Update:** Every time you add/remove a doc file
- **Format:** Table with name, description, and when to read it

### README.md

- **Purpose:** Project overview for humans (GitHub landing page)
- **Content:** High-level project description, tech stack, getting started
- **Do NOT merge** with DOCUMENTATION_INDEX.md

### DOCUMENTATION_GUIDELINES.md (this file)

- **Purpose:** Teach AI agents how to manage docs
- **Update:** When doc management rules change
- **Never remove** this file

---

## ⚠️ Anti-Patterns to Avoid

### 1. Creating "Quick Reference" Files

```
❌ Service has: SERVICE_GUIDE.md + SERVICE_QUICK_REFERENCE.md
✅ Service has: SERVICE.md (with a "Quick Reference" section at the top)
```

### 2. Multi-Part Documentation

```
❌ NEW_SERVICE_STAGE_1.md, NEW_SERVICE_STAGE_2.md, NEW_SERVICE_STAGE_3.md
✅ NEW_SERVICE.md (with sections: Stage 1, Stage 2, Stage 3)
```

### 3. Changelog Files

```
❌ FEATURE_IMPLEMENTATION_SUMMARY.md, FEATURE_UPDATE_JAN_2026.md
✅ Update FEATURE.md directly, use git history for changelog
```

### 4. Bug Fix Documentation

```
❌ JWT_BUG_FIX_SUMMARY.md
✅ Update JWT_AUTHENTICATION.md to reflect correct implementation
```

### 5. Verification/Audit Files

```
❌ SERVICE_VERIFICATION_SUMMARY.md, SERVICE_AUDIT_COMPLETE.md
✅ Verification complete? Update SERVICE.md if needed, then delete audit doc
```

---

## 🤖 AI Agent Workflow

### When Starting a Task

1. **Read `DOCUMENTATION_INDEX.md` first** - Understand what docs exist
2. **Identify relevant files** - Based on task description
3. **Read only necessary files** - Don't read everything
4. **Check if files are current** - Look at "Last Updated" date

### When Completing a Task

1. **Did you change architecture/API?** → Update relevant doc file
2. **Did you add a new service?** → Create new doc file (follow template)
3. **Did you fix a bug?** → Update doc if architecture changed, otherwise skip
4. **Did you refactor code?** → Usually no doc update needed

### When Creating Documentation

```
BEFORE creating a file, ask yourself:
1. Is this a permanent architectural decision? → Create file
2. Is this just tracking implementation progress? → Don't create file
3. Does a file for this topic already exist? → Update existing, don't create new
4. Will this file be useful 6 months from now? → If no, don't create
```

---

## 📊 Documentation Health Metrics

### Good Documentation Repo

- ✅ **30-40 total files** (for a microservices project with 5-8 services)
- ✅ **One file per major topic**
- ✅ **No files with dates in names** (e.g., `*_JAN_2026.md`)
- ✅ **No files with status words** (e.g., `*_COMPLETE.md`, `*_FIX.md`)
- ✅ **All files referenced in DOCUMENTATION_INDEX.md**

### Bad Documentation Repo

- ❌ **100+ files** (too many)
- ❌ **Multiple files per topic** (fragmentation)
- ❌ **Many "\*\_SUMMARY.md" files** (temporary logs)
- ❌ **Broken links** (references to deleted files)
- ❌ **Duplicate information** across multiple files

---

## 🔍 Consolidation Checklist

When you find multiple files on the same topic:

1. **Identify the main file** - Usually the one with the simplest name
2. **Read all related files** - Understand all content
3. **Merge into main file** - Combine unique information
4. **Organize with sections** - Use ## and ### headers
5. **Delete duplicate files** - Remove all others
6. **Update DOCUMENTATION_INDEX.md** - Remove deleted entries
7. **Fix broken links** - Search and replace in all .md files

**Example Consolidation:**

```
BEFORE (4 files):
- NOTIFICATION_SERVICE_README.md
- NOTIFICATION_QUICK_REFERENCE.md
- NOTIFICATION_HUB_GUIDE.md
- NOTIFICATION_FLOW.md

AFTER (1 file):
- NOTIFICATIONS.md
  ## Overview
  ## Architecture
  ## SignalR Hub
  ## API Reference (quick reference content)
  ## Flow Diagrams (flow content)
  ## Usage Examples
```

---

## ✅ Quality Checks

Before committing documentation changes:

- [ ] No duplicate files on the same topic?
- [ ] No temporary/summary files?
- [ ] No files with dates in names?
- [ ] All files listed in DOCUMENTATION_INDEX.md?
- [ ] All links working (no 404s)?
- [ ] "Last Updated" date is current?
- [ ] File follows the template structure?
- [ ] Content is concise and accurate?

---

## 🎓 Examples

### Example 1: Adding a New Service

```markdown
Task: "I just created a Payment Service"

❌ WRONG Approach:

1. Create PAYMENT_SERVICE_IMPLEMENTATION_SUMMARY.md (logs implementation)
2. Create PAYMENT_SERVICE_GUIDE.md (full guide)
3. Create PAYMENT_SERVICE_QUICK_REFERENCE.md (API reference)
   Result: 3 files, fragmented information

✅ CORRECT Approach:

1. Create PAYMENT_SERVICE.md (one comprehensive file)
2. Add sections: Overview, Architecture, API, Examples
3. Add entry to DOCUMENTATION_INDEX.md
   Result: 1 file, all information in one place
```

### Example 2: Fixing a Bug

```markdown
Task: "Fixed JWT token expiration bug"

❌ WRONG Approach:

1. Create JWT_TOKEN_EXPIRATION_FIX_SUMMARY.md
2. Document the bug, the fix, the date
   Result: Temporary file that will become outdated

✅ CORRECT Approach:

1. Update JWT_AUTHENTICATION.md if architecture changed
2. If just a code fix, update nothing (git commit message is enough)
   Result: No unnecessary documentation
```

### Example 3: Adding a Feature

```markdown
Task: "Added batch upload to File Manager"

❌ WRONG Approach:

1. Create FILE_MANAGER_BATCH_UPLOAD_SUMMARY.md
   Result: Feature-specific file that fragments docs

✅ CORRECT Approach:

1. Update FILE_MANAGER.md
2. Add new endpoint to API Reference section
3. Add usage example for batch upload
4. Update "Last Updated" date
   Result: One file stays comprehensive and current
```

---

## 🚀 Final Checklist for AI Agents

When you finish ANY task:

1. **Did I create any new .md files?**
   - If yes, are they in DOCUMENTATION_INDEX.md? ✅
   - Do they follow naming conventions? ✅
   - Are they permanent reference docs (not summaries)? ✅

2. **Did I update existing .md files?**
   - Did I update "Last Updated" date? ✅
   - Did I remove outdated information? ✅
   - Did I avoid adding version history? ✅

3. **Did I remove any .md files?**
   - Did I update DOCUMENTATION_INDEX.md? ✅
   - Did I fix broken links in other files? ✅

4. **Is the Doc/ folder clean?**
   - No files with "_\_SUMMARY", "_\_FIX", "\*\_MIGRATION"? ✅
   - No duplicate topics (Guide + Quick Ref)? ✅
   - 30-40 files total? ✅

---

**Remember:** Documentation is for **understanding the current system**, not **tracking how it was built**. Keep it clean, consolidated, and current.

**When in doubt:** Update existing file instead of creating new one.
