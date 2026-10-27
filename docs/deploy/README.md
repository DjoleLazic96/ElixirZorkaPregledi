# Deploy dokumentacija — indeks

| Dokument | Tema | Za koga |
|---|---|---|
| [`../../deploy/README.md`](../../deploy/README.md) | operativni postupak: prvi deploy, update, backup, restore, logovi, rollback | programer koji deploy-uje |
| [`server-environment.md`](server-environment.md) | produkcioni host, pravila IT-a, folder, mreže | programer, IT |
| [`pristup.md`](pristup.md) | kako se dolazi do servera (VPN, Jump, SSH, Passbolt) i šta se traži pri onboardingu | novi programer |
| [`sysadmin-tasks.md`](sysadmin-tasks.md) | šta tražimo od IT-a, sa statusom | IT, vlasnik projekta |

```
[dev mašina]  export-images.sh → tar  ──SFTP kroz Jump──►  [/opt/apps/tpm-pregledi]  deploy.sh <tar>
                                                                 │
                                           Traefik (IT) ─► frontend ─► backend ─► db
```
