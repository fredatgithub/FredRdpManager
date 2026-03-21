# Fred RDP Manager

Application **WPF** en **C#** ciblant **.NET Framework 4.8** pour centraliser et lancer des connexions **Bureau à distance (RDP)** vers des serveurs Windows.

## Fonctionnalités

- **Liste des connexions** dans une colonne à gauche, avec aperçu des détails (serveur, port, domaine, utilisateur) à droite.
- **Ajouter / modifier / supprimer** une connexion via une boîte de dialogue (serveur, port RDP, domaine, utilisateur, mot de passe).
- **Port RDP personnalisable** (par défaut **3389**) ; affichage du port dans la liste lorsqu’il est différent du port standard.
- **Connexion en un clic** : bouton « Se connecter » ou **double-clic** sur une entrée de la liste.
- **Persistance** : les connexions sont enregistrées dans le fichier **`connections.xml`** placé dans le **répertoire de l’application** (dossier de l’exécutable). La sauvegarde a lieu **après chaque ajout, modification ou suppression**, ainsi qu’à la **fermeture** de l’application.
- **Fenêtre principale** : la taille, la position et l’état (normal / agrandi) sont enregistrés dans **`window-layout.xml`** (même dossier) à la fermeture et restaurés au lancement ; la position est ajustée si l’écran ou la résolution ont changé.
- **Mots de passe** : stockés chiffrés avec **DPAPI** (profil Windows utilisateur), pas en clair dans le fichier XML.

## Lancement d’une session

L’application enregistre les identifiants auprès du gestionnaire d’informations d’identification Windows (**`cmdkey`**, cible `TERMSRV/…`) puis lance le client **`mstsc`** avec l’adresse appropriée (`serveur` ou `serveur:port`).

## Compilation

Ouvrir `FredRdpManager.sln` dans Visual Studio et compiler (configuration Debug ou Release). L’exécutable est produit sous `FredRdpManager\bin\<Configuration>\`.

## Remarques

- Si l’application est installée dans un répertoire protégé (par ex. **Program Files**), l’écriture de `connections.xml` et `window-layout.xml` peut nécessiter des droits administrateur ou un autre emplacement de déploiement.
- Pour les adresses **IPv6** avec port non standard, le format `hôte:port` peut nécessiter une syntaxe spécifique (crochets) ; ce cas limite n’est pas géré explicitement.
