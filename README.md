# Générateur d'emplois du temps scolaires

Application desktop **Windows** (C# 12 / .NET 8 / WPF) qui produit automatiquement l'emploi du temps
hebdomadaire d'un établissement scolaire : configuration de la semaine, classes, matières, enseignants,
programme, génération par solveur de contraintes (Google OR-Tools CP-SAT), édition manuelle, export
(PDF / Excel / CSV) et impression.

L'application fonctionne **hors ligne**, sur un seul poste, sans serveur, sans télémétrie. Chaque projet
est un fichier SQLite `.edt`.

L'interface s'appuie sur **MahApps.Metro** (thème moderne clair/sombre, transitions animées, contrôles
restylés) et **MahApps.Metro.IconPacks** (icônes). La semaine commence le **dimanche**, et la
configuration de la semaine (jours, horaires, pauses, durée) peut être mémorisée comme configuration
globale appliquée à tous les projets.

## Sommaire

- [Fonctionnalités](#fonctionnalités)
- [Architecture](#architecture)
- [Prérequis](#prérequis)
- [Compilation et exécution](#compilation-et-exécution)
- [Tests](#tests)
- [Publication (exécutable unique)](#publication-exécutable-unique)
- [Structure de la base de données](#structure-de-la-base-de-données)
- [Solveur](#solveur)
- [Raccourcis](#raccourcis)
- [Licences](#licences)

## Fonctionnalités

| Domaine | Détail | Exigence |
|---|---|---|
| Projets | Nouveau / Ouvrir / Enregistrer / Enregistrer sous / Dupliquer / projets récents ; enregistrement automatique ; fichier `.edt` SQLite | FR-01 |
| Semaine | Jours actifs, horaires d'ouverture par jour, durée de créneau (30/45/50/55/60/90 ou 20–180), aperçu de la grille en direct | FR-02, FR-04 |
| Pauses | Ajout / modification / suppression par jour, « appliquer à tous les jours », validation des chevauchements | FR-03 |
| Classes | Nombre de classes (1–60), noms par défaut, renommage, unicité, suppression avec confirmation | FR-05 |
| Matières | Nom, couleur, taille de bloc (1–4 ; sport = 2), matières pré-remplies supprimables | FR-06 |
| Enseignants | Nom, prénom, matière (obligatoires), unicité, recherche, indisponibilités cliquables | FR-07 |
| Programme | Tableau matière → séances/semaine par classe, enseignant imposé optionnel, indicateur de charge, copie vers d'autres classes | FR-08 |
| Pré-validation | Rapport bloquant / avertissement avant génération | FR-09 |
| Génération | Solveur CP-SAT en tâche de fond, barre de progression, annulation, durée maximale, graine, poids SC-01…SC-06 | FR-10 |
| Résultats | Vues par classe / enseignant / jour, statistiques, panneau de conflits | FR-11 |
| Édition manuelle | Glisser-déposer, échange, contrôle en temps réel, verrouillage, annuler/rétablir (100 niveaux) | FR-12 |
| Exports | PDF (paysage), Excel, CSV, impression directe | FR-13 |
| Versions | Plusieurs scénarios par projet, sélection de la version retenue | FR-14 |
| Paramètres | Langue, thème clair/sombre, dossier par défaut, sauvegardes rotatives | FR-15 |

## Architecture

```
TimetableApp.sln
├─ src/
│  ├─ Timetable.Domain/         Entités, value objects, règles métier, validation (aucune dépendance)
│  ├─ Timetable.Application/    Ports (interfaces), DTO, services : SlotGenerator, PreCheckService,
│  │                            GenerationService, TimetableStatisticsService
│  ├─ Timetable.Solver/         Modèle CP-SAT (Google.OrTools), diagnostic d'infaisabilité
│  ├─ Timetable.Infrastructure/ EF Core 8 + SQLite, migrations, dépôts, export PDF/Excel/CSV, projets .edt
│  └─ Timetable.Desktop/        WPF, MVVM CommunityToolkit, vues, DI, Serilog
└─ tests/
   ├─ Timetable.Domain.Tests/        Générateur de créneaux, validateur de contraintes dures
   ├─ Timetable.Application.Tests/   Pré-validation, persistance (aller-retour), exports
   ├─ Timetable.Solver.Tests/        Critères d'acceptation AC-01…AC-06
   └─ Timetable.Testing/             Jeu de données de référence partagé (§12)
```

Principes : dépendances orientées vers le `Domain` ; les ports (`ISlotGenerator`, `ITimetableSolver`,
`ITimetableRepository`, `IExporter`, `IProjectFileService`) sont définis dans `Application` et implémentés
dans `Solver`, `Infrastructure` et `Desktop`. Les ViewModels n'ont aucune référence aux vues ; les
commandes longues sont asynchrones et annulables.

## Prérequis

- **Windows 10 ou 11 (x64)** pour exécuter l'application WPF.
- **.NET SDK 8.0** (ou plus récent) pour compiler. Le SDK 8 peut construire la solution cible `net8.0`.
- Connexion Internet uniquement pour restaurer les packages NuGet lors de la première compilation.

## Compilation et exécution

```powershell
# Restauration + compilation
dotnet build TimetableApp.sln -c Release

# Exécution de l'application
dotnet run --project src/Timetable.Desktop -c Release
```

> Si seul un runtime .NET plus récent est installé (par ex. .NET 10 sans runtime 8.0), lancez avec
> `$env:DOTNET_ROLL_FORWARD = "Major"` pour exécuter l'application cible `net8.0` sur le runtime disponible.

## Tests

```powershell
dotnet test TimetableApp.sln
```

Les tests couvrent notamment :

- le générateur de créneaux (pauses, temps résiduel, adjacence, blocs) ;
- la validation des contraintes dures HC-01 à HC-09 (classe, enseignant, matière, blocs sport, indisponibilités) ;
- la pré-validation (capacités, matières sans enseignant, blocs) ;
- l'aller-retour de persistance d'un fichier `.edt` (AC-08) ;
- la génération sur le jeu de référence A : solution trouvée < 10 s, zéro violation dure, sport en blocs
  adjacents, infaisabilité diagnostiquée (AC-01…AC-06) ;
- les exports PDF, Excel et CSV.

## Publication (exécutable unique)

```powershell
dotnet publish src/Timetable.Desktop/Timetable.Desktop.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish
```

Le dossier `publish` contient `TimetableApp.exe`, lançable sur un Windows 10/11 vierge **sans installer .NET**.
Un script d'installation **Inno Setup** est fourni dans `installer/TimetableApp.iss`.

## Structure de la base de données

Chaque projet `.edt` est un fichier SQLite (`PRAGMA foreign_keys = ON`, `journal_mode = WAL`). Les heures
sont stockées en minutes depuis minuit ; les jours vont de 1 (lundi) à 7 (dimanche). Tables :

`project_setting`, `school_day`, `break_period`, `time_slot`, `school_class`, `subject`, `teacher`,
`teacher_unavailability`, `class_subject_requirement`, `timetable_version`, `lesson`, `generation_preference`.

Un déclencheur `trg_lesson_teacher_subject` garantit en base que l'enseignant d'une séance enseigne bien la
matière (HC-05), en complément des contrôles applicatifs.

## Solveur

Le modèle CP-SAT (`Timetable.Solver`) crée une variable booléenne par affectation classe × matière ×
enseignant × créneau (`x[c,s,t,l]`) et une variable de bloc pour les matières à séances groupées
(sport). Les contraintes dures HC-01 à HC-09 sont imposées exactement ; les contraintes souples
SC-01 à SC-06 sont pondérées dans la fonction objectif (curseurs 0–10). Les séances verrouillées sont
réinjectées lors d'une régénération.

## Raccourcis

- **Ctrl+Z / Ctrl+Y** : annuler / rétablir une modification de l'emploi du temps (vue « Résultats »).
- **Double-clic** sur une séance : verrouiller / déverrouiller.
- **Glisser-déposer** une séance vers un autre créneau (les blocs sport se déplacent en bloc).

## Licences

Toutes les dépendances sont libres et utilisables commercialement (MIT, Apache 2.0). Voir
[`THIRD-PARTY-LICENSES.md`](THIRD-PARTY-LICENSES.md). Les décisions d'implémentation et interprétations
du cahier des charges sont consignées dans [`DECISIONS.md`](DECISIONS.md).
