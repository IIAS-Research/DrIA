# DrIA

DrIA accompagne les médecins dans la création de modèles d’intelligence artificielle à partir de leurs données. Son parcours guidé permet d’importer un fichier CSV, d’explorer les données, d’entraîner des modèles, de comprendre leurs résultats et d’essayer des prédictions. Les calculs sont réalisés par le webservice IAML.

## Lancement local

Prérequis : SDK .NET 8, Node.js 18 et PostgreSQL. Une base PostgreSQL doit être disponible, et le webservice IAML doit fonctionner sur `http://localhost:8000`.

Depuis la racine du projet, configurer puis lancer le serveur en remplaçant les valeurs entre chevrons :

```bash
cd Server
dotnet user-secrets set "ConnectionString" "Host=localhost;Database=dr_ia_dev;Username=<utilisateur>;Password=<mot-de-passe>"
dotnet user-secrets set "JwtPrivateKey" "<clé aléatoire d'au moins 32 caractères>"
dotnet user-secrets set "PathMainStorage" "$PWD/Storage"
dotnet user-secrets set "Iaml:BaseUrl" "http://localhost:8000"
dotnet run --launch-profile http
```

Le serveur est accessible sur `http://localhost:3000` et initialise la base au démarrage.

Dans un second terminal, depuis la racine du projet, lancer l’interface :

```bash
cd client
npm ci
npm run dev
```

Ouvrir [http://localhost:9000](http://localhost:9000).

## Compte local

Un compte administrateur est créé automatiquement pour le lancement en développement :

- Adresse : `admin@dria.local`
- Mot de passe : `DrIA-Dev123!`

Si ce compte existe déjà, ses identifiants sont conservés.
