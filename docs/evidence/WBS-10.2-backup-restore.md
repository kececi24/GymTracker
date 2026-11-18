# WBS-10.2: Database Backup and Restore Rehearsal

**Date performed:** 2026-11-18  
**Result:** PASS

## Environment

- Docker Compose services: GymTracker API and PostgreSQL 17
- Restore target: a separate PostgreSQL 17 container with an empty `gymtracker_restore` database
- Backup format: PostgreSQL custom-format dump (`pg_dump -Fc`)

## Procedure and result

1. Started the API and database with `docker compose up --build -d`. The PostgreSQL service reported healthy and the API started.
2. Confirmed the source database had 20 seeded exercises.
3. Created a custom-format dump from the source database and copied it to the local machine.
4. Started a separate PostgreSQL 17 container, copied the dump into it, and restored it with `pg_restore`. The restore completed without errors.
5. Compared row counts between the source and restored databases. All counts matched:

| Table | Source rows | Restored rows | Match |
|---|---:|---:|---|
| `user_accounts` | 0 | 0 | Yes |
| `exercises` | 20 | 20 | Yes |
| `program_templates` | 3 | 3 | Yes |
| `program_template_exercises` | 12 | 12 | Yes |
| `training_programs` | 0 | 0 | Yes |
| `program_versions` | 0 | 0 | Yes |
| `program_version_exercises` | 0 | 0 | Yes |

6. Compared a sample exercise record. Both databases returned `barbell-back-squat|Barbell Back Squat`.
7. Removed the temporary restore container and local dump file. The original Compose database was left intact.

## Notes

The rehearsal database contained seeded catalogue data but no user accounts or user-created programs. This rehearsal verified the backup and restore of the current schema and seeded records; it did not exercise restoration of user-generated records or a cloud-hosted database.

The database dump was a temporary local artifact and is not included in the repository.
