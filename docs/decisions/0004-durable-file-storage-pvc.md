# ADR-0004 — Almacenamiento durable de archivos en Kubernetes PVC con evolución futura a AWS S3

## Estado

Aceptada (2026-10-22).

## Decisión aprobada

> Todas las fotografías de evidencias, imágenes cargadas y cualquier archivo binario administrado por COBROUP se almacenarán actualmente en un PersistentVolumeClaim de Kubernetes. La lógica de negocio, los controladores y la base de datos no dependerán de rutas físicas de Kubernetes. El acceso se realizará mediante un puerto de almacenamiento y claves lógicas independientes del proveedor, de forma que el adaptador basado en PVC pueda sustituirse en el futuro por AWS S3 mediante configuración e inyección de dependencias, sin reescribir Core, Domain ni los contratos HTTP.

## Implementación

1. **Puerto de Dominio Hexagonal (`IFileStorageService`):**
   - Reside en `MS.Cobroup.Backend.Domain.Interfaces.Services`.
   - Métodos asíncronos: `StoreEvidenceAsync`, `ReadEvidenceAsync`, `DeleteEvidenceAsync`, todos con `CancellationToken`.
   - Opera mediante `Stream` (sin cargar archivos completos en memoria) y devuelve `StoredFileResult(StorageKey, ContentType, ByteSize, FileHashSha256)`.
   - La base de datos almacena exclusivamente una **clave lógica relativa** normalizada: `tenants/{tenantId}/payments/{paymentId}/{fileHash}.{ext}`, nunca rutas absolutas del pod, URLs públicas ni detalles de Kubernetes.

2. **Adaptador de Infraestructura (`FileSystemObjectStorage`):**
   - Implementa `IFileStorageService` y se configura mediante `IOptions<FileSystemStorageOptions>`.
   - El root procede de `Storage:EvidencePath` (o `Storage:RootPath`).
   - Escribe primero en un directorio temporal (`{RootPath}/.tmp/{Guid}.tmp`) dentro del mismo volumen persistente, garantizando que la operación final `File.Move` sea una publicación atómica y evitando dependencias del filesystem efímero del pod (`/tmp`).
   - Valida magic bytes en streaming (JPEG: `FF D8 FF`, PNG: `89 50 4E 47 0D 0A 1A 0A`, WebP: `RIFF...WEBP`).
   - Calcula hash SHA-256 de forma incremental y valida el límite máximo de tamaño (10 MB).
   - Limpieza garantizada del archivo temporal en bloque `finally`.
   - Prevención estricta de Path Traversal: rechazo de `..`, `:`, caracteres nulos, rutas absolutas y verificación de pertenencia al subárbol del root.

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
