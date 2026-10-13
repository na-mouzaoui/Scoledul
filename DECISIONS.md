# Décisions d'implémentation et interprétations

Ce document consigne les hypothèses retenues en cas d'ambiguïté du cahier des charges
(consigne §14.3), ainsi que les choix techniques structurants.

## Hypothèses métier (§4)

1. **Un enseignant, une seule classe à la fois.** À un créneau donné, un enseignant est affecté à au plus
   une classe (HC-02). Il peut enseigner à plusieurs classes à des créneaux différents. L'option
   facultative « un enseignant n'enseigne qu'à une seule classe dans toute la semaine » (HC-09) est
   désactivée par défaut et activable dans l'écran « Génération ».
2. **Sport = deux séances consécutives.** Deux créneaux adjacents, même jour, même classe, même
   enseignant, sans pause intercalée. Une séance de sport isolée est interdite (HC-06, vérifiée par le
   domaine et les tests). La règle est généralisée à toute matière dont `block_size` ∈ {2,3,4}.
3. **Volume horaire.** Le nombre de séances hebdomadaires par matière et par classe est saisi par
   l'utilisateur (FR-05, écran « Programme »).
4. **Un enseignant = une matière.** Chaque enseignant enseigne la matière qui lui est associée ; une
   matière peut avoir plusieurs enseignants (HC-05).
5. **Une classe, une séance par créneau** (HC-01).
6. **Créneaux de durée uniforme** définie globalement (FR-02).

## Interprétations complémentaires

- **SC-03 (répartition par jour)** est modélisée comme une pénalité sur le nombre de jours où une matière
  apparaît plus d'une fois (`max(0, count_jour − 1)`), complémentaire de SC-02 qui pénalise au-delà de
  deux séances (`max(0, count_jour − 2)`).
- **SC-05 (équilibre des enseignants)** combine la pénalité de trous des enseignants et l'écart
  `|nb_jours × charge_jour − charge_totale|`.
- **SC-06 (sport autour du déjeuner)** : le libellé « déjeuner » n'étant pas identifiable de façon
  certaine, toute pause adjacente à un bloc de sport (juste avant ou juste après) est pénalisée. Option
  activable/désactivable dans l'écran « Génération ».
- **Diagnostic d'infaisabilité (§8.5)** : l'application produit d'abord les messages ciblés
  (capacité de classe, capacité par matière, absence d'enseignant, impossibilité de bloc), puis, en
  dernier recours, un message générique invitant à vérifier indisponibilités et enseignants imposés.
  Les *assumptions* CP-SAT ne sont pas utilisées (approche par vérifications ciblées, plus lisible).
- **Bloc de sport hors horaires** : un bloc ne peut jamais enjamber une pause (AC-04), garanti par la
  génération de créneaux et la contrainte HC-06.
- **Premier jour de la semaine : dimanche.** L'ordre d'affichage des jours est dimanche → samedi
  (`WeekdayExtensions.DisplayOrder` / `DisplayIndex`), dans la configuration, les grilles, les vues par
  jour et les exports. Le stockage en base reste inchangé (1 = lundi … 7 = dimanche, §10).
- **Configuration de la semaine globale.** Une configuration (jours actifs, horaires, pauses, durée des
  créneaux) peut être **mémorisée comme défaut** : elle est alors appliquée automatiquement à tout
  nouveau projet, et un bouton permet de l'**appliquer à tous les projets récents**. Par sécurité, les
  projets contenant déjà un emploi du temps généré sont ignorés (appliquer la configuration remplace les
  créneaux, ce qui supprimerait les séances par cascade).

## Choix techniques

- **Entités du domaine = modèle de persistance.** Les entités de `Timetable.Domain` sont mappées
  directement par EF Core (via `TimetableDbContext`). Cela évite une couche de mapping redondante pour un
  domaine simple ; l'organisation en couches reste respectée (EF n'est référencé que par
  `Timetable.Infrastructure`).
- **Ports dans `Application`.** Les interfaces `ISlotGenerator`, `ITimetableSolver`, `ITimetableRepository`,
  `IExporter`, `ITimetablePrinter`, `IProjectFileService`, `IAppSettingsService` sont définies dans la
  couche `Application` et implémentées dans `Solver`, `Infrastructure` et `Desktop`.
- **Identité des créneaux.** La grille de créneaux est persistée (table `time_slot`) afin que les séances
  référencent des identifiants stables. `SaveWeekAsync` remplace atomiquement pauses et créneaux d'un jour.
- **Enregistrement automatique.** Chaque modification validée déclenche un enregistrement différé
  (anti-rebond de 700 ms) ; l'indicateur `●/○` de la barre d'état signale l'état. Les valeurs et
  l'identifiant de la classe sont figés au moment de la saisie (étape « Programme ») pour éviter toute
  perte si l'on change de classe avant la fin de l'enregistrement.
- **Créneaux générés dès la création.** La grille de créneaux est générée et persistée à la création du
  projet (et régénérée à l'ouverture si un projet n'en contient aucun). Sans créneaux, le solveur
  produirait un emploi du temps vide : c'est la cause du symptôme « emploi du temps vide ».
- **Versions.** Chaque génération réussie crée une nouvelle `timetable_version` et la sélectionne
  (FR-14). Les séances verrouillées de la version courante sont réinjectées dans le solveur (FR-10).
- **Annuler/rétablir.** Pile en mémoire de 100 états (≥ 50 exigés), portée sur la version éditée. La
  pile est réinitialisée à l'entrée dans l'écran « Résultats » (les données sont rechargées depuis la base).
- **Reproductibilité.** La graine aléatoire est transmise au solveur (`random_seed`). La reproductibilité
  stricte n'est garantie qu'à nombre de travailleurs fixé (CP-SAT multi-thread n'est pas déterministe au
  bit près). Le paramètre `num_search_workers` vaut par défaut le nombre de cœurs.
- **Valeur par défaut de la semaine.** Un nouveau projet active lundi→vendredi, 08:00–16:00, créneaux de
  60 min, sans pause, avec 8 matières pré-remplies (dont Sport en bloc de 2) et les poids SC-01…SC-06.
- **Localisation.** Les textes de l'interface sont en français ; les ressources `.resx` (`Strings.resx`
  neutre/français et `Strings.en.resx`) et `LocalizationService` préparent d'autres langues (NFR-13). Une
  partie des libellés est écrite directement en français dans les vues XAML ; l'architecture i18n est en place.
- **Impression.** L'impression directe utilise `PrintDialog` et un `FlowDocument` construit à partir des
  grilles (une section par classe/enseignant).
- **PDFsharp 6.** `GlobalFontSettings.UseWindowsFontsUnderWindows = true` est activé afin d'utiliser les
  polices système sous Windows.
- **FluentAssertions.** Version 7.2.0 retenue (Apache 2.0) car les versions ≥ 8 sont sous licence
  commerciale Xceed, incompatible avec l'exigence de gratuité (voir `THIRD-PARTY-LICENSES.md`).
- **Interface moderne.** Le style de l'application s'appuie sur **MahApps.Metro** (MIT) et
  **MahApps.Metro.IconPacks.Material** (MIT) : fenêtre `MetroWindow` avec barre de titre moderne, thème
  clair/sombre basculable (via ControlzEx), transitions animées entre les étapes
  (`TransitioningContentControl`), navigation latérale avec icônes, cartes arrondies et restylage des
  contrôles. Le contenu des étapes est encapsulé dans un `ScrollViewer` vertical.

## Écarts et limites connus

- La couverture de tests cible (≥ 80 % sur Domain et Solver) est approchée par des tests unitaires et
  d'acceptation ciblés ; la couverture n'est pas mesurée automatiquement dans ce livrable
  (`coverlet` est présent pour une mesure ultérieure).
- L'installateur Inno Setup (`installer/TimetableApp.iss`) est fourni mais non compilé dans cet
  environnement (nécessite Inno Setup sous Windows).
- Les traductions autres que le français ne couvrent que les titres d'étapes et les commandes
  principales.
