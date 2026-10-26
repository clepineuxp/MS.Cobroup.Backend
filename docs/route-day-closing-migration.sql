DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'operations') THEN
        CREATE SCHEMA operations;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS operations."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021181821_AddCustomerManagement') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'operations') THEN
            CREATE SCHEMA operations;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021181821_AddCustomerManagement') THEN
    CREATE TABLE operations.customers (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "RouteId" uuid NOT NULL,
        "FirstNames" character varying(120) NOT NULL,
        "LastNames" character varying(120) NOT NULL,
        "BusinessOrWorkName" character varying(160),
        "Observations" character varying(1000),
        "Status" character varying(16) NOT NULL,
        "CreatedByUserId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_customers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021181821_AddCustomerManagement') THEN
    CREATE TABLE operations.customer_locations (
        "Id" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "Latitude" numeric(9,6) NOT NULL,
        "Longitude" numeric(9,6) NOT NULL,
        "Alias" character varying(120),
        "Address" character varying(300),
        "IsPrimary" boolean NOT NULL,
        "Source" character varying(16) NOT NULL,
        CONSTRAINT "PK_customer_locations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_customer_locations_customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES operations.customers ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021181821_AddCustomerManagement') THEN
    CREATE TABLE operations.customer_phones (
        "Id" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "Number" character varying(21) NOT NULL,
        "IsPrimary" boolean NOT NULL,
        CONSTRAINT "PK_customer_phones" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_customer_phones_customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES operations.customers ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021181821_AddCustomerManagement') THEN
    CREATE INDEX "IX_customer_locations_CustomerId" ON operations.customer_locations ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021181821_AddCustomerManagement') THEN
    CREATE UNIQUE INDEX "IX_customer_phones_CustomerId_Number" ON operations.customer_phones ("CustomerId", "Number");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021181821_AddCustomerManagement') THEN
    CREATE INDEX "IX_customers_TenantId_LastNames_FirstNames" ON operations.customers ("TenantId", "LastNames", "FirstNames");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021181821_AddCustomerManagement') THEN
    CREATE INDEX "IX_customers_TenantId_RouteId_Status" ON operations.customers ("TenantId", "RouteId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021181821_AddCustomerManagement') THEN
    INSERT INTO operations."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021181821_AddCustomerManagement', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021183855_AddCustomerAudit') THEN
    ALTER TABLE operations.customers ADD "UpdatedByUserId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021183855_AddCustomerAudit') THEN
    CREATE TABLE operations.customer_audit (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "ActorUserId" uuid NOT NULL,
        "Action" character varying(40) NOT NULL,
        "DetailsJson" jsonb NOT NULL,
        "OccurredAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_customer_audit" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_customer_audit_customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES operations.customers ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021183855_AddCustomerAudit') THEN
    CREATE INDEX "IX_customer_audit_CustomerId" ON operations.customer_audit ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021183855_AddCustomerAudit') THEN
    CREATE INDEX "IX_customer_audit_TenantId_CustomerId_OccurredAtUtc" ON operations.customer_audit ("TenantId", "CustomerId", "OccurredAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021183855_AddCustomerAudit') THEN
    CREATE INDEX "IX_customer_audit_TenantId_OccurredAtUtc" ON operations.customer_audit ("TenantId", "OccurredAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021183855_AddCustomerAudit') THEN
    INSERT INTO operations."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021183855_AddCustomerAudit', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021183926_NormalizeCustomerAuditActor') THEN
    UPDATE operations.customers
    SET "UpdatedByUserId" = "CreatedByUserId"
    WHERE "UpdatedByUserId" = '00000000-0000-0000-0000-000000000000';

    ALTER TABLE operations.customers
    ALTER COLUMN "UpdatedByUserId" DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021183926_NormalizeCustomerAuditActor') THEN
    INSERT INTO operations."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021183926_NormalizeCustomerAuditActor', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021200640_AddLoanExceptionRequests') THEN
    CREATE TABLE operations.loan_exception_requests (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "RouteId" uuid NOT NULL,
        "ProposedAmount" numeric(18,2) NOT NULL,
        "ProposedDays" integer NOT NULL,
        "ProposedPercentage" numeric(6,2) NOT NULL,
        "Reason" character varying(500) NOT NULL,
        "Status" character varying(16) NOT NULL,
        "CreatedByUserId" uuid NOT NULL,
        "CreatedByRole" character varying(16) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "DecidedByUserId" uuid,
        "DecidedAtUtc" timestamp with time zone,
        "DecisionNotes" character varying(500),
        "CancelledByUserId" uuid,
        "CancelledAtUtc" timestamp with time zone,
        CONSTRAINT "PK_loan_exception_requests" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_loan_exception_requests_customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES operations.customers ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021200640_AddLoanExceptionRequests') THEN
    CREATE INDEX "IX_loan_exception_requests_CustomerId" ON operations.loan_exception_requests ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021200640_AddLoanExceptionRequests') THEN
    CREATE INDEX "IX_loan_exception_requests_TenantId_CustomerId_Status" ON operations.loan_exception_requests ("TenantId", "CustomerId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021200640_AddLoanExceptionRequests') THEN
    CREATE INDEX "IX_loan_exception_requests_TenantId_RouteId_Status" ON operations.loan_exception_requests ("TenantId", "RouteId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021200640_AddLoanExceptionRequests') THEN
    INSERT INTO operations."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021200640_AddLoanExceptionRequests', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021210019_AddLoansAndInstallments') THEN
    CREATE TABLE operations.loans (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "RouteId" uuid NOT NULL,
        "LoanTemplateId" uuid,
        "LoanTemplateVersion" integer,
        "ExceptionRequestId" uuid,
        "Status" character varying(20) NOT NULL,
        "CreatedByUserId" uuid NOT NULL,
        "CreatedByRole" character varying(20) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "Terms_BalanceAmount" numeric(18,2) NOT NULL,
        "Terms_DueAtUtc" timestamp with time zone NOT NULL,
        "Terms_FrequencyNDays" integer,
        "Terms_InstallmentCount" integer NOT NULL,
        "Terms_InterestAmount" numeric(18,2) NOT NULL,
        "Terms_InterestRate" numeric(5,2) NOT NULL,
        "Terms_PaidAmount" numeric(18,2) NOT NULL,
        "Terms_PaymentFrequency" character varying(20) NOT NULL,
        "Terms_PrincipalAmount" numeric(18,2) NOT NULL,
        "Terms_StartsAtUtc" timestamp with time zone NOT NULL,
        "Terms_TotalAmount" numeric(18,2) NOT NULL,
        CONSTRAINT "PK_loans" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_loans_customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES operations.customers ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021210019_AddLoansAndInstallments') THEN
    CREATE TABLE operations.installments (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "LoanId" uuid NOT NULL,
        "Number" integer NOT NULL,
        "DueDate" date NOT NULL,
        "ExpectedAmount" numeric(18,2) NOT NULL,
        "PaidAmount" numeric(18,2) NOT NULL,
        "Status" character varying(20) NOT NULL,
        CONSTRAINT "PK_installments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_installments_loans_LoanId" FOREIGN KEY ("LoanId") REFERENCES operations.loans ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021210019_AddLoansAndInstallments') THEN
    CREATE INDEX "IX_installments_LoanId" ON operations.installments ("LoanId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021210019_AddLoansAndInstallments') THEN
    CREATE INDEX "IX_installments_TenantId_DueDate_Status" ON operations.installments ("TenantId", "DueDate", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021210019_AddLoansAndInstallments') THEN
    CREATE UNIQUE INDEX "IX_installments_TenantId_LoanId_Number" ON operations.installments ("TenantId", "LoanId", "Number");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021210019_AddLoansAndInstallments') THEN
    CREATE INDEX "IX_loans_CustomerId" ON operations.loans ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021210019_AddLoansAndInstallments') THEN
    CREATE INDEX "IX_loans_TenantId_CustomerId_Status" ON operations.loans ("TenantId", "CustomerId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021210019_AddLoansAndInstallments') THEN
    CREATE INDEX "IX_loans_TenantId_RouteId_Status" ON operations.loans ("TenantId", "RouteId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021210019_AddLoansAndInstallments') THEN
    INSERT INTO operations."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021210019_AddLoansAndInstallments', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021220458_AddPaymentsAndEvidences') THEN
    CREATE TABLE operations.payments (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "LoanId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "RouteId" uuid NOT NULL,
        "CollectorUserId" uuid NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "PaymentMethodId" uuid NOT NULL,
        "PaymentMethodName" character varying(50) NOT NULL,
        "Notes" character varying(500),
        "IdempotencyKey" character varying(100) NOT NULL,
        "RegisteredAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_payments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_payments_loans_LoanId" FOREIGN KEY ("LoanId") REFERENCES operations.loans ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021220458_AddPaymentsAndEvidences') THEN
    CREATE TABLE operations.payment_allocations (
        "Id" uuid NOT NULL,
        "PaymentId" uuid NOT NULL,
        "InstallmentId" uuid NOT NULL,
        "InstallmentNumber" integer NOT NULL,
        "AmountAllocated" numeric(18,2) NOT NULL,
        "IsFullyCovered" boolean NOT NULL,
        CONSTRAINT "PK_payment_allocations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_payment_allocations_payments_PaymentId" FOREIGN KEY ("PaymentId") REFERENCES operations.payments ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021220458_AddPaymentsAndEvidences') THEN
    CREATE TABLE operations.payment_evidences (
        "Id" uuid NOT NULL,
        "PaymentId" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "FileName" character varying(255) NOT NULL,
        "ContentType" character varying(100) NOT NULL,
        "ByteSize" bigint NOT NULL,
        "StoragePath" character varying(500) NOT NULL,
        "UploadedByUserId" uuid NOT NULL,
        "UploadedAtUtc" timestamp with time zone NOT NULL,
        "IsDeleted" boolean NOT NULL,
        "DeletionReason" character varying(500),
        "DeletedByUserId" uuid,
        "DeletedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_payment_evidences" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_payment_evidences_payments_PaymentId" FOREIGN KEY ("PaymentId") REFERENCES operations.payments ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021220458_AddPaymentsAndEvidences') THEN
    CREATE INDEX "IX_payment_allocations_PaymentId_InstallmentId" ON operations.payment_allocations ("PaymentId", "InstallmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021220458_AddPaymentsAndEvidences') THEN
    CREATE INDEX "IX_payment_evidences_PaymentId" ON operations.payment_evidences ("PaymentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021220458_AddPaymentsAndEvidences') THEN
    CREATE INDEX "IX_payment_evidences_TenantId_PaymentId_IsDeleted" ON operations.payment_evidences ("TenantId", "PaymentId", "IsDeleted");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021220458_AddPaymentsAndEvidences') THEN
    CREATE INDEX "IX_payments_LoanId" ON operations.payments ("LoanId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021220458_AddPaymentsAndEvidences') THEN
    CREATE UNIQUE INDEX "IX_payments_TenantId_IdempotencyKey" ON operations.payments ("TenantId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021220458_AddPaymentsAndEvidences') THEN
    CREATE INDEX "IX_payments_TenantId_LoanId" ON operations.payments ("TenantId", "LoanId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021220458_AddPaymentsAndEvidences') THEN
    CREATE INDEX "IX_payments_TenantId_RouteId_RegisteredAtUtc" ON operations.payments ("TenantId", "RouteId", "RegisteredAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261021220458_AddPaymentsAndEvidences') THEN
    INSERT INTO operations."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021220458_AddPaymentsAndEvidences', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261022050048_FixAuditFindingsOperations') THEN
    ALTER TABLE operations.payment_evidences ADD "FileHashSha256" character varying(64) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261022050048_FixAuditFindingsOperations') THEN
    ALTER TABLE operations.loan_exception_requests ADD "ConsumedAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261022050048_FixAuditFindingsOperations') THEN
    ALTER TABLE operations.loan_exception_requests ADD "ConsumedByLoanId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261022050048_FixAuditFindingsOperations') THEN
    ALTER TABLE operations.loan_exception_requests ADD "IsConsumed" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261022050048_FixAuditFindingsOperations') THEN
    CREATE UNIQUE INDEX "IX_loans_TenantId_ExceptionRequestId" ON operations.loans ("TenantId", "ExceptionRequestId") WHERE "ExceptionRequestId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261022050048_FixAuditFindingsOperations') THEN
    CREATE UNIQUE INDEX "IX_loan_exception_requests_TenantId_ConsumedByLoanId" ON operations.loan_exception_requests ("TenantId", "ConsumedByLoanId") WHERE "ConsumedByLoanId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261022050048_FixAuditFindingsOperations') THEN
    INSERT INTO operations."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261022050048_FixAuditFindingsOperations', '10.0.11');
    END IF;
END $EF$;
COMMIT;

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

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261024064552_AddRouteFinancialEntries') THEN
    CREATE TABLE operations.route_financial_entries (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "RouteId" uuid NOT NULL,
        "Type" character varying(16) NOT NULL,
        "Concept" character varying(160) NOT NULL,
        "Description" character varying(1000),
        "Amount" numeric(18,2) NOT NULL,
        "CreatedByUserId" uuid NOT NULL,
        "CreatedByDisplayName" character varying(160) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedByUserId" uuid NOT NULL,
        "UpdatedByDisplayName" character varying(160) NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_route_financial_entries" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261024064552_AddRouteFinancialEntries') THEN
    CREATE INDEX "IX_route_financial_entries_TenantId_RouteId_CreatedAtUtc" ON operations.route_financial_entries ("TenantId", "RouteId", "CreatedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261024064552_AddRouteFinancialEntries') THEN
    CREATE INDEX "IX_route_financial_entries_TenantId_RouteId_Type" ON operations.route_financial_entries ("TenantId", "RouteId", "Type");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261024064552_AddRouteFinancialEntries') THEN
    INSERT INTO operations."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261024064552_AddRouteFinancialEntries', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261026200421_AddDayClosingAndFinancialAudit') THEN
    CREATE TABLE operations.financial_movement_audit_logs (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "RouteId" uuid NOT NULL,
        "EntityType" character varying(32) NOT NULL,
        "EntityId" uuid NOT NULL,
        "Action" character varying(32) NOT NULL,
        "Concept" character varying(160) NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "Details" character varying(4000) NOT NULL,
        "PerformedByUserId" uuid NOT NULL,
        "PerformedByDisplayName" character varying(160) NOT NULL,
        "PerformedByUserRole" character varying(32) NOT NULL,
        "TimestampUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_financial_movement_audit_logs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261026200421_AddDayClosingAndFinancialAudit') THEN
    CREATE TABLE operations.route_day_closings (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "RouteId" uuid NOT NULL,
        "BusinessDate" date NOT NULL,
        "IsClosed" boolean NOT NULL,
        "ClosedAtUtc" timestamp with time zone,
        "ClosedByUserId" uuid,
        "ClosedByDisplayName" character varying(160),
        "ReopenedAtUtc" timestamp with time zone,
        "ReopenedByUserId" uuid,
        "ReopenedByDisplayName" character varying(160),
        CONSTRAINT "PK_route_day_closings" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261026200421_AddDayClosingAndFinancialAudit') THEN
    CREATE INDEX "IX_financial_movement_audit_logs_TenantId_EntityId" ON operations.financial_movement_audit_logs ("TenantId", "EntityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261026200421_AddDayClosingAndFinancialAudit') THEN
    CREATE INDEX "IX_financial_movement_audit_logs_TenantId_RouteId_TimestampUtc" ON operations.financial_movement_audit_logs ("TenantId", "RouteId", "TimestampUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261026200421_AddDayClosingAndFinancialAudit') THEN
    CREATE UNIQUE INDEX "IX_route_day_closings_TenantId_RouteId_BusinessDate" ON operations.route_day_closings ("TenantId", "RouteId", "BusinessDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operations."__EFMigrationsHistory" WHERE "MigrationId" = '20261026200421_AddDayClosingAndFinancialAudit') THEN
    INSERT INTO operations."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261026200421_AddDayClosingAndFinancialAudit', '10.0.11');
    END IF;
END $EF$;
COMMIT;

