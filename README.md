# Účtovníctvo

Desktopová aplikácia pre Windows 11 (jeden `.exe`, bez inštalácie) na evidenciu
spoločenstiev vlastníkov bytov a nebytových priestorov a neskôr aj na účtovníctvo
SZČO a s.r.o. Plán fáz: https://claude.ai/artifact/1kTvw5b8YGGokzXtbfffMn

## Čo vie verzia 0.1 (fáza 1)

- viac firiem a spoločenstiev v jednom programe
- byty a nebytové priestory (plocha, balkón, podiel, VS, vodomer), vlastníci a ich IBAN-y
- položky nákladov s kľúčom rozpočtu (plocha, vodomer, rovnako na byt, osoby, podiel; len vybrané byty; fond opráv)
- ročné predpisy záloh po položkách, hromadné nastavenie (€/m² alebo €/byt), prevzatie z minulého roka
- odpočty vody, poistné, zostatok minulého roka
- úhrady: ručne alebo importom výpisu camt.053 (XML) s párovaním podľa VS a IBAN
- ročné vyúčtovanie, PDF list pre každý byt podľa vzoru spoločenstva, export do Excelu
- záloha a obnova všetkých údajov (ZIP)

Údaje sú v `Dokumenty\Uctovnictvo\uctovnictvo.db`.

## Zostavenie

Potrebné je .NET 8 SDK.

```
dotnet test
dotnet publish src/Uctovnictvo.App -c Release -r win-x64 -o publish/win-x64
```

`tools/Snimky` vytvorí snímky obrazovky všetkých záložiek s ukážkovými dátami (aj na Linuxe).

## Štruktúra

- `src/Uctovnictvo.Core` – dátový model (EF Core + SQLite), výpočty, exporty, PDF, záloha
- `src/Uctovnictvo.App` – používateľské rozhranie (Avalonia)
- `tests/Uctovnictvo.Tests` – testy; `Vzor.cs` overuje výpočet voči vzoru vyúčtovania 2025 (byt 3)
