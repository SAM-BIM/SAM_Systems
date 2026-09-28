"""Repository-specific classification decisions for SAM_Systems (the only non-shared tool file).

OVERRIDES     : component display name -> (object glyph, op, extra)   extra: None | "plural" | "library" | note
PARAM_OBJECTS : param type key (Goo<X>Param class or typeof(X) name) -> glyph | (glyph, container, plural)
OBJECTS/VERBS : extra noun/verb rules tried before the shared ones (same shapes as SAM's OBJECTS/VERBS)
"""
OVERRIDES = {
    # system components (coils, fans, exchangers...) are equipment objects -> SAM `ahu` glyph
    "SAM.AnalyticalSystemComponentType": ("ahu", "value", None),
    "SAMSystems.CreateComponentByMollierProcess": ("ahu", "create", "produces a SystemComponent from a Mollier process"),
    "SAMAnalytical.SystemToAHU": ("ahu", "convert", None),
    "SAMAnalytical.ConnectedSystemComponents": ("ahu", "get", "plural"),
    "SAMAnalytical.OrderedSystemComponents": ("ahu", "sort", "plural"),
    "SAMAnalytical.ConnectedSystemGroups": ("system", "get", "plural"),
    "SAMAnalytical.ConnectedSystemObjects": ("object", "get", "plural"),
    "SAMAnalytical.ConnectedSensor": ("thermometer", "get", "the sensor a controller reads"),
    # connectors and display managers
    "SAMAnalytical.SystemConnector": ("connector", "create", None),
    "SAMAnalytical.CreateDisplaySystemConnectorManager": ("connector", "create", "plural"),
    "SAMAnalytical.CreateDisplaySystemManager": ("settings", "create", "component-type -> symbol display settings"),
    "SAMAnalytical.CreateSystemGeometrySymbol": ("geometry2D", "create", "2D schematic symbol"),
    "SAMSystems.CreateDisplaySystemEnergyCentre": ("energyCentre", "display", "previewable schematic"),
    # airflow edits on the energy centre
    "SystemEnergyCentre.ModifyByAirflows": ("airflow", "set", None),
    "SystemEnergyCentre.ModifyFanByAirflows": ("fan", "modify", None),
    # results
    "SAMAnalytical.SystemResults": ("result", "get", "plural"),
    "SAMAnalytical.SystemResultValueByHourOfYear": ("result", "get", None),
    # catalogue / examples
    "SAMAnalytical.SystemVentilationUnitCatalogue": ("fan", "import", "library"),
    "SAMSystems.MollierTwinWheelExample": ("processHeatRecovery", "create", "worked twin-wheel heat-recovery example"),
}
PARAM_OBJECTS = {
    "SystemComponent": "ahu", "ISystemComponent": "ahu",
    "SystemGroup": ("system", None, True), "ISystemGroup": ("system", None, True),
    "SystemObject": "object", "ISystemObject": "object",
    "SystemSpace": "space", "ISystemSpace": "space",
    "SystemResult": "result", "ISystemResult": "result",
}
OBJECTS = []
VERBS = []
