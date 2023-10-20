using System;
using System.Collections.Generic;

namespace AutoCommandQueuePickup.Hooks;

public class HookManager
{
    public readonly Dictionary<Type, AbstractHookHandler> HookHandlers = new();

    public readonly AutoCommandQueuePickup Plugin;

    public HookManager(AutoCommandQueuePickup pluginInstance)
    {
        Plugin = pluginInstance;

        RegisterHandler<CreatePickupDropletHandler>();
        RegisterHandler<PrinterTargetHandler>();
        RegisterHandler<ScrapperTargetHandler>();
        RegisterHandler<CommandTargetHandler>();
        RegisterHandler<CommandHandler>();
        RegisterHandler<ItemHandler>();
        RegisterHandler<PickupDropletOnCollisionOverrideHandler>();
    }

    public void RegisterHandler<T>() where T : AbstractHookHandler, new()
    {
        var instance = new T();
        instance.Init(this);
        HookHandlers[typeof(T)] = instance;
    }

    public T GetHandler<T>() where T : AbstractHookHandler
    {
        return (T)HookHandlers[typeof(T)];
    }

    public void RegisterHooks()
    {
        foreach (var handler in HookHandlers.Values) handler.RegisterHooks();
    }

    public void UnregisterHooks()
    {
        foreach (var handler in HookHandlers.Values) handler.UnregisterHooks();
    }
}