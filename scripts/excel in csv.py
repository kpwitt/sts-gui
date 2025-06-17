import pandas as pd
import os
import io
import msoffcrypto

# Pfad zum Ordner mit den Excel-Dateien
excel_ordner = '.'

# Pfad und Name der Ausgabe-CSV-Datei
ausgabe_csv = 'datei.csv'

# Das Passwort für die Excel-Dateien
password = 'XXXXX'

# Liste zum Speichern aller DataFrames
alle_dfs = []


# Funktion zum Lesen einer passwortgeschützten Excel-Datei
def read_protected_excel(file_path, password):
    decrypted = io.BytesIO()
    with open(file_path, 'rb') as file:
        office_file = msoffcrypto.OfficeFile(file)
        office_file.load_key(password=password)
        office_file.decrypt(decrypted)

    decrypted.seek(0)
    return pd.read_excel(decrypted)


# Durchsuchen des Ordners nach Excel-Dateien
for datei in os.listdir(excel_ordner):
    if datei.endswith('.xlsx') or datei.endswith('.xls'):
        datei_pfad = os.path.join(excel_ordner, datei)

        try:
            # Lesen der passwortgeschützten Excel-Datei
            df = read_protected_excel(datei_pfad, password)

            # Hinzufügen des DataFrames zur Liste
            alle_dfs.append(df)
            print(f"Erfolgreich gelesen: {datei}")
        except Exception as e:
            print(f"Fehler beim Lesen von {datei}: {str(e)}")

# Zusammenführen aller DataFrames
if alle_dfs:
    gesamt_df = pd.concat(alle_dfs, ignore_index=True)

    # Speichern als CSV
    gesamt_df.to_csv(ausgabe_csv, index=False)
    print(f"Alle Excel-Dateien wurden in {ausgabe_csv} zusammengeführt.")
else:
    print("Keine Dateien konnten gelesen werden.")