---
description: Исправление PATH для glab CLI
---

# Добавление glab в PATH

Если команда `glab` не распознаётся:

```
glab: The term 'glab' is not recognized as a name of a cmdlet...
```

Выполни однократно:

// turbo
```powershell
$env:Path += ";C:\Program Files (x86)\glab"
```

После этого `glab` будет работать в текущей сессии терминала.
