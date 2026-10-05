using System;
using System.Linq;
using System.Reflection;

namespace FFLogsUploaderPlugin.Integration;

public class EngageTimer
{
    private const BindingFlags BindingFlagsAll = BindingFlags.Public
                                       | BindingFlags.NonPublic
                                       | BindingFlags.Instance
                                       | BindingFlags.Static
                                       | BindingFlags.GetField
                                       | BindingFlags.SetField
                                       | BindingFlags.GetProperty
                                       | BindingFlags.SetProperty;
    
    private object? combatStopwatch;
    private FieldInfo? combatTimeStartField;
    private FieldInfo? combatTimeEndField;
    // private FieldInfo? shouldRestartCombatTimerField;

    private object? etState;
    private PropertyInfo? inCombatProperty;

    public bool InCombat
    {
        get => etState != null
               && inCombatProperty is { GetMethod: not null }
               && (bool)inCombatProperty.GetMethod.Invoke(etState, [])!;
        set
        {
            if (etState != null && inCombatProperty is { SetMethod: not null })
                inCombatProperty.SetMethod.Invoke(etState, [value]);
        }
    }

    public DateTime? CombatStart
    {
        get => combatStopwatch != null
                   ? (DateTime?)combatTimeStartField?.GetValue(combatStopwatch)
                   : null;
        set
        {
            if (combatStopwatch != null && value.HasValue)
                combatTimeStartField?.SetValue(combatStopwatch, value.Value);
        }
    }
    
    public DateTime? CombatEnd
    {
        get => combatStopwatch != null
                   ? (DateTime?)combatTimeEndField?.GetValue(combatStopwatch)
                   : null;
        set
        {
            if (combatStopwatch != null && value.HasValue)
                combatTimeEndField?.SetValue(combatStopwatch, value.Value);
        }
    }
    
    public EngageTimer()
    {
        ReloadTypes();
    }

    public void ReloadTypes()
    {
        var etPluginType = AppDomain.CurrentDomain.GetAssemblies()
                                .SelectMany(a => a.GetTypes())
                                .FirstOrDefault(t => t.FullName == "EngageTimer.Plugin");
        var etFrameworkThings = etPluginType?.GetProperty("FrameworkThings", BindingFlagsAll)
                                                    ?.GetMethod?.Invoke(null, []);
        var etFrameworkThingsType = etPluginType?.Assembly.GetType("EngageTimer.Status.FrameworkThings");
        var etCombatStopwatchType = etPluginType?.Assembly.GetType("EngageTimer.Status.CombatStopwatch");

        combatStopwatch = etFrameworkThings != null
                              ? etFrameworkThingsType?.GetField("_combatStopwatch", BindingFlagsAll)
                                                     ?.GetValue(etFrameworkThings)
                              : null;
        combatTimeStartField = etCombatStopwatchType?.GetField("_combatTimeStart", BindingFlagsAll);
        combatTimeEndField = etCombatStopwatchType?.GetField("_combatTimeEnd", BindingFlagsAll);
        // shouldRestartCombatTimerField = etCombatStopwatchType?.GetField("_shouldRestartCombatTimer", BindingFlagsAll);
        
        etState = etPluginType?.GetProperty("State", BindingFlagsAll)?.GetMethod?.Invoke(null, []);
        var etStateType = etPluginType?.Assembly.GetType("EngageTimer.Status.State");
        inCombatProperty = etStateType?.GetProperty("InCombat", BindingFlagsAll);
        
        Plugin.Log.Debug(
            "EngageTimer: State={State} InCombat={InCombat} CombatStopwatch={CombatStopwatch} _combatTimeStart={CombatTimeStart} _combatTimeEnd={CombatTimeEnd}",
            etState, InCombat, combatStopwatch, CombatStart, CombatEnd);
    }
}
