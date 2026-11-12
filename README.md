<div align="center">

<img src="src/GhostAndroid.App/Assets/ghost-logo.png" width="120" alt="GHOST ANDROID" />

# GHOST ANDROID

**Logiciel Windows professionnel de gestion, diagnostic, réparation et récupération Android.**

Interface futuriste · Moteurs ADB/Fastboot réels · Sauvegardes chiffrées AES-256-GCM · 100 % hors ligne

[![build](https://github.com/mamadounthiam1998-source/android/actions/workflows/build.yml/badge.svg)](https://github.com/mamadounthiam1998-source/android/actions/workflows/build.yml)

</div>

---

## ⚠️ À lire avant tout : honnêteté sur l'état du projet

Ce dépôt contient un **vrai logiciel fonctionnel**, pas une maquette. Mais c'est une **base solide (phase 1)**, livrée de façon transparente :

| Composant | État | Vérifiable |
|-----------|------|-----------|
| **Cœur multiplateforme** (`GhostAndroid.Core`) — moteurs ADB/Fastboot, détection, chiffrement, SQLite, diagnostic | ✅ Fonctionnel | ✔️ compilé + **testé en CI** (Linux) |
| **Tests unitaires réels** (crypto, parsing, sauvegarde, garde-fou) | ✅ Passants | ✔️ exécutés en CI |
| **Interface WPF** (`GhostAndroid.App`) — 7 modules câblés aux moteurs | ✅ Code complet | ✔️ **compilée en CI** (Windows) |
| **Installateur** Inno Setup | ✅ Script complet | ✔️ construit en CI (Windows) |
| **Flashage de firmware / déverrouillage de verrou** | 🚫 **Volontairement non automatisé** | — |

> **Aucun bouton fictif.** Chaque fonction s'appuie sur un moteur réel, **ou** est clairement signalée comme indisponible/assistée. Conformément aux principes de sécurité, GHOST ANDROID **ne contourne pas** les verrous d'écran ni les protections du fabricant (FRP), et **ne flashe pas** de firmware via un simple bouton.

La **preuve de build** est le workflow GitHub Actions `build` : s'il est vert, la solution compile réellement sur Windows et les tests passent.

---

## ✨ Fonctionnalités

- **Tableau de bord** — détection animée des appareils, marque/modèle, version Android, build, batterie, mode de connexion (ADB/Fastboot/Recovery), état des outils.
- **Console ADB / Fastboot** — exécution réelle de commandes, actions rapides, **garde-fou** qui exige une confirmation avant toute commande destructive.
- **Réparation & diagnostic** — contrôles réels : présence des outils, réactivité, état de démarrage (détection de blocage/boucle), stockage, batterie, Fastboot.
- **Système & applications** — informations système, liste/installation/désinstallation d'APK, stockage, redémarrages (normal, recovery, bootloader).
- **Sauvegarde & restauration** — `adb pull` des données accessibles → archive **chiffrée AES-256-GCM** (clé dérivée PBKDF2-SHA256), manifeste honnête des données **non** incluses, **vérification d'intégrité SHA-256**, historique SQLite.
- **Firmware & mise à jour** — identification de l'appareil, lecture du firmware, évaluation des conditions de sécurité (batterie, bootloader). Opérations de flash **guidées, jamais automatiques**.
- **Verrous & accès** — diagnostic de sécurité **en lecture seule** (chiffrement, OEM unlock). Pas de contournement. Reprise d'accès uniquement par réinitialisation d'usine **autorisée** (effacement total, confirmé sur l'appareil).

## 🏛️ Architecture

```
GhostAndroid.sln
├── src/
│   ├── GhostAndroid.Core/          (net8.0  — multiplateforme, testable)
│   │   ├── Infrastructure/         ProcessRunner (exéc. robuste de processus)
│   │   ├── PlatformTools/          localisation adb/fastboot
│   │   ├── Adb/                    AdbEngine, FastbootEngine, parseurs purs
│   │   ├── Devices/                détection, enrichissement, moniteur
│   │   ├── Diagnostics/            DiagnosticService
│   │   ├── Backup/                 BackupService, RestoreService
│   │   ├── Security/               GcmFileCrypto (AES-256-GCM), LockDiagnostic
│   │   ├── Storage/                HistoryRepository (SQLite)
│   │   ├── Commands/               CommandGuard (classification du risque)
│   │   └── Models/
│   └── GhostAndroid.App/           (net8.0-windows — WPF, MVVM)
│       ├── Themes/                 thème sombre futuriste
│       ├── ViewModels/             un VM par module
│       ├── Views/                  un UserControl par module
│       └── Services/               dialogues
├── tests/GhostAndroid.Core.Tests/  (xUnit — tests réels)
├── installer/                      Inno Setup
└── .github/workflows/build.yml     CI : build Windows + tests
```

Séparation stricte **UI ↔ moteurs techniques**. Le cœur ne dépend d'aucune API Windows : il compile et se teste partout.

## 🚀 Compilation & exécution

### Prérequis
- Windows 10/11, **.NET 8 SDK** (compatible .NET 10 : remplacez `net8.0-windows` par `net10.0-windows`).
- [Android Platform Tools](https://developer.android.com/tools/releases/platform-tools) (adb/fastboot) : placez-les dans `src/GhostAndroid.App/tools/platform-tools/`, ou dans le `PATH`, ou laissez l'app utiliser votre SDK Android.

### Commandes
```bash
# Restaurer et compiler toute la solution
dotnet build GhostAndroid.sln -c Release

# Lancer les tests réels (fonctionne aussi sous Linux/macOS)
dotnet test tests/GhostAndroid.Core.Tests/GhostAndroid.Core.Tests.csproj

# Lancer l'application (Windows)
dotnet run --project src/GhostAndroid.App/GhostAndroid.App.csproj

# Publier + construire l'installateur (Windows, voir le workflow CI)
dotnet publish src/GhostAndroid.App/GhostAndroid.App.csproj -c Release -r win-x64 --self-contained false -o artifacts/app
ISCC /DAppVersion=0.1.0 /DSourceDir=..\artifacts\app installer\GhostAndroid.iss
```

## 🔐 Sécurité & vie privée

- **Aucune collecte de données**, aucun accès distant, aucun envoi réseau. Tout est local.
- Sauvegardes chiffrées **AES-256-GCM** (authentifié) ; clé dérivée par **PBKDF2-HMAC-SHA256** (210 000 itérations).
- Protection anti-traversée de chemin à la restauration (anti « zip-slip »).
- Commandes destructives classées et confirmées explicitement.
- **Usage responsable** : n'utilisez ce logiciel que sur des appareils vous appartenant ou pour lesquels vous disposez d'une autorisation explicite. GHOST ANDROID ne contourne aucune protection de sécurité.

## 🗺️ Feuille de route (phases suivantes)
- Assistant de flash firmware guidé (téléchargement depuis sources officielles, vérification de signatures/sommes de contrôle).
- Export VCF des contacts via intent officiel.
- Détection avancée de pilotes Windows et assistant d'installation.
- Mode multi-appareils avec opérations parallèles contrôlées.

## 📄 Licence
Voir [`LICENSE`](LICENSE). Marques et noms de fabricants cités à titre de compatibilité uniquement ; GHOST ANDROID n'est affilié à aucun d'eux.
