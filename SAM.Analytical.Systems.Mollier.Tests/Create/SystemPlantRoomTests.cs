using System.Collections.Generic;
using System.Linq;
using SAM.Analytical;
using SAM.Analytical.Systems.Mollier;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using Xunit;
using Mollier = SAM.Analytical.Systems.Mollier;

namespace SAM.Analytical.Systems.Mollier.Tests.Create
{
    public class SystemPlantRoomTests
    {
        private static readonly double Pressure = 101325.0;
        private static readonly double Airflow = 2.0;

        private static MollierPoint CreatePoint(double dryBulb, double humidityRatio = 0.008)
        {
            return new MollierPoint(dryBulb, humidityRatio, Pressure);
        }

        [Fact]
        public void SingleChain_CreatesPlantRoom()
        {
            MollierPoint start = CreatePoint(20.0);
            List<IMollierProcess> processes = new List<IMollierProcess>
            {
                start.HeatingProcess(30.0),
                start.FanProcess(1.5),
                start.CoolingProcess(12.0, 0.8)
            };

            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(processes, Airflow);
            Assert.NotNull(plantRoom);
        }

        [Fact]
        public void SingleChain_ContainsCorrect_CcomponentCount()
        {
            MollierPoint start = CreatePoint(20.0);
            List<IMollierProcess> processes = new List<IMollierProcess>
            {
                start.HeatingProcess(30.0),
                start.FanProcess(1.5)
            };

            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(processes, Airflow);
            List<ISystemComponent> components = plantRoom.GetSystemComponents<ISystemComponent>();
            Assert.NotNull(components);
            Assert.True(components.Count >= 2);
        }

        [Fact]
        public void SingleChain_ContainsAirSystem()
        {
            MollierPoint start = CreatePoint(20.0);
            List<IMollierProcess> processes = new List<IMollierProcess>
            {
                start.HeatingProcess(30.0)
            };

            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(processes, Airflow);
            List<ISystem> systems = plantRoom.GetSystems();
            Assert.NotNull(systems);
            Assert.NotEmpty(systems);
            Assert.Contains(systems, s => s is AirSystem);
        }

        [Fact]
        public void SingleChain_HasHeatingCoil_WhenHeatingIncluded()
        {
            MollierPoint start = CreatePoint(20.0);
            List<IMollierProcess> processes = new List<IMollierProcess>
            {
                start.HeatingProcess(35.0)
            };

            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(processes, Airflow);
            List<SystemHeatingCoil> coils = plantRoom.GetSystemComponents<SystemHeatingCoil>();
            Assert.NotNull(coils);
            Assert.Single(coils);
        }

        [Fact]
        public void SingleChain_EmptyProcesses_ReturnsNull()
        {
            List<IMollierProcess> processes = new List<IMollierProcess>();
            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(processes, Airflow);
            Assert.Null(plantRoom);
        }

        [Fact]
        public void SingleChain_NaN_Airflow_StillWorks()
        {
            MollierPoint start = CreatePoint(20.0);
            List<IMollierProcess> processes = new List<IMollierProcess>
            {
                start.HeatingProcess(30.0)
            };

            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(processes, double.NaN);
            Assert.NotNull(plantRoom);
        }

        [Fact]
        public void SingleChain_ConnectionsPresent()
        {
            MollierPoint start = CreatePoint(20.0);
            List<IMollierProcess> processes = new List<IMollierProcess>
            {
                start.HeatingProcess(30.0),
                start.FanProcess(1.5)
            };

            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(processes, Airflow);
            List<ISystemConnection> connections = plantRoom.GetSystemConnections();
            Assert.NotNull(connections);
            Assert.True(connections.Count >= 1);
        }

        [Fact]
        public void MollierGroup_CreatesPlantRoom()
        {
            MollierPoint start = CreatePoint(20.0);
            MollierGroup group = new MollierGroup("Test Group");
            group.Add(start.HeatingProcess(30.0));
            group.Add(start.FanProcess(1.5));

            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(group, Airflow);
            Assert.NotNull(plantRoom);
        }
    }
}
