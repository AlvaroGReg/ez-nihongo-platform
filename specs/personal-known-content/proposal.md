# Propuesta SDD — Lista personal de contenido conocido

## Estado

Plan de desarrollo coordinado con
[`ez-nihongo-web/specs/personal-known-content/`](../../../ez-nihongo-web/specs/personal-known-content/).
Esta especificación es la fuente de verdad para contratos HTTP, autorización,
validación y persistencia. El contrato compartido quedó cerrado en la Fase 1 SDD;
la implementación puede comenzar siguiendo las tareas por sesión.

## Problema y objetivo

Cada estudiante necesita un espacio privado para consultar y organizar vocabulario
y kanji del catálogo, además de guardar entradas propias como diccionario personal.
Esta fase se limita a la gestión y consulta de ese contenido; las entradas propias
no se ofrecen a los ejercicios.

## Decisiones de alcance acordadas

- Es obligatorio iniciar sesión para consultar o cambiar la lista.
- Cada cuenta tiene una sola lista, denominada «Contenido conocido».
- La lista admite vocabulario y kanji, tanto referencias a los catálogos existentes
  como contenido personalizado propiedad del usuario.
- La lista se gestiona manualmente: añadir y quitar. El resultado de un ejercicio
  no modifica automáticamente la lista.
- El contenido personal sólo se expone en la lista/diccionario autenticado; no
  cambia los ejercicios ni los catálogos públicos.
- No hay persistencia local anónima ni sincronización offline para esta función.

## Alcance de plataforma

- Persistir entradas de catálogo y entradas personalizadas con propietario de
  Identity.
- Exponer consulta paginada, incorporación desde catálogo, creación de contenido
  propio y eliminación de la lista.
- Validar cada referencia de catálogo contra los catálogos de plataforma.
- Mantener la privacidad entre cuentas, deduplicación y contratos OpenAPI.
- No se añaden endpoints ni integración de contenido personal para ejercicios.

## Fuera de alcance

- Varias listas, etiquetas, carpetas, notas privadas o compartir contenido.
- SRS, inferencia automática de dominio, analítica nueva o conversión de intentos
  existentes en elementos conocidos.
- Importación masiva, edición de entradas personalizadas después de crearlas,
  borrado administrativo y disponibilidad offline.
- Crear contenido propio anónimo o reutilizarlo como contenido de otro usuario.

## Identidad de los elementos y datos personalizados

Una entrada de catálogo conserva su tipo (`vocabulary` o `kanji`) y el ID estable
del catálogo. Una entrada personalizada guarda los campos necesarios para
presentarse y generar una pregunta; no se le asigna un ID de catálogo ni un nivel
JLPT oficial. El usuario autenticado se deriva siempre del JWT, nunca del cuerpo.

Para permitir las modalidades actuales sin transliteración automática ni
dependencias nuevas, los campos mínimos propuestos son:

- Vocabulario propio: escritura japonesa, lectura kana, romaji y significado.
- Kanji propio: un kanji, una o más lecturas kana y uno o más significados.

El borrador de diseño concreta las validaciones, límites y forma de los DTO. La
web debe permitir buscar en los catálogos y añadir un resultado existente; si no
se encuentra un elemento adecuado, debe ofrecer el formulario de contenido
propio.

## Criterio de éxito

Un usuario autenticado puede construir su diccionario personal con elementos
propios y del catálogo y volver a consultarlo. La API mantiene aislamiento por
usuario y no modifica los catálogos compartidos ni integra el contenido propio en
ejercicios.
