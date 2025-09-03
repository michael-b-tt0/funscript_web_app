namespace funscript_web_app.models;

public class FunHalver_options
{
    public bool RemoveShortPauses { get; set; }
    public int ShortPauseDuration { get; set; } = 2000;
    public bool MatchFirstDownstroke { get; set; }
    public bool ResetAfterPause { get; set; }
    
    public bool MatchGroupEndPosition { get; set; }
}

public class FunDoublerOptions
{
    public bool MatchGroupEnd { get; set; }
    public int ShortPauseDuration { get; set; } = 100;
}

public class FunHalver_options_2
{
    public bool RemoveShortPauses { get; set; }
    public int ShortPauseDuration { get; set; } = 2000;
    public bool MatchFirstDownstroke { get; set; }
    public bool ResetAfterPause { get; set; }

    public bool MatchGroupEndPosition { get; set; }

    public float SpeedThreshold { get; set; } = 0f;
}

public class FunQuarter_options
{
    public bool RemoveShortPauses { get; set; }
    public int ShortPauseDuration { get; set; } = 2000;
    public bool MatchFirstDownstroke { get; set; }
    public bool ResetAfterPause { get; set; }

    public bool MatchGroupEndPosition { get; set; }

    public float SpeedThreshold { get; set; } = 0f;
}