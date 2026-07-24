# Propuestas Creativas de Combate - Niveles 1 al 3

## Base de referencia

Este documento se construyo tomando como guia el lenguaje visual y mecanico ya versionado en el proyecto:

- `Assets/_Arte/Environment/Abismo de los Lamentos`
- `Assets/_Arte/Environment/Rios de la Oscuridad`
- `Assets/_Arte/Environment/Campos de la Deformidad`
- `Assets/Scripts/Francisco/Jefes/Jefe1/AstarothController.cs`
- `Assets/Scripts/Joaquin/Boss N2/BloodKnightBoss.cs`
- `Assets/Scripts/Joaquin/Enemies/Kronus/KronusEnemy.cs`
- `Assets/Scripts/Joaquin/Enemies/Morlock/MorlockEnemy.cs`
- `Assets/Scripts/Joaquin/Enemies/Drogath/DrogathEnemy.cs`
- `Assets/Scripts/Joaquin/Enemies/Veynar/VeynarEnemy.cs`
- Configuraciones de oleadas en `Assets/Scripts/Francisco/GeneratorEnemies/Resources`

Nota: no se encontro un archivo llamado `Art Bible` dentro del repositorio actual. Por eso, los conceptos opositorios de cada nivel se dejan explicitos como inferencias de trabajo para validacion antes de subir el avance aprobado al canvas.

## Nivel 1 - Abismo de los Lamentos

**Tema principal inferido:** carne viva, sangre, dolor ritual y opresion organica.

**Conceptos opositorios inferidos:** organico vs mineral, pulso vivo vs quietud mortuoria, atraccion visceral vs rechazo.

### 1. Pulso Carnal Reactivo

La sala entra en pulsacion cada vez que se elimina una oleada o cuando un enemigo elite pierde cierto porcentaje de vida. El piso de carne late desde un nucleo central hacia afuera y obliga a recolocarse, mientras pequenas islas de roca quedan como zonas seguras temporales.

Esto mejora el combate porque convierte el posicionamiento en una decision activa y anticipable, en lugar de solo esquivar enemigos. Tambien sirve para adelantar el lenguaje del jefe Astaroth y su `Pulso Carnal`, haciendo que el boss se sienta anunciado por el nivel.

### 2. Costras de Sangre

Los charcos de sangre se coagulan por unos segundos cuando un enemigo muere sobre ellos. Mientras estan coagulado actuan como cobertura parcial o frenan proyectiles; cuando revientan, dejan una salpicadura que dana o empuja a quien este cerca.

Esto mejora el combate porque transforma una zona pasiva del escenario en una herramienta de control espacial. El jugador puede decidir si usarla defensivamente contra proyectiles o detonarla a proposito para limpiar grupos pequenos.

### 3. Lamentos Encadenados

Dos o tres enemigos aparecen unidos por un vinculo de lamento. Si el jugador rompe la cadena matandolos en cierto orden o separandolos con empujes, se libera una onda de grito que aturde al resto. Si ignora la cadena demasiado tiempo, esta otorga regeneracion o armadura temporal.

Esto mejora el combate porque agrega prioridad de objetivos, lectura del grupo y microdecisiones tacticas. El nivel deja de sentirse solo como dano directo y gana una capa de resolucion de patrones acorde a la idea de sufrimiento compartido.

### 4. Herida Viva

Las zonas del piso donde el jugador repite demasiado una misma ruta se "abren" y generan nervios o espinas. El castigo no es solo negativo: si el jugador golpea esa herida en el momento correcto, la hace estallar y dana enemigos cercanos.

Esto mejora el combate porque combate el juego estatico sin sentirse injusto. Obliga a variar la movilidad, pero tambien recompensa al jugador que aprenda a usar el propio escenario como arma.

### 5. Altar del Dolor

En algunas salas aparece un altar organico opcional. El jugador puede activarlo sacrificando una pequena fraccion de vida o alimentandolo con la muerte de un enemigo marcado. A cambio, obtiene un beneficio de sala corto: mas dano, robo de vida o ruptura de armadura enemiga.

Esto mejora el combate porque introduce una decision de riesgo-recompensa alineada con el tono del nivel. No solo aumenta tension, tambien hace que cada sala tenga una decision dramatica propia y mas identidad.

## Nivel 2 - Rios de la Oscuridad

**Tema principal inferido:** rios corruptos, niebla, profundidad, eco y amenaza oculta.

**Conceptos opositorios inferidos:** visibilidad vs ocultamiento, corriente vs firmeza, silencio opresivo vs resonancia.

### 1. Mareas de Acido

Los rios o grietas del piso suben y bajan por fases cortas. Cuando el nivel entra en "marea alta", ciertos caminos se cierran, otros se vuelven peligrosos y algunos enemigos ganan bonificaciones si pisan zonas inundadas. En "marea baja", quedan costras o residuos que ralentizan.

Esto mejora el combate porque le da ritmo a la sala y hace que el jugador lea el terreno como un reloj tactico. Tambien refuerza la identidad del nivel sin necesitar enemigos completamente nuevos.

### 2. Eco Persistente de Sala

Algunos ataques potentes dejan un eco diferido en el lugar donde impactaron. Un segundo despues, el golpe se repite como una silueta oscura o una onda residual. El jugador puede cebar ese eco para que vuelva a activarse cuando los enemigos ya se movieron hacia esa zona.

Esto mejora el combate porque profundiza la lectura temporal del espacio. Ademas, conecta directamente con `Eco Persistente`, que ya existe en Kronus nivel 2, y lo eleva de rasgo individual a identidad de nivel.

### 3. Faroles de Vigilia

Cada sala tiene uno o dos faroles o braseros interactivos. Cuando estan encendidos, abren conos de vision que revelan telegraphs, debilitan emboscadas y reducen la punteria predictiva de enemigos como Morlock. Cuando se apagan, la sala gana tension y los enemigos recuperan ventajas de ocultamiento.

Esto mejora el combate porque convierte la visibilidad en una decision del jugador, no solo en una penalizacion ambiental. Da agencia y permite estilos distintos: asegurar informacion o dejar la sala oscura a cambio de otros beneficios.

### 4. Rostros en la Corriente

Las caras atrapadas en muros, niebla o corrientes expulsan rafagas en intervalos. Esas rafagas no hacen dano directo, pero desvian proyectiles, alteran trayectoria de dashes y empujan ligeramente enemigos. Bien usadas, permiten redirigir disparos o agrupar enemigos para castigarlos.

Esto mejora el combate porque el escenario deja de ser solo fondo y pasa a ser una herramienta manipulable. Tambien hace que la atmosfera del nivel tenga una funcion jugable inmediata.

### 5. Caceria de Sombras

Si una oleada se limpia con precision o en poco tiempo, la oscuridad remanente se concentra en un objetivo opcional: una sombra elite, un cofre o un buff de sala. Si el desempeno es bajo, esa misma sombra se dispersa y genera un refuerzo tardio o una distorsion breve.

Esto mejora el combate porque recompensa el dominio sin volverlo obligatorio. Tambien encaja muy bien con la logica de manipulacion/distorsion ya presente en el proyecto y hace que el nivel responda al desempeno del jugador.

## Nivel 3 - Campos de la Deformidad

**Tema principal inferido:** deformacion del espacio, tablero ritual, naturaleza alterada y simetria corrompida.

**Conceptos opositorios inferidos:** orden geometrico vs mutacion organica, simetria vs caos, control tactico vs proliferacion.

### 1. Tablero Vivo

El piso alterna entre casillas de orden y casillas deformadas. Las casillas ordenadas favorecen precision, defensa o recarga; las deformadas mejoran movilidad, dano caotico o efectos secundarios, pero vuelven el espacio menos estable.

Esto mejora el combate porque convierte cada sala en una postura tactica elegible: jugar seguro y metronomico o asumir riesgo para generar picos ofensivos. Tambien aterriza de forma clara la oposicion visual entre ajedrez y campo deformado.

### 2. Formaciones Herejes

Las oleadas aparecen en formaciones reconocibles, casi como piezas de un tablero: escudos protegiendo tiradores, invocadores en diagonales, acosadores presionando flancos. Si el jugador rompe la formacion adecuada, el grupo pierde bonos y entra en desorden.

Esto mejora el combate porque da lectura estrategica inmediata a la composicion de enemigos. En vez de ser solo "mas enemigos", cada oleada comunica una idea que el jugador puede resolver.

### 3. Colmenas de Reescritura

Las colmenas de Veynar no solo invocan, tambien reescriben el suelo alrededor. Algunas zonas pasan de tablero limpio a pasto mutado y otras de terreno organico a casilla ritual. Destruir la colmena en uno u otro tipo de suelo genera efectos distintos: santuario, explosion, slow o silencio de invocacion.

Esto mejora el combate porque hace que el territorio cambie durante la pelea y que el jugador quiera pensar donde deja vivir o donde destruye a cada invocador. La sala se siente viva y disputada.

### 4. Reflejos Deformes

Despues de ciertos dashes o ataques pesados del jugador, aparece un reflejo deformado que repite esa accion desde la casilla opuesta o una posicion espejada. El eco puede herir enemigos, activar trampas o incluso obligar a recolocarse si el jugador no lo planifica.

Esto mejora el combate porque introduce expresion avanzada sin quitar claridad. El jugador experimentado puede "programar" la sala con su propio movimiento y usar la deformacion del espacio a su favor.

### 5. Jaque Ritual

En algunas salas aparece un enemigo marcado como "Rey" y varios apoyos con rol. Mientras el Rey siga vivo, el grupo conserva un patron de orden; cuando el jugador elimina ciertas piezas de soporte, se habilita una ventana corta de "jaque mate" que rompe su defensa o lo deja vulnerable.

Esto mejora el combate porque crea mini-encuentros memorables dentro de salas normales y hace que el concepto de orden corrompido tenga un payoff mecanico claro. Tambien se integra muy bien con enemigos de escudo, invocadores y rangos mixtos.

## Cierre

Estas 15 propuestas estan pensadas para entrar como:

- eventos de sala,
- modificadores temporales,
- reglas de oleada,
- props interactivos,
- o extensiones ligeras de sistemas que ya existen.

Si el equipo aprueba esta direccion, el siguiente paso ideal es convertir cada propuesta en una mini-ficha de produccion con:

- objetivo emocional,
- enemigos compatibles,
- costo de implementacion,
- prioridad,
- y mock de sala.
