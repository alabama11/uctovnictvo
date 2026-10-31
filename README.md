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

## Verzia 0.4 – daňová evidencia, majetok, kniha jázd

- peňažný denník z banky a pokladne, kategórie príjmov a výdavkov (daňové, nedaňové, neutrálne), automatické zaradenie úhrad faktúr
- súhrn daňovej evidencie (§ 6 ods. 11 ZDP) s odpismi, cestovnými náhradami, pohľadávkami a záväzkami k 31. 12. – PDF a Excel
- majetok: odpisové skupiny 0–6, rovnomerné a zrýchlené daňové odpisy podľa postupu Finančnej správy, pomerná časť v roku zaradenia, vyradenie, podiel použitia na podnikanie
- kniha jázd: vozidlá, jazdy, spiatočná jazda, základná náhrada za km (0,296 € od 1. 6. 2025, 0,313 € od 1. 1. 2026, upraviteľné) a náhrada za PHM; PDF

## Verzia 0.5 – podvojné účtovníctvo (s.r.o.)

- účtový rozvrh podľa rámcovej účtovej osnovy (upraviteľný, analytické účty s bodkou, napr. 221.001)
- „Zaúčtovať doklady“: predkontácie z faktúr (311/6xx, 5xx/321, zálohové faktúry cez 324/314), banky (221), pokladne (211), majetku (022/042, odpisy 551/082, vyradenie 082/022 a 541/022); účet na faktúre alebo podľa kategórie
- ručné zápisy (interné doklady), počiatočné stavy proti 701, otvorenie roka z konečných zostatkov (VH do 431), uzamknutie roka
- hlavná kniha a obratová predvaha, saldokonto k 31. 12.
- účtovná závierka mikro účtovnej jednotky v štruktúre Úč MÚJ (súvaha r. 01–45, výkaz ziskov a strát r. 01–38, netto, bežné a minulé obdobie) s kontrolami; PDF a Excel – podklad na vyplnenie formulára vo Finančnej správe

## Verzia 0.6 – podklady k daňovým priznaniam

- SZČO: DP FO typ B z daňovej evidencie – skutočné alebo paušálne výdavky (60 %, max. 20 000 €) + poistné, odpočet straty, nezdaniteľná časť na daňovníka, III. pilier, sadzba 15 % / pásma, daňový bonus, preddavky; hodnoty po riadkoch tlačiva
- s.r.o.: DP PO z podvojného účtovníctva – VH pred zdanením, nedaňové náklady (účty označené ako nedaňové), sadzba 10 / 21 / 24 %, minimálna daň, preddavky, zaúčtovanie dane 591/341
- parametre pre roky 2025 a 2026 overené na podpora.financnasprava.sk; PDF podklad na prepis do eFormulára FS
- XML na import do eDane zatiaľ nie je (chýba overená schéma)

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
