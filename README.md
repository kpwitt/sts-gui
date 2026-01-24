![StS-Logo](/Assets/gfx/school-building_small.png)

# SchildToSchule - StS

StS nimmt den Daten- und Leistungsdatenexport von
SchiLD-NRW ([Schulverwaltungsprogram von ribeka](https://www.ribeka.com/schild-nrw/)) und formatiert ihn
für [Moodle](https://moodle.org/), [AixConcept](https://aixconcept.de/)-Systeme und [JAMF](https://www.jamf.com/de/) so
um, dass Schüler:innen, Lehkräfte und Kurse in diese Systeme importiert werden können.
Lizenziert ist es unter der MIT-Lizenz (siehe LICENSE)

### Features

- Einlesen:
    - Schilddaten (Schüler:innendaten, Leistungsdaten)
    - Lehrkräftedaten
    - Zustimmungen für die Verwendung von JAMF
    - Seriennummern von Geräten für JAMF
- Export von Schüler:innen, Lehrkräften, Eltern (nur Moodle) und Kurse
    - Moodle
    - AIX
    - JAMF
    - Mailverteiler (Fachschaften und Lehrkräfte) als HTML-Seite
- Verwaltung der Fachschaftsvorsitzenden, Stufenleitungen und Klassen/Stufenzuordnung

### Testen

Mit Hilfe des [Datengenerators](https://git.wittboldkp.de/kayp/sts-datengenerator) lassen sich Testdaten erzeugen und
einlesen.

### Erstellt mit

[Avalonia](https://avaloniaui.net/), [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite), [App-Icon](https://publicdomainq.net/school-building-0042468/?PageSpeed=noscript)