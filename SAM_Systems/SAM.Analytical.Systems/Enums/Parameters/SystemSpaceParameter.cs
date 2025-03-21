﻿using System.ComponentModel;
using SAM.Core.Attributes;

namespace SAM.Analytical.Systems
{
    [AssociatedTypes(typeof(SystemSpace)), Description("System Space Parameter")]
    public enum SystemSpaceParameter
    {
        [ParameterProperties("Domestic Hot Water Collection", "Domestic Hot Water Collection"), SAMObjectParameterValue(typeof(CollectionLink))] DomesticHotWaterCollection,
        [ParameterProperties("Equipment Electrical Collection", "Equipment Electrical Collection"), SAMObjectParameterValue(typeof(CollectionLink))] EquipmentElectricalCollection,
        [ParameterProperties("Lighting Electrical Collection", "Lighting Electrical Collection"), SAMObjectParameterValue(typeof(CollectionLink))] LightingElectricalCollection,
        [ParameterProperties("Space Name", "Space Name"), ParameterValue(Core.ParameterType.String)] SpaceName,
    }
}
