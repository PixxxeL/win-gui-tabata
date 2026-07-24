# ScheduleTimer — техническое руководство

[English version](technical.md)

WPF-приложение под .NET 8 (`net8.0-windows`). Зависимость:
[NAudio](https://github.com/naudio/NAudio) (воспроизведение WAV/MP3).

## Структура проекта

Код лежит в `src/`, ассеты (иконка, звук тиканья, картинки) — в `assets/`.
Изменения по версиям — в `CHANGELOG.md`.

Релизные сборки делает GitHub Actions по тегу `vX.Y.Z`
(см. `.github/workflows/release.yml`): два zip — self-contained (рантайм внутри)
и no-runtime (нужен .NET 8 Desktop Runtime). Версия берётся из имени тега.
Сборка MSIX в CI временно отключена.

## Обычная сборка

Требуется .NET 8 SDK.

```cmd
dotnet build ScheduleTimer.sln -c Debug
```

## Аналитика (AppMetrica)

Ключи AppMetrica **вшиваются в бинарник при сборке** из переменных окружения
`APPMETRICA_POST_API_KEY` и `APPMETRICA_APP_ID` (см. `Analytics.cs`).
В репозитории значений нет. Если переменные при сборке не заданы — аналитика
в этой сборке выключена и в сеть ничего не отправляется.

Сборка с аналитикой (cmd; `set` действует только в текущем окне,
значение — без кавычек и без пробелов вокруг `=`):

```cmd
set APPMETRICA_POST_API_KEY=<ключ Post API>
set APPMETRICA_APP_ID=<id приложения>

dotnet build ScheduleTimer.sln -c Release
```

Удобно держать эти две `set`-строки в файле **вне репозитория**
(например, `E:\pro\secrets.cmd`) и вызывать `call E:\pro\secrets.cmd`
перед сборкой.

## MSIX-пакет

`.wapproj` собирается только «большим» MSBuild из Visual Studio
(не `dotnet build`). В том же окне cmd, где заданы переменные:

```cmd
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ScheduleTimer.sln /restore /p:Configuration=Release /p:Platform=x64 /p:UapAppxPackageBuildMode=SideloadOnly /p:AppxBundle=Never
```

## Сборка из Visual Studio 2022

VS должна видеть переменные окружения на момент запуска. Чтобы не хранить
секреты в системных переменных, запускайте VS из cmd с заданными переменными —
они живут только внутри этого экземпляра VS:

```cmd
call E:\pro\secrets.cmd
start "" "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe" ScheduleTimer.sln
```

## Проверить, вшиты ли ключи в сборку

Ключи хранятся как assembly-атрибуты `AssemblyMetadata` в `ScheduleTimer.dll`.
Посмотреть их можно через PowerShell 7 (`pwsh`; встроенный PowerShell 5.1
не читает net8-сборки):

```cmd
pwsh -c "[Reflection.Assembly]::LoadFile('<путь>\ScheduleTimer.dll').GetCustomAttributesData() | Where-Object { $_.AttributeType.Name -eq 'AssemblyMetadataAttribute' } | ForEach-Object { \"$($_.ConstructorArguments[0].Value) = $($_.ConstructorArguments[1].Value)\" }"
```

Пустые значения — ключи не вшиты, аналитика выключена. Альтернатива —
открыть dll в [ILSpy](https://github.com/icsharpcode/ILSpy). Если dll внутри
`.msix`: это обычный zip — переименуйте копию в `.zip` и распакуйте.

Диагностика на рантайме: приложение пишет `analytics.log` в каталог данных —
там статус аналитики (ENABLED/DISABLED) и ответы сервера AppMetrica.
