# Contrat d'API

La référence complète, endpoint par endpoint, est générée depuis le code et
servie par Scalar sur `/scalar/v1` (document brut : `/openapi/v1.json`), en
environnement de développement. Ce fichier ne la duplique pas : il fixe les
conventions transverses, celles qu'un document OpenAPI exprime mal.

## Enveloppes

| Cas | Forme |
|-----|-------|
| Lecture simple | `{ "data": ... }` |
| Liste paginée | `{ "data": [...], "meta": { "current_page", "last_page", "total" } }` |
| Écriture | `{ "message": "..." }`, plus `data` quand la ressource modifiée sert au client |
| Erreur | `{ "message": "..." }`, plus `errors` en 422 |

`GET /api/v1/me` fait exception et rend `{ "user": ... }` : contrat historique,
conservé tel quel.

## Codes d'erreur

| Code | Signification |
|------|---------------|
| 400 | Requête malformée : paramètre non numérique, corps illisible |
| 401 | Jeton absent, expiré, ou révoqué par une déconnexion |
| 403 | Rôle insuffisant, ou ressource appartenant à un autre compte |
| 404 | Ressource inexistante — ou masquée à un appelant non autorisé |
| 422 | Validation ou règle métier refusée |
| 429 | Limite de débit atteinte |

Le choix entre 403 et 404 est délibéré : une notification appartenant à autrui
sort en 404, parce que répondre « interdit » confirmerait son existence.

## Pagination

Deux régimes coexistent, pour ne pas casser les écrans qui lisent encore des
listes entières.

**Toujours paginé** — `GET /api/v1/transactions`, `/admin/users`,
`/admin/transactions`, `/admin/audit-logs`. La réponse porte toujours `meta`.

**Paginé à la demande** — `GET /api/v1/notifications`, `/admin/disputes`,
`/admin/identities`. Sans paramètre `page`, la liste complète est rendue et la
réponse ne porte pas `meta`, exactement comme avant. Dès qu'une page est
demandée, la liste est découpée et `meta` apparaît.

Paramètres, communs à toutes les listes :

- `page` — 1 par défaut. Une valeur `< 1` est ramenée à 1 ; une page au-delà du
  total rend une liste vide et un 200, jamais une erreur.
- `per_page` — plafonné à 100, pour qu'aucun client ne puisse exiger la table
  entière en une requête.

`last_page` vaut 1 sur une liste vide, jamais 0 : « page 1 sur 0 » n'a pas de
sens à l'écran.

## Montants

Les montants sortent en chaîne, avec un point décimal et deux décimales
(`"1500.00"`), jamais en nombre flottant.

## Compression

Les réponses sont compressées en Brotli ou gzip selon l'en-tête
`Accept-Encoding`. Un client qui n'en demande pas reçoit exactement ce qu'il
recevait avant. Les binaires déjà compressés (PNG, JPEG, PDF) sont exclus.

## Journal d'audit

`GET /api/v1/admin/audit-logs` — réservé aux administrateurs, toujours paginé,
du plus récent au plus ancien.

Filtres : `action` (nom exact, insensible à la casse), `user_id`, `entity_type`,
`entity_id`, `succeeded`. Un nom d'action inconnu rend une liste vide plutôt
qu'un filtre ignoré — un filtre silencieusement ignoré laisserait croire à un
journal complet.

Le journal ne contient par construction aucun secret : mots de passe, jetons,
codes à usage unique et clés d'API sont masqués avant écriture.
