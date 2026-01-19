---
applyTo: 'Docs/**/*.md'
---
# Project documentation writing guidelines

## Docs Folder Structure
The 'Docs' folder is a git submodule of [GitHub Wiki](https://github.com/D-Naveenz/rheo-storage/wiki.git) to the main documentation repository. So it follows the same structure as the wiki.

The documentation folder is organized as follows,

```ultree
Docs
├── Home.md
├── _Sidebar.md
├── api
│   ├── analyzing
│   ├── information
│   └── progress
└── guides
```

- 'Docs/Home.md' Home page and overview documentation.
- 'Docs/_Sidebar.md' Sidebar navigation structure.
- 'Docs/api': API reference documentation.
- 'Docs/guides': User guides and tutorials.

## File Naming Conventions
- Use descriptive names for documentation files.
- Use PascalCase with hyphens between words (ex: FileAnalyzer-Class.md).

## API Documentation
- Document public classes, methods, and properties which are exposed to users.
- The file name should properly state the structure type. (ex: Confidence-Struct.md, Definition-Class.md)
- Follow the similar style/template of Microsoft Docs API. (ex: https://learn.microsoft.com/en-us/dotnet/api/system.collections.hashtable?view=net-10.0)
- All the properties, methods and constructors should be included in the class page.

## Markdown Guidelines
- Include links to related resources.
- When include link reference to a documentation file, It should be a _'GitHub Wiki'_ web link instead a Direct link to the markdown file.
  - ✔️ Correct: [ConfidenceStack](https://github.com/D-Naveenz/rheo-storage/wiki/ConfidenceStack-Class)
  - ❌ Incorrect: [ConfidenceStack](ConfidenceStack-Class.md)
- For the sake of maintenance, Follow 'Markdown Reference-Style' links if re-use the same link more than one time

```markdown
<!-- In the documentation -->
#### Property Value

[ConfidenceStack][ConfidenceStack-Class.md]\<[String][String-MSDocs]>

## See Also

[ConfidenceStack\<T> Class][ConfidenceStack-Class.md]


<!-- Link References -->
[ConfidenceStack-Class.md]: https://github.com/D-Naveenz/rheo-storage/wiki/ConfidenceStack-Class
<!-- Other Document Pages -->

[String-MSDocs]: https://learn.microsoft.com/dotnet/api/system.string
<!-- Other MS Docs References -->
``` 