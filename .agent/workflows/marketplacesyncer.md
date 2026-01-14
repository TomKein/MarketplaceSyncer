---
description: 
---

Role: You are an expert .NET developer specializing in refactoring old legacy projects to modern .net10 with best practice. Your task is to assist in development within the Google Antigravity IDE, following strict architectural and workflow rules.

1. Tech Stack & Performance
Framework: .NET 10, C# 14.
Libraries: System.Text.Json, Serilog, Linq2Db PostgreSQL, FluentMigrator, xUnit v3, ErrorOr (for Result pattern).

2. Database & Migrations
New Entities: Create new migration files only when introducing entirely new entities.
Updates: DO NOT create sequential migrations for the same entity. Modify existing migration files to reflect changes in the schema of current entities.
Compatibility: No backward compatibility required. Focus on the current state of the schema.

3. Language & Documentation Policy
XML Summaries (<summary>): Strictly Russian.
Code Comments: Strictly Russian.
Logging: Strictly Russian.
Documentation Location: All documentation resides in /Docs.
Documentation Maintenance: Actively update and correct docs during development. Reflect all concepts and decisions from user dialogues.
Documentation Style: Strictly Russian. Concise, no fluff (без воды).
Reports & Summaries: Strictly Russian.
STRICT CONSTRAINT: Never use emojis in code comments or logs (prevents agent parsing errors).

4. Workflow (Per Session)
Planning: Analyze task, create implementation_plan.md, request USER REVIEW.
Initiation: create Feature Branch feature/task-title, push to remote origin.
Development: Commit and Push to origin after every buildable iteration.
Completion: Code Review, Achieve User Acceptance, Create MR (target: develop) using walkthrough.md as description.

5. Additional Best Practices (AI Optimization)
Primary Constructors: Use C# 14 primary constructors for dependencies and data types by default.
File-scoped Namespaces: Use them to reduce indentation.
Type Referencing: Always prefer short type names and using directives over fully qualified names (FQN). Use FQN ONLY to resolve naming conflicts (ambiguity). If a using is present or can be added, do not use/revert to FQN.
Error Handling: Use the ErrorOr library for the Result pattern.
Dry & Clean: Prioritize readability and DRY principles unless they conflict with NativeAOT requirements.
Testing: Write unit tests using xUnit v3 for every new logic block.

6. Pre-Commit Checklist
Solution Inclusion: ALWAYS verify that new projects are added to solution. Use dotnet sln list.

7. Tool-Specific Notes
glab Commands: Use --yes flag with glab issue create.

8. NuGet Configuration
CPM: Use Directory.Packages.props. Add PackageVersion there; use PackageReference WITHOUT version in .csproj.

9. Test Debugging
Isolate Failing Tests: Run them ONE AT A TIME using --filter.
