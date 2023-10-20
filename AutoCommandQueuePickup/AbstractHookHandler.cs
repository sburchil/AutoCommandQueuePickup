using AutoCommandQueuePickup.Configuration;

namespace AutoCommandQueuePickup.Hooks;

public abstract class AbstractHookHandler
{
    internal HookManager hookManager;

    public AutoCommandQueuePickup Plugin => hookManager.Plugin;
    public Config ModConfig => AutoCommandQueuePickup.AutoPickupConfig;

    public void Init(HookManager _hookManager)
    {
        hookManager = _hookManager;
    }

    //TODO: Figure out if those can be automated by storing reference to hook and method
    public abstract void RegisterHooks();

    public abstract void UnregisterHooks();
}