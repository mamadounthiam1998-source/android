# Guide utilisateur — GHOST ANDROID

## 1. Installation

1. Téléchargez `GhostAndroid-Setup-x.y.z.exe` (artefact du workflow CI ou page des versions).
2. Exécutez l'installateur, acceptez les étapes, choisissez de créer un raccourci Bureau si souhaité.
3. Au premier lancement, aucune connexion Internet n'est requise.

> **Pilotes USB** : sous Windows, installez au besoin le pilote USB du fabricant (Samsung, Xiaomi, etc.) ou le pilote « Google USB Driver ». Sans pilote, l'appareil peut ne pas être détecté.

## 2. Préparer l'appareil Android

1. Ouvrez **Paramètres → À propos du téléphone**, tapez 7 fois sur **Numéro de build** pour activer les options développeur.
2. Dans **Options pour les développeurs**, activez **Débogage USB**.
3. Branchez l'appareil en USB. Sur l'appareil, acceptez l'invite **« Autoriser le débogage USB »**.

## 3. Tableau de bord

- Cliquez sur **« Détecter les appareils »**. Les appareils connectés apparaissent sous forme de cartes.
- Chaque carte indique : mode (ADB/Fastboot/Recovery), marque et modèle, version Android, build, batterie.
- Cliquez une carte pour la **sélectionner** : c'est l'appareil ciblé par tous les modules.

## 4. Console ADB / Fastboot

- Basculez entre **ADB** et **Fastboot**.
- Utilisez les **actions rapides** (devices, getprop, batterie, logcat, reboot…).
- Saisissez une commande libre (ex. `shell pm list packages`) et pressez **Entrée**.
- Les commandes **destructives** (flash, erase, wipe…) déclenchent une **demande de confirmation**.

## 5. Réparation & diagnostic

- Cliquez **« Lancer le diagnostic »**. GHOST ANDROID vérifie l'environnement (outils, pilotes) puis l'appareil (réactivité, démarrage, stockage, batterie).
- Les résultats sont codés par couleur : vert (OK), bleu (info), orange (avertissement), rouge (erreur), gris (non déterminé).
- Les recommandations apparaissent en cyan sous les contrôles concernés.

## 6. Système & applications

- **Charger les informations** : affiche les détails système et le stockage.
- **Lister** : affiche les applications (tierces par défaut). Filtrez par nom.
- **Installer un APK…** : choisissez un fichier `.apk` à installer.
- **Désinstaller la sélection** : désinstalle l'application sélectionnée (confirmation requise).
- **Redémarrages** : normal, recovery, bootloader.

## 7. Sauvegarde & restauration

1. Sélectionnez les **catégories** (Photos, Vidéos, Musique, Documents, Téléchargements…).
2. Saisissez un **mot de passe de chiffrement** (≥ 6 caractères). **Ne le perdez pas** : il est indispensable pour restaurer.
3. Cliquez **« Sauvegarder »** et choisissez le fichier `.ghostbak` de destination.
4. Pour restaurer : **« Restaurer… »**, choisissez l'archive, saisissez le mot de passe, puis le dossier de destination. L'intégrité est vérifiée (SHA-256).
5. **« Vérifier… »** affiche le manifeste (contenu et données non incluses) sans tout extraire.

> **Important** : une sauvegarde de fichiers n'est **pas** une image intégrale de l'appareil. Les SMS, données privées d'applications et partitions système ne sont pas inclus (restrictions Android). La liste exacte est affichée et enregistrée dans chaque sauvegarde.

## 8. Firmware & mise à jour

- **« Identifier l'appareil »** lit l'empreinte firmware, la version, le patch de sécurité, l'état du bootloader.
- Les **conditions de sécurité** (batterie ≥ 50 %, état du bootloader) sont évaluées.
- Le flashage est une opération **guidée et manuelle**, jamais déclenchée par un simple bouton, afin d'éviter tout firmware incompatible.

## 9. Verrous & accès

- **« Diagnostiquer »** affiche en lecture seule : chiffrement du stockage, déverrouillage OEM, et des notes.
- GHOST ANDROID **ne contourne aucun verrou**. En cas d'oubli de code sur un appareil vous appartenant, la seule option est la **réinitialisation d'usine autorisée** (effacement total), confirmée manuellement sur l'appareil en mode Recovery.

## 10. Dépannage

| Problème | Solution |
|----------|----------|
| Appareil non détecté | Vérifiez le câble/port USB, installez le pilote du fabricant, acceptez l'invite de débogage. |
| « Non autorisé » | Acceptez l'invite « Autoriser le débogage USB » sur l'écran de l'appareil. |
| « adb introuvable » | Installez les Android Platform Tools (dans `tools/platform-tools`, le PATH, ou votre SDK). |
| Fastboot ne répond pas | Installez le pilote Fastboot/Bootloader du fabricant. |
| Restauration échoue | Mot de passe incorrect ou fichier altéré (le chiffrement authentifié le détecte). |

## 11. Confidentialité

GHOST ANDROID fonctionne **entièrement hors ligne** : aucune donnée n'est collectée ni envoyée. Les sauvegardes et la base locale (`%AppData%\GhostAndroid`) restent sur votre PC.
