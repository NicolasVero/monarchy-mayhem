<div align="center">
  <img src="site/public/logo.png" alt="Logo de Monarchy Mayhem" width="420" />

  <h1>Monarchy Mayhem</h1>

  <p>
    <strong>Le royaume vous hait. Reprenez-le.</strong><br />
    Un jeu d'action-aventure médiéval où chaque mauvaise décision vous rapproche du trône.
  </p>

  <p>
    <img src="https://img.shields.io/badge/Unity-6.5-000000?style=for-the-badge&logo=unity&logoColor=white" alt="Unity 6.5" />
    <img src="https://img.shields.io/badge/plateforme-Windows-0078D4?style=for-the-badge&logo=windows&logoColor=white" alt="Plateforme Windows" />
    <img src="https://img.shields.io/badge/statut-en%20développement-D6A84B?style=for-the-badge" alt="En développement" />
  </p>

  <p>
    <a href="https://monarchy-mayhem.nex0d.chatgpt.site/"><strong>👑 Visiter le site officiel</strong></a>
  </p>
</div>

![Le roi Makram face à son royaume en révolte](site/public/hero-monarchy.png)

## À propos

Dans **Monarchy Mayhem**, vous incarnez le roi Makram, un monarque déchu bien décidé à récupérer sa couronne. Traversez un royaume en pleine révolte, affrontez des hordes de paysans et prêtez l'oreille à des conseillers dont les plans sont rarement aussi brillants qu'ils le prétendent.

La route vers le château mêle combats en temps réel, progression, exploration et quêtes délicieusement absurdes. Après tout, reconquérir un royaume n'a jamais garanti de conserver sa dignité.

## Au programme

- ⚔️ **Combat royal** — frappez, bloquez, esquivez et adaptez votre style à chaque arme.
- 📈 **Progression sur mesure** — gagnez des niveaux, choisissez vos améliorations et façonnez les statistiques de Makram.
- 🗺️ **Royaume à reconquérir** — traversez plusieurs régions, du village révolté jusqu'au château.
- 📜 **Quêtes décalées** — retrouvez des parchemins, aidez un médecin douteux et dansez si la cour l'exige.
- 👥 **Personnages hauts en couleur** — croisez Armand le prêtre, Docteur Lapeste, Foufou le fou et bien d'autres.

## Aperçu

<div align="center">
  <img src="site/public/gallery-battle.png" alt="Bataille dans Monarchy Mayhem" width="900" />
</div>

## Lancer le projet Unity

### Prérequis

- [Unity Hub](https://unity.com/download)
- Unity **6000.5.6f1**
- Git

### Installation

```bash
git clone https://github.com/NicolasVero/monarchy-mayhem.git
cd monarchy-mayhem
```

Ajoutez ensuite le dossier du projet dans Unity Hub, ouvrez-le avec Unity `6000.5.6f1`, puis lancez la scène `Assets/Scenes/Introduction.unity`.

> La première ouverture peut prendre quelques minutes : Unity doit importer les ressources et restaurer les paquets du projet.

## Lancer le site vitrine

Le site officiel est développé avec React, TypeScript et vinext.

### Prérequis

- Node.js **22.13.0** ou plus récent
- npm

```bash
cd site
npm install
npm run dev
```

Commandes utiles :

```bash
npm run build   # Générer une version de production
npm test        # Construire et exécuter les tests
npm run lint    # Vérifier la qualité du code
```

## Structure du dépôt

```text
monarchy-mayhem/
├── Assets/             # Scènes, scripts, modèles et ressources Unity
├── Packages/           # Dépendances Unity
├── ProjectSettings/    # Configuration du projet Unity
└── site/               # Site vitrine React / TypeScript
```

## Découvrir le royaume

Monarchy Mayhem est actuellement en développement pour Windows.

**[Rendez-vous sur le site officiel pour découvrir l'univers du jeu →](https://monarchy-mayhem.nex0d.chatgpt.site/)**

<div align="center">
  <sub>Un royaume en ruine. Un roi très motivé.</sub>
</div>
