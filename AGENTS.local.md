# Agent Configuration and Environment Notes

## Environment Information
- **Operating System**: Windows 11
- **Shell**: PowerShell
- **Date Created**: 2025-12-31

## Important Notes
- All terminal commands should use PowerShell syntax
- Avoid commands that may hang or require interactive input
- When possible, use background jobs for long-running processes
- File paths use Windows-style backslashes (though forward slashes also work in PowerShell)

## Selen Project Context 
This is a C# .NET legacy project with **poor code quality**.

**Purpose**: Use ONLY as a reference for:
- Business logic and mechanics
- Marketplace API integration examples (Avito, Drom, Ozon, Wildberries, Yandex Market, etc.)
- Understanding existing workflows

**DO NOT**:
- Copy code patterns or architecture from Selen
- Use as an example of good code practices

Components:
- Main application in root directory
- Windows Forms UI components
- Multiple site integrations

## WorkerService1 Project Context
This is a **NEW .NET 10 background service** project with high quality standards.

**Requirements**:
- No UI components (console/service only)
- Clean architecture and best practices
- High code quality standards
- Proper testing and validation
- Modern C# patterns and conventions

**All code MUST be**:
- Well-structured and maintainable
- Following SOLID principles
- Properly documented
- Thoroughly tested
- Using modern .NET best practices

**Code Style Requirements**:
- No files with "everything in one" - separate concerns properly
- Maximum line length: 128 characters
- No emojis in logs and comments
- Use proper folder structure and file organization

## Best Practices
- Use PowerShell-native commands (Get-*, Set-*, etc.) when appropriate
- For .NET operations, prefer `dotnet` CLI commands
- Test commands with short timeouts to avoid hanging
- Use `-NoNewWindow` flag for processes when needed

## File Editing Guidelines
**IMPORTANT**: Always use specialized file editing functions instead of PowerShell commands:

1. **find_and_replace_code** - PRIMARY method for editing existing files
   - Use for partial content changes
   - Safest and most precise method
   - Preserves formatting and structure

2. **create_file** - For new files or complete rewrites
   - Use for creating new files
   - Use with `overwrite=True` for complete file replacement
   - Not recommended for partial edits

3. **delete_file** - For removing files
   - Use when files need to be deleted

4. **move_file** - For renaming or moving files
   - Use for file organization tasks

**DO NOT use PowerShell commands** (like `Set-Content`, `Add-Content`, `Out-File`) for file editing.
Only use PowerShell for:
- Running tests
- Building projects
- Git operations
- Process management
