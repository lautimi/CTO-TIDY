import math
from qgis.PyQt.QtCore import QCoreApplication, QVariant
from qgis.core import (
    QgsProcessing,
    QgsProcessingAlgorithm,
    QgsProcessingParameterFeatureSource,
    QgsProcessingParameterField,
    QgsProcessingParameterFeatureSink,
    QgsFeatureSink,
    QgsFeature,
    QgsGeometry,
    QgsSpatialIndex,
    QgsField,
    QgsFields,
    QgsPointXY,
    QgsCoordinateTransform,
    QgsWkbTypes
)

class ConectarPosteSegmento(QgsProcessingAlgorithm):
    INPUT_POSTES = 'INPUT_POSTES'
    INPUT_MANZANAS = 'INPUT_MANZANAS'
    INPUT_SEGMENTOS = 'INPUT_SEGMENTOS'
    CAMPO_DIR_POSTE = 'CAMPO_DIR_POSTE'
    CAMPO_DIR_SEGMENTO = 'CAMPO_DIR_SEGMENTO'
    CAMPO_ID_SEGMENTO = 'CAMPO_ID_SEGMENTO'
    OUTPUT_LINEAS = 'OUTPUT_LINEAS'
    OUTPUT_POSTES = 'OUTPUT_POSTES'

    def tr(self, string):
        return QCoreApplication.translate('Processing', string)

    def createInstance(self):
        return ConectarPosteSegmento()

    def name(self):
        return 'conectarpostesegmento_definitivo'

    def displayName(self):
        return self.tr('1. Conectar Poste (Ortogonal + Direcciones + ID_SEGMENT)')

    def group(self):
        return self.tr('Scripts FTTH')

    def groupId(self):
        return 'Scripts FTTH'

    def shortHelpString(self):
        return self.tr("Genera acometidas ortogonales, audita direcciones y transfiere el ID_SEGMENT a la capa de postes y líneas.")

    def initAlgorithm(self, config=None):
        self.addParameter(QgsProcessingParameterFeatureSource(
            self.INPUT_POSTES, self.tr('Capa de Puntos Esquina (Postes)'), [QgsProcessing.TypeVectorPoint]))
        self.addParameter(QgsProcessingParameterFeatureSource(
            self.INPUT_MANZANAS, self.tr('Capa de Manzanas (Polilíneas)'), [QgsProcessing.TypeVectorLine]))
        self.addParameter(QgsProcessingParameterFeatureSource(
            self.INPUT_SEGMENTOS, self.tr('Capa de Segmentos de Calle'), [QgsProcessing.TypeVectorLine]))
        
        self.addParameter(QgsProcessingParameterField(
            self.CAMPO_DIR_POSTE, self.tr('Campo DIRECCIÓN en POSTES (Ej: "San Martin 1250")'), 
            parentLayerParameterName=self.INPUT_POSTES, type=QgsProcessingParameterField.String))
        self.addParameter(QgsProcessingParameterField(
            self.CAMPO_DIR_SEGMENTO, self.tr('Campo DIRECCIÓN en SEGMENTOS (Ej: "San Martin")'), 
            parentLayerParameterName=self.INPUT_SEGMENTOS, type=QgsProcessingParameterField.String))
        self.addParameter(QgsProcessingParameterField(
            self.CAMPO_ID_SEGMENTO, self.tr('Campo ID en SEGMENTOS (Ej: "ID_SEGMENT")'), 
            parentLayerParameterName=self.INPUT_SEGMENTOS, type=QgsProcessingParameterField.Any))
        
        self.addParameter(QgsProcessingParameterFeatureSink(
            self.OUTPUT_LINEAS, self.tr('Acometidas Generadas (Líneas)')))
        self.addParameter(QgsProcessingParameterFeatureSink(
            self.OUTPUT_POSTES, self.tr('Postes Actualizados (Puntos)')))

    def processAlgorithm(self, parameters, context, feedback):
        postes = self.parameterAsSource(parameters, self.INPUT_POSTES, context)
        manzanas = self.parameterAsSource(parameters, self.INPUT_MANZANAS, context)
        segmentos = self.parameterAsSource(parameters, self.INPUT_SEGMENTOS, context)
        
        campo_dir_poste = self.parameterAsString(parameters, self.CAMPO_DIR_POSTE, context)
        campo_dir_seg = self.parameterAsString(parameters, self.CAMPO_DIR_SEGMENTO, context)
        campo_id_seg = self.parameterAsString(parameters, self.CAMPO_ID_SEGMENTO, context)

        # 1. Preparar la Tabla de Atributos (Agregamos ID_SEGMENT en mayúsculas al final)
        campos_nuevos = QgsFields()
        for field in postes.fields():
            campos_nuevos.append(field)
        campos_nuevos.append(QgsField('Direccion_2', QVariant.String))
        campos_nuevos.append(QgsField('REVISAR', QVariant.String))
        campos_nuevos.append(QgsField('ID_SEGMENT', QVariant.String))

        crs_target = postes.sourceCrs()
        
        (sink_lineas, dest_id_lineas) = self.parameterAsSink(
            parameters, self.OUTPUT_LINEAS, context, campos_nuevos, QgsWkbTypes.LineString, crs_target)
        (sink_postes, dest_id_postes) = self.parameterAsSink(
            parameters, self.OUTPUT_POSTES, context, campos_nuevos, QgsWkbTypes.Point, crs_target)
        
        def force_2d_line(geom):
            if geom.isNull(): return QgsGeometry()
            if geom.isMultipart():
                return QgsGeometry.fromMultiPolylineXY(geom.asMultiPolyline())
            else:
                return QgsGeometry.fromPolylineXY(geom.asPolyline())

        xform_mz = QgsCoordinateTransform(manzanas.sourceCrs(), crs_target, context.transformContext())
        xform_seg = QgsCoordinateTransform(segmentos.sourceCrs(), crs_target, context.transformContext())

        idx_mz = QgsSpatialIndex()
        dict_mz = {}
        for f in manzanas.getFeatures():
            g = f.geometry()
            g.transform(xform_mz) 
            g_2d = force_2d_line(g)
            if not g_2d.isNull():
                idx_mz.addFeature(f.id(), g_2d.boundingBox())
                dict_mz[f.id()] = g_2d

        idx_seg = QgsSpatialIndex()
        dict_seg = {}
        dict_seg_dir = {}
        dict_seg_id = {}
        seg_field_names = segmentos.fields().names()
        
        for f in segmentos.getFeatures():
            g = f.geometry()
            g.transform(xform_seg)
            g_2d = force_2d_line(g)
            if not g_2d.isNull():
                idx_seg.addFeature(f.id(), g_2d.boundingBox())
                dict_seg[f.id()] = g_2d
                # Guardamos Dirección e ID del segmento
                dict_seg_dir[f.id()] = str(f.attribute(campo_dir_seg)) if campo_dir_seg in seg_field_names else ""
                dict_seg_id[f.id()] = str(f.attribute(campo_id_seg)) if campo_id_seg in seg_field_names else ""

        total = postes.featureCount()
        paso = 100.0 / total if total else 0
        creados = 0

        # --- BUCLE PRINCIPAL ---
        for current, poste in enumerate(postes.getFeatures()):
            if feedback.isCanceled(): break

            geom_poste = poste.geometry()
            if geom_poste.isNull(): continue
            pt_P = QgsPointXY(geom_poste.asPoint())

            val_poste = poste.attribute(campo_dir_poste)
            dir_poste_str = str(val_poste).strip() if val_poste and str(val_poste) != 'NULL' else ""

            mejor_S = None
            mejor_id_seg = None
            
            cands_mz = idx_mz.nearestNeighbor(pt_P, 10) 
            if cands_mz:
                mejor_id_mz = None
                min_dist_mz = float('inf')
                
                for id_mz in cands_mz:
                    geom_cand = dict_mz[id_mz]
                    dist_real = geom_cand.distance(geom_poste)
                    if dist_real < min_dist_mz:
                        min_dist_mz = dist_real
                        mejor_id_mz = id_mz
                
                geom_mz = dict_mz[mejor_id_mz]
                res = geom_mz.closestSegmentWithContext(pt_P)
                
                if res and res[0] >= 0:
                    pt_M = res[1] 
                    idx_v2 = res[2]
                    v2 = geom_mz.vertexAt(idx_v2)
                    v1 = geom_mz.vertexAt(idx_v2 - 1)

                    dx = v2.x() - v1.x()
                    dy = v2.y() - v1.y()
                    largo = math.hypot(dx, dy)
                    
                    if largo > 0:
                        nx = -dy / largo
                        ny = dx / largo

                        DIST_RAYO = 150
                        rayo1 = QgsGeometry.fromPolylineXY([QgsPointXY(pt_M.x(), pt_M.y()), QgsPointXY(pt_M.x() + nx * DIST_RAYO, pt_M.y() + ny * DIST_RAYO)])
                        rayo2 = QgsGeometry.fromPolylineXY([QgsPointXY(pt_M.x(), pt_M.y()), QgsPointXY(pt_M.x() - nx * DIST_RAYO, pt_M.y() - ny * DIST_RAYO)])

                        min_dist_S = float('inf')
                        dist_P_to_M = math.hypot(pt_M.x() - pt_P.x(), pt_M.y() - pt_P.y())
                        tolerancia_cruce = dist_P_to_M + 2.0 

                        for rayo in [rayo1, rayo2]:
                            cands_seg = idx_seg.intersects(rayo.boundingBox())
                            for id_seg in cands_seg:
                                geom_seg = dict_seg[id_seg]
                                
                                if rayo.intersects(geom_seg):
                                    inter = rayo.intersection(geom_seg)
                                    it = inter.vertices()
                                    while it.hasNext():
                                        pt_i = it.next()
                                        d = math.hypot(pt_i.x() - pt_P.x(), pt_i.y() - pt_P.y())
                                        
                                        if d > 0.01 and d < min_dist_S:
                                            # Filtro Anti-Cruce
                                            linea_prueba = QgsGeometry.fromPolylineXY([pt_P, QgsPointXY(pt_i.x(), pt_i.y())])
                                            cruce_invalido = False
                                            
                                            cands_mz_inter = idx_mz.intersects(linea_prueba.boundingBox())
                                            for id_cruzada in cands_mz_inter:
                                                geom_mz_cand = dict_mz[id_cruzada]
                                                if linea_prueba.intersects(geom_mz_cand):
                                                    inter_mz = linea_prueba.intersection(geom_mz_cand)
                                                    it_mz = inter_mz.vertices()
                                                    while it_mz.hasNext():
                                                        pt_cruce = it_mz.next()
                                                        dist_cruce = math.hypot(pt_cruce.x() - pt_P.x(), pt_cruce.y() - pt_P.y())
                                                        if dist_cruce > tolerancia_cruce:
                                                            cruce_invalido = True
                                                            break
                                                if cruce_invalido: break
                                            
                                            if cruce_invalido: continue 

                                            min_dist_S = d
                                            mejor_S = QgsPointXY(pt_i.x(), pt_i.y())
                                            mejor_id_seg = id_seg

            # --- APLICACIÓN DEL ÁRBOL DE DECISIONES LÓGICO ---
            atributos_base = poste.attributes()

            if mejor_S:
                # CASO B: Encontró un segmento para conectarse
                dir_seg_str = dict_seg_dir.get(mejor_id_seg, "").strip()
                valor_id_seg = dict_seg_id.get(mejor_id_seg, "") # Obtenemos el ID del segmento
                
                if not dir_poste_str:
                    texto_dir2 = dir_seg_str
                    texto_revisar = "SIN CALLE POSTE"
                else:
                    dp_clean = dir_poste_str.lower()
                    ds_clean = dir_seg_str.lower()
                    texto_dir2 = dir_seg_str
                    
                    if ds_clean and (ds_clean in dp_clean):
                        texto_revisar = "OK"       
                    else:
                        texto_revisar = "REVISAR"  

                atributos_base.append(texto_dir2)
                atributos_base.append(texto_revisar)
                atributos_base.append(valor_id_seg) # Agregamos el ID_SEGMENT

                linea_final = QgsGeometry.fromPolylineXY([QgsPointXY(pt_M.x(), pt_M.y()), pt_P, mejor_S])
                f_linea = QgsFeature(campos_nuevos)
                f_linea.setGeometry(linea_final)
                f_linea.setAttributes(atributos_base)
                sink_lineas.addFeature(f_linea, QgsFeatureSink.FastInsert)
                creados += 1
                
                f_punto = QgsFeature(campos_nuevos)
                f_punto.setGeometry(geom_poste)
                f_punto.setAttributes(atributos_base)
                sink_postes.addFeature(f_punto, QgsFeatureSink.FastInsert)

            else:
                # CASO A: NO encontró segmento (Aislado o Filtro Anti-Cruce)
                texto_dir2 = dir_poste_str      
                texto_revisar = "SIN SEGMENTO"  
                valor_id_seg = ""               # Sin segmento = Sin ID

                atributos_base.append(texto_dir2)
                atributos_base.append(texto_revisar)
                atributos_base.append(valor_id_seg) # Agregamos el ID_SEGMENT en blanco
                
                f_punto_aislado = QgsFeature(campos_nuevos)
                f_punto_aislado.setGeometry(geom_poste)
                f_punto_aislado.setAttributes(atributos_base)
                sink_postes.addFeature(f_punto_aislado, QgsFeatureSink.FastInsert)
                
            feedback.setProgress(int(current * paso))

        feedback.pushInfo(f"PROCESO TERMINADO: Se auditaron {total} postes y se trazaron {creados} acometidas.")
        return {self.OUTPUT_LINEAS: dest_id_lineas, self.OUTPUT_POSTES: dest_id_postes}