using System.Collections.Generic;
using System.Linq;
using Koovra.Cto.Core;
using Xunit;

namespace CtoAutocadAddin.Tests
{
    public class CapacityAllocatorTests
    {
        private static CapacityAllocator.PropRef Prop(
            string id, double param, int side = 1, int hp = 1, double x = 0, double y = 0,
            string seg = "S1")
        {
            return new CapacityAllocator.PropRef
            { Id = id, Param = param, Side = side, Hp = hp, X = x, Y = y, SegmentId = seg };
        }

        private static CapacityAllocator.BoxRef Box(
            string id, double param, int side = 1, double x = 0, double y = 0, string seg = "S1")
        {
            return new CapacityAllocator.BoxRef
            { Id = id, Param = param, Side = side, X = x, Y = y, SegmentId = seg };
        }

        [Fact]
        public void CabenTodas_UnaSolaCaja()
        {
            var props = new List<CapacityAllocator.PropRef> { Prop("p1", 10), Prop("p2", 20), Prop("p3", 30) };
            var boxes = new List<CapacityAllocator.BoxRef> { Box("b1", 15) };

            var r = CapacityAllocator.Allocate(props, boxes);

            Assert.Equal(3, r.BoxByProperty.Count);
            Assert.All(props, p => Assert.Equal("b1", r.BoxByProperty[p.Id]));
            Assert.Equal(3, r.LoadByBox["b1"]);
            Assert.Empty(r.OverCapacity);
            Assert.Empty(r.Unassigned);
        }

        [Fact]
        public void RespetaElCupo_DesbordaALaSiguienteCaja()
        {
            var props = Enumerable.Range(1, 10).Select(i => Prop("p" + i, i * 10)).ToList();
            var boxes = new List<CapacityAllocator.BoxRef> { Box("b1", 5), Box("b2", 200) };

            var r = CapacityAllocator.Allocate(props, boxes, capacity: 8);

            Assert.Equal(8, r.LoadByBox["b1"]);
            Assert.Equal(2, r.LoadByBox["b2"]);
            Assert.Empty(r.OverCapacity);
        }

        [Fact]
        public void AsignaEnOrdenParametrico_SinCruces()
        {
            // Propiedades y cajas alternadas a lo largo del eje: la asignación debe ser monótona.
            var props = Enumerable.Range(0, 12).Select(i => Prop("p" + i, i)).ToList();
            var boxes = new List<CapacityAllocator.BoxRef> { Box("b1", 0), Box("b2", 100) };

            var r = CapacityAllocator.Allocate(props, boxes, capacity: 8);

            // Los primeros 8 (menor param) van a b1; los últimos 4 a b2. Nunca se intercalan.
            for (int i = 0; i < 8; i++) Assert.Equal("b1", r.BoxByProperty["p" + i]);
            for (int i = 8; i < 12; i++) Assert.Equal("b2", r.BoxByProperty["p" + i]);
        }

        [Fact]
        public void PrefiereCajaDeLaMismaVereda_AunqueEsteMasLejos()
        {
            var props = new List<CapacityAllocator.PropRef> { Prop("p1", 50, side: 1, x: 0, y: 0) };
            var boxes = new List<CapacityAllocator.BoxRef>
            {
                Box("cerca_otra_vereda", 50, side: -1, x: 1,   y: 0),
                Box("lejos_misma_vereda", 50, side:  1, x: 500, y: 0),
            };

            var r = CapacityAllocator.Allocate(props, boxes);

            Assert.Equal("lejos_misma_vereda", r.BoxByProperty["p1"]);
        }

        [Fact]
        public void SinCajasEnLaVereda_UsaLasDelSegmentoSinMarcarExceso()
        {
            var props = new List<CapacityAllocator.PropRef> { Prop("p1", 10, side: 1) };
            var boxes = new List<CapacityAllocator.BoxRef> { Box("b1", 10, side: -1) };

            var r = CapacityAllocator.Allocate(props, boxes);

            Assert.Equal("b1", r.BoxByProperty["p1"]);
            Assert.Empty(r.OverCapacity);
        }

        [Fact]
        public void SegmentoSinCajas_QuedaSinAsignar()
        {
            var props = new List<CapacityAllocator.PropRef>
            {
                Prop("p1", 10, seg: "S1"),
                Prop("p2", 20, seg: "SIN_CAJAS"),
            };
            var boxes = new List<CapacityAllocator.BoxRef> { Box("b1", 10, seg: "S1") };

            var r = CapacityAllocator.Allocate(props, boxes);

            Assert.Equal("b1", r.BoxByProperty["p1"]);
            Assert.Equal(new[] { "p2" }, r.Unassigned);
            Assert.False(r.BoxByProperty.ContainsKey("p2"));
        }

        [Fact]
        public void CupoAgotado_IgualAsigna_YMarcaExceso()
        {
            var props = Enumerable.Range(1, 10).Select(i => Prop("p" + i, i)).ToList();
            var boxes = new List<CapacityAllocator.BoxRef> { Box("b1", 0) };

            var r = CapacityAllocator.Allocate(props, boxes, capacity: 8);

            // Nunca dejamos una propiedad sin línea: 10 propiedades = 10 asignaciones.
            Assert.Equal(10, r.BoxByProperty.Count);
            Assert.Equal(2, r.OverCapacity.Count);
            Assert.Equal(10, r.LoadByBox["b1"]);
        }

        [Fact]
        public void PropiedadMasGrandeQueElCupo_SeAsignaYSeMarca()
        {
            var props = new List<CapacityAllocator.PropRef> { Prop("mdu", 10, hp: 12) };
            var boxes = new List<CapacityAllocator.BoxRef> { Box("b1", 10) };

            var r = CapacityAllocator.Allocate(props, boxes, capacity: 8);

            Assert.Equal("b1", r.BoxByProperty["mdu"]);
            Assert.Equal(new[] { "mdu" }, r.OverCapacity);
            Assert.Equal(12, r.LoadByBox["b1"]);
        }

        [Fact]
        public void HpCeroONegativo_CuentaComoUno()
        {
            var props = new List<CapacityAllocator.PropRef> { Prop("p1", 1, hp: 0), Prop("p2", 2, hp: -3) };
            var boxes = new List<CapacityAllocator.BoxRef> { Box("b1", 0) };

            var r = CapacityAllocator.Allocate(props, boxes);

            Assert.Equal(2, r.LoadByBox["b1"]);
        }

        [Fact]
        public void EsDeterminista()
        {
            var props = Enumerable.Range(1, 20).Select(i => Prop("p" + i, i % 7, side: i % 2 == 0 ? 1 : -1)).ToList();
            var boxes = new List<CapacityAllocator.BoxRef>
            { Box("b1", 1), Box("b2", 3, side: -1), Box("b3", 5) };

            var a = CapacityAllocator.Allocate(props, boxes);
            var b = CapacityAllocator.Allocate(props, boxes);

            Assert.Equal(a.BoxByProperty.OrderBy(k => k.Key).ToList(),
                         b.BoxByProperty.OrderBy(k => k.Key).ToList());
        }

        [Fact]
        public void ConservacionTotal_TodaPropiedadEstaAsignadaOReportada()
        {
            var props = Enumerable.Range(1, 50)
                .Select(i => Prop("p" + i, i, side: i % 3 == 0 ? -1 : 1, seg: i > 40 ? "VACIO" : "S1"))
                .ToList();
            var boxes = new List<CapacityAllocator.BoxRef>
            { Box("b1", 5), Box("b2", 25, side: -1), Box("b3", 35) };

            var r = CapacityAllocator.Allocate(props, boxes, capacity: 8);

            foreach (var p in props)
                Assert.True(r.BoxByProperty.ContainsKey(p.Id) || r.Unassigned.Contains(p.Id),
                            "propiedad perdida: " + p.Id);
        }

        [Fact]
        public void SinPropiedades_DevuelveResultadoVacio()
        {
            var r = CapacityAllocator.Allocate(new List<CapacityAllocator.PropRef>(), null);

            Assert.Empty(r.BoxByProperty);
            Assert.Empty(r.Unassigned);
        }
    }
}
