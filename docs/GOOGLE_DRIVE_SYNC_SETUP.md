# Google Drive-synkronisering — oppsett

Slik kobler du FemVoice Studio til din egen Google-konto, så treningsdata kan flyttes mellom PC og telefon.

Du gjør dette **én gang**. Det tar ca. 10 minutter.

---

## Hva du får

Sikkerhetskopiene dine lagres i en **skjult app-mappe** i din egen Google Drive (`appDataFolder`):

- Den vises **ikke** når du blar i Drive.
- Den deles ikke med noen.
- Den slettes automatisk hvis du trekker tilbake appens tilgang.
- FemVoice kan ikke se andre filer i Driven din — bare sine egne.

Når du henter ned, blir øktene **slått sammen**, aldri erstattet. Trener du på PC mandag og mobil tirsdag, beholder begge enhetene begge dagene.

---

## Del 1 — Lag din egen OAuth-klient

> **Hvorfor din egen, og ikke book-writers?** Deler du klient, blir begge appene *samme app* i Google-kontoen din: trekker du tilbake tilgangen for den ene, mister den andre den også, og samtykkeskjermen viser feil appnavn. De ville også delt app-mappe. Fem minutter her gir deg separat tilbaketrekking og en egen mappe.

### 1. Åpne Google Cloud Console

Gå til **[console.cloud.google.com](https://console.cloud.google.com)** og logg inn med kontoen du vil lagre til.

Øverst til venstre: velg prosjekt, eller **New Project**. Kall det f.eks. `FemVoice Studio`.

### 2. Slå på Google Drive API

**APIs & Services → Library** → søk `Google Drive API` → **Enable**.

### 3. Sett opp samtykkeskjermen

**APIs & Services → OAuth consent screen**

| Felt | Verdi |
|---|---|
| User type | **External** |
| App name | `FemVoice Studio` |
| User support email | din e-post |
| Developer contact | din e-post |

**Scopes** → **Add or remove scopes** → søk opp og huk av begge:

```
https://www.googleapis.com/auth/drive.appdata
https://www.googleapis.com/auth/userinfo.email
```

> `drive.appdata` er den skjulte app-mappen. `userinfo.email` er kun så appen kan vise *hvilken* konto du er logget inn med.

**Test users** → **Add users** → legg til din egen Google-adresse.

> La appen stå i **Testing**. Da slipper du Googles verifiseringsprosess. Eneste konsekvens: kun kontoene du legger inn som testbrukere kan logge inn — som er akkurat det du vil. Du får en «Google hasn't verified this app»-advarsel ved innlogging; velg **Advanced → Go to FemVoice Studio (unsafe)**. Det er din egen app.

### 4. Lag klienten

**APIs & Services → Credentials → Create credentials → OAuth client ID**

| Felt | Verdi |
|---|---|
| Application type | **Desktop app** |
| Name | `FemVoice Studio Desktop` |

Trykk **Create**. Kopier **Client ID** og **Client secret**.

> «Desktop app» er riktig type her. Google regner desktop-hemmeligheten som ikke-konfidensiell — den ligger uansett inni programmet og kan ikke holdes hemmelig. Derfor bruker flyten i tillegg **PKCE**, som er den støttede måten for installerte apper.

---

## Del 2 — Legg legitimasjonen på plass

Lag fila `google_client.json` i FemVoice sin datamappe:

| Plattform | Sti |
|---|---|
| **Linux** | `~/Documents/FemVoiceStudio/google_client.json` |
| **Windows** | `%USERPROFILE%\Documents\FemVoiceStudio\google_client.json` |
| **macOS** | `~/Documents/FemVoiceStudio/google_client.json` |

Innhold:

```json
{
  "client_id": "1234567890-abcdefg.apps.googleusercontent.com",
  "client_secret": "GOCSPX-din-hemmelighet-her"
}
```

**På Linux, i ett steg:**

```bash
mkdir -p ~/Documents/FemVoiceStudio
cat > ~/Documents/FemVoiceStudio/google_client.json <<'EOF'
{
  "client_id": "LIM_INN_CLIENT_ID",
  "client_secret": "LIM_INN_CLIENT_SECRET"
}
EOF
chmod 600 ~/Documents/FemVoiceStudio/google_client.json
```

> `chmod 600` gjør at bare du kan lese fila.

### Hvorfor en fil, og ikke inne i programmet?

Legitimasjonen leses ved **kjøring**, ikke kompilert inn. Da havner den aldri i kildekoden — heller ikke i en gitignorert fil, som er lett å lime inn i en feilrapport ved et uhell. Og et friskt klon av repoet bygger alltid, uten oppsett.

Finner ikke appen fila, er skysynkronisering rett og slett **av**, og knappene vises ikke. Ingenting kan feile halvveis.

---

## Del 3 — Bruk

1. Start FemVoice Studio.
2. **Innstillinger → Data** → logg inn med Google. Nettleseren åpnes, du godkjenner, og fanen sier at du kan lukke den.
3. **Last opp** på enheten du har trent mest på.
4. På den andre enheten: logg inn med samme konto og **hent ned**.

Øktene blir **slått sammen**. Ingenting overskrives, og å hente ned flere ganger gjør ingen skade — det legges bare til det som mangler.

---

## Feilsøking

| Symptom | Årsak |
|---|---|
| Ingen innloggingsknapp | `google_client.json` mangler, ligger feil sted, eller har feil JSON. Sjekk stien i tabellen over. |
| `invalid_client` | Client ID/secret er feil kopiert, eller klienten er ikke av typen **Desktop app**. |
| `access_denied` | Google-kontoen din er ikke lagt til under **Test users**. |
| `invalid_scope` | `drive.appdata` mangler på samtykkeskjermen (Del 1, steg 3). |
| «Google hasn't verified this app» | Forventet mens appen står i Testing. **Advanced → Go to … (unsafe)**. |
| Ingen økter kom over | Sjekk at du lastet opp fra den andre enheten *først*, og at begge er logget inn på samme konto. |

**Trekke tilbake tilgangen når som helst:** [myaccount.google.com/permissions](https://myaccount.google.com/permissions) → velg FemVoice Studio → **Remove access**. Da slettes også den skjulte app-mappen med sikkerhetskopiene.

---

## Uten sky

Du trenger ikke dette for å flytte data mellom enheter. **Innstillinger → Data** har allerede:

1. **Lag sikkerhetskopi** på enhet A
2. Flytt fila slik du vil (USB, e-post, Drive manuelt)
3. **Slå sammen valgt** på enhet B

Samme fletting, samme resultat — bare uten Google.

---

## Status

- ✅ **Desktop** (Windows / macOS / Linux) — loopback + PKCE
- ⏳ **Android / iOS** — krever egne innloggingsflyter (custom tabs / `ASWebAuthenticationSession`). Ikke implementert ennå; på mobil bruker du fremgangsmåten «uten sky» over.
