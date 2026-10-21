// Commands the officer can shout, in menu order clockwise from the top
public enum VerbalCommand
{
    Stop,
    StepBack,
    FaceTheWall,
    HandsBehindBack,
    ForceWarning,
    CalmDown
}

public static class VerbalCommands
{
    // How far a shout carries, m
    public const float ShoutRadius = 12f;

    // Short label for the menu
    public static string MenuLabel(VerbalCommand c)
    {
        switch (c)
        {
            case VerbalCommand.Stop: return "STOP";
            case VerbalCommand.StepBack: return "STEP BACK";
            case VerbalCommand.FaceTheWall: return "FACE THE WALL";
            case VerbalCommand.HandsBehindBack: return "HANDS BEHIND BACK";
            case VerbalCommand.ForceWarning: return "WARNING";
            case VerbalCommand.CalmDown: return "CALM DOWN";
            default: return c.ToString();
        }
    }

    // What the officer says out loud
    public static string Spoken(VerbalCommand c)
    {
        switch (c)
        {
            case VerbalCommand.Stop: return "Stop! Stay where you are!";
            case VerbalCommand.StepBack: return "Step back! Keep your distance!";
            case VerbalCommand.FaceTheWall: return "Turn around, face the wall, hands on the wall!";
            case VerbalCommand.HandsBehindBack: return "Hands behind your back!";
            case VerbalCommand.ForceWarning: return "Stop now or force will be used!";
            case VerbalCommand.CalmDown: return "Calm down. Talk to me, what's going on?";
            default: return c.ToString();
        }
    }
}
