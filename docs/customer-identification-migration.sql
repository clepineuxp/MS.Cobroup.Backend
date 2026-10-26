START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261024055522_AddCustomerIdentification') THEN
    ALTER TABLE operations.customers ADD "Identification" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261024055522_AddCustomerIdentification') THEN
    UPDATE operations.customers SET "Identification" = '0' WHERE "Identification" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261024055522_AddCustomerIdentification') THEN
    ALTER TABLE operations.customers ALTER COLUMN "Identification" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261024055522_AddCustomerIdentification') THEN
    INSERT INTO operations."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261024055522_AddCustomerIdentification', '10.0.11');
    END IF;
END $EF$;
COMMIT;
