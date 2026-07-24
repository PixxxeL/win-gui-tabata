# ScheduleTimer — User Guide

[Русская версия](user-guide.ru.md)

Desktop schedule timer for Windows. Shows a large dial of glowing tick marks, counts down periods according to a schedule from `config.json`, plays sounds and a clock-ticking effect.

## Features

- **Schedules built from periods** with pomodoro-style nesting
  (work → rest → work → …).
- **Prepare + work**: each period can have a preparation phase
  (countdown before the start) and a main phase.
- **Repeats** of periods and nested periods interleaved after each repeat.
- **Dial** of 240 tick marks: fills with the period's color and glow
  proportionally to elapsed time.
- **Clicking the dial** seeks time proportionally to the angle (scrubbing).
- **Schedule picker**: if the config contains more than one schedule, clicking
  the title (while idle) opens a selection list. The choice lasts until the app
  restarts; by default the first schedule with `active: true` is used.
- **Summary**: on Stop or when the scenario finishes — a window with time per
  period (preparation and pauses excluded) and a grand total.
- **Project name**: a schedule with `askProjectName: true` asks for a project
  name on start; it is appended as a tag to period names in the log, analytics
  and the summary — "Work [Project #1]". Empty input — work without a tag, as usual.
- **Sounds** on period events (start / 5-second warning / finish) —
  WAV and MP3 formats.
- **Clock ticking** (`zvuk-chasov.mp3`) while running; louder during the last
  5 seconds of a period. Toggled with the 🔊 button in the title bar.
- **Window dragging** by any spot except the dial circle and the buttons.
- Controls: **Start/Resume**, **Pause** (pressing again unpauses),
  **Stop** — with the mouse or the keyboard.

## Hotkeys

| Key     | Action                    |
|---------|---------------------------|
| `Space` | Start / Pause (toggle)    |
| `S`     | Stop                      |
| `M`     | Ticking sound on/off      |

The key is shown in the tooltip when hovering over a button.

## UI language

The UI language is picked automatically from the OS language. Supported:
English, Russian, Chinese, Japanese, German, Spanish, French, Italian,
Portuguese; anything else falls back to English. Period and schedule names come
from `config.json` and are not translated (that is your data).

You can force a language with an environment variable, e.g.: `SCHEDULETIMER_LANG=ja`.

## Running

The ticking sound and the icon are **embedded in the exe**. Next to
`ScheduleTimer.exe` you only need `config.json` (plus the period sound files,
if you referenced any in the config).

## Configuration file `config.json`

UTF-8 encoding. The root is an object with an array of schedules:

```json
{
  "schedules": [
    {
      "name": "Default",
      "active": true,
      "askProjectName": true,
      "periods": [ /* ... */ ]
    }
  ]
}
```

### Schedule (`schedule`)

| Field     | Type      | Description                                          |
|-----------|-----------|------------------------------------------------------|
| `name`    | string    | Title (shown in the center of the dial).             |
| `active`  | bool      | Default schedule. **Only the first** active one is used; the rest are available via clicking the title. |
| `askProjectName` | bool | Ask for a project name on start (up to 256 characters). The entered name is appended as a tag to period names in the log, analytics and the summary. Works only at the schedule level. Default `false`. |
| `periods` | array     | Top-level periods.                                   |

### Period (`period`)

| Field      | Type    | Default | Description                                                     |
|------------|---------|---------|-----------------------------------------------------------------|
| `name`     | string  | `""`    | Period name (shown below the time).                             |
| `color`    | color   | `0`     | Color of active ticks and glow. Formats — see below.            |
| `prepare`  | int, s  | `0`     | Preparation in **seconds** before the start. `0` — no preparation. With `repeat>1` it plays **only before the first repeat**. |
| `duration` | int, s  | `0`     | Duration of the main phase in **seconds**.                      |
| `repeat`   | int     | `1`     | How many times to repeat the period.                            |
| `sound`    | object  | empty   | Event sounds (see below).                                       |
| `periods`  | array   | `[]`    | Nested periods (pomodoro) — inserted **between** the parent's repeats (not added after the last repeat). |

### Color (`color`)

Accepted in any of these formats:

- number: `10027008`
- hex with `0x`: `"0x990000"`
- hex with `#`: `"#990000"`
- hex without a prefix: `"990000"`

The lower 24 bits are used — `RRGGBB` (alpha is ignored).

### Sounds (`sound`)

| Field          | When it plays                                            |
|----------------|----------------------------------------------------------|
| `start`        | At the beginning of a phase.                             |
| `beforeFinish` | When 5 seconds remain until the end of a phase.          |
| `finish`       | At the end of a phase.                                   |

The value is a file name (`"beep.wav"`, `"gong.mp3"`) next to the `exe`,
or `null`. WAV and MP3 are supported.

## How the queue is built

Preparation plays once (before the first repeat), then `repeat` main phases,
with nested periods inserted between them. Example for "work ×3" with a nested
"rest" (no rest after the last work):

```
Work(prepare) → Work → Rest → Work → Rest → Work
```

The text below the clock changes through this example as:

```
Prepare → Work
Work [1/3] → Rest
Rest → Work
Work [2/3] → Rest
Rest → Work
Work [3/3] → Finish
```

## Examples

### Minimal

```json
{
  "schedules": [
    {
      "name": "Pomodoro 25/5",
      "active": true,
      "periods": [
        {
          "name": "Work",
          "color": "#C0392B",
          "duration": 1500,
          "repeat": 4,
          "periods": [
            { "name": "Rest", "color": "#27AE60", "duration": 300 }
          ]
        }
      ]
    }
  ]
}
```
→ 25 minutes of work and 5 minutes of rest, 4 rounds (rest also follows the last work).

### With preparation and sounds

```json
{
  "schedules": [
    {
      "name": "Workout",
      "active": true,
      "periods": [
        {
          "name": "Round",
          "color": "0x2980B9",
          "prepare": 10,
          "duration": 180,
          "repeat": 5,
          "sound": {
            "start": "bell.wav",
            "beforeFinish": "beep.wav",
            "finish": "gong.mp3"
          },
          "periods": [
            { "name": "Break", "color": "0x8E44AD", "duration": 60 }
          ]
        }
      ]
    }
  ]
}
```

## Limitations

- Durations are whole **seconds**; `prepare`/`duration` — positive integers only.
- By default the **first** schedule with `active: true` is applied; others are
  selected by clicking the title, but the choice is not saved between runs.
- Color is 24-bit `RRGGBB`, transparency is ignored.
- State is not saved between runs; Stop re-reads `config.json`.
- Sound files and `config.json` must be located **next to the exe**.
- The `beforeFinish` warning sound fires exactly 5 seconds before the end,
  so for phases shorter than ~6 seconds it may not fire.
