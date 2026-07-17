// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SAM.Analytical.Systems.Mollier;
using SAM.Core.Systems;
using Xunit;
using Mollier = SAM.Analytical.Systems.Mollier;

namespace SAM.Analytical.Systems.Mollier.Tests.Create
{
    /// <summary>
    /// Proves the liquid-loop injection (Create.InjectLiquidSystems) that TwinWheelExample.Create() triggers
    /// because its supply chain carries both a cooling coil and a reheat heating coil: a closed Boiler loop for
    /// the heating coil, a closed Chiller loop for the cooling coil, each on its own named LiquidSystem, plus the
    /// two default energy sources linked to the stored plant equipment by name. Also proves the template-merge
    /// path (SystemEnergyCentre(..., template)) skips injection entirely when the caller supplies its own plant.
    /// </summary>
    public class LiquidLoopTests
    {
        private const string MissingTemplateMessage =
            "Plantroom-Only.json fixture missing from test output - the csproj <None CopyToOutputDirectory> item must copy files\\resources\\Analytical\\Systems\\SystemEnergyCentre\\Plantroom-Only.json.";

        private static SystemPlantRoom TwinWheelPlantRoom(out SystemEnergyCentre energyCentre)
        {
            energyCentre = TwinWheelExample.Create();
            return energyCentre.GetSystemPlantRooms().Single();
        }

        [Fact]
        public void TwinWheel_Injects_OneBoiler_And_OneChiller()
        {
            SystemPlantRoom plantRoom = TwinWheelPlantRoom(out _);
            Assert.Single(plantRoom.GetSystemComponents<SystemBoiler>());
            Assert.Single(plantRoom.GetSystemComponents<SystemAirSourceChiller>());
        }

        [Fact]
        public void TwinWheel_Injects_HeatingAndCooling_LiquidSystems()
        {
            SystemPlantRoom plantRoom = TwinWheelPlantRoom(out _);
            List<LiquidSystem> liquidSystems = plantRoom.GetSystems().OfType<LiquidSystem>().ToList();

            Assert.Equal(2, liquidSystems.Count);
            Assert.Contains(liquidSystems, x => x.Name == "Heating Hot Water");
            Assert.Contains(liquidSystems, x => x.Name == "Chilled Water");
        }

        [Fact]
        public void HeatingCoil_Has_ConnectedLiquid_InAndOut()
        {
            SystemPlantRoom plantRoom = TwinWheelPlantRoom(out _);
            SystemType liquidType = new SystemType(typeof(LiquidSystem));

            List<SystemHeatingCoil> coils = plantRoom.GetSystemComponents<SystemHeatingCoil>();
            Assert.NotEmpty(coils);
            foreach (SystemHeatingCoil coil in coils)
            {
                List<int> inIndexes = plantRoom.Indexes(coil, liquidType, ConnectorStatus.Connected, SAM.Core.Direction.In);
                List<int> outIndexes = plantRoom.Indexes(coil, liquidType, ConnectorStatus.Connected, SAM.Core.Direction.Out);
                Assert.Contains(3, inIndexes);
                Assert.Contains(4, outIndexes);
            }
        }

        [Fact]
        public void CoolingCoil_Has_ConnectedLiquid_InAndOut()
        {
            SystemPlantRoom plantRoom = TwinWheelPlantRoom(out _);
            SystemType liquidType = new SystemType(typeof(LiquidSystem));

            List<SystemCoolingCoil> coils = plantRoom.GetSystemComponents<SystemCoolingCoil>();
            Assert.NotEmpty(coils);
            foreach (SystemCoolingCoil coil in coils)
            {
                List<int> inIndexes = plantRoom.Indexes(coil, liquidType, ConnectorStatus.Connected, SAM.Core.Direction.In);
                List<int> outIndexes = plantRoom.Indexes(coil, liquidType, ConnectorStatus.Connected, SAM.Core.Direction.Out);
                Assert.Contains(2, inIndexes);
                Assert.Contains(3, outIndexes);
            }
        }

        [Fact]
        public void Boiler_And_Chiller_LoopsAreClosed()
        {
            SystemPlantRoom plantRoom = TwinWheelPlantRoom(out _);
            SystemType liquidType = new SystemType(typeof(LiquidSystem));

            // boiler.Out(1) -> coil.In(3) ... coil.Out(4) -> boiler.In(0): both boiler connectors are used, and
            // neither is left unconnected, proving the loop actually closes rather than dead-ending.
            SystemBoiler boiler = plantRoom.GetSystemComponents<SystemBoiler>().Single();
            Assert.Contains(0, plantRoom.Indexes(boiler, liquidType, ConnectorStatus.Connected, SAM.Core.Direction.In));
            Assert.Contains(1, plantRoom.Indexes(boiler, liquidType, ConnectorStatus.Connected, SAM.Core.Direction.Out));
            Assert.Empty(plantRoom.Indexes(boiler, liquidType, ConnectorStatus.Unconnected, SAM.Core.Direction.In));
            Assert.Empty(plantRoom.Indexes(boiler, liquidType, ConnectorStatus.Unconnected, SAM.Core.Direction.Out));

            // Same connector layout on the chiller (confirmed by direct probe: In=0/Out=1, same as the boiler).
            SystemAirSourceChiller chiller = plantRoom.GetSystemComponents<SystemAirSourceChiller>().Single();
            Assert.Contains(0, plantRoom.Indexes(chiller, liquidType, ConnectorStatus.Connected, SAM.Core.Direction.In));
            Assert.Contains(1, plantRoom.Indexes(chiller, liquidType, ConnectorStatus.Connected, SAM.Core.Direction.Out));
            Assert.Empty(plantRoom.Indexes(chiller, liquidType, ConnectorStatus.Unconnected, SAM.Core.Direction.In));
            Assert.Empty(plantRoom.Indexes(chiller, liquidType, ConnectorStatus.Unconnected, SAM.Core.Direction.Out));
        }

        [Fact]
        public void Boiler_IsRelatedTo_HeatingSystem_And_Chiller_ToCoolingSystem()
        {
            SystemPlantRoom plantRoom = TwinWheelPlantRoom(out _);

            LiquidSystem heating = plantRoom.GetSystems().OfType<LiquidSystem>().Single(x => x.Name == "Heating Hot Water");
            LiquidSystem cooling = plantRoom.GetSystems().OfType<LiquidSystem>().Single(x => x.Name == "Chilled Water");

            List<ISystemComponent> heatingMembers = plantRoom.GetSystemComponents<ISystemComponent>(heating);
            List<ISystemComponent> coolingMembers = plantRoom.GetSystemComponents<ISystemComponent>(cooling);

            Assert.Contains(heatingMembers, c => c is SystemBoiler);
            Assert.Contains(heatingMembers, c => c is SystemHeatingCoil);
            Assert.DoesNotContain(heatingMembers, c => c is SystemAirSourceChiller);

            Assert.Contains(coolingMembers, c => c is SystemAirSourceChiller);
            Assert.Contains(coolingMembers, c => c is SystemCoolingCoil);
            Assert.DoesNotContain(coolingMembers, c => c is SystemBoiler);
        }

        [Fact]
        public void EnergySources_AreCreated_AndLinkedByName()
        {
            SystemPlantRoom plantRoom = TwinWheelPlantRoom(out SystemEnergyCentre energyCentre);
            List<SystemEnergySource> sources = energyCentre.GetSystemEnergySources();

            Assert.Equal(2, sources.Count);

            SystemEnergySource gas = sources.Single(x => x.Name == "Natural Gas");
            Assert.IsNotType<ElectricalEnergySource>(gas);
            Assert.Equal(0.216, gas.CO2Factor[0], 6);

            SystemEnergySource electricity = sources.Single(x => x.Name == "Grid Supplied Electricity");
            Assert.IsType<ElectricalEnergySource>(electricity);
            Assert.Equal(0.519, electricity.CO2Factor[0], 6);

            // The STORED boiler/chiller (fetched from the plant room, since Add clones its argument), not the
            // detached objects the injector built locally.
            SystemBoiler storedBoiler = plantRoom.GetSystemComponents<SystemBoiler>().Single();
            Assert.Equal("Natural Gas", storedBoiler.GetValue<string>(SystemObjectParameter.EnergySourceName));

            SystemAirSourceChiller storedChiller = plantRoom.GetSystemComponents<SystemAirSourceChiller>().Single();
            Assert.Equal("Grid Supplied Electricity", storedChiller.GetValue<string>(SystemObjectParameter.EnergySourceName));
            Assert.Equal("Grid Supplied Electricity", storedChiller.GetValue<string>(SystemObjectParameter.FanEnergySourceName));
        }

        private static string GetTemplatePath()
        {
            string candidate = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"files\resources\Analytical\Systems\SystemEnergyCentre\Plantroom-Only.json"));
            return File.Exists(candidate) ? candidate : null;
        }

        [Fact]
        public void TemplatePath_DoesNotInject_DuplicatePlant()
        {
            string templatePath = GetTemplatePath();
            Assert.False(templatePath == null, MissingTemplateMessage);

            SystemEnergyCentre template = SAM.Analytical.Systems.Query.SystemEnergyCentre(templatePath);
            Assert.NotNull(template);

            SystemPlantRoom templateRoom = template.GetSystemPlantRooms().Single();
            int templateBoilerCount = templateRoom.GetSystemComponents<SystemBoiler>()?.Count ?? 0;
            int templateChillerCount = templateRoom.GetSystemComponents<SystemAirSourceChiller>()?.Count ?? 0;
            List<SystemEnergySource> templateSources = template.GetSystemEnergySources();

            // Ground truth for this fixture (confirmed by direct probe): Plantroom-Only.json carries no
            // SystemBoiler / SystemAirSourceChiller of its own - only an unrelated DisplaySystemMultiBoiler - so
            // asserting these are zero is what makes the merge assertions below a real proof of "no injection",
            // not a coincidence of matching counts.
            Assert.Equal(0, templateBoilerCount);
            Assert.Equal(0, templateChillerCount);

            SystemEnergyCentre merged = TwinWheelExample.Create(2.5, 2.3, template);
            Assert.NotNull(merged);

            SystemPlantRoom mergedRoom = merged.GetSystemPlantRooms().Single();

            // No more boiler/chiller plant than the template already had: the template path must skip
            // InjectLiquidSystems entirely (SystemEnergyCentreByTemplate.cs only calls it when template == null).
            Assert.Equal(templateBoilerCount, mergedRoom.GetSystemComponents<SystemBoiler>()?.Count ?? 0);
            Assert.Equal(templateChillerCount, mergedRoom.GetSystemComponents<SystemAirSourceChiller>()?.Count ?? 0);

            // No injector-named liquid loops were added on top of the template's own.
            List<LiquidSystem> mergedLiquidSystems = mergedRoom.GetSystems()?.OfType<LiquidSystem>().ToList() ?? new List<LiquidSystem>();
            Assert.DoesNotContain(mergedLiquidSystems, x => x.Name == "Heating Hot Water");
            Assert.DoesNotContain(mergedLiquidSystems, x => x.Name == "Chilled Water");

            // Energy sources are exactly the template's own, not doubled up with the injector's defaults.
            List<SystemEnergySource> mergedSources = merged.GetSystemEnergySources();
            Assert.Equal(templateSources.Count, mergedSources.Count);
            Assert.Equal(templateSources.Select(x => x.Name).OrderBy(n => n), mergedSources.Select(x => x.Name).OrderBy(n => n));

            // The Mollier air side really was merged in (proves the merge did something, not a no-op).
            Assert.Equal(1, mergedRoom.GetSystems()?.OfType<AirSystem>().Count() ?? 0);
        }
    }
}
