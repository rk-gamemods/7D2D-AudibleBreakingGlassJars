# AudibleBreakingGlassJars - Copilot Instructions

## ⚠️ CRITICAL: Use the Toolkit FIRST for Code Research

**ALWAYS use the QueryDb toolkit as your FIRST approach when researching game code, understanding mechanics, or analyzing how features work.**

### Toolkit Location
```
C:\Users\Admin\Documents\GIT\GameMods\7D2DMods\7D2D-DecompilerScript\toolkit\QueryDb\bin\Release\net8.0\QueryDb.exe
```

### Database Path
```
C:\Users\Admin\Documents\GIT\GameMods\7D2DMods\7D2D-DecompilerScript\toolkit\callgraph_full.db
```

### Essential Commands

```powershell
# Search for text/code patterns
QueryDb.exe <db_path> search "ItemActionEat"

# Find who calls a method (callers/upstream)
QueryDb.exe <db_path> callers "ItemAction.ExecuteAction"

# Find what a method calls (callees/downstream)
QueryDb.exe <db_path> callees "ItemActionEat.ExecuteAction"

# Find method definitions
QueryDb.exe <db_path> search "PlaySound"
```

### When to Use the Toolkit

| Scenario | Toolkit Command |
|----------|-----------------|
| "How does item consumption work?" | `search "ItemActionEat"` |
| "What calls this method?" | `callers "ClassName.MethodName"` |
| "What does this method do?" | `callees "ClassName.MethodName"` |
| "Where are sounds played?" | `search "PlayOneShot"` |
| "How does glass jar breaking work?" | `search "glassJar"` |

**Do NOT skip the toolkit and go straight to reading files.** The callgraph is faster and more comprehensive than manual file searches.

---

## 📚 AI Knowledge Base

Pre-generated knowledge files are available for quick reference without running tools:

**Location:** `C:\Users\Admin\Documents\GIT\GameMods\7D2DMods\7D2D-DecompilerScript\knowledge\`

| File | Description | Use When |
|------|-------------|----------|
| [AI_KNOWLEDGE.md](../7D2D-DecompilerScript/knowledge/AI_KNOWLEDGE.md) | Index of all knowledge files | Starting point |
| [entities.md](../7D2D-DecompilerScript/knowledge/entities.md) | Items, blocks, buffs with descriptions | Understanding game entities |
| [events.md](../7D2D-DecompilerScript/knowledge/events.md) | Event system documentation | Working with game events |
| [methods.md](../7D2D-DecompilerScript/knowledge/methods.md) | Key methods and call statistics | Finding important methods |
| [modding-patterns.md](../7D2D-DecompilerScript/knowledge/modding-patterns.md) | Common Harmony patch patterns | Writing mods |

**Full documentation:** See [AI_CONTEXT.md](../7D2D-DecompilerScript/AI_CONTEXT.md) for database schema and advanced queries.

---

## Project Overview

AudibleBreakingGlassJars is a simple 7 Days to Die mod that plays glass breaking sounds when consuming items from glass jars (like murky water). It uses Harmony patching to hook into the item consumption system.

**Framework:** .NET 4.8, C# 12, Harmony 2.x

---

## Workspace Structure

```
AudibleBreakingGlassJars/
├── Harmony_ItemActionEat.cs    # Main Harmony patch
├── Properties/
│   └── AssemblyInfo.cs
├── Release/                    # Distribution folder
│   └── AudibleBreakingGlassJars/
│       ├── AudibleBreakingGlassJars.dll
│       └── ModInfo.xml
├── AudibleBreakingGlassJars.csproj
├── README.md
├── NEXUS_DESCRIPTION.txt
└── TECHNICAL.md
```

---

## Build & Deploy

```powershell
cd C:\Users\Admin\Documents\GIT\GameMods\7D2DMods\AudibleBreakingGlassJars
dotnet build -c Release
```

Copy `Release\AudibleBreakingGlassJars\` to game's Mods folder for testing.

---

## Key Technical Pattern

The mod patches `ItemActionEat.ExecuteAction` to detect when glass jar items are consumed and plays a glass breaking sound effect. The sound plays at the player's position using Unity's AudioManager.

### Harmony Patching Strategy

- **Postfix patch** on `ExecuteAction` - runs after vanilla code
- Checks if consumed item returns glass jar
- Uses vanilla sound system for consistency
