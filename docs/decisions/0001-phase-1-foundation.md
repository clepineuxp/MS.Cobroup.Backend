# ADR-000 — Fundación de fase 1

- Estado: aceptada para scaffolding.
- Decisión: .NET 10 LTS, C# 14, cinco proyectos productivos separados y dos proyectos de prueba.
- Health: endpoint técnico `/health`, sin endpoint de negocio de ejemplo.
- Persistencia e identidad: deliberadamente no seleccionadas; corresponden a ADR-001/ADR-002 del documento maestro.
- Consecuencia: la DI es válida para el host actual; adaptadores se añadirán solo cuando exista contrato aprobado.
