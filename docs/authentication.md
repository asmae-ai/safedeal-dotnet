# Authentification — SafeDeal API

## Comment ça fonctionne

SafeDeal utilise des **tokens JWT Bearer** pour l'authentification.

Quand un utilisateur s'inscrit ou se connecte, l'API retourne un token.
Ce token doit être envoyé dans chaque requête protégée via le header :

```
Authorization: Bearer <votre_token_ici>
```

Les tokens expirent après **24 heures**. Après expiration, l'utilisateur doit se reconnecter.

---

## Flux complet d'un utilisateur

```
1. L'utilisateur s'inscrit
   → reçoit un token JWT + un email de vérification

2. L'utilisateur vérifie son email avec le code reçu
   → obligatoire avant de pouvoir se connecter

3. L'utilisateur se connecte
   → reçoit un nouveau token JWT

4. L'utilisateur utilise le token pour accéder aux endpoints protégés

5. L'utilisateur se déconnecte
   → le token est blacklisté (ne peut plus être réutilisé)
```

---

## Règles de sécurité

| Règle | Détail |
|-------|--------|
| Email obligatoirement vérifié | L'utilisateur ne peut pas se connecter sans vérifier son email |
| Rate limit login | Bloqué après 5 tentatives échouées par minute |
| Rate limit register | Bloqué après 10 tentatives par minute |
| Rate limit OTP | Bloqué après 3 demandes par minute |
| Expiration OTP | Tous les codes OTP expirent après 10 minutes |
| OTP usage unique | Un code OTP ne peut pas être réutilisé après validation |
| Blacklist token | Le token est invalidé immédiatement à la déconnexion |
| Reset password | Le lien de réinitialisation expire après 10 minutes |

---

## Endpoints

---

### 1. Inscription

```
POST /api/v1/register
```

Crée un nouveau compte utilisateur.
Après l'inscription, un code de vérification est envoyé automatiquement par email.
L'utilisateur **ne peut pas se connecter** tant que son email n'est pas vérifié.

**Rôles disponibles :** `vendor` (vendeur) ou `buyer` (acheteur)

**Body**
```json
{
  "name": "Jean Dupont",
  "email": "jean@example.com",
  "password": "motdepasse123",
  "passwordConfirmation": "motdepasse123",
  "role": "vendor"
}
```

**Réponse 200**
```json
{
  "token": "eyJhbGci...",
  "user": {
    "id": 1,
    "name": "Jean Dupont",
    "email": "jean@example.com",
    "role": "vendor",
    "phone": null,
    "identity_status": "notsubmitted",
    "reputation_score": "0.00",
    "created_at": "2026-08-05T12:00:00Z"
  }
}
```

**Erreurs**
| Code | Message | Raison |
|------|---------|--------|
| 422 | The email has already been taken. | Email déjà utilisé |
| 422 | Role must be 'vendor' or 'buyer'. | Rôle invalide |
| 429 | Too many attempts. Try again later. | Trop de tentatives |

---

### 2. Connexion

```
POST /api/v1/login
```

Connecte un utilisateur et retourne un token JWT.
L'email doit être vérifié avant de pouvoir se connecter.

**Body**
```json
{
  "email": "jean@example.com",
  "password": "motdepasse123"
}
```

**Réponse 200**
```json
{
  "token": "eyJhbGci...",
  "user": {
    "id": 1,
    "name": "Jean Dupont",
    "email": "jean@example.com",
    "role": "vendor",
    "phone": null,
    "identity_status": "notsubmitted",
    "reputation_score": "0.00",
    "created_at": "2026-08-05T12:00:00Z"
  }
}
```

**Erreurs**
| Code | Message | Raison |
|------|---------|--------|
| 401 | Invalid credentials. | Email ou mot de passe incorrect |
| 403 | Please verify your email before logging in. | Email non vérifié |
| 429 | Too many attempts. Try again later. | Bloqué après 5 tentatives |

---

### 3. Déconnexion

```
POST /api/v1/logout
🔒 Authorization: Bearer <token>
```

Invalide le token JWT. Le token ne peut plus être utilisé après cette action.

**Réponse 200**
```json
{ "message": "Logged out successfully." }
```

---

### 4. Profil utilisateur connecté

```
GET /api/v1/me
🔒 Authorization: Bearer <token>
```

Retourne les informations de l'utilisateur connecté.

**Réponse 200**
```json
{
  "user": {
    "id": 1,
    "name": "Jean Dupont",
    "email": "jean@example.com",
    "role": "vendor",
    "phone": null,
    "identity_status": "notsubmitted",
    "reputation_score": "0.00",
    "created_at": "2026-08-05T12:00:00Z"
  }
}
```

---

### 5. Vérification de l'email

```
POST /api/v1/auth/email/verify
🔒 Authorization: Bearer <token>
```

Vérifie l'email avec le code reçu après l'inscription.

**Body**
```json
{ "code": "904020" }
```

**Réponse 200**
```json
{ "message": "Email verified successfully." }
```

**Erreurs**
| Code | Message | Raison |
|------|---------|--------|
| 422 | Invalid or expired verification code. | Code invalide ou expiré |

---

### 6. Renvoyer le code de vérification

```
POST /api/v1/auth/email/resend
🔒 Authorization: Bearer <token>
```

Renvoie un nouveau code de vérification par email.

**Réponse 200**
```json
{ "message": "Verification code sent." }
```

**Erreurs**
| Code | Message | Raison |
|------|---------|--------|
| 422 | Email is already verified. | Email déjà vérifié |

---

### 7. Envoyer un code 2FA

```
POST /api/v1/auth/2fa/send
🔒 Authorization: Bearer <token>
```

Envoie un code OTP par email pour la double authentification.

**Réponse 200**
```json
{ "message": "OTP sent." }
```

**Erreurs**
| Code | Message | Raison |
|------|---------|--------|
| 422 | Please wait before requesting a new OTP. | Cooldown actif |
| 429 | Too many attempts. Try again later. | Trop de demandes |

---

### 8. Vérifier le code 2FA

```
POST /api/v1/verify-2fa
🔒 Authorization: Bearer <token>
```

Vérifie le code OTP reçu par email.

**Body**
```json
{ "code": "622935" }
```

**Réponse 200**
```json
{ "message": "2FA verified." }
```

**Erreurs**
| Code | Message | Raison |
|------|---------|--------|
| 422 | Invalid or expired OTP code. | Code invalide ou expiré |

---

### 9. Mot de passe oublié

```
POST /api/v1/forgot-password
```

Envoie un lien de réinitialisation par email.

> ⚠️ La réponse est identique que l'email existe ou non (sécurité).

**Body**
```json
{ "email": "jean@example.com" }
```

**Réponse 200**
```json
{ "message": "If this email exists, a reset link has been sent." }
```

**Lien reçu dans l'email :**
```
http://localhost:5173/reset-password?token=544278&email=jean@example.com
```

---

### 10. Réinitialisation du mot de passe

```
POST /api/v1/reset-password
```

Réinitialise le mot de passe avec le token reçu par email.

**Body**
```json
{
  "email": "jean@example.com",
  "token": "544278",
  "password": "NouveauMotDePasse123",
  "passwordConfirmation": "NouveauMotDePasse123"
}
```

**Réponse 200**
```json
{ "message": "Password reset successfully." }
```

**Erreurs**
| Code | Message | Raison |
|------|---------|--------|
| 404 | User not found. | Email inconnu |
| 422 | Invalid or expired reset token. | Token invalide ou expiré |

---

## Format standard des erreurs

**Erreur de validation (422)**
```json
{
  "message": "Validation failed.",
  "errors": {
    "email": ["The email has already been taken."],
    "password": ["Minimum 8 characters required."]
  }
}
```

**Non authentifié (401)**
```json
{ "message": "Unauthenticated." }
```

**Accès refusé (403)**
```json
{ "message": "Forbidden." }
```

**Trop de tentatives (429)**
```json
{ "message": "Too many attempts. Try again later." }
```