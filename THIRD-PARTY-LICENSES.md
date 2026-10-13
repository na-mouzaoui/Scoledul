# Licences des composants tiers

Toutes les dépendances de ce projet sont **gratuites**, **open source** et **utilisables à des fins
commerciales**. Aucune dépendance payante, aucun service cloud, aucune télémétrie.

## Bibliothèques de l'application

| Composant | Version | Licence | Usage |
|---|---|---|---|
| CommunityToolkit.Mvvm | 8.4.2 | MIT | Pattern MVVM (observables, commandes) |
| MahApps.Metro | 2.4.11 | MIT | Thème et contrôles modernes (fenêtre, transitions, clair/sombre) |
| MahApps.Metro.IconPacks.Material | 6.2.1 | MIT | Icônes Material Design |
| ControlzEx | (transitif) | MIT | Gestion des thèmes (utilisé par MahApps) |
| Google.OrTools | 9.11.4210 | Apache 2.0 | Solveur de contraintes CP-SAT |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.31 | MIT | ORM et accès SQLite |
| Microsoft.EntityFrameworkCore.Design | 8.0.31 | MIT | Outils de conception / migrations |
| Microsoft.Data.Sqlite | 8.0.31 | MIT | Fournisseur ADO.NET SQLite |
| Microsoft.Extensions.DependencyInjection(.Abstractions) | 8.0.x | MIT | Injection de dépendances |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | MIT | Abstraction de journalisation |
| Serilog | 4.4.0 | Apache 2.0 | Journalisation |
| Serilog.Sinks.File | 7.0.0 | Apache 2.0 | Journal roulant sur fichier |
| PDFsharp-MigraDoc | 6.2.4 | MIT | Génération des exports PDF |
| ClosedXML | 0.104.2 | MIT | Génération des exports Excel |

## Dépendances transitives notables

| Composant | Licence | Remarque |
|---|---|---|
| SQLitePCLRaw.* | Apache 2.0 | Accès natif SQLite |
| DocumentFormat.OpenXml | MIT | Utilisé par ClosedXML |
| SixLabors.ImageSharp | Apache 2.0 | Utilisé par ClosedXML pour les images |
| System.Text.Json / System.* | MIT | Base .NET |

## Outils de développement et de test

| Composant | Version | Licence |
|---|---|---|
| xUnit | 2.9.3 | Apache 2.0 |
| xunit.runner.visualstudio | 3.1.5 | Apache 2.0 |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT |
| coverlet.collector | 6.0.4 | MIT |
| dotnet-ef (outil local) | 8.0.31 | MIT |

## Note importante sur FluentAssertions

Le cahier des charges mentionne **FluentAssertions**. À partir de la version **8.0**, cette bibliothèque
est passée sous une licence commerciale (Xceed) qui **exige une licence payante pour un usage
commercial**, ce qui enfreindrait l'exigence de gratuité (§2 et NFR-14).

Pour rester conforme, le projet utilise **FluentAssertions 7.2.0**, dernière version publiée sous
licence **Apache 2.0**.

## Licence du projet

Le code de cette application est distribué sous licence **MIT** (voir `LICENSE`).
