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

## Project Context
This is a C# .NET project (Selen) with:
- Main application in root directory
- WorkerService1 - a background worker service
- Multiple site integrations (Avito, Drom, Ozon, Wildberries, Yandex Market, etc.)
- Windows Forms UI components

## Best Practices
- Use PowerShell-native commands (Get-*, Set-*, etc.) when appropriate
- For .NET operations, prefer `dotnet` CLI commands
- Test commands with short timeouts to avoid hanging
- Use `-NoNewWindow` flag for processes when needed
