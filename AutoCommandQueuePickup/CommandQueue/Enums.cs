namespace AutoCommandQueuePickup
{
    public enum Cause
    {
        Teleport,
        Drop
    }

    public enum Mode
    {
        Sequential,
        Random,
        Closest,
        LeastItems
    }

    public enum Distribution
    {
        OnTeleport,
        OnDrop
    }
}