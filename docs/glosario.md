# Glosario

Términos del dominio CTO / FTTH que aparecen en la spec, código y comentarios.

| Término | Definición |
|---|---|
| **CTO** | Caja Terminal Óptica. Punto de derivación donde se conectan los domicilios a la red de fibra. En el DWG se materializan como `BlockReference` en la capa CTO. |
| **FTTH** | Fiber To The Home. Tecnología de fibra óptica hasta el hogar. |
| **HP** | Hogares Pasados (Futuro 100%). Cantidad total de domicilios que la red pasa frente a un segmento de calle. Es el input principal de la tabla de cálculo. |
| **Segmento** | Eje de calle (Polyline en el DWG). Unidad de agrupamiento del cálculo. Un segmento = 2 frentes de manzana + 1 bloque CONT_HP. |
| **Manzana** | Polígono cerrado delimitado por 4 calles. Cada manzana tiene múltiples frentes (uno por lado). |
| **Frente** | Arco de la polilínea de manzana entre dos esquinas reales (V4). Una **esquina real** es la intersección entre dos calles con nombres distintos (leídos del OD campo `CALLE_1`). Su largo (`LARGO_FRENTE`) es el valor que entra en la tabla CTO. Identificado por `"<manzanaHandle>#<segmentHandle>"` (V4/V3) o `"<manzanaHandle>#<frenteIdx>"` (V2 legacy). |
| **Esquina de calle** | Punto donde dos segmentos con `CALLE_1` distintos se intersectan (o sus líneas extendidas se intersectan dentro de `MAX_INTERSECTION_DIST = 15m`). Construida por `StreetCornerLibrary` en dos fases: endpoint matching (tolerancia 0.5m) + intersección geométrica de líneas extendidas. |
| **Linga** | Cable físico (Line) que conecta un poste al segmento de calle. Tiene tipo `PRIORIDAD` o `SECUNDARIA`. |
| **Poste** | Entidad seleccionada por el usuario (típicamente un `BlockReference` o un punto). Recibe las CTOs. En el código se abre como `Entity` genérica y se obtiene la posición con `Extensions.GetInsertionOrPosition`. |
| **PRIORIDAD** | Tipo de linga que indica que el poste recibe CTOs en su frente. Un segmento puede tener 2 frentes PRIORIDAD (uno por lado) pero el HP es único. |
| **SECUNDARIA** | Linga que no recibe CTOs. El poste tiene `C_DESP=0, C_CREC=0`. |
| **C_DESP** | Cajas de Despliegue Inicial (40%). Instaladas en la primera tanda. |
| **C_CREC** | Cajas de Crecimiento Futuro (100%). Reservadas para expansión. |
| **Despliegue** | Proceso de insertar los bloques CTO en el DWG (paso 5). |
| **CONT_HP** | Bloque de conteo de HP en el DWG. Hay uno por segmento. |
| **Buffer circular** | Zona circular (radio `TEXT_BUFFER_DEFAULT = 5.0 m`) alrededor de un poste donde se capturan textos para extraer HP. |
| **Raycast ortogonal** | Método para asociar un poste a un segmento: se disparan 4 rayos perpendiculares desde el poste y se toma la primera intersección con una Polyline de calle. |
| **Anti-cruce** | Filtro geométrico que descarta intersecciones donde el rayo cruza un vacío o manzana antes de llegar al segmento real. Margen en `ANTI_CROSS_MARGIN = 2.0 m`. |
| **XData** | Extended Entity Data de AutoCAD. Permite adjuntar datos custom (string/int/real) a cualquier entidad. En este proyecto todo va bajo el AppName `KOOVRA_CTO`. |
| **Handle hex** | Identificador hexadecimal persistente de una entidad (sobrevive save/load). Más estable que `ObjectId`. Usado para `ID_SEGMENT`, `ID_LINGA`. |
| **NETLOAD** | Comando de AutoCAD para cargar un DLL managed en la sesión. |
| **PaletteSet** | Ventana flotante/acoplable de AutoCAD. El `CTO_PANEL` usa una PaletteSet para exponer los comandos. |
| **Overflow (OVF)** | Cajas D o C de un segmento que no pudieron asignarse a ningún poste por el cap 1D+1C. Se almacenan en `C_DESP_OVF`/`C_CREC_OVF` al primer poste del segmento y se despliegan al midpoint en el paso 5. |
| **Midpoint deploy** | Inserción de cajas overflow (o de segmentos sin postes seleccionados) en el punto medio de la curva del segmento, con la rotación del bloque CONT_HP. Acompañado de un círculo de alerta. |
| **Círculo de alerta** | Círculo dibujado en la capa "0" con radio `CTO_ALERT_CIRCLE_RADIUS = 10m` en el midpoint del segmento, indicando que las cajas fueron insertadas fuera de un poste real. Es idempotente (se purga antes de cada deploy). |
