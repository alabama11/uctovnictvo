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

## Verzia 0.2 – spoločenstvo

- import bytov, vlastníkov a predpisov z Excelu (šablóna z programu)
- konto vlastníka po mesiacoch, dlžníci, upomienky (PDF, e-mail)
- hromadné posielanie vyúčtovaní a predpisov e-mailom, QR kód PAY by square
- zmena vlastníka počas roka, výmena vodomera, fond opráv, návrh záloh
- prezenčná listina, správa o hospodárení, automatické zálohy

## Verzia 0.3 – faktúry, banka, pokladňa

- adresár partnerov, načítanie názvu a adresy podľa IČO z registra RPO
- vydané faktúry, zálohové faktúry a ich vyúčtovanie, dobropisy, číselný rad podľa formátu
- PDF faktúry s QR platbou, posielanie e-mailom, upomienky
- prijaté faktúry; import e-faktúry UBL 2.1 (Peppol BIS 3.0) – prijímanie je povinné od 1. 1. 2027 aj pre neplatiteľov DPH
- banka: import výpisu camt.053 (príjmy aj výdavky), automatické párovanie na faktúry podľa VS a účtu
- pokladňa: príjmové a výdavkové doklady, pokladničná kniha (PDF, Excel), kontrola záporného zostatku
- spoločenstvo: prijaté faktúry podľa kategórie nákladov sa dajú prevziať ako skutočné náklady roka

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
