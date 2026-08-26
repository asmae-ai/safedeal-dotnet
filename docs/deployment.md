# Exploitation

## Sonde d'état

| Route | Couvre | Usage |
|-------|--------|-------|
| `GET /health` | API, PostgreSQL, Redis | Supervision, sortie de rotation |
| `GET /health/live` | Le processus seul | Sonde de vie de l'orchestrateur |

Réponse :

```json
{
  "status": "Healthy",
  "totalDurationMs": 4.2,
  "checks": [
    { "name": "api", "status": "Healthy", "durationMs": 0, "description": "API is running." },
    { "name": "postgres", "status": "Healthy", "durationMs": 3.1, "description": "Database reachable." },
    { "name": "redis", "status": "Healthy", "durationMs": 1.1, "description": "Redis reachable (0 ms)." }
  ]
}
```

Trois états, et ce qu'ils veulent dire :

- **Healthy** — 200. Tout répond.
- **Degraded** — 200. Le service tient, quelque chose manque : Redis injoignable
  (le cache et la révocation immédiate de jetons tombent, le parcours métier
  fonctionne), ou base joignable avec des migrations en attente.
- **Unhealthy** — 503. PostgreSQL est injoignable : l'instance doit sortir de la
  rotation.

Les deux routes sont publiques et ne rendent que des noms, des états et des
durées — ni chaîne de connexion, ni message d'exception, ni version d'assemblage.

Les sondes de dépendance sont plafonnées en durée (`HealthChecks:TimeoutSeconds`,
5 s par défaut) : sans plafond, une base qui ne répond pas laisse la sonde
ouverte jusqu'au délai TCP et l'orchestrateur conclut à un timeout plutôt qu'à
un état.

## Cache

Redis sert de cache de lecture pour les tableaux de bord et les compteurs
d'administration. Rien d'autre : aucune décision — paiement, séquestre,
permission, litige — ne lit le cache.

| Clé | Défaut | Effet |
|-----|--------|-------|
| `Cache:Enabled` | `true` | Coupe entièrement le cache sans redéploiement |
| `Cache:DashboardSeconds` | `60` | Durée de vie d'un tableau de bord utilisateur |
| `Cache:AdminStatsSeconds` | `30` | Durée de vie des compteurs de plateforme |

L'invalidation se fait par portée, via un compteur de génération incrémenté à
chaque écriture concernée : toutes les entrées de la portée deviennent
inatteignables d'un coup, sans balayage de l'espace de clés. Les portées sont
`dash:u:{userId}` (un utilisateur) et `dash:admin` (la plateforme). L'expiration
reste le filet quand une invalidation est perdue.

Une panne de Redis dégrade la latence, pas les réponses : lecture, écriture et
invalidation retombent en silence sur la source de vérité.

## Compression

Brotli puis gzip, au niveau `Fastest` — Brotli au niveau maximal passe des
dizaines de millisecondes sur une réponse de quelques dizaines de kilo-octets
pour un gain marginal, et sur une API la latence coûte plus cher que ces octets.

`Compression:EnableForHttps` vaut `false` par défaut, délibérément : compresser
une réponse chiffrée qui mêle un jeton et une valeur contrôlée par l'appelant
ouvre la voie à BREACH. En production, TLS est terminé par le proxy, la requête
arrive en clair côté API, et la compression s'applique normalement. Ce drapeau
n'est à activer que pour un déploiement qui expose directement l'API en HTTPS et
accepte ce risque.

## Limitation de débit

Les seuils sont ajustables par configuration, section `RateLimiting`, un entier
par politique et par minute : `login` (5), `register` (10), `otp` (3),
`verify-otp` (10), `refresh` (30), `password-reset` (5), `email-verification`
(10), `mutations` (60), `webhooks` (300).

L'identification se fait par compte quand l'utilisateur est connu, par IP sinon :
un attaquant derrière une IP partagée ne doit pas pouvoir bloquer les autres
utilisateurs du même réseau.

## Documentation d'API

`/scalar/v1` et `/openapi/v1.json`, exposés en environnement de développement
uniquement. Le document est généré depuis les contrôleurs : commentaires XML,
attributs d'autorisation et politiques de débit y sont repris automatiquement,
sans annotation parallèle à maintenir.

## Migrations

Appliquées au démarrage. `AddListIndexes` ajoute les index de tri et de filtrage
des listes (transactions par partie et par date, litiges et dossiers d'identité
par statut, notifications par utilisateur et date, comptes par date et rôle).
