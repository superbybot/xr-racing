var type = System.Type.GetType("Meta.XR.Simulator.Editor.XRSimMenu, MetaXRSimulatorCore.Editor");
if (type != null)
{
    var method = type.GetMethod("ActivateSimulator", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
    method?.Invoke(null, null);
    return "Invoked XRSimMenu.ActivateSimulator";
}
return "XRSimMenu type not found";
