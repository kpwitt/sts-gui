import os
import re
import csv

# Pfad zum Ordner, den Sie durchsuchen möchten
path = "Pfad/zu/Ihrem/Ordner"

# Pfad zur Ausgabedatei
output_datei = "output.csv"

# Liste zum Speichern der Ergebnisse
ergebnisse = []

# Funktion zum Extrahieren der Seriennummer
def extrahiere_seriennummer(inner_inhalt):
    # Suche nach Großbuchstaben und Ziffern innerhalb von <span> in <p>
    match = re.search(r'<p[^>]*>.*?<span[^>]*>(?:\s*<br>\s*)*([A-Z0-9]+)(?:\s*<br>\s*)*</span>.*?</p>', inner_inhalt, re.DOTALL)
    if match:
        return match.group(1)
    
    # Falls nicht gefunden, suche direkt in <p>
    match = re.search(r'<p[^>]*>(?:\s*<br>\s*)*([A-Z0-9]+)(?:\s*<br>\s*)*</p>', inner_inhalt)
    if match:
        return match.group(1)
    
    return "Nicht gefunden"

# Durchgehen aller Dateien im angegebenen Ordner
for ordner_pfad in os.listdir(path):
    if os.path.isfile(ordner_pfad):
        continue
    for dateiname in os.listdir(ordner_pfad):
        # i) Extrahieren des Teilstrings bis zum ersten Unterstrich
        name = ordner_pfad.split('_')[0]
        
        # Vollständiger Pfad zur Datei
        datei_pfad = os.path.join(ordner_pfad, dateiname)
        
        # ii) Lesen des Dateiinhalts und Extrahieren der Seriennummer
        try:
            with open(datei_pfad, 'r', encoding='utf-8') as file:
                inhalt = file.read()
                seriennummer = extrahiere_seriennummer(inhalt)
        except Exception as e:
            print(f"Fehler beim Lesen der Datei {dateiname}: {str(e)}")
            seriennummer = "Fehler"
        
        # Hinzufügen des Ergebnisses zur Liste
        ergebnisse.append([name, seriennummer])

# iii) Schreiben der Ergebnisse in die CSV-Datei
try:
    with open(output_datei, 'w', newline='', encoding='utf-8') as csvfile:
        csv_writer = csv.writer(csvfile)
        csv_writer.writerow(['Name', 'Seriennummer'])  # Schreiben der Kopfzeile
        csv_writer.writerows(ergebnisse)
    print(f"Ergebnisse wurden erfolgreich in {output_datei} geschrieben.")
except Exception as e:
    print(f"Fehler beim Schreiben der CSV-Datei: {str(e)}")