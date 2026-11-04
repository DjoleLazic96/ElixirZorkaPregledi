"""Lokalni probni nalozi za proveru prikaza po rolama (samo dev baza, http://localhost:8080).

Pravi `provera.inzenjer`, `provera.supervizor` i `provera.direktor` sa podrazumevanim pravima svoje role; nalog koji već postoji
preskače. Lozinka je ista kao u CLAUDE.md („Dev nalozi“). Pokretanje: python scripts/dev-probni-nalozi.py
"""
import json
import urllib.error
import urllib.request

API = 'http://localhost:8080/api/v1'
ADMIN = ('admin', 'DevTpmPregledi2026!')
LOZINKA = 'DevProvera2026!'
NALOZI = [('provera.inzenjer', 'Provera Inženjer', 'Inzenjer'), ('provera.supervizor', 'Provera Supervizor', 'Supervizor'),
          ('provera.direktor', 'Provera Direktor', 'Direktor')]


def zahtev(metod, putanja, telo=None, token=None):
    r = urllib.request.Request(API + putanja, method=metod, data=json.dumps(telo).encode() if telo is not None else None,
                               headers={'Content-Type': 'application/json', **({'Authorization': f'Bearer {token}'} if token else {})})
    try:
        with urllib.request.urlopen(r) as odg:
            return odg.status, json.loads(odg.read() or b'null')
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()[:300]


_, prijava = zahtev('POST', '/auth/login', {'userName': ADMIN[0], 'password': ADMIN[1]})
token = prijava['accessToken']
for ime, puno, rola in NALOZI:
    status, odg = zahtev('POST', '/korisnici', {'korisnickoIme': ime, 'ime': puno, 'email': f'{ime}@tpm-pregledi.local', 'lozinka': LOZINKA, 'rola': rola}, token)
    print(ime, 'napravljen' if status in (200, 201) else f'preskočen ({status})')
