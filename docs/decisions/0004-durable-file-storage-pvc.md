# ADR-0004 — Almacenamiento durable de archivos en Kubernetes PVC con evolución futura a AWS S3

## Estado

Aceptada (2026-10-22).

## Decisión aprobada

> Todas las fotografías de evidencias, imágenes cargadas y cualquier archivo binario administrado por COBROUP se almacenarán actualmente en un PersistentVolumeClaim de Kubernetes. La lógica de negocio, los controladores y la base de datos no dependerán de rutas físicas de Kubernetes. El acceso se realizará mediante un puerto de almacenamiento y claves lógicas independientes del proveedor, de forma que el adaptador basado en PVC pueda sustituirse en el futuro por AWS S3 mediante configuración e inyección de dependencias, sin reescribir Core, Domain ni los contratos HTTP.

## Implementación

1. **Puerto de Dominio Hexagonal Agnóstico (`IFileStorageService`):**
   - Reside en `MS.Cobroup.Backend.Domain.Interfaces.Services`.
   - Expone operaciones agnósticas de almacenamiento binario general (`PutAsync`, `OpenReadAsync`, `DeleteAsync`, `ExistsAsync`, `GetMetadataAsync`) y fachada retrocompatible para evidencias (`StoreEvidenceAsync`, `ReadEvidenceAsync`, `DeleteEvidenceAsync`).
   - Opera mediante `Stream` (sin cargar archivos completos en memoria) y maneja `StorageObjectMetadata(StorageKey, ContentType, ByteSize, Sha256Hex, CreatedAtUtc)`.
   - Clave lógica canónica: `tenants/{tenantId}/{category}/{aggregateId}/{sha256}.{ext}` con allowlist de servidor (`evidences`, `payments`, `documents`, `attachments`), y compatibilidad explícita con claves legadas `{tenantId}/{paymentId}/{sha256}.{ext}`.
   - La base de datos almacena exclusivamente la clave lógica relativa, directamente reusable como object key futuro de AWS S3.

2. **Adaptador de Infraestructura (`FileSystemObjectStorage`):**
   - Implementa `IFileStorageService` y se configura mediante `IOptions<FileSystemStorageOptions>`.
   - El root procede de `Storage:EvidencePath` (o `Storage:RootPath`).
   - Escribe primero en un directorio temporal (`{RootPath}/.tmp/{Guid}.tmp`) dentro del mismo volumen persistente, garantizando atomicidad sin depender de `/tmp`.
   - Valida estructura completa de imágenes mediante `ImageStructuralValidator` (JPEG SOI/SOF/EOI, PNG firma/IHDR/IEND, WebP RIFF/VP8/VP8L/VP8X) y límite máximo de resolución de 8192px contra decompression bombs.
   - Calcula hash SHA-256 de forma incremental y valida el límite máximo de tamaño (10 MB).
   - Publicación atómica mediante `File.Move(..., overwrite: false)`: si el archivo de destino existe con idéntico tamaño y SHA-256 es un éxito idempotente; si el contenido difiere, arroja `InvalidOperationException` (violación de integridad).
   - Prevención estricta de Path Traversal y symlinks: rechazo de secuencias `..`, `:`, caracteres nulos, rutas absolutas, verificación de pertenencia al root con separador real (`rootWithSep`) y validación de ausencia de symlinks/reparse points.
   - Reconciliación segura de temporales huérfanos (`ReconcileOrphanTempsAsync`).

3. **Inyección de Dependencias y Selección de Proveedor:**
   - La API (`Program.cs`) selecciona el adaptador mediante la configuración `Storage:Provider`:
     - `Storage:Provider=FileSystem`: registra `FileSystemObjectStorage`.
     - `Storage:Provider=S3`: reservado para evolución arquitectónica futura; en la presente fase provoca fallo explícito indicando que no está habilitado.
     - Proveedores desconocidos o rutas no configuradas fuera de desarrollo provocan fail-fast inmediato.

4. **Despliegue y Concurrencia en Kubernetes:**
   - Cada ambiente define un `PersistentVolumeClaim` dedicado (`backend-evidence-pvc`).
   - **Producción:** Configura dos (2) réplicas activas (`replicas: 2`), requiriendo acceso compartido concurrente: modo **`ReadWriteMany` (RWX)**.
   - **Dev / QA:** Configura una (1) réplica con modo `ReadWriteOnce` (RWO).
   - Montaje consistente en `/var/cobroup/evidences`.
   - Permisos compatibles con el usuario no-root del contenedor (`runAsUser: 1000`, `fsGroup: 1000`).
   - Aislamiento total: prohibido almacenar claves privadas JWT o secretos en este volumen.
