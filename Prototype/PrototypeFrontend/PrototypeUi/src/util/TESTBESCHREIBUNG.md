# Testbeschreibung – Frontend Utility-Tests

Testrahmen: **Vitest 4** · Umgebung: **jsdom** · Bibliotheken: **@testing-library/react**

---

## 1. `work-profile.ts` – Utility-Funktionen (`work-profile.test.ts`)

### 1.1 `timeToMinutes`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 1 | `"00:00"` ergibt 0 Minuten | keine | `"00:00"` | `0` | Unit |
| 2 | `"09:00"` ergibt 540 Minuten | keine | `"09:00"` | `540` | Unit |
| 3 | `"17:30"` ergibt 1050 Minuten | keine | `"17:30"` | `1050` | Unit |
| 4 | `"23:59"` ergibt 1439 Minuten | keine | `"23:59"` | `1439` | Unit |
| 5 | Ungültiges Format `"abc"` ergibt `NaN` | keine | `"abc"` | `NaN` | Unit |
| 6 | Stunden außerhalb des Bereichs (25 h) ergibt `NaN` | keine | `"25:00"` | `NaN` | Unit |
| 7 | Minuten außerhalb des Bereichs (60 min) ergibt `NaN` | keine | `"10:60"` | `NaN` | Unit |
| 8 | Leerer String ergibt `NaN` | keine | `""` | `NaN` | Unit |

### 1.2 `minutesToTime`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 9 | 0 Minuten ergibt `"00:00"` | keine | `0` | `"00:00"` | Unit |
| 10 | 540 Minuten ergibt `"09:00"` | keine | `540` | `"09:00"` | Unit |
| 11 | 1050 Minuten ergibt `"17:30"` | keine | `1050` | `"17:30"` | Unit |
| 12 | 1439 Minuten ergibt `"23:59"` | keine | `1439` | `"23:59"` | Unit |

### 1.3 `createWorkBlock`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 13 | Block enthält korrekte Firma und Zeiten | keine | `company`, `"08:00"`, `"16:00"` | `companyId="co-1"`, `startTime="08:00"`, `endTime="16:00"` | Unit |
| 14 | Zwei Blöcke haben unterschiedliche IDs | keine | gleiche Firma, keine Zeiten | `a.id !== b.id` | Unit |
| 15 | Standardwerte ohne Zeitangabe = 09:00–17:00 | keine | nur `company` | `startTime="09:00"`, `endTime="17:00"` | Unit |

### 1.4 `createWorkBreak`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 16 | Pause enthält korrekte Zeiten | keine | `"12:00"`, `"12:30"` | `startTime="12:00"`, `endTime="12:30"` | Unit |
| 17 | Zwei Pausen haben unterschiedliche IDs | keine | keine Argumente | `a.id !== b.id` | Unit |

### 1.5 `getProductiveHoursForBlock`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 18 | 8-Stunden-Schicht ergibt 8,0 h | Block 09:00–17:00 | Block-Objekt | `8.0` | Unit |
| 19 | 30-Minuten-Block ergibt 0,5 h | Block 12:00–12:30 | Block-Objekt | `0.5` | Unit |
| 20 | Start = Ende ergibt 0 h | Block 12:00–12:00 | Block-Objekt | `0` | Unit |

### 1.6 `normalizeWorkProfile`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 21 | Profil mit nur einem Tag wird auf alle 7 Tage erweitert | Profil mit nur Montag | Profil-Objekt | 7 Tage, Reihenfolge Mo–So | Unit |
| 22 | Blöcke werden nach Startzeit aufsteigend sortiert | Mon mit 14:00- und 08:00-Block | Profil-Objekt | erster Block `startTime="08:00"` | Unit |
| 23 | `undefined` ergibt leeres 7-Tage-Profil | keine | `undefined` | 7 Tage, je 0 Blöcke, 0 Pausen | Unit |

### 1.7 `createEmptyWorkProfile`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 24 | Leeres Profil hat 7 Tage, alle leer | keine | keine | 7 Tage, alle `blocks=[]`, `breaks=[]` | Unit |

### 1.8 `getWorkProfileSummary`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 25 | Leeres Profil = 0 Arbeitstage, 0 h, 0 Blöcke | Leeres 7-Tage-Profil | Profil-Objekt | `activeDayCount=0`, `weeklyHours=0`, `totalBlocks=0` | Unit |
| 26 | Mon 09:00–17:00 = 8 h, 1 Arbeitstag | Profil mit einem Mon-Block | Profil-Objekt | `activeDayCount=1`, `weeklyHours=8`, `earliestStart="09:00"`, `latestEnd="17:00"` | Unit |
| 27 | Mo + Di je 4 h = 8 h gesamt, 2 Arbeitstage | Profil mit zwei 4-h-Blöcken | Profil-Objekt | `activeDayCount=2`, `weeklyHours=8` | Unit |
| 28 | Pausenanzahl wird korrekt gezählt | Mon mit 2 Pausen | Profil-Objekt | `totalBreaks=2` | Unit |

### 1.9 `validateWorkProfile`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 29 | Gültiges Profil ergibt kein Fehler | Profil mit korrektem Mon-Block | Profil-Objekt | `undefined` | Unit |
| 30 | Block mit Ende ≤ Start ergibt Fehler mit „Monday" | Block `"10:00"–"09:00"` an Mon | Profil-Objekt | Fehlermeldung enthält „Monday" | Unit |
| 31 | Zwei überlappende Blöcke ergibt Fehler „overlap" | Mon mit Blöcken `09:00–13:00` und `12:00–17:00` | Profil-Objekt | Fehlermeldung enthält „overlap" (Groß-/Kleinschreibung egal) | Unit |
| 32 | Block ohne Firmenname ergibt Fehler „company" | Block mit `id=""`, `name=""` | Profil-Objekt | Fehlermeldung enthält „company" | Unit |
| 33 | Zwei überlappende Pausen ergibt Fehler „overlap" | Mon mit Pausen `12:00–13:00` und `12:30–13:30` | Profil-Objekt | Fehlermeldung enthält „overlap" | Unit |

### 1.10 `createWorkProfileFromLegacyUser`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 34 | Nutzt `user.workProfile` wenn vorhanden | User mit gespeichertem Profil (Mon `08:00–16:00`) | User-Objekt | Mon-Block hat `startTime="08:00"` | Unit |
| 35 | Erstellt Blöcke aus Legacy-`workDays` wenn kein Profil | User mit `workDays=["Mon","Tue","Wed"]`, `workStart="09:00"`, `workEnd="17:00"` | User-Objekt | Mon/Tue/Wed haben je 1 Block, Do hat 0 Blöcke | Unit |
| 36 | Ungültige Strings in `workDays` werden ignoriert | User mit `workDays=["Mon","INVALID","Fri"]` | User-Objekt | Mon und Fri haben je 1 Block | Unit |

### 1.11 `getLegacyWorkSettings`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 37 | `workCapacityHours` = Maximum der täglichen Arbeitsstunden | Profil: Mon 8 h, Tue 4 h | Profil-Objekt | `workCapacityHours=8` | Unit |
| 38 | `workDays` enthält genau die aktiven Tage | Profil: Mon und Mi aktiv | Profil-Objekt | `workDays=["Mon","Wed"]` | Unit |
| 39 | `breakRules` enthält „No manual breaks" wenn keine Pausen vorhanden | Profil ohne Pausen | Profil-Objekt | `breakRules` enthält „No manual breaks" (Groß-/Kleinschreibung egal) | Unit |
| 40 | `breakRules` enthält Pausenanzahl wenn Pausen vorhanden | Mon mit 1 Pause | Profil-Objekt | `breakRules` enthält „1 manual break" (Groß-/Kleinschreibung egal) | Unit |

---

## 2. `use-work-profile.ts` – Custom React Hook (`use-work-profile.test.tsx`)

### 2.1 Reine Hilfsfunktionen (re-exportiert)

#### `sortBlocks`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 41 | Blöcke werden nach `startTime` aufsteigend sortiert | zwei Blöcke (14:00 und 08:00) | Array von Blöcken | erstes Element `startTime="08:00"` | Unit |
| 42 | Gibt neues Array zurück (kein Mutieren) | ein Block | Array von einem Block | Ergebnis-Referenz ≠ Eingabe-Referenz | Unit |

#### `sortBreaks`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 43 | Pausen werden nach `startTime` aufsteigend sortiert | zwei Pausen (15:00 und 12:00) | Array von Pausen | erstes Element `startTime="12:00"` | Unit |

#### `blocksOverlap`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 44 | Kandidat vollständig vor vorhandenem Block → kein Konflikt | vorhandener Block 09:00–12:00 | Kandidat 07:00–08:00 (420–480 min) | `false` | Unit |
| 45 | Kandidat vollständig nach vorhandenem Block → kein Konflikt | vorhandener Block 09:00–12:00 | Kandidat 13:00–15:00 (780–900 min) | `false` | Unit |
| 46 | Kandidat überlappt Anfang des vorhandenen Blocks | vorhandener Block 09:00–12:00 | Kandidat 08:00–09:30 (480–570 min) | `true` | Unit |
| 47 | Kandidat vollständig innerhalb des vorhandenen Blocks | vorhandener Block 09:00–12:00 | Kandidat 09:30–11:00 (570–660 min) | `true` | Unit |
| 48 | Kandidat umfasst den gesamten vorhandenen Block | vorhandener Block 09:00–12:00 | Kandidat 08:00–13:00 (480–780 min) | `true` | Unit |
| 49 | Kandidat beginnt exakt am Ende des vorhandenen Blocks (berührend) → kein Konflikt | vorhandener Block 09:00–12:00 | Kandidat 12:00–13:00 (720–780 min) | `false` | Unit |
| 50 | Block mit gleicher ID wird ignoriert (Verschieben) | vorhandener Block 09:00–12:00 | Kandidat 09:30–11:00, `ignoredBlockId=block.id` | `false` | Unit |

#### `breaksOverlap`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 51 | Kandidat nach vorhandener Pause → kein Konflikt | vorhandene Pause 12:00–12:30 | Kandidat 12:30–13:30 (750–810 min) | `false` | Unit |
| 52 | Kandidat überlappt vorhandene Pause | vorhandene Pause 12:00–12:30 | Kandidat 11:50–12:10 (710–730 min) | `true` | Unit |
| 53 | Pause mit gleicher ID wird ignoriert | vorhandene Pause 12:00–12:30 | Kandidat 11:50–12:10, `ignoredBreakId=break.id` | `false` | Unit |

### 2.2 Hook – Initialzustand

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 54 | `isDirty = false` beim ersten Rendern | User mit leerem Profil | Hook-Initialisierung | `isDirty === false` | Unit (Hook) |
| 55 | `showEncouragement = true` bei leerem Profil | User mit leerem Profil | Hook-Initialisierung | `showEncouragement === true` | Unit (Hook) |
| 56 | `showEncouragement = false` wenn mindestens ein Block vorhanden | User mit Mon-Block | Hook-Initialisierung | `showEncouragement === false` | Unit (Hook) |

### 2.3 Hook – `addShift`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 57 | Schicht wird zu korrektem Tag hinzugefügt, `isDirty` wird `true` | leeres Profil | `addShift("Mon","09:00","17:00")` | Mon hat 1 Block mit `startTime="09:00"`, `isDirty=true` | Unit (Hook) |
| 58 | `onErrorChange` wird gerufen wenn Ende vor Start liegt | leeres Profil | `addShift("Mon","17:00","09:00")` | `onErrorChange` aufgerufen, Mon hat 0 Blöcke | Unit (Hook) |
| 59 | `onErrorChange` wird gerufen wenn neue Schicht überlappt | Mon mit Block 09:00–13:00 | `addShift("Mon","12:00","17:00")` | `onErrorChange` aufgerufen, Mon hat weiterhin 1 Block | Unit (Hook) |

### 2.4 Hook – `addBreak`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 60 | Pause wird zu korrektem Tag hinzugefügt | leeres Profil | `addBreak("Wed","12:00","12:30")` | Wed hat 1 Pause mit `startTime="12:00"` | Unit (Hook) |
| 61 | `onErrorChange` wird gerufen bei überlappender Pause | Mon mit Pause 12:00–12:30 | `addBreak("Mon","12:15","12:45")` | `onErrorChange` aufgerufen | Unit (Hook) |

### 2.5 Hook – `moveBlock`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 62 | Block wird auf neues Zeitfenster am gleichen Tag verschoben | Mon-Block 09:00–17:00 | `moveBlock(id,"Mon","10:00","18:00")` | Mon-Block hat `startTime="10:00"`, `endTime="18:00"` | Unit (Hook) |
| 63 | Block wird tagesübergreifend verschoben (Mon → Tue) | Mon-Block 09:00–17:00 | `moveBlock(id,"Tue","09:00","17:00")` | Mon hat 0 Blöcke, Tue hat 1 Block mit `startTime="09:00"` | Unit (Hook) |
| 64 | Gibt `false` zurück und ruft `onErrorChange` bei Überlappung am Ziel | Mon 09:00–13:00, Tue 10:00–14:00 | `moveBlock(monId,"Tue","09:00","13:00")` | Rückgabe `false`, `onErrorChange` aufgerufen, Mon-Block bleibt erhalten | Unit (Hook) |

### 2.6 Hook – `moveBreak`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 65 | Pause wird auf neues Zeitfenster am gleichen Tag verschoben | Mon-Pause 12:00–12:30 | `moveBreak(id,"Mon","15:00","15:30")` | Mon-Pause hat `startTime="15:00"` | Unit (Hook) |
| 66 | Pause wird tagesübergreifend verschoben (Mon → Wed) | Mon-Pause 12:00–12:30 | `moveBreak(id,"Wed","13:00","13:30")` | Mon hat 0 Pausen, Wed hat 1 Pause mit `startTime="13:00"` | Unit (Hook) |

### 2.7 Hook – `removeWorkBlock`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 67 | Block wird entfernt und `isDirty` wird `true` | Mon-Block 09:00–17:00 | `removeWorkBlock("Mon", blockId)` | Mon hat 0 Blöcke, `isDirty=true` | Unit (Hook) |

### 2.8 Hook – `selectRange`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 68 | Gültiger leerer Bereich setzt `pendingSelection` ohne Fehler | leeres Profil | `selectRange("Mon","09:00","10:00")` | Rückgabe `undefined`, `pendingSelection.dayKey="Mon"`, `entryType="shift"` | Unit (Hook) |
| 69 | Ungültiger Zeitbereich (Ende < Start) gibt Fehlerstring zurück | leeres Profil | `selectRange("Mon","10:00","09:00")` | Rückgabe ist ein string, `pendingSelection` bleibt `undefined` | Unit (Hook) |
| 70 | Bereich innerhalb vorhandener Schicht setzt `entryType="break"` | Mon-Block 09:00–17:00 | `selectRange("Mon","10:00","11:00")` | `pendingSelection.entryType="break"` | Unit (Hook) |

### 2.9 Hook – `saveWork`

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 71 | Gültiges Profil: `onSaveUser` und `onStatusChange` werden gerufen | Profil mit Mon-Block 09:00–17:00 | `saveWork("06:00","22:00")` | `onSaveUser` 1× aufgerufen, `onStatusChange` mit `"Work profile saved."` | Unit (Hook) |
| 72 | Plannervalidierungsfehler: `onSaveUser` wird nicht gerufen | leeres Profil | `saveWork("06:00","22:00","Sichtbares Ende muss nach Start liegen.")` | `onSaveUser` nicht aufgerufen, `onErrorChange` mit Fehlermeldung | Unit (Hook) |
| 73 | Offene Auswahl (pendingSelection): Speichern wird blockiert | nach `selectRange("Mon","09:00","10:00")` | `saveWork("06:00","22:00")` | `onSaveUser` nicht aufgerufen, `onErrorChange` aufgerufen | Unit (Hook) |
| 74 | `isDirty` wird nach Speichern `false` wenn Parent mit neuem User re-rendert | leeres Profil; nach `addShift` und `saveWork` wird Hook mit `nextUser` neu gerendert | rerender mit dem von `onSaveUser` erhaltenen User | `isDirty === false` | Integration (Hook) |

### 2.10 Hook – `onDirtyChange`-Callback

| # | Testbeschreibung | Vorbedingungen | Eingaben | Erwartetes Ergebnis | Testart |
|---|---|---|---|---|---|
| 75 | `onDirtyChange(true)` wird gerufen wenn Schicht hinzugefügt | leeres Profil | `addShift("Mon","09:00","17:00")` | `onDirtyChange` mit `true` aufgerufen | Unit (Hook) |
| 76 | `onDirtyChange(false)` wird beim Unmount gerufen | Hook gemountet | `unmount()` | `onDirtyChange` mit `false` aufgerufen | Unit (Hook) |

---

## Zusammenfassung

| Datei | Testart | Anzahl Tests |
|---|---|---|
| `work-profile.test.ts` | Unit (reine Funktionen) | 40 |
| `use-work-profile.test.tsx` | Unit / Integration (Hook) | 36 |
| **Gesamt** | | **76** |
