namespace XrRacing.Gameplay.Race
{
    /// <summary>
    /// Where the race is: RaceManager moves through these from spawn to the finish line.
    /// </summary>
    public enum RaceState
    {
        None,       // Playground / free roam: no race logic
        Ready,      // Placed at spawn
        Countdown,  // 3, 2, 1, START!
        Racing,
        Finished    // Crossed the goal, controls locked
    }
}
